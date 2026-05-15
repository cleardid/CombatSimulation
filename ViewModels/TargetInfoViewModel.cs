using CombatSimulation.Models;
using CombatSimulation.Services.Targets;
using CombatSimulation.Services.Unity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;

namespace CombatSimulation.ViewModels
{
    public sealed partial class TargetInfoViewModel : ObservableObject
    {
        private const string StructureInfoPanel = "目标结构信息";
        private const string DamageTreeInfoPanel = "毁伤树信息";
        private const string AllTargetCategory = "全部类型";

        private readonly MySqlTargetInfoRepository _targetInfoRepository;
        private readonly IUnityTargetCommandService _unityTargetCommandService;
        private TargetInfoItem? _checkStateSubscriptionTarget;
        private CancellationTokenSource? _targetSelectionCts;
        private CancellationTokenSource? _partHighlightCts;
        private CancellationTokenSource? _checkedPartSyncCts;
        private string? _targetLoadStatusText;

        /// <summary>
        /// 默认构造函数。
        /// </summary>
        public TargetInfoViewModel()
            : this(new MySqlTargetInfoRepository(), new UnityTargetCommandService(UnityService.Instance))
        {
        }

        /// <summary>
        /// 支持替换 MySQL 仓储和 Unity 命令服务的构造函数，便于显式传入连接配置或进行界面测试。
        /// </summary>
        public TargetInfoViewModel(MySqlTargetInfoRepository targetInfoRepository, IUnityTargetCommandService unityTargetCommandService)
        {
            _targetInfoRepository = targetInfoRepository;
            _unityTargetCommandService = unityTargetCommandService;

            LoadTargetsFromRepository();

            // 使用 WPF 集合视图筛选目标，不重建原始目标集合。
            FilteredTargets = CollectionViewSource.GetDefaultView(Targets);
            FilteredTargets.Filter = FilterTargetByCategory;

            RebuildTargetCategories(refreshFilteredTargets: false);

            // 默认选择数据库读取到的第一个目标。
            SelectedTarget = FilteredTargets.Cast<TargetInfoItem>().FirstOrDefault();
            if (SelectedTarget == null)
            {
                RefreshSelectedDetailRows(null);
            }

            // 默认进入目标结构信息模块
            SelectedInfoPanel = StructureInfoPanel;
            if (!string.IsNullOrWhiteSpace(_targetLoadStatusText) && (SelectedTarget == null || Targets.Count == 0))
            {
                StatusText = _targetLoadStatusText;
            }
        }

        /// <summary>
        /// 目标种类列表
        /// </summary>
        public ObservableCollection<string> TargetCategories { get; } = new();

        /// <summary>
        /// 符合选中类型的目标信息列表
        /// </summary>
        public ICollectionView FilteredTargets { get; }

        /// <summary>
        /// 数据库读取到的目标信息列表。当前数据结构已经按目标信息表、目标系统信息表、目标部件信息表组织。
        /// </summary>
        public ObservableCollection<TargetInfoItem> Targets { get; } = new();

        public ObservableCollection<TargetDetailRow> SelectedBasicDetailRows { get; } = new();

        public ObservableCollection<TargetDetailRow> SelectedStructureDetailRows { get; } = new();

        public ObservableCollection<TargetDetailRow> SelectedPositionDetailRows { get; } = new();

        public ObservableCollection<TargetDetailRow> SelectedAngleDetailRows { get; } = new();

        /// <summary>
        /// 请求 View 显示操作提示。
        /// </summary>
        public event EventHandler<OperationMessageRequestedEventArgs>? OperationMessageRequested;

        /// <summary>
        /// 请求 View 打开目标添加/修改弹窗。
        /// </summary>
        public event EventHandler<TargetEditRequestedEventArgs>? TargetEditRequested;

        /// <summary>
        /// 请求 View 确认删除目标。
        /// </summary>
        public event EventHandler<TargetDeleteRequestedEventArgs>? TargetDeleteRequested;

        /// <summary>
        /// 请求 View 打开结构节点修改弹窗。
        /// </summary>
        public event EventHandler<StructureNodeEditRequestedEventArgs>? StructureNodeEditRequested;

        /// <summary>
        /// 请求 View 确认删除结构节点。
        /// </summary>
        public event EventHandler<StructureNodeDeleteRequestedEventArgs>? StructureNodeDeleteRequested;

        /// <summary>
        /// 请求 View 打开子系统添加弹窗。
        /// </summary>
        public event EventHandler<StructureChildSystemAddRequestedEventArgs>? StructureChildSystemAddRequested;

        /// <summary>
        /// 请求 View 打开底层部件添加弹窗。
        /// </summary>
        public event EventHandler<StructureChildPartAddRequestedEventArgs>? StructureChildPartAddRequested;

        /// <summary>
        /// 选中的目标类型
        /// </summary>
        [ObservableProperty]
        private string _selectedTargetCategory = AllTargetCategory;

        /// <summary>
        /// 选中的目标
        /// </summary>
        [ObservableProperty]
        private TargetInfoItem? _selectedTarget;

        /// <summary>
        /// 结构树当前选中节点。
        /// </summary>
        [ObservableProperty]
        private TargetStructureTreeNode? _selectedStructureNode;

        private bool _isSynchronizingStructureNodeSelection;

        /// <summary>
        /// 当前模块状态信息。界面中不再单独展示，但保留供日志和后续调试使用。
        /// </summary>
        [ObservableProperty]
        private string _statusText = "目标信息模块已加载";

        /// <summary>
        /// 当前选中的分模块：目标结构 或 目标毁伤树
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsStructureInfoSelected))]
        [NotifyPropertyChangedFor(nameof(IsDamageTreeInfoSelected))]
        [NotifyPropertyChangedFor(nameof(StructureInfoVisibility))]
        [NotifyPropertyChangedFor(nameof(DamageTreeInfoVisibility))]
        private string _selectedInfoPanel = string.Empty;

        /// <summary>
        /// 是否选择了目标结构树模块，用于控制高亮按钮
        /// </summary>
        public bool IsStructureInfoSelected => SelectedInfoPanel == StructureInfoPanel;

        /// <summary>
        /// 是否选择了目标毁伤树模块，用于控制高亮按钮
        /// </summary>
        public bool IsDamageTreeInfoSelected => SelectedInfoPanel == DamageTreeInfoPanel;

        /// <summary>
        /// 是否选择了目标结构树模块，用于显示结构树模块
        /// </summary>
        public Visibility StructureInfoVisibility =>
            IsStructureInfoSelected ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 是否选择了目标毁伤树模块，用于显示毁伤树模块
        /// </summary>
        public Visibility DamageTreeInfoVisibility =>
            IsDamageTreeInfoSelected ? Visibility.Visible : Visibility.Collapsed;
    }
}
