using CombatSimulation.Models;
using CombatSimulation.Services.Unity;
using CombatSimulation.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace CombatSimulation.Views;

/// <summary>
/// 目标结构信息子视图。
/// </summary>
/// <remarks>
/// XAML 中的 UnityHostControl 会创建一个稳定的原生 HWND；本代码只负责把该 HWND 同步给
/// UnityService。UnityWindowHost 仅在宿主句柄、尺寸或可见状态变化时更新 Unity 窗口。
/// </remarks>
public partial class TargetStructureInfoView : UserControl
{
    private const int MaxHostBoundsRetryCount = 80;
    private const int StabilizationTickLimit = 30;

    private static readonly TimeSpan HostBoundsRetryInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan StabilizationInterval = TimeSpan.FromMilliseconds(100);

    private bool _hostBoundsUpdatePending;
    private bool _showAfterHostBoundsUpdate;
    private bool _hostBoundsReady;
    private bool _showRequestedForCurrentActivation;
    private bool _unityWindowActive;
    private int _hostBoundsRetryCount;
    private int _stabilizationTickCount;
    private long _activationVersion;
    private nint _lastHostHwnd;
    private int _lastHostWidth;
    private int _lastHostHeight;
    private CancellationTokenSource? _visibleCts;
    private DispatcherTimer? _stabilizationTimer;
    private DispatcherOperation? _queuedShowOperation;

    public TargetStructureInfoView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (IsVisible)
        {
            ActivateUnityWindow();
        }
        else
        {
            RequestUnityHostBoundsUpdate(showAfterUpdate: false);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DeactivateUnityWindow();
        StopHostBoundsStabilization();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            ActivateUnityWindow();
        }
        else
        {
            DeactivateUnityWindow();
        }
    }

    private void OnUnityHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        MarkHostBoundsDirtyAndUpdate(showAfterUpdate: IsVisible);
        StartHostBoundsStabilization();
    }

    private void OnUnityNativeHostChanged(object sender, EventArgs e)
    {
        MarkHostBoundsDirtyAndUpdate(showAfterUpdate: IsVisible);
        StartHostBoundsStabilization();
    }

    private void OnUnityNativeHostDestroying(object sender, EventArgs e)
    {
        _activationVersion++;
        AbortQueuedShowOperation();
        _showRequestedForCurrentActivation = false;
        _hostBoundsReady = false;
        CancelVisibleToken();

        try
        {
            // 此事件发生时 HostHandle 仍然有效。必须先把 Unity 子窗口移走，随后 HwndHost 才能销毁宿主 HWND。
            UnityService.Instance.DetachUnityWindowFromHost(UnityNativeHost.HostHandle);
        }
        catch (Exception ex)
        {
            // 宿主销毁流程不能因外部窗口已经失效而中断；此时应用退出流程仍会关闭 Unity 进程。
            Debug.WriteLine($"[TargetStructureInfoView] Unity 窗口脱离原生宿主失败：{ex.Message}");
        }

        if (IsLoaded && IsVisible)
        {
            EnsureVisibleToken();
        }
    }

    private void OnUnityNativeHostBoundsChanged(object sender, EventArgs e)
    {
        MarkHostBoundsDirtyAndUpdate(showAfterUpdate: IsVisible);
        StartHostBoundsStabilization();
    }

    private void OnStructureTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is TargetInfoViewModel viewModel && e.NewValue is TargetStructureTreeNode node)
        {
            viewModel.SelectStructureNode(node);
        }
    }

    private void OnOpenAddContextMenuClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not Button button || button.ContextMenu == null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }

    private void ActivateUnityWindow()
    {
        if (!IsLoaded || _unityWindowActive)
        {
            return;
        }

        _unityWindowActive = true;
        _activationVersion++;
        AbortQueuedShowOperation();
        EnsureVisibleToken();
        _showRequestedForCurrentActivation = false;

        RequestUnityHostBoundsUpdate(showAfterUpdate: true);
        StartHostBoundsStabilization();
    }

    private void DeactivateUnityWindow()
    {
        if (!_unityWindowActive)
        {
            return;
        }

        _unityWindowActive = false;
        _activationVersion++;
        AbortQueuedShowOperation();
        _showAfterHostBoundsUpdate = false;
        _showRequestedForCurrentActivation = false;
        StopHostBoundsStabilization();
        CancelVisibleToken();
        UnityService.Instance.HideUnityWindow();
    }

    private void MarkHostBoundsDirtyAndUpdate(bool showAfterUpdate)
    {
        _hostBoundsReady = false;
        RequestUnityHostBoundsUpdate(showAfterUpdate);
    }

    private void RequestUnityHostBoundsUpdate(bool showAfterUpdate)
    {
        if (!IsLoaded)
        {
            return;
        }

        _showAfterHostBoundsUpdate |= showAfterUpdate;

        if (_hostBoundsUpdatePending)
        {
            return;
        }

        _hostBoundsUpdatePending = true;
        Dispatcher.BeginInvoke(new Action(async () => await ProcessUnityHostBoundsUpdateAsync()), DispatcherPriority.ContextIdle);
    }

    private async Task ProcessUnityHostBoundsUpdateAsync()
    {
        try
        {
            await Dispatcher.Yield(DispatcherPriority.Render);

            bool shouldShow = _showAfterHostBoundsUpdate;
            _showAfterHostBoundsUpdate = false;

            bool hostBoundsReady = UpdateUnityHostBounds();
            if (hostBoundsReady)
            {
                _hostBoundsRetryCount = 0;

                if (shouldShow && IsVisible)
                {
                    QueueShowUnityWindow();
                }

                return;
            }

            if (shouldShow && IsVisible && _hostBoundsRetryCount < MaxHostBoundsRetryCount)
            {
                _hostBoundsRetryCount++;
                _showAfterHostBoundsUpdate = true;
                await Task.Delay(HostBoundsRetryInterval);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TargetStructureInfoView] 更新 Unity 原生宿主区域失败：{ex.Message}");
        }
        finally
        {
            _hostBoundsUpdatePending = false;

            if (_showAfterHostBoundsUpdate && IsLoaded)
            {
                RequestUnityHostBoundsUpdate(showAfterUpdate: false);
            }
        }
    }

    private void StartHostBoundsStabilization()
    {
        if (!IsLoaded || !IsVisible)
        {
            return;
        }

        _stabilizationTickCount = 0;

        if (_stabilizationTimer == null)
        {
            _stabilizationTimer = new DispatcherTimer(DispatcherPriority.ContextIdle, Dispatcher)
            {
                Interval = StabilizationInterval
            };
            _stabilizationTimer.Tick += OnHostBoundsStabilizationTick;
        }

        if (!_stabilizationTimer.IsEnabled)
        {
            _stabilizationTimer.Start();
        }
    }

    private void StopHostBoundsStabilization()
    {
        if (_stabilizationTimer == null)
        {
            return;
        }

        _stabilizationTimer.Stop();
        _stabilizationTickCount = 0;
    }

    private void OnHostBoundsStabilizationTick(object? sender, EventArgs e)
    {
        if (!IsLoaded || !IsVisible)
        {
            StopHostBoundsStabilization();
            return;
        }

        _stabilizationTickCount++;

        if (UpdateUnityHostBounds() && !_showRequestedForCurrentActivation)
        {
            QueueShowUnityWindow();
        }

        if (_stabilizationTickCount >= StabilizationTickLimit)
        {
            StopHostBoundsStabilization();
        }
    }

    private void QueueShowUnityWindow()
    {
        if (!IsLoaded ||
            !IsVisible ||
            !_unityWindowActive ||
            !_hostBoundsReady ||
            _showRequestedForCurrentActivation)
        {
            return;
        }

        CancellationToken token = EnsureVisibleToken();
        long activationVersion = _activationVersion;
        _showRequestedForCurrentActivation = true;

        // ProcessUnityHostBoundsUpdateAsync 已经等待过一次 Render；这里继续使用 Render 优先级，
        // 避免 ApplicationIdle 在快速切页和 HwndHost 布局通知期间长期得不到执行。
        DispatcherOperation? operation = null;
        operation = Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ReferenceEquals(_queuedShowOperation, operation))
            {
                _queuedShowOperation = null;
            }

            if (!_unityWindowActive ||
                activationVersion != _activationVersion ||
                !IsLoaded ||
                !IsVisible ||
                token.IsCancellationRequested)
            {
                if (activationVersion == _activationVersion)
                {
                    _showRequestedForCurrentActivation = false;
                }

                return;
            }

            _ = ShowUnityWindowAsync(token, activationVersion);
        }), DispatcherPriority.Render);
        _queuedShowOperation = operation;
    }

    private async Task ShowUnityWindowAsync(CancellationToken cancellationToken, long activationVersion)
    {
        try
        {
            await UnityService.Instance.ShowUnityWindowAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TargetStructureInfoView] 显示 Unity 窗口失败：{ex.Message}");

            if (_unityWindowActive && activationVersion == _activationVersion)
            {
                _showRequestedForCurrentActivation = false;
            }
        }
    }

    private void AbortQueuedShowOperation()
    {
        DispatcherOperation? operation = _queuedShowOperation;
        _queuedShowOperation = null;

        if (operation is { Status: DispatcherOperationStatus.Pending })
        {
            operation.Abort();
        }
    }

    private CancellationToken EnsureVisibleToken()
    {
        if (_visibleCts == null || _visibleCts.IsCancellationRequested)
        {
            _visibleCts?.Dispose();
            _visibleCts = new CancellationTokenSource();
        }

        return _visibleCts.Token;
    }

    private void CancelVisibleToken()
    {
        if (_visibleCts == null)
        {
            return;
        }

        _visibleCts.Cancel();
        _visibleCts.Dispose();
        _visibleCts = null;
    }

    private bool UpdateUnityHostBounds()
    {
        if (!IsLoaded)
        {
            _hostBoundsReady = false;
            return false;
        }

        nint hostHwnd = UnityNativeHost.HostHandle;
        if (hostHwnd == 0 || !TryGetExpectedHostSize(out int width, out int height))
        {
            _hostBoundsReady = false;
            return false;
        }

        if (width <= 2 || height <= 2)
        {
            _hostBoundsReady = false;
            return false;
        }

        if (_hostBoundsReady && _lastHostHwnd == hostHwnd && _lastHostWidth == width && _lastHostHeight == height)
        {
            return true;
        }

        // Unity 已经被挂到 UnityNativeHost 的原生 HWND 下，因此坐标固定为宿主客户区左上角。
        UnityService.Instance.SetUnityWindowHostBounds(hostHwnd, 0, 0, width, height);

        _lastHostHwnd = hostHwnd;
        _lastHostWidth = width;
        _lastHostHeight = height;
        _hostBoundsReady = true;

        return true;
    }

    private bool TryGetExpectedHostSize(out int width, out int height)
    {
        if (UnityNativeHost.TryGetClientSize(out width, out height) && width > 2 && height > 2)
        {
            return true;
        }

        System.Windows.Media.Matrix transformToDevice = PresentationSource.FromVisual(UnityNativeHost)?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        width = Math.Max(0, (int)Math.Round(UnityNativeHost.ActualWidth * transformToDevice.M11));
        height = Math.Max(0, (int)Math.Round(UnityNativeHost.ActualHeight * transformToDevice.M22));

        return width > 2 && height > 2;
    }
}
