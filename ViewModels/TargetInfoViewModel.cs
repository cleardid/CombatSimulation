using CombatSimulation.Models;
using CombatSimulation.Services.DamageTrees;
using CombatSimulation.Services.Targets;
using CombatSimulation.Services.Unity;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace CombatSimulation.ViewModels;

/// <summary>
/// 目标信息界面的主 ViewModel。
/// </summary>
/// <remarks>
/// 该类通过 partial 文件拆分为数据加载、命令、树结构、详情刷新、Unity 通信和增删改业务几部分，
/// 以降低单个文件的复杂度。主文件只保留跨文件共享的字段、集合、属性和事件声明。
/// </remarks>
public sealed partial class TargetInfoViewModel : ObservableObject
{
    private const string StructureInfoPanel = "目标结构信息";
    private const string DamageTreeInfoPanel = "毁伤树信息";
    private const string AllTargetCategory = "全部类型";

    private readonly MySqlTargetInfoRepository _targetInfoRepository;
    private readonly MySqlDamageTreeRepository _damageTreeRepository;
    private readonly IUnityCommandService _unityCommandService;
    private TargetInfoItem? _checkStateSubscriptionTarget;
    private CancellationTokenSource? _targetSelectionCts;
    private CancellationTokenSource? _partHighlightCts;
    private CancellationTokenSource? _checkedPartSyncCts;
    private CancellationTokenSource? _databaseSnapshotSyncCts;
    private string? _targetLoadStatusText;

    /// <summary>
    /// 默认构造函数。
    /// </summary>
    /// <remarks>
    /// 生产运行时直接创建 MySQL 仓储和 Unity 命令服务；测试时可使用另一个构造函数注入替代对象。
    /// </remarks>
    public TargetInfoViewModel()
        : this(new MySqlTargetInfoRepository(), new MySqlDamageTreeRepository(), new UnityCommandService(UnityService.Instance))
    {
    }

    /// <summary>
    /// 支持替换 MySQL 仓储和 Unity 命令服务的构造函数，便于显式传入连接配置或进行界面测试。
    /// </summary>
    public TargetInfoViewModel(MySqlTargetInfoRepository targetInfoRepository, IUnityCommandService unityCommandService)
        : this(targetInfoRepository, new MySqlDamageTreeRepository(), unityCommandService)
    {
    }

    /// <summary>
    /// 支持替换目标仓储、毁伤树仓储和 Unity 命令服务的构造函数。
    /// </summary>
    public TargetInfoViewModel(
        MySqlTargetInfoRepository targetInfoRepository,
        MySqlDamageTreeRepository damageTreeRepository,
        IUnityCommandService unityCommandService)
    {
        _targetInfoRepository = targetInfoRepository;
        _damageTreeRepository = damageTreeRepository;
        _unityCommandService = unityCommandService;

        // 先订阅 Unity 事件。SelectedTarget 在构造末尾会触发 show_target，
        // 若此时 Unity 刚建立连接并立即上报 server_ready，WPF 不能错过该事件。
        _unityCommandService.EventReceived += OnUnityEventReceived;

        // 先从数据库加载目标，再建立 ICollectionView。否则筛选视图会拿到空集合的初始状态。
        LoadTargetsFromRepository();

        // 使用 WPF 集合视图筛选目标，不重建原始目标集合，避免选中项和树节点引用失效。
        FilteredTargets = CollectionViewSource.GetDefaultView(Targets);
        FilteredTargets.Filter = FilterTargetByCategory;

        // 目标类型依赖数据库结果，加载完成后统一重建下拉列表。
        RebuildTargetCategories(refreshFilteredTargets: false);

        // 默认选择数据库读取到的第一个目标。SelectedTarget 的 partial 回调会同步 Unity 显示命令。
        SelectedTarget = FilteredTargets.Cast<TargetInfoItem>().FirstOrDefault();
        if (SelectedTarget == null)
        {
            RefreshSelectedDetailRows(null);
        }

        // 默认进入目标结构信息模块。
        SelectedInfoPanel = StructureInfoPanel;
        if (!string.IsNullOrWhiteSpace(_targetLoadStatusText) && (SelectedTarget == null || Targets.Count == 0))
        {
            StatusText = _targetLoadStatusText;
        }
    }

    /// <summary>
    /// 目标种类列表。
    /// </summary>
    public ObservableCollection<string> TargetCategories { get; } = new();

    /// <summary>
    /// 符合选中类型的目标信息列表。
    /// </summary>
    public ICollectionView FilteredTargets { get; }

    /// <summary>
    /// 数据库读取到的目标信息列表。当前数据结构已经按目标信息表、目标系统信息表、目标部件信息表组织。
    /// </summary>
    public ObservableCollection<TargetInfoItem> Targets { get; } = new();

    /// <summary>
    /// 当前选中目标下的毁伤树集合。
    /// </summary>
    /// <remarks>
    /// 当 <see cref="SelectedTarget"/> 变化时，毁伤树模块会重新从 MySQL 读取该目标对应的毁伤树。
    /// </remarks>
    public ObservableCollection<DamageTreeInfoItem> DamageTrees { get; } = new();

    /// <summary>
    /// 毁伤树节点详情行，用于右侧详情面板展示当前毁伤树或节点的属性。
    /// </summary>
    public ObservableCollection<TargetDetailRow> SelectedDamageNodeDetailRows { get; } = new();

    /// <summary>
    /// 右侧详情面板的基础信息行。
    /// </summary>
    public ObservableCollection<TargetDetailRow> SelectedBasicDetailRows { get; } = new();

    /// <summary>
    /// 右侧详情面板的结构/几何信息行。
    /// </summary>
    public ObservableCollection<TargetDetailRow> SelectedStructureDetailRows { get; } = new();

    /// <summary>
    /// 右侧详情面板的位置信息行。
    /// </summary>
    public ObservableCollection<TargetDetailRow> SelectedPositionDetailRows { get; } = new();

    /// <summary>
    /// 右侧详情面板的旋转角度信息行。
    /// </summary>
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
    /// 请求 View 打开毁伤树添加/修改弹窗。
    /// </summary>
    public event EventHandler<DamageTreeEditRequestedEventArgs>? DamageTreeEditRequested;

    /// <summary>
    /// 请求 View 确认删除毁伤树。
    /// </summary>
    public event EventHandler<DamageTreeDeleteRequestedEventArgs>? DamageTreeDeleteRequested;

    /// <summary>
    /// 请求 View 打开毁伤树中间节点添加/修改弹窗。
    /// </summary>
    public event EventHandler<DamageTreeMiddleNodeEditRequestedEventArgs>? DamageTreeMiddleNodeEditRequested;

    /// <summary>
    /// 请求 View 打开毁伤树叶子节点添加/修改弹窗。
    /// </summary>
    public event EventHandler<DamageTreeLeafNodeEditRequestedEventArgs>? DamageTreeLeafNodeEditRequested;

    /// <summary>
    /// 请求 View 确认删除毁伤树节点。
    /// </summary>
    public event EventHandler<DamageTreeNodeDeleteRequestedEventArgs>? DamageTreeNodeDeleteRequested;

    /// <summary>
    /// 选中的目标类型。
    /// </summary>
    [ObservableProperty]
    private string _selectedTargetCategory = AllTargetCategory;

    /// <summary>
    /// 选中的目标。
    /// </summary>
    [ObservableProperty]
    private TargetInfoItem? _selectedTarget;

    /// <summary>
    /// 结构树当前选中节点。
    /// </summary>
    [ObservableProperty]
    private TargetStructureTreeNode? _selectedStructureNode;

    /// <summary>
    /// 当前选中的毁伤树。
    /// </summary>
    /// <remarks>
    /// 该属性由 CommunityToolkit.Mvvm 生成 public 属性，并触发
    /// <c>OnSelectedDamageTreeChanged</c> partial 回调。
    /// </remarks>
    [ObservableProperty]
    private DamageTreeInfoItem? _selectedDamageTree;

    /// <summary>
    /// 当前选中的毁伤树节点。
    /// </summary>
    /// <remarks>
    /// 该属性由 CommunityToolkit.Mvvm 生成 public 属性，并触发
    /// <c>OnSelectedDamageTreeNodeChanged</c> partial 回调。
    /// </remarks>
    [ObservableProperty]
    private DamageTreeNodeItem? _selectedDamageTreeNode;

    /// <summary>
    /// 是否正在由 ViewModel 主动同步树节点选中状态。用于避免 TreeView 选中回写造成递归。
    /// </summary>
    private bool _isSynchronizingStructureNodeSelection;

    /// <summary>
    /// 是否正在由 ViewModel 同步毁伤树节点选中状态。用于避免毁伤树 TreeView 的选中状态回写递归。
    /// </summary>
    private bool _isSynchronizingDamageNodeSelection;

    /// <summary>
    /// 当前模块状态信息。界面中不再单独展示，但保留供日志和后续调试使用。
    /// </summary>
    [ObservableProperty]
    private string _statusText = "目标信息模块已加载";

    /// <summary>
    /// 当前选中的分模块：目标结构 或 目标毁伤树。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStructureInfoSelected))]
    [NotifyPropertyChangedFor(nameof(IsDamageTreeInfoSelected))]
    [NotifyPropertyChangedFor(nameof(StructureInfoVisibility))]
    [NotifyPropertyChangedFor(nameof(DamageTreeInfoVisibility))]
    private string _selectedInfoPanel = string.Empty;

    /// <summary>
    /// 是否选择了目标结构树模块，用于控制高亮按钮。
    /// </summary>
    public bool IsStructureInfoSelected => SelectedInfoPanel == StructureInfoPanel;

    /// <summary>
    /// 是否选择了目标毁伤树模块，用于控制高亮按钮。
    /// </summary>
    public bool IsDamageTreeInfoSelected => SelectedInfoPanel == DamageTreeInfoPanel;

    /// <summary>
    /// 目标结构树模块可见性。
    /// </summary>
    public Visibility StructureInfoVisibility =>
        IsStructureInfoSelected ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// 毁伤树模块可见性。
    /// </summary>
    public Visibility DamageTreeInfoVisibility =>
        IsDamageTreeInfoSelected ? Visibility.Visible : Visibility.Collapsed;
}
