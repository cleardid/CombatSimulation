using CombatSimulation.Services.DamageTrees;
using CombatSimulation.Services.Targets;
using CombatSimulation.Services.Unity;
using CombatSimulation.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CombatSimulation.Views;

/// <summary>
/// 目标信息模块的组合根，负责创建页面依赖、异步初始化和生命周期释放。
/// </summary>
public partial class TargetInfoView : UserControl, IDisposable
{
    private TargetInfoViewModel? _ownedViewModel;
    private UnityCommandService? _ownedUnityCommandService;
    private bool _disposed;

    public TargetInfoView()
    {
        InitializeComponent();

        UnityCommandService unityCommandService = new(UnityService.Instance);
        try
        {
            _ownedViewModel = new TargetInfoViewModel(
                new MySqlTargetInfoRepository(),
                new MySqlDamageTreeRepository(),
                unityCommandService,
                new TargetInteractionService(() => Window.GetWindow(this)));
            _ownedUnityCommandService = unityCommandService;
        }
        catch
        {
            unityCommandService.Dispose();
            throw;
        }

        DataContext = _ownedViewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TargetInfoViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Loaded -= OnLoaded;
        _ownedViewModel?.Dispose();
        _ownedUnityCommandService?.Dispose();
        _ownedViewModel = null;
        _ownedUnityCommandService = null;
        GC.SuppressFinalize(this);
    }
}
