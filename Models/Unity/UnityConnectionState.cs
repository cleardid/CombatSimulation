namespace CombatSimulation.Models.Unity
{
    /// <summary>
    /// 表示 WPF 到 Unity TCP 服务端的连接状态。
    /// </summary>
    /// <remarks>
    /// 该状态通常由网络服务层维护，并通过事件通知界面层刷新连接提示或按钮可用状态。
    /// </remarks>
    public enum UnityConnectionState
    {
        /// <summary>
        /// 当前未连接 Unity TCP 服务端。
        /// </summary>
        Disconnected,

        /// <summary>
        /// 正在建立 TCP 连接。
        /// </summary>
        Connecting,

        /// <summary>
        /// TCP 连接已经建立，可以发送请求或事件。
        /// </summary>
        Connected,

        /// <summary>
        /// 连接中断后正在尝试重新连接。
        /// </summary>
        Reconnecting,

        /// <summary>
        /// 正在主动断开 TCP 连接。
        /// </summary>
        Disconnecting,

        /// <summary>
        /// 连接进入异常状态，通常表示连接失败、协议解析失败或接收循环出现异常。
        /// </summary>
        Faulted
    }
}
