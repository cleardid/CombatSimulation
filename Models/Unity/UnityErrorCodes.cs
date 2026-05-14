namespace CombatSimulation.Models.Unity
{
    /// <summary>
    /// 定义 WPF 与 Unity 通信中使用的错误码常量。
    /// </summary>
    /// <remarks>
    /// 错误码通常写入 <see cref="UnityError.Code"/> 字段，用于让调用方根据错误类型执行分支处理。
    /// </remarks>
    public static class UnityErrorCodes
    {
        /// <summary>
        /// 未分类错误。无法归入其他明确错误码时使用。
        /// </summary>
        public const string UnknownError = "UNKNOWN_ERROR";

        /// <summary>
        /// 消息格式无效，例如 JSON 反序列化失败、缺少必要字段或消息类型非法。
        /// </summary>
        public const string InvalidMessage = "INVALID_MESSAGE";

        /// <summary>
        /// 未知命令。接收方无法识别 <c>command</c> 字段。
        /// </summary>
        public const string UnknownCommand = "UNKNOWN_COMMAND";

        /// <summary>
        /// 数据内容无效，例如字段类型错误、参数范围不合法或必要数据为空。
        /// </summary>
        public const string InvalidData = "INVALID_DATA";

        /// <summary>
        /// Unity 尚未完成初始化，暂时无法处理当前命令。
        /// </summary>
        public const string UnityNotReady = "UNITY_NOT_READY";

        /// <summary>
        /// 未找到指定目标对象。
        /// </summary>
        public const string TargetNotFound = "TARGET_NOT_FOUND";

        /// <summary>
        /// 未找到指定模型资源。
        /// </summary>
        public const string ModelNotFound = "MODEL_NOT_FOUND";

        /// <summary>
        /// 模型加载失败，通常表示资源路径错误、文件损坏或 Unity 端加载异常。
        /// </summary>
        public const string ModelLoadFailed = "MODEL_LOAD_FAILED";

        /// <summary>
        /// 命令执行或请求等待超时。
        /// </summary>
        public const string CommandTimeout = "COMMAND_TIMEOUT";
    }
}
