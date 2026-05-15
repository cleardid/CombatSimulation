using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CombatSimulation.Services.Unity
{
    /// <summary>
    /// Unity 窗口托管器。
    /// </summary>
    /// <remarks>
    /// 该类只负责 Unity 主窗口句柄查找、父窗口绑定、显示隐藏和位置尺寸维护。
    ///
    /// 目标结构信息界面现在使用 HwndHost 创建原生宿主窗口，因此 Unity 显示时只需要被设置为
    /// 该宿主 HWND 的子窗口，并填满宿主客户区。隐藏时会优先把 Unity 窗口重新挂回 WPF 主窗口，
    /// 避免页面卸载时 HwndHost 被销毁，从而连带销毁 Unity 的渲染窗口。
    /// </remarks>
    internal sealed class UnityWindowHost
    {
        private static readonly TimeSpan HiddenPreparationTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan WindowSearchInterval = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan VisibleFastApplyInterval = TimeSpan.FromMilliseconds(120);
        private static readonly TimeSpan VisibleSlowApplyInterval = TimeSpan.FromMilliseconds(500);

        private const int FastApplyCount = 40;

        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const long WsChild = 0x40000000L;
        private const long WsVisible = 0x10000000L;
        private const long WsDisabled = 0x08000000L;
        private const long WsPopup = unchecked((long)0x80000000);
        private const long WsCaption = 0x00C00000L;
        private const long WsThickFrame = 0x00040000L;
        private const long WsMinimize = 0x20000000L;
        private const long WsMaximize = 0x01000000L;
        private const long WsClipChildren = 0x02000000L;
        private const long WsClipSiblings = 0x04000000L;
        private const long WsExNoActivate = 0x08000000L;

        private static readonly nint HwndTop = 0;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;
        private const uint SwpShowWindow = 0x0040;
        private const uint SwpHideWindow = 0x0080;
        private const int SwHide = 0;
        private const int SwShow = 5;
        private const uint GwOwner = 4;

        private readonly SemaphoreSlim _windowLock = new(1, 1);
        private readonly object _stateLock = new();
        private readonly Func<Process> _getRunningProcess;

        private nint _defaultParentHwnd;
        private nint _hostParentHwnd;
        private nint _unityWindowHwnd;
        private nint _attachedParentHwnd;

        private int _hostX;
        private int _hostY;
        private int _hostWidth;
        private int _hostHeight;

        private bool _hasHostBounds;
        private bool _shouldBeVisible;
        private bool _stopping;
        private long _stateVersion;
        private DateTime _hiddenPreparationDeadlineUtc;
        private Task? _reconcileTask;

        public UnityWindowHost(Func<Process> getRunningProcess)
        {
            _getRunningProcess = getRunningProcess;
        }

        /// <summary>
        /// 窗口托管日志事件。
        /// </summary>
        public event EventHandler<string>? LogReceived;

        /// <summary>
        /// 设置默认父窗口。Unity 隐藏时会被重新挂回该窗口，防止临时宿主 HWND 销毁时影响 Unity 窗口。
        /// </summary>
        public void SetDefaultParentWindow(nint parentHwnd)
        {
            lock (_stateLock)
            {
                _defaultParentHwnd = parentHwnd;
                _stateVersion++;
            }

            RequestReconcile();
        }

        /// <summary>
        /// 设置 Unity 当前应该显示到的原生宿主区域。
        /// </summary>
        /// <remarks>
        /// 推荐传入 HwndHost 创建的宿主 HWND，并使用 x=0、y=0、width=宿主客户区宽度、height=宿主客户区高度。
        /// </remarks>
        public void SetHostBounds(nint parentHwnd, int x, int y, int width, int height)
        {
            if (parentHwnd == 0 || width <= 0 || height <= 0)
            {
                return;
            }

            lock (_stateLock)
            {
                _hostParentHwnd = parentHwnd;
                _hostX = x;
                _hostY = y;
                _hostWidth = width;
                _hostHeight = height;
                _hasHostBounds = true;
                _stateVersion++;
            }

            RequestReconcile();
        }

        /// <summary>
        /// Unity 启动后提前查找并隐藏窗口，避免 Unity 独立窗口闪现。
        /// </summary>
        public Task PrepareHiddenAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            lock (_stateLock)
            {
                _hiddenPreparationDeadlineUtc = DateTime.UtcNow + HiddenPreparationTimeout;
                _stateVersion++;
            }

            RequestReconcile();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 按当前缓存的宿主区域显示 Unity 窗口。
        /// </summary>
        public Task ShowAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            lock (_stateLock)
            {
                _shouldBeVisible = true;
                _stateVersion++;
            }

            RequestReconcile();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 隐藏 Unity 窗口，但保留 Unity 进程和 TCP 连接。
        /// </summary>
        public void Hide()
        {
            nint unityHwnd;
            nint defaultParentHwnd;

            lock (_stateLock)
            {
                _shouldBeVisible = false;
                unityHwnd = _unityWindowHwnd;
                defaultParentHwnd = _defaultParentHwnd;
                _stateVersion++;
            }

            // 页面切换时 HwndHost 可能马上被销毁。这里同步把 Unity 挂回主窗口并隐藏，避免被临时宿主销毁。
            if (unityHwnd != 0 && IsWindow(unityHwnd))
            {
                if (_windowLock.Wait(0))
                {
                    try
                    {
                        HideWindowCore(unityHwnd);
                        AttachToParent(unityHwnd, defaultParentHwnd);
                    }
                    finally
                    {
                        _windowLock.Release();
                    }
                }
                else
                {
                    HideWindowCore(unityHwnd);
                }
            }

            RequestReconcile();
        }

        /// <summary>
        /// 重置缓存的 Unity 窗口句柄和宿主区域信息。
        /// </summary>
        public void Reset()
        {
            lock (_stateLock)
            {
                _shouldBeVisible = false;
                _hasHostBounds = false;
                _hostParentHwnd = 0;
                _hostX = 0;
                _hostY = 0;
                _hostWidth = 0;
                _hostHeight = 0;
                _unityWindowHwnd = 0;
                _attachedParentHwnd = 0;
                _hiddenPreparationDeadlineUtc = DateTime.MinValue;
                _stateVersion++;
            }
        }

        /// <summary>
        /// 停止窗口托管。程序退出时调用。
        /// </summary>
        public void Stop()
        {
            nint unityHwnd;

            lock (_stateLock)
            {
                _stopping = true;
                _shouldBeVisible = false;
                unityHwnd = _unityWindowHwnd;
                _stateVersion++;
            }

            if (unityHwnd != 0 && IsWindow(unityHwnd))
            {
                HideWindowCore(unityHwnd);
            }
        }

        private void RequestReconcile()
        {
            lock (_stateLock)
            {
                if (_stopping)
                {
                    return;
                }

                if (_reconcileTask == null || _reconcileTask.IsCompleted)
                {
                    _reconcileTask = Task.Run(ReconcileLoopAsync);
                }
            }
        }

        private async Task ReconcileLoopAsync()
        {
            int visibleApplyCount = 0;

            while (true)
            {
                HostStateSnapshot snapshot = GetHostStateSnapshot();
                if (snapshot.IsStopping)
                {
                    return;
                }

                try
                {
                    if (snapshot.ShouldBeVisible)
                    {
                        visibleApplyCount++;
                        await ApplyVisibleStateOnceAsync(snapshot).ConfigureAwait(false);

                        TimeSpan interval = visibleApplyCount <= FastApplyCount
                            ? VisibleFastApplyInterval
                            : VisibleSlowApplyInterval;

                        await Task.Delay(interval).ConfigureAwait(false);
                        continue;
                    }

                    visibleApplyCount = 0;
                    bool hiddenStateHandled = await ApplyHiddenStateOnceAsync(snapshot).ConfigureAwait(false);

                    if (hiddenStateHandled && CanWorkerExit(snapshot.Version))
                    {
                        return;
                    }

                    await Task.Delay(WindowSearchInterval).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log($"Unity 窗口状态收敛失败：{ex.Message}");
                    await Task.Delay(VisibleFastApplyInterval).ConfigureAwait(false);
                }
            }
        }

        private async Task ApplyVisibleStateOnceAsync(HostStateSnapshot snapshot)
        {
            if (!snapshot.HasHostBounds || snapshot.ParentHwnd == 0 || !IsWindow(snapshot.ParentHwnd))
            {
                return;
            }

            Process process = _getRunningProcess();
            if (process.HasExited)
            {
                ClearCachedWindowHandle();
                return;
            }

            nint unityHwnd = FindBestWindowHandle(process);
            if (unityHwnd == 0 || !IsWindow(unityHwnd))
            {
                return;
            }

            await _windowLock.WaitAsync().ConfigureAwait(false);
            try
            {
                snapshot = GetHostStateSnapshot();
                if (snapshot.IsStopping)
                {
                    return;
                }

                if (!snapshot.ShouldBeVisible)
                {
                    ReparentToDefaultAndHide(unityHwnd);
                    return;
                }

                if (!snapshot.HasHostBounds || snapshot.ParentHwnd == 0 || !IsWindow(snapshot.ParentHwnd))
                {
                    return;
                }

                AttachToParent(unityHwnd, snapshot.ParentHwnd);

                // 这里不完全信任视图层传入的宽高。HwndHost 的原生子窗口可能先以旧尺寸创建，
                // 随后才被 WPF 布局系统调整。每次显示收敛时直接读取当前父 HWND 的客户区，
                // 可以避免 Unity 偶发只占左侧一部分区域。
                GetEffectiveHostBounds(snapshot, out int x, out int y, out int width, out int height);
                ShowWindowAt(unityHwnd, x, y, width, height);
            }
            finally
            {
                _windowLock.Release();
            }
        }

        private async Task<bool> ApplyHiddenStateOnceAsync(HostStateSnapshot snapshot)
        {
            bool shouldWaitForWindow = snapshot.HiddenPreparationDeadlineUtc > DateTime.UtcNow;
            nint unityHwnd = GetCachedWindowHandle();

            if (unityHwnd == 0 && shouldWaitForWindow)
            {
                try
                {
                    Process process = _getRunningProcess();
                    if (!process.HasExited)
                    {
                        unityHwnd = FindBestWindowHandle(process);
                    }
                }
                catch
                {
                    // Unity 进程尚未启动或已退出时，不需要把隐藏准备过程视为错误。
                }
            }

            if (unityHwnd == 0 || !IsWindow(unityHwnd))
            {
                ClearCachedWindowHandle();
                return !shouldWaitForWindow;
            }

            await _windowLock.WaitAsync().ConfigureAwait(false);
            try
            {
                HostStateSnapshot current = GetHostStateSnapshot();
                if (current.ShouldBeVisible)
                {
                    return false;
                }

                ReparentToDefaultAndHide(unityHwnd);
                return true;
            }
            finally
            {
                _windowLock.Release();
            }
        }

        private bool CanWorkerExit(long observedVersion)
        {
            lock (_stateLock)
            {
                return !_shouldBeVisible && !_stopping && _stateVersion == observedVersion;
            }
        }

        private HostStateSnapshot GetHostStateSnapshot()
        {
            lock (_stateLock)
            {
                return new HostStateSnapshot(
                    _hostParentHwnd,
                    _hostX,
                    _hostY,
                    Math.Max(1, _hostWidth),
                    Math.Max(1, _hostHeight),
                    _hasHostBounds && _hostParentHwnd != 0,
                    _shouldBeVisible,
                    _stopping,
                    _hiddenPreparationDeadlineUtc,
                    _stateVersion);
            }
        }

        private nint GetCachedWindowHandle()
        {
            nint unityHwnd = _unityWindowHwnd;
            if (unityHwnd != 0 && IsWindow(unityHwnd))
            {
                return unityHwnd;
            }

            ClearCachedWindowHandle();
            return 0;
        }

        private void ClearCachedWindowHandle()
        {
            lock (_stateLock)
            {
                _unityWindowHwnd = 0;
                _attachedParentHwnd = 0;
            }
        }

        private nint FindBestWindowHandle(Process process)
        {
            // Unity 启动阶段可能先创建 Splash 或中间窗口，随后才创建真正的 UnityWndClass 主窗口。
            nint freshTopLevelHwnd = FindTopLevelWindowByProcessId(process.Id);
            if (freshTopLevelHwnd != 0 && IsWindow(freshTopLevelHwnd))
            {
                if (freshTopLevelHwnd != _unityWindowHwnd)
                {
                    lock (_stateLock)
                    {
                        _unityWindowHwnd = freshTopLevelHwnd;
                        _attachedParentHwnd = 0;
                    }
                }

                return freshTopLevelHwnd;
            }

            nint cachedHwnd = GetCachedWindowHandle();
            if (cachedHwnd != 0)
            {
                return cachedHwnd;
            }

            process.Refresh();
            nint mainWindowHwnd = process.MainWindowHandle;
            if (mainWindowHwnd != 0 && IsWindow(mainWindowHwnd))
            {
                lock (_stateLock)
                {
                    _unityWindowHwnd = mainWindowHwnd;
                    _attachedParentHwnd = 0;
                }

                return mainWindowHwnd;
            }

            return 0;
        }

        private void ReparentToDefaultAndHide(nint unityHwnd)
        {
            nint defaultParentHwnd;
            lock (_stateLock)
            {
                defaultParentHwnd = _defaultParentHwnd;
            }

            // 先隐藏，再切换父窗口。否则 Unity 仍然可见时被挂回主窗口，可能在主窗口左上角闪一下。
            HideWindowCore(unityHwnd);

            if (defaultParentHwnd != 0 && IsWindow(defaultParentHwnd))
            {
                AttachToParent(unityHwnd, defaultParentHwnd);
            }
        }

        private void AttachToParent(nint unityHwnd, nint parentHwnd)
        {
            if (unityHwnd == 0 || parentHwnd == 0 || !IsWindow(unityHwnd) || !IsWindow(parentHwnd))
            {
                return;
            }

            EnsureInteractiveWindowStyle(unityHwnd);

            long style = GetWindowStyle(unityHwnd).ToInt64();
            long childStyle = (style | WsChild | WsClipChildren | WsClipSiblings) &
                              ~(WsPopup | WsCaption | WsThickFrame | WsMinimize | WsMaximize | WsDisabled);

            if (style != childStyle)
            {
                SetWindowStyle(unityHwnd, new nint(childStyle));
            }

            nint currentParent = GetParent(unityHwnd);
            if (_attachedParentHwnd != parentHwnd || currentParent != parentHwnd)
            {
                SetParent(unityHwnd, parentHwnd);
                _attachedParentHwnd = parentHwnd;
            }

            SetWindowPos(
                unityHwnd,
                HwndTop,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
        }

        private static void GetEffectiveHostBounds(HostStateSnapshot snapshot, out int x, out int y, out int width, out int height)
        {
            x = snapshot.X;
            y = snapshot.Y;
            width = snapshot.Width;
            height = snapshot.Height;

            // 当前方案中 ParentHwnd 是 UnityHostControl 创建的原生宿主窗口。
            // Unity 应始终填满该窗口的客户区，因此优先使用 GetClientRect 的实时值。
            if (snapshot.ParentHwnd != 0 &&
                IsWindow(snapshot.ParentHwnd) &&
                GetClientRect(snapshot.ParentHwnd, out WindowRect clientRect))
            {
                int clientWidth = Math.Max(0, clientRect.Right - clientRect.Left);
                int clientHeight = Math.Max(0, clientRect.Bottom - clientRect.Top);

                if (clientWidth > 2 && clientHeight > 2)
                {
                    x = 0;
                    y = 0;
                    width = clientWidth;
                    height = clientHeight;
                }
            }
        }

        private static void ShowWindowAt(nint unityHwnd, int x, int y, int width, int height)
        {
            int effectiveWidth = Math.Max(1, width);
            int effectiveHeight = Math.Max(1, height);

            // 关键顺序：先在隐藏状态下移动并缩放到宿主区域，再显示。
            // 如果先 ShowWindow，再 MoveWindow，Unity 会短暂显示在默认父窗口的 (0,0) 位置。
            MoveWindow(unityHwnd, x, y, effectiveWidth, effectiveHeight, false);

            EnsureInteractiveWindowStyle(unityHwnd);

            SetWindowPos(
                unityHwnd,
                HwndTop,
                x,
                y,
                effectiveWidth,
                effectiveHeight,
                SwpNoZOrder | SwpFrameChanged);

            ShowWindow(unityHwnd, SwShow);

            SetWindowPos(
                unityHwnd,
                HwndTop,
                x,
                y,
                effectiveWidth,
                effectiveHeight,
                SwpNoZOrder | SwpFrameChanged | SwpShowWindow);

            MoveWindow(unityHwnd, x, y, effectiveWidth, effectiveHeight, true);
            UpdateWindow(unityHwnd);
            FocusUnityWindowCore(unityHwnd);
        }


        /// <summary>
        /// 清理 Unity 窗口的禁用/禁止激活样式，确保嵌入后仍能接收鼠标点击。
        /// </summary>
        private static void EnsureInteractiveWindowStyle(nint unityHwnd)
        {
            if (unityHwnd == 0 || !IsWindow(unityHwnd))
            {
                return;
            }

            EnableWindow(unityHwnd, true);

            long style = GetWindowStyle(unityHwnd).ToInt64();
            long enabledStyle = style & ~WsDisabled;
            if (style != enabledStyle)
            {
                SetWindowStyle(unityHwnd, new nint(enabledStyle));
            }

            long extendedStyle = GetWindowExtendedStyle(unityHwnd).ToInt64();
            long activatableExtendedStyle = extendedStyle & ~WsExNoActivate;
            if (extendedStyle != activatableExtendedStyle)
            {
                SetWindowExtendedStyle(unityHwnd, new nint(activatableExtendedStyle));
            }
        }

        /// <summary>
        /// 显示后主动把输入焦点交给 Unity 子窗口。
        /// Unity 内部 UI 按钮依赖窗口焦点处理鼠标事件，长期使用 SW_SHOWNA/SWP_NOACTIVATE 会导致画面可见但按钮无响应。
        /// </summary>
        private static void FocusUnityWindowCore(nint unityHwnd)
        {
            if (unityHwnd == 0 || !IsWindow(unityHwnd))
            {
                return;
            }

            BringWindowToTop(unityHwnd);
            SetActiveWindow(unityHwnd);
            SetFocus(unityHwnd);
        }

        private static void HideWindowCore(nint unityHwnd)
        {
            ShowWindow(unityHwnd, SwHide);
            SetWindowPos(
                unityHwnd,
                HwndTop,
                0,
                0,
                1,
                1,
                SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpHideWindow);
        }

        private static nint FindTopLevelWindowByProcessId(int processId)
        {
            List<WindowCandidate> candidates = new();

            EnumWindows((hwnd, _) =>
            {
                if (hwnd == GetShellWindow() || !IsWindow(hwnd))
                {
                    return true;
                }

                _ = (nint)GetWindowThreadProcessId(hwnd, out int windowProcessId);
                if (windowProcessId != processId || GetWindow(hwnd, GwOwner) != 0)
                {
                    return true;
                }

                string className = GetWindowClassName(hwnd);
                bool isVisible = IsWindowVisible(hwnd);
                int area = GetWindowArea(hwnd);
                bool isUnityWindow = className.Contains("UnityWndClass", StringComparison.OrdinalIgnoreCase);

                candidates.Add(new WindowCandidate(hwnd, isUnityWindow, isVisible, area));
                return true;
            }, 0);

            return candidates
                .OrderByDescending(candidate => candidate.IsUnityWindow)
                .ThenByDescending(candidate => candidate.IsVisible)
                .ThenByDescending(candidate => candidate.Area)
                .Select(candidate => candidate.Hwnd)
                .FirstOrDefault();
        }

        private static string GetWindowClassName(nint hwnd)
        {
            StringBuilder className = new(256);
            int length = GetClassName(hwnd, className, className.Capacity);
            return length > 0 ? className.ToString(0, length) : string.Empty;
        }

        private static int GetWindowArea(nint hwnd)
        {
            if (!GetWindowRect(hwnd, out WindowRect rect))
            {
                return 0;
            }

            int width = Math.Max(0, rect.Right - rect.Left);
            int height = Math.Max(0, rect.Bottom - rect.Top);
            return width * height;
        }

        private static nint GetWindowStyle(nint hWnd)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, GwlStyle)
                : GetWindowLong32(hWnd, GwlStyle);
        }

        private static nint SetWindowStyle(nint hWnd, nint style)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, GwlStyle, style)
                : SetWindowLong32(hWnd, GwlStyle, style);
        }

        private static nint GetWindowExtendedStyle(nint hWnd)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, GwlExStyle)
                : GetWindowLong32(hWnd, GwlExStyle);
        }

        private static nint SetWindowExtendedStyle(nint hWnd, nint style)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, GwlExStyle, style)
                : SetWindowLong32(hWnd, GwlExStyle, style);
        }

        private void Log(string message)
        {
            LogReceived?.Invoke(this, message);
            Debug.WriteLine($"[UnityWindowHost] {message}");
        }

        private readonly record struct HostStateSnapshot(
            nint ParentHwnd,
            int X,
            int Y,
            int Width,
            int Height,
            bool HasHostBounds,
            bool ShouldBeVisible,
            bool IsStopping,
            DateTime HiddenPreparationDeadlineUtc,
            long Version);

        private readonly record struct WindowCandidate(
            nint Hwnd,
            bool IsUnityWindow,
            bool IsVisible,
            int Area);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            nint hWnd,
            nint hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(nint hWnd, int x, int y, int width, int height, bool repaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateWindow(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint SetParent(nint hWndChild, nint hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint GetParent(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(nint hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnableWindow(nint hWnd, bool enable);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool BringWindowToTop(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint SetActiveWindow(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint SetFocus(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindow(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(nint hWnd, out int processId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint GetWindow(nint hWnd, uint command);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint GetShellWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassName(nint hWnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(nint hWnd, out WindowRect rect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(nint hWnd, out WindowRect rect);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern nint GetWindowLong32(nint hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern nint SetWindowLong32(nint hWnd, int nIndex, nint dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);
    }
}
