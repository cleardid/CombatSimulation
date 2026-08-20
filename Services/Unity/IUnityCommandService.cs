using CombatSimulation.Models.Unity;

namespace CombatSimulation.Services.Unity;

/// <summary>
/// WPF 向 Unity 发送业务命令的统一入口。
/// </summary>
/// <remarks>
/// 当前接口承载目标结构、数据库快照和通用 request/event 通信。
/// 后续如果新增毁伤树、推演控制、视角控制等命令，应优先在该接口中扩展，
/// 不再为每一个界面重新创建一套 TCP 调用服务。
/// </remarks>
public interface IUnityCommandService
{
    /// <summary>
    /// Unity 主动发送 event 时触发。
    /// </summary>
    /// <remarks>
    /// 典型用途是监听 <see cref="UnityCommandNames.ServerReady"/>，然后由 WPF 推送数据库快照。
    /// </remarks>
    event EventHandler<UnityMessage>? EventReceived;

    /// <summary>
    /// 通知 Unity 显示指定目标。
    /// data 为目标唯一标识。
    /// </summary>
    Task ShowTargetAsync(string targetCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通知 Unity 高亮指定部件。
    /// data 为部件唯一标识。
    /// </summary>
    Task HighlightPartAsync(string partCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通知 Unity 显示当前勾选的全部部件。
    /// data 为部件唯一标识列表。
    /// </summary>
    Task ShowPartsAsync(IReadOnlyList<string> partCodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向 Unity 推送当前 MySQL 数据库快照。
    /// </summary>
    /// <remarks>
    /// Unity 收到后只更新运行期缓存，不写数据库。
    /// </remarks>
    Task LoadDatabaseSnapshotAsync(CombatDatabaseSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送不要求 Unity 返回业务结果的事件命令。
    /// </summary>
    Task SendEventAsync(string command, object? data = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送要求 Unity 返回处理结果的请求命令。
    /// </summary>
    Task SendRequestAsync(string command, object? data = null, CancellationToken cancellationToken = default);
}
