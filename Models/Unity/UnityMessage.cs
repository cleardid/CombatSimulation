using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CombatSimulation.Models.Unity
{
    /// <summary>
    /// 表示 WPF 与 Unity 之间传输的统一 JSON 消息模型。
    /// </summary>
    /// <remarks>
    /// TCP 层使用 4 字节小端长度头加 UTF-8 JSON 负载进行分帧。
    /// 本类型只描述 JSON 负载内容，不包含 TCP 长度头。
    /// </remarks>
    public sealed class UnityMessage
    {
        /// <summary>
        /// 消息 ID
        /// </summary>
        private static long _msgId = 0;

        /// <summary>
        /// 消息标识。
        /// </summary>
        /// <remarks>
        /// 请求和响应通过该字段进行匹配。事件消息可以没有 <c>msgId</c>，也可以由发送方指定。
        /// </remarks>
        [JsonProperty("msgId", NullValueHandling = NullValueHandling.Include)]
        public string? MsgId { get; set; }

        /// <summary>
        /// 消息类型。
        /// </summary>
        /// <remarks>
        /// 取值通常为 <see cref="UnityMessageTypes.Request"/>、<see cref="UnityMessageTypes.Response"/> 或 <see cref="UnityMessageTypes.Event"/>。
        /// </remarks>
        [JsonProperty("type")]
        public string Type { get; set; } = UnityMessageTypes.Request;

        /// <summary>
        /// 命令名或事件名。
        /// </summary>
        /// <remarks>
        /// 建议使用 <see cref="UnityCommandNames"/> 中定义的常量，保证 WPF 端和 Unity 端协议一致。
        /// </remarks>
        [JsonProperty("command")]
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// 消息业务数据。
        /// </summary>
        /// <remarks>
        /// 请求、响应和事件都可以携带该字段。具体结构由 <see cref="Command"/> 对应的业务协议决定。
        /// </remarks>
        [JsonProperty("data", NullValueHandling = NullValueHandling.Include)]
        public JToken? Data { get; set; }

        /// <summary>
        /// 响应是否成功。
        /// </summary>
        /// <remarks>
        /// 该字段只在 <see cref="Type"/> 为 <see cref="UnityMessageTypes.Response"/> 时序列化。
        /// </remarks>
        [JsonProperty("success")]
        public bool? Success { get; set; }

        /// <summary>
        /// 响应失败时的错误信息。
        /// </summary>
        /// <remarks>
        /// 该字段只在 <see cref="Type"/> 为 <see cref="UnityMessageTypes.Response"/> 时序列化。
        /// 成功响应中通常为 <c>null</c>。
        /// </remarks>
        [JsonProperty("error", NullValueHandling = NullValueHandling.Include)]
        public UnityError? Error { get; set; }

        /// <summary>
        /// 创建一条请求消息。
        /// </summary>
        /// <param name="command">请求命令名。</param>
        /// <param name="data">可选请求数据，会被转换为 <see cref="JToken"/>。</param>
        /// <returns>包含新 <c>msgId</c> 的请求消息。</returns>
        public static UnityMessage CreateRequest(string command, object? data = null)
        {
            return new UnityMessage
            {
                // MsgId = Guid.NewGuid().ToString("D"),
                MsgId = _msgId++.ToString(),
                Type = UnityMessageTypes.Request,
                Command = command,
                Data = ToJToken(data),
                Success = null,
                Error = null
            };
        }

        /// <summary>
        /// 基于请求消息创建成功响应。
        /// </summary>
        /// <param name="request">原始请求消息，响应会复用其 <c>msgId</c> 和 <c>command</c>。</param>
        /// <param name="data">可选响应数据，会被转换为 <see cref="JToken"/>。</param>
        /// <returns>成功响应消息。</returns>
        public static UnityMessage CreateSuccessResponse(UnityMessage request, object? data = null)
        {
            return new UnityMessage
            {
                MsgId = request.MsgId,
                Type = UnityMessageTypes.Response,
                Command = request.Command,
                Data = ToJToken(data),
                Success = true,
                Error = null
            };
        }

        /// <summary>
        /// 基于请求消息创建失败响应。
        /// </summary>
        /// <param name="request">原始请求消息，响应会复用其 <c>msgId</c> 和 <c>command</c>。</param>
        /// <param name="code">错误码，建议使用 <see cref="UnityErrorCodes"/> 中的常量。</param>
        /// <param name="message">错误描述。</param>
        /// <param name="details">可选扩展错误信息，会被转换为 <see cref="JToken"/>。</param>
        /// <returns>失败响应消息。</returns>
        public static UnityMessage CreateFailureResponse(UnityMessage request, string code, string message, object? details = null)
        {
            return new UnityMessage
            {
                MsgId = request.MsgId,
                Type = UnityMessageTypes.Response,
                Command = request.Command,
                Data = null,
                Success = false,
                Error = new UnityError
                {
                    Code = string.IsNullOrWhiteSpace(code) ? UnityErrorCodes.UnknownError : code,
                    Message = message ?? string.Empty,
                    Details = details == null ? null : JToken.FromObject(details)
                }
            };
        }

        /// <summary>
        /// 创建一条事件消息。
        /// </summary>
        /// <param name="command">事件名。</param>
        /// <param name="data">可选事件数据，会被转换为 <see cref="JToken"/>。</param>
        /// <param name="msgId">可选消息标识。事件通常不依赖该字段。</param>
        /// <returns>事件消息。</returns>
        public static UnityMessage CreateEvent(string command, object? data = null, string? msgId = null)
        {
            return new UnityMessage
            {
                MsgId = msgId,
                Type = UnityMessageTypes.Event,
                Command = command,
                Data = ToJToken(data),
                Success = null,
                Error = null
            };
        }

        /// <summary>
        /// 控制 Newtonsoft.Json 是否序列化 <see cref="Success"/> 属性。
        /// 此方法是在使用 JsonConvert.SerializeObject 序列化此类对象是自动调用
        /// </summary>
        /// <returns>仅响应消息返回 <c>true</c>。</returns>
        public bool ShouldSerializeSuccess()
        {
            return string.Equals(Type, UnityMessageTypes.Response, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 控制 Newtonsoft.Json 是否序列化 <see cref="Error"/> 属性。
        /// 此方法是在使用 JsonConvert.SerializeObject 序列化此类对象是自动调用
        /// </summary>
        /// <returns>仅响应消息返回 <c>true</c>。</returns>
        public bool ShouldSerializeError()
        {
            return string.Equals(Type, UnityMessageTypes.Response, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将 <see cref="Data"/> 转换为指定类型。
        /// </summary>
        /// <typeparam name="T">目标数据类型。</typeparam>
        /// <returns>转换后的对象；当 <see cref="Data"/> 为空时返回默认值。</returns>
        public T? GetData<T>()
        {
            return Data == null ? default : Data.ToObject<T>();
        }

        /// <summary>
        /// 将普通对象转换为 JSON Token。
        /// </summary>
        /// <param name="data">待转换对象。</param>
        /// <returns>转换后的 <see cref="JToken"/>；输入为空时返回 <c>null</c>。</returns>
        private static JToken? ToJToken(object? data)
        {
            return data == null ? null : JToken.FromObject(data);
        }
    }
}
