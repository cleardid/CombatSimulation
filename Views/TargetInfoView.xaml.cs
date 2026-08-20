using CombatSimulation.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CombatSimulation.Views;

/// <summary>
/// 目标信息模块宿主，只负责创建页面级依赖、异步初始化和生命周期释放。
/// </summary>
public partial class TargetInfoView : UserControl, IDisposable
{
    private TargetInfoViewModel? _ownedViewModel;
    private bool _disposed;

    public TargetInfoView()
    {
        InitializeComponent();
        _ownedViewModel = new TargetInfoViewModel(new TargetInteractionService(() => Window.GetWindow(this)));
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
        _ownedViewModel = null;
        GC.SuppressFinalize(this);
    }
}