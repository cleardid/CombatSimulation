namespace CombatSimulation.Models.Unity;

/// <summary>
/// 定义 WPF 与 Unity 通信时使用的命令名和事件名。
/// </summary>
/// <remarks>
/// 这些常量会写入 <c>UnityMessage.Command</c> 字段，用于标识当前消息的业务含义。
/// 字符串必须与 Unity 端 <c>UnityCommandNames</c> 保持一致。
/// </remarks>
public static class UnityCommandNames
{
    /// <summary>
    /// 心跳检测命令，用于验证 TCP 通道是否可用。
    /// </summary>
    public const string Ping = "ping";

    /// <summary>
    /// 显示目标命令。data 为目标唯一标识。
    /// </summary>
    public const string ShowTarget = "show_target";

    /// <summary>
    /// 高亮部件命令。data 为部件唯一标识。
    /// </summary>
    public const string HighlightPart = "highlight_part";

    /// <summary>
    /// 显示部件命令。data 为当前需要显示的部件唯一标识列表。
    /// </summary>
    public const string ShowParts = "show_parts";

    /// <summary>
    /// 修改目标中的单个部件。data 为 Target_Part_Info_By_MySQL。
    /// </summary>
    public const string UpdateTarget = "update_target";

    /// <summary>
    /// 加载数据库快照。data 为 CombatDatabaseSnapshot。
    /// WPF 在 Unity 上报 server_ready 后以及数据库发生结构性变化后发送该命令。
    /// </summary>
    public const string LoadDatabaseSnapshot = "load_database_snapshot";

    /// <summary>
    /// Unity 请求 WPF 执行数据库变更的通用命令。
    /// Unity 侧不直接写 MySQL，确需持久化时通过该命令交由 WPF 处理。
    /// </summary>
    public const string RequestDatabaseMutation = "request_database_mutation";

    /// <summary>
    /// Unity 请求 WPF 重新推送数据库快照的命令。
    /// 当前主流程由 WPF 在 server_ready 后主动推送，此命令作为后续兜底扩展保留。
    /// </summary>
    public const string RequestDatabaseSnapshot = "request_database_snapshot";

    /// <summary>
    /// Unity TCP 客户端已连接事件。
    /// </summary>
    public const string ClientConnected = "client_connected";

    /// <summary>
    /// Unity TCP Server 已准备接收命令事件。
    /// WPF 收到该事件后应立即发送 load_database_snapshot。
    /// </summary>
    public const string ServerReady = "server_ready";

    /// <summary>
    /// Unity TCP 客户端断开连接事件。
    /// </summary>
    public const string ClientDisconnected = "client_disconnected";

    /// <summary>
    /// 加载模型命令。保留旧协议兼容。
    /// </summary>
    public const string LoadModel = "load_model";

    /// <summary>
    /// 设置属性命令。保留旧协议兼容。
    /// </summary>
    public const string SetProperty = "set_property";

    /// <summary>
    /// 同步目标数据命令。保留旧协议兼容。
    /// </summary>
    public const string SyncTarget = "sync_target";

    /// <summary>
    /// 聚焦相机命令。保留旧协议兼容。
    /// </summary>
    public const string FocusCamera = "focus_camera";

    /// <summary>
    /// 模型加载完成事件。保留旧协议兼容。
    /// </summary>
    public const string ModelLoaded = "model_loaded";

    /// <summary>
    /// 属性变化事件。保留旧协议兼容。
    /// </summary>
    public const string PropertyChanged = "property_changed";

    /// <summary>
    /// 错误事件，由 Unity 主动上报非请求响应场景下的错误信息。
    /// </summary>
    public const string Error = "error";
}
