using CombatSimulation.Services.Unity;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace CombatSimulation.Controls;

/// <summary>
/// 协调 WPF 视图、原生宿主 HWND 与 Unity 子窗口的激活、隐藏和尺寸同步。
/// </summary>
internal sealed class UnityHostLifecycleCoordinator
{
    private const int MaxHostBoundsRetryCount = 80;

    private static readonly TimeSpan HostBoundsRetryInterval = TimeSpan.FromMilliseconds(50);

    private readonly FrameworkElement _view;
    private readonly UnityHostControl _nativeHost;
    private bool _hostBoundsUpdatePending;
    private bool _showAfterHostBoundsUpdate;
    private bool _hostBoundsReady;
    private bool _showRequestedForCurrentActivation;
    private bool _unityWindowActive;
    private int _hostBoundsRetryCount;
    private long _activationVersion;
    private CancellationTokenSource? _visibleCts;
    private DispatcherOperation? _queuedShowOperation;

    public UnityHostLifecycleCoordinator(FrameworkElement view, UnityHostControl nativeHost)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _nativeHost = nativeHost ?? throw new ArgumentNullException(nameof(nativeHost));

        _view.Loaded += OnLoaded;
        _view.Unloaded += OnUnloaded;
        _view.IsVisibleChanged += OnIsVisibleChanged;
        _nativeHost.NativeHostChanged += OnNativeHostChanged;
        _nativeHost.NativeHostDestroying += OnNativeHostDestroying;
        _nativeHost.NativeHostBoundsChanged += OnNativeHostBoundsChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_view.IsVisible)
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
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_view.IsVisible)
        {
            ActivateUnityWindow();
        }
        else
        {
            DeactivateUnityWindow();
        }
    }

    private void OnNativeHostChanged(object? sender, EventArgs e)
    {
        MarkHostBoundsDirtyAndUpdate(showAfterUpdate: _view.IsVisible);
    }

    private void OnNativeHostDestroying(object? sender, EventArgs e)
    {
        _activationVersion++;
        AbortQueuedShowOperation();
        _showRequestedForCurrentActivation = false;
        _hostBoundsReady = false;
        CancelVisibleToken();

        try
        {
            // 此事件发生时 HostHandle 仍然有效。先移走 Unity 子窗口，再允许 HwndHost 销毁宿主 HWND。
            UnityService.Instance.DetachUnityWindowFromHost(_nativeHost.HostHandle);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UnityHostLifecycleCoordinator] Unity 窗口脱离原生宿主失败：{ex.Message}");
        }

        if (_view.IsLoaded && _view.IsVisible)
        {
            EnsureVisibleToken();
        }
    }

    private void OnNativeHostBoundsChanged(object? sender, EventArgs e)
    {
        MarkHostBoundsDirtyAndUpdate(showAfterUpdate: _view.IsVisible);
    }

    private void ActivateUnityWindow()
    {
        if (!_view.IsLoaded || _unityWindowActive)
        {
            return;
        }

        _unityWindowActive = true;
        _activationVersion++;
        AbortQueuedShowOperation();
        EnsureVisibleToken();
        _showRequestedForCurrentActivation = false;
        _hostBoundsRetryCount = 0;

        RequestUnityHostBoundsUpdate(showAfterUpdate: true);
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
        if (!_view.IsLoaded)
        {
            return;
        }

        _showAfterHostBoundsUpdate |= showAfterUpdate;

        if (_hostBoundsUpdatePending)
        {
            return;
        }

        _hostBoundsUpdatePending = true;
        _view.Dispatcher.BeginInvoke(
            new Action(async () => await ProcessUnityHostBoundsUpdateAsync()),
            DispatcherPriority.ContextIdle);
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

                if (shouldShow && _view.IsVisible)
                {
                    QueueShowUnityWindow();
                }

                return;
            }

            if (shouldShow && _view.IsVisible && _hostBoundsRetryCount < MaxHostBoundsRetryCount)
            {
                _hostBoundsRetryCount++;
                _showAfterHostBoundsUpdate = true;
                await Task.Delay(HostBoundsRetryInterval);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UnityHostLifecycleCoordinator] 更新 Unity 原生宿主区域失败：{ex.Message}");
        }
        finally
        {
            _hostBoundsUpdatePending = false;

            if (_showAfterHostBoundsUpdate && _view.IsLoaded)
            {
                RequestUnityHostBoundsUpdate(showAfterUpdate: false);
            }
        }
    }

    private void QueueShowUnityWindow()
    {
        if (!_view.IsLoaded
            || !_view.IsVisible
            || !_unityWindowActive
            || !_hostBoundsReady
            || _showRequestedForCurrentActivation)
        {
            return;
        }

        CancellationToken token = EnsureVisibleToken();
        long activationVersion = _activationVersion;
        _showRequestedForCurrentActivation = true;

        // 尺寸更新已经等待过一次 Render；继续使用 Render 优先级，避免快速切页期间显示任务长期排队。
        DispatcherOperation? operation = null;
        operation = _view.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ReferenceEquals(_queuedShowOperation, operation))
            {
                _queuedShowOperation = null;
            }

            if (!_unityWindowActive
                || activationVersion != _activationVersion
                || !_view.IsLoaded
                || !_view.IsVisible
                || token.IsCancellationRequested)
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
            Debug.WriteLine($"[UnityHostLifecycleCoordinator] 显示 Unity 窗口失败：{ex.Message}");

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
        if (!_view.IsLoaded)
        {
            _hostBoundsReady = false;
            return false;
        }

        nint hostHwnd = _nativeHost.HostHandle;
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

        UnityService.Instance.SetUnityWindowHostBounds(hostHwnd, width, height);
        _hostBoundsReady = true;
        return true;
    }

    private bool TryGetExpectedHostSize(out int width, out int height)
    {
        if (_nativeHost.TryGetClientSize(out width, out height) && width > 2 && height > 2)
        {
            return true;
        }

        System.Windows.Media.Matrix transformToDevice =
            PresentationSource.FromVisual(_nativeHost)?.CompositionTarget?.TransformToDevice
            ?? System.Windows.Media.Matrix.Identity;
        width = Math.Max(0, (int)Math.Round(_nativeHost.ActualWidth * transformToDevice.M11));
        height = Math.Max(0, (int)Math.Round(_nativeHost.ActualHeight * transformToDevice.M22));

        return width > 2 && height > 2;
    }
}
