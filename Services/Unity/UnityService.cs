using CombatSimulation.Models.Unity;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace CombatSimulation.Services.Unity;

/// <summary>
/// Unity 全局访问服务。
/// 对外保留单一入口，内部把 TCP 通信、Unity 进程生命周期和 Unity 窗口托管拆分到独立组件。
/// </summary>
public sealed class UnityService : IAsyncDisposable
{
    private static readonly TimeSpan StartupConnectTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StartupRetryInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StartupHostWaitTimeout = TimeSpan.FromSeconds(3);

    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<UnityMessage>> _pendingRequests = new();
    private readonly UnityTcpClient _tcpClient = new();
    private readonly UnityProcessManager _processManager = new();
    private readonly UnityWindowHost _windowHost;
    private readonly object _startupParentLock = new();

    private TaskCompletionSource<nint> _startupHostAvailable = CreateStartupHostCompletion();
    private nint _defaultParentHwnd;
    private nint _preferredHostHwnd;


    private bool _disposed;
    private bool _stopping;

    public static UnityService Instance { get; } = new();

    public UnityConnectionState State => _tcpClient.State;
    public bool IsConnected => _tcpClient.IsConnected;
    public bool IsUnityProcessRunning => _processManager.IsRunning;

    public event EventHandler<UnityConnectionState>? StateChanged;
    public event EventHandler<UnityMessage>? EventReceived;
    public event EventHandler<string>? LogReceived;

    private UnityService()
    {
        _windowHost = new UnityWindowHost(_processManager.GetRunningProcess);

        _tcpClient.StateChanged += (_, state) => StateChanged?.Invoke(this, state);
        // 当接收到信息时
        _tcpClient.MessageReceived += OnTcpMessageReceived;
        _tcpClient.ConnectionLost += OnTcpConnectionLost;
        _tcpClient.LogReceived += (_, message) => Log(message);
        _processManager.LogReceived += (_, message) => Log(message);
        _windowHost.LogReceived += (_, message) => Log(message);
    }

    /// <summary>
    /// 启动 Unity 进程并连接 Unity TCP Server。
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
        CancellationToken effectiveToken = linkedCts.Token;

        await _lifecycleLock.WaitAsync(effectiveToken).ConfigureAwait(false);
        try
        {
            effectiveToken.ThrowIfCancellationRequested();
            _stopping = false;

            if (IsConnected)
            {
                return;
            }

            await StartProcessAndPrepareWindowAsync(effectiveToken).ConfigureAwait(false);
            await ConnectWithRetryAsync(StartupConnectTimeout, effectiveToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// 断开 TCP 连接，并关闭当前程序启动的 Unity 进程。
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        _stopping = true;
        _windowHost.Stop();

        if (!_shutdownCts.IsCancellationRequested)
        {
            _shutdownCts.Cancel();
        }

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _tcpClient.CloseAsync(setDisconnectedState: true).ConfigureAwait(false);
            FailPendingRequests(new IOException("Unity 连接已主动断开。"));
            _processManager.StopIfNeeded();
            _windowHost.Reset();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// 发送 request，并等待相同 msgId 的 response。
    /// 调用方不需要关心 Unity 是否已经启动；未连接时会先尝试启动和连接。
    /// </summary>
    public async Task<UnityMessage> SendRequestAsync(
        string command,
        object? data = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Unity request command 不能为空。", nameof(command));
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        UnityMessage request = UnityMessage.CreateRequest(command, data);
        string msgId = request.MsgId ?? throw new InvalidOperationException("Unity request msgId 创建失败。");
        TimeSpan effectiveTimeout = timeout ?? DefaultRequestTimeout;

        var completion = new TaskCompletionSource<UnityMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingRequests.TryAdd(msgId, completion))
        {
            throw new InvalidOperationException($"Unity request msgId 冲突：{msgId}");
        }

        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        using var timeoutRegistration = timeoutCts.Token.Register(() =>
        {
            if (_pendingRequests.TryRemove(msgId, out TaskCompletionSource<UnityMessage>? pending))
            {
                pending.TrySetException(new TimeoutException($"Unity request 超时：{command}，msgId={msgId}"));
            }
        });

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            if (_pendingRequests.TryRemove(msgId, out TaskCompletionSource<UnityMessage>? pending))
            {
                pending.TrySetCanceled(cancellationToken);
            }
        });

        try
        {
            await _tcpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            Log($"已发送 Unity request：{command}，msgId={msgId}");
            return await completion.Task.ConfigureAwait(false);
        }
        catch
        {
            if (_pendingRequests.TryRemove(msgId, out TaskCompletionSource<UnityMessage>? pending))
            {
                pending.TrySetCanceled();
            }

            throw;
        }
        finally
        {
            _pendingRequests.TryRemove(msgId, out _);
        }
    }

    /// <summary>
    /// 发送不等待 response 的 event 消息。
    /// </summary>
    public async Task SendEventAsync(
        string command,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Unity event command 不能为空。", nameof(command));
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        UnityMessage message = UnityMessage.CreateEvent(command, data);
        await _tcpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        Log($"已发送 Unity event：{command}");
    }

    /// <summary>
    /// 设置 Unity 窗口默认父窗口。
    /// Unity 进程启动后会被设置为该 WPF 主窗口的子窗口，并默认隐藏。
    /// </summary>
    public void SetDefaultParentWindow(nint parentHwnd)
    {
        lock (_startupParentLock)
        {
            _defaultParentHwnd = parentHwnd;
        }

        _windowHost.SetDefaultParentWindow(parentHwnd);

        if (parentHwnd != 0 && _processManager.IsRunning && !_shutdownCts.IsCancellationRequested)
        {
            _ = _windowHost.PrepareHiddenAsync(_shutdownCts.Token);
        }
    }

    /// <summary>
    /// 更新 Unity 窗口的宿主区域。
    /// </summary>
    /// <remarks>
    /// 坐标必须是相对于 WPF 主窗口客户区左上角的物理像素坐标。
    /// 该方法只缓存位置和大小；是否显示由 <see cref="ShowUnityWindowAsync(CancellationToken)"/> 控制。
    /// </remarks>
    public void SetUnityWindowHostBounds(nint parentHwnd, int x, int y, int width, int height)
    {
        if (parentHwnd == 0 || width <= 0 || height <= 0)
        {
            return;
        }

        RegisterPreferredHost(parentHwnd);
        _windowHost.SetHostBounds(parentHwnd, x, y, width, height);
    }

    /// <summary>
    /// 按当前缓存的宿主区域显示 Unity 窗口。
    /// </summary>
    public async Task ShowUnityWindowAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _processManager.StartIfNeeded(GetPreferredStartupParent());
        await _windowHost.ShowAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 更新 Unity 窗口宿主区域，并立即按该区域显示 Unity 窗口。
    /// </summary>
    /// <remarks>
    /// 保留该重载是为了兼容旧调用。新代码应优先调用
    /// <see cref="SetUnityWindowHostBounds"/> 后再调用 <see cref="ShowUnityWindowAsync(CancellationToken)"/>。
    /// </remarks>
    public async Task ShowUnityWindowAsync(
        nint parentHwnd,
        int x,
        int y,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        SetUnityWindowHostBounds(parentHwnd, x, y, width, height);
        await ShowUnityWindowAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 隐藏 Unity 窗口，但不关闭 Unity 进程，也不断开 TCP 长连接。
    /// </summary>
    public void HideUnityWindow()
    {
        _windowHost.Hide();
    }

    /// <summary>
    /// 原生宿主 HWND 即将销毁时，将 Unity 窗口安全地脱离该宿主。
    /// 普通页面 Visibility 切换只应调用 <see cref="HideUnityWindow"/>。
    /// </summary>
    public void DetachUnityWindowFromHost(nint hostHwnd)
    {
        _windowHost.DetachFromHost(hostHwnd);
        UnregisterPreferredHost(hostHwnd);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return;
        }

        await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task StartProcessAndPrepareWindowAsync(CancellationToken cancellationToken)
    {
        nint startupParent = await WaitForStartupParentAsync(cancellationToken).ConfigureAwait(false);
        _processManager.StartIfNeeded(startupParent);
        if (!_shutdownCts.IsCancellationRequested)
        {
            _ = _windowHost.PrepareHiddenAsync(_shutdownCts.Token);
        }
    }

    // Prefer the real HwndHost used by the target-structure view. If that view is not
    // created, fall back to the main WPF window after a short bounded wait.
    private async Task<nint> WaitForStartupParentAsync(CancellationToken cancellationToken)
    {
        Task<nint> hostTask;
        nint fallbackParent;

        lock (_startupParentLock)
        {
            if (_preferredHostHwnd != 0)
            {
                return _preferredHostHwnd;
            }

            hostTask = _startupHostAvailable.Task;
            fallbackParent = _defaultParentHwnd;
        }

        try
        {
            return await hostTask.WaitAsync(StartupHostWaitTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            lock (_startupParentLock)
            {
                if (_preferredHostHwnd != 0)
                {
                    return _preferredHostHwnd;
                }

                return _defaultParentHwnd != 0 ? _defaultParentHwnd : fallbackParent;
            }
        }
    }

    private void RegisterPreferredHost(nint hostHwnd)
    {
        if (hostHwnd == 0)
        {
            return;
        }

        TaskCompletionSource<nint> completion;
        lock (_startupParentLock)
        {
            _preferredHostHwnd = hostHwnd;
            completion = _startupHostAvailable;
        }

        completion.TrySetResult(hostHwnd);
    }

    private void UnregisterPreferredHost(nint hostHwnd)
    {
        if (hostHwnd == 0)
        {
            return;
        }

        lock (_startupParentLock)
        {
            if (_preferredHostHwnd != hostHwnd)
            {
                return;
            }

            _preferredHostHwnd = 0;
            _startupHostAvailable = CreateStartupHostCompletion();
        }
    }

    private nint GetPreferredStartupParent()
    {
        lock (_startupParentLock)
        {
            return _preferredHostHwnd != 0 ? _preferredHostHwnd : _defaultParentHwnd;
        }
    }

    private static TaskCompletionSource<nint> CreateStartupHostCompletion()
    {
        return new TaskCompletionSource<nint>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private async Task ConnectWithRetryAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        DateTime deadlineUtc = DateTime.UtcNow + timeout;
        Exception? lastException = null;

        while (DateTime.UtcNow < deadlineUtc)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _tcpClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is IOException or SocketException or TimeoutException or InvalidOperationException)
            {
                lastException = ex;
                Log($"Unity TCP 连接失败，稍后重试：{ex.Message}");
                await _tcpClient.CloseAsync(setDisconnectedState: true).ConfigureAwait(false);
            }

            await Task.Delay(StartupRetryInterval, cancellationToken).ConfigureAwait(false);
        }

        _tcpClient.MarkFaulted();
        throw new TimeoutException("未能在限定时间内连接 Unity TCP Server。", lastException);
    }

    private void OnTcpMessageReceived(object? sender, UnityMessage message)
    {
        // 响应回复
        if (string.Equals(message.Type, UnityMessageTypes.Response, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(message.MsgId) &&
                _pendingRequests.TryRemove(message.MsgId, out TaskCompletionSource<UnityMessage>? pending))
            {
                pending.TrySetResult(message);
            }
            else
            {
                Log($"收到未匹配的 Unity response：{message.Command}，msgId={message.MsgId}");
            }

            return;
        }

        // 事件回复
        if (string.Equals(message.Type, UnityMessageTypes.Event, StringComparison.OrdinalIgnoreCase))
        {
            EventReceived?.Invoke(this, message);
            return;
        }

        Log($"收到未处理的 Unity 消息类型：{message.Type}");
    }

    private void OnTcpConnectionLost(object? sender, Exception exception)
    {
        if (_disposed || _stopping)
        {
            return;
        }

        FailPendingRequests(exception);
        BeginReconnect();
    }

    private void BeginReconnect()
    {
        if (_disposed || _stopping)
        {
            return;
        }

        if (!_reconnectLock.Wait(0))
        {
            return;
        }

        _ = Task.Run(ReconnectLoopAsync);
    }

    private async Task ReconnectLoopAsync()
    {
        try
        {
            while (!_disposed && !_stopping && !IsConnected)
            {
                _tcpClient.MarkReconnecting();
                await Task.Delay(ReconnectInterval, _shutdownCts.Token).ConfigureAwait(false);

                try
                {
                    await _lifecycleLock.WaitAsync(_shutdownCts.Token).ConfigureAwait(false);
                    try
                    {
                        if (_disposed || _stopping || IsConnected)
                        {
                            return;
                        }

                        await StartProcessAndPrepareWindowAsync(_shutdownCts.Token).ConfigureAwait(false);
                        await _tcpClient.ConnectAsync(_shutdownCts.Token).ConfigureAwait(false);
                        return;
                    }
                    finally
                    {
                        _lifecycleLock.Release();
                    }
                }
                catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (ex is IOException or SocketException or TimeoutException or InvalidOperationException)
                {
                    Log($"Unity TCP 自动重连失败：{ex.Message}");
                    await _tcpClient.CloseAsync(setDisconnectedState: false).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
        {
        }
        finally
        {
            _reconnectLock.Release();
        }
    }

    private void FailPendingRequests(Exception exception)
    {
        foreach (KeyValuePair<string, TaskCompletionSource<UnityMessage>> item in _pendingRequests)
        {
            if (_pendingRequests.TryRemove(item.Key, out TaskCompletionSource<UnityMessage>? pending))
            {
                pending.TrySetException(exception);
            }
        }
    }

    private void Log(string message)
    {
        LogReceived?.Invoke(this, message);
        Debug.WriteLine($"[UnityService] {message}");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UnityService));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _disposed = true;
    }
}
