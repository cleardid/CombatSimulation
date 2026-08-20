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
public sealed partial class TargetInfoViewModel : ObservableObject, IDisposable
{
    private const string StructureInfoPanel = "目标结构信息";
    private const string DamageTreeInfoPanel = "毁伤树信息";
    private const string AllTargetCategory = "全部类型";

    private readonly ITargetInfoRepository _targetInfoRepository;
    private readonly IDamageTreeRepository _damageTreeRepository;
    private readonly IUnityCommandService _unityCommandService;
    private readonly ITargetInteractionService _interactionService;
    private readonly bool _ownsUnityCommandService;
    private bool _disposed;
    private TargetInfoItem? _checkStateSubscriptionTarget;
    private CancellationTokenSource? _targetSelectionCts;
    private CancellationTokenSource? _partHighlightCts;
    private CancellationTokenSource? _checkedPartSyncCts;
    private CancellationTokenSource? _databaseSnapshotSyncCts;
    private CancellationTokenSource? _damageTreeLoadCts;
    private readonly SemaphoreSlim _repositoryOperationLock = new(1, 1);
    private readonly object _initializationSync = new();
    private string? _targetLoadStatusText;
    private Task? _initializationTask;

    /// <summary>
    /// 默认构造函数。无界面宿主时使用空交互实现，适合设计器和非交互场景。
    /// </summary>
    public TargetInfoViewModel()
        : this(NullTargetInteractionService.Instance)
    {
    }

    /// <summary>
    /// 生产界面构造函数，由 View 注入 WPF 交互服务；运行时依赖在这里统一创建并由 ViewModel 释放。
    /// </summary>
    public TargetInfoViewModel(ITargetInteractionService interactionService)
        : this(
            new MySqlTargetInfoRepository(),
            new MySqlDamageTreeRepository(),
            new UnityCommandService(UnityService.Instance),
            interactionService,
            ownsUnityCommandService: true)
    {
    }

    public TargetInfoViewModel(ITargetInfoRepository targetInfoRepository, IUnityCommandService unityCommandService)
        : this(targetInfoRepository, new MySqlDamageTreeRepository(), unityCommandService)
    {
    }

    public TargetInfoViewModel(
        ITargetInfoRepository targetInfoRepository,
        IDamageTreeRepository damageTreeRepository,
        IUnityCommandService unityCommandService)
        : this(targetInfoRepository, damageTreeRepository, unityCommandService, NullTargetInteractionService.Instance)
    {
    }

    public TargetInfoViewModel(
        ITargetInfoRepository targetInfoRepository,
        IDamageTreeRepository damageTreeRepository,
        IUnityCommandService unityCommandService,
        ITargetInteractionService interactionService)
        : this(targetInfoRepository, damageTreeRepository, unityCommandService, interactionService, ownsUnityCommandService: false)
    {
    }

    private TargetInfoViewModel(
        ITargetInfoRepository targetInfoRepository,
        IDamageTreeRepository damageTreeRepository,
        IUnityCommandService unityCommandService,
        ITargetInteractionService interactionService,
        bool ownsUnityCommandService)
    {
        _targetInfoRepository = targetInfoRepository ?? throw new ArgumentNullException(nameof(targetInfoRepository));
        _damageTreeRepository = damageTreeRepository ?? throw new ArgumentNullException(nameof(damageTreeRepository));
        _unityCommandService = unityCommandService ?? throw new ArgumentNullException(nameof(unityCommandService));
        _interactionService = interactionService ?? throw new ArgumentNullException(nameof(interactionService));
        _ownsUnityCommandService = ownsUnityCommandService;

        _unityCommandService.EventReceived += OnUnityEventReceived;
        FilteredTargets = CollectionViewSource.GetDefaultView(Targets);
        FilteredTargets.Filter = FilterTargetByCategory;
        RebuildTargetCategories(refreshFilteredTargets: false);
        RefreshSelectedDetailRows(null);
        SelectedInfoPanel = StructureInfoPanel;
    }
    /// <summary>
    /// 在界面加载后异步读取数据库。重复调用会复用同一个初始化任务。
    /// </summary>
    public Task InitializeAsync()
    {
        lock (_initializationSync)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TargetInfoViewModel));
            }

            return _initializationTask ??= LoadTargetsFromRepositoryAsync();
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
    /// 选中的目标类型。
    /// </summary>
    [ObservableProperty]
    private string _selectedTargetCategory = AllTargetCategory;
    /// <summary>
    /// 首次数据库读取结束后允许界面执行增删改操作。
    /// </summary>
    [ObservableProperty]
    private bool _isDatabaseReady;


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

    /// <summary>
    /// 取消后台同步并解除所有长生命周期事件订阅。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _unityCommandService.EventReceived -= OnUnityEventReceived;
        DetachCheckStateHandlers(_checkStateSubscriptionTarget);
        _checkStateSubscriptionTarget = null;

        _targetSelectionCts?.Cancel();
        _targetSelectionCts?.Dispose();
        _targetSelectionCts = null;

        _partHighlightCts?.Cancel();
        _partHighlightCts?.Dispose();
        _partHighlightCts = null;

        // 这三个来源由对应后台任务在 finally 中释放；这里只发出取消信号。
        _checkedPartSyncCts?.Cancel();
        _databaseSnapshotSyncCts?.Cancel();
        _damageTreeLoadCts?.Cancel();

        if (_ownsUnityCommandService && _unityCommandService is IDisposable disposableService)
        {
            disposableService.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
