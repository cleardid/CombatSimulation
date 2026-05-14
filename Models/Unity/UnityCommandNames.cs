namespace CombatSimulation.Models.Unity
{
    /// <summary>
    /// 定义 WPF 与 Unity 通信时使用的命令名和事件名。
    /// </summary>
    /// <remarks>
    /// 这些常量会写入 <c>UnityMessage.Command</c> 字段，用于标识当前消息的业务含义。
    /// 命令名需要与 Unity 端约定保持一致。
    /// </remarks>
    public static class UnityCommandNames
    {
        /// <summary>
        /// 心跳检测命令，用于验证 TCP 通道是否可用。
        /// </summary>
        public const string Ping = "ping";

        /// <summary>
        /// 加载模型命令，通常由 WPF 通知 Unity 加载指定目标或结构模型。
        /// </summary>
        public const string LoadModel = "load_model";

        /// <summary>
        /// 设置属性命令，用于修改 Unity 中对象、组件或场景的指定属性。
        /// </summary>
        public const string SetProperty = "set_property";

        /// <summary>
        /// 同步目标数据命令，用于将 WPF 侧目标结构信息同步到 Unity。
        /// </summary>
        public const string SyncTarget = "sync_target";

        /// <summary>
        /// 聚焦相机命令，用于控制 Unity 相机定位到指定目标或部件。
        /// </summary>
        public const string FocusCamera = "focus_camera";

        /// <summary>
        /// 模型加载完成事件，由 Unity 通知 WPF 指定模型已经完成加载。
        /// </summary>
        public const string ModelLoaded = "model_loaded";

        /// <summary>
        /// 属性变化事件，由 Unity 通知 WPF 某个对象或组件属性已经发生变化。
        /// </summary>
        public const string PropertyChanged = "property_changed";

        /// <summary>
        /// 错误事件，由 Unity 主动上报非请求响应场景下的错误信息。
        /// </summary>
        public const string Error = "error";
    }
}
