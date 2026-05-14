using CombatSimulation.Services.Unity;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CombatSimulation.Views
{
    /// <summary>
    /// 目标结构信息子视图。
    /// </summary>
    /// <remarks>
    /// XAML 中的 UnityHostControl 会创建一个稳定的原生 HWND；本代码只负责把该 HWND 同步给
    /// UnityService。UnityWindowHost 会在显示期间持续读取该 HWND 的实时客户区，使 Unity 填满宿主区域。
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
        private int _hostBoundsRetryCount;
        private int _stabilizationTickCount;
        private nint _lastHostHwnd;
        private int _lastHostWidth;
        private int _lastHostHeight;
        private CancellationTokenSource? _visibleCts;
        private DispatcherTimer? _stabilizationTimer;

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

        private void OnUnityNativeHostBoundsChanged(object sender, EventArgs e)
        {
            MarkHostBoundsDirtyAndUpdate(showAfterUpdate: IsVisible);
            StartHostBoundsStabilization();
        }

        private void ActivateUnityWindow()
        {
            if (!IsLoaded)
            {
                return;
            }

            EnsureVisibleToken();
            _showRequestedForCurrentActivation = false;

            RequestUnityHostBoundsUpdate(showAfterUpdate: true);
            StartHostBoundsStabilization();
        }

        private void DeactivateUnityWindow()
        {
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
            if (!IsLoaded || !IsVisible || !_hostBoundsReady)
            {
                return;
            }

            CancellationToken token = EnsureVisibleToken();
            _showRequestedForCurrentActivation = true;

            // 让显示请求排到当前布局和切页命令之后执行，避免 Show/Hide 在同一轮 UI 事件里交叉。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsLoaded || !IsVisible || token.IsCancellationRequested)
                {
                    return;
                }

                _ = ShowUnityWindowAsync(token);
            }), DispatcherPriority.ApplicationIdle);
        }

        private async Task ShowUnityWindowAsync(CancellationToken cancellationToken)
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

            Matrix transformToDevice = PresentationSource.FromVisual(UnityNativeHost)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            width = Math.Max(0, (int)Math.Round(UnityNativeHost.ActualWidth * transformToDevice.M11));
            height = Math.Max(0, (int)Math.Round(UnityNativeHost.ActualHeight * transformToDevice.M22));

            return width > 2 && height > 2;
        }
    }
}
