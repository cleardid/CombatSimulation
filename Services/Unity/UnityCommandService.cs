using CombatSimulation.Models.Unity;

namespace CombatSimulation.Services.Unity;

/// <summary>
/// Unity 命令服务的默认实现。
/// </summary>
/// <remarks>
/// 该类只负责把 WPF 侧的业务调用转换为 Unity TCP 协议消息，
/// 不直接参与界面状态维护和数据库更新。
/// </remarks>
public sealed class UnityCommandService : IUnityCommandService, IDisposable
{
    private readonly UnityService _unityService;
    private bool _disposed;

    /// <summary>
    /// Unity 主动上报 event 时触发。
    /// </summary>
    public event EventHandler<UnityMessage>? EventReceived;

    /// <summary>
    /// 通过底层 UnityService 创建命令服务。
    /// </summary>
    public UnityCommandService(UnityService unityService)
    {
        _unityService = unityService ?? throw new ArgumentNullException(nameof(unityService));
        _unityService.EventReceived += OnUnityEventReceived;
    }

    /// <summary>
    /// 显示指定目标。
    /// </summary>
    public Task ShowTargetAsync(string targetCode, CancellationToken cancellationToken = default)
    {
        return SendEventAsync(UnityCommandNames.ShowTarget, targetCode, cancellationToken);
    }

    /// <summary>
    /// 高亮指定部件。
    /// </summary>
    public Task HighlightPartAsync(string partCode, CancellationToken cancellationToken = default)
    {
        return SendEventAsync(UnityCommandNames.HighlightPart, partCode, cancellationToken);
    }

    /// <summary>
    /// 显示当前勾选部件集合。
    /// </summary>
    public Task ShowPartsAsync(IReadOnlyList<string> partCodes, CancellationToken cancellationToken = default)
    {
        // 复制一份列表，避免调用方后续修改集合时影响正在序列化的 Unity 消息。
        return SendEventAsync(UnityCommandNames.ShowParts, partCodes.ToList(), cancellationToken);
    }


    /// <summary>
    /// 向 Unity 推送数据库快照。
    /// </summary>
    public Task LoadDatabaseSnapshotAsync(CombatDatabaseSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        // 快照用于初始化或刷新 Unity 运行期全局表缓存，使用 request 便于 WPF 确认 Unity 已接收成功。
        return SendRequestAsync(UnityCommandNames.LoadDatabaseSnapshot, snapshot, cancellationToken);
    }

    /// <summary>
    /// 发送事件命令，适用于“显示目标”“高亮部件”等不需要业务回执的操作。
    /// </summary>
    public Task SendEventAsync(string command, object? data = null, CancellationToken cancellationToken = default)
    {
        return _unityService.SendEventAsync(command, data, cancellationToken);
    }

    /// <summary>
    /// 发送请求命令，Unity 返回失败时转换为异常，由上层决定是否回滚数据库操作。
    /// </summary>
    public async Task SendRequestAsync(string command, object? data = null, CancellationToken cancellationToken = default)
    {
        UnityMessage response = await _unityService
            .SendRequestAsync(command, data, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (response.Success == false)
        {
            string message = response.Error?.Message ?? $"Unity 命令执行失败：{command}";
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 解除对长生命周期 UnityService 的事件订阅。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _unityService.EventReceived -= OnUnityEventReceived;
        EventReceived = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 将底层 UnityService 收到的事件转发给依赖 IUnityCommandService 的业务层。
    /// </summary>
    private void OnUnityEventReceived(object? sender, UnityMessage message)
    {
        EventReceived?.Invoke(this, message);
    }
}
