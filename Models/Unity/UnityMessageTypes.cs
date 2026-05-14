using System;

namespace CombatSimulation.Models.Unity
{
    /// <summary>
    /// 定义 Unity 通信消息类型常量。
    /// </summary>
    /// <remarks>
    /// 协议中使用字符串而不是枚举，是为了让 JSON 内容直接匹配 WPF 与 Unity 双端的通信约定。
    /// </remarks>
    public static class UnityMessageTypes
    {
        /// <summary>
        /// 请求消息。通常由 WPF 发送给 Unity，并等待对应 <see cref="Response"/>。
        /// </summary>
        public const string Request = "request";

        /// <summary>
        /// 响应消息。通常由接收方根据请求的 <c>msgId</c> 返回。
        /// </summary>
        public const string Response = "response";

        /// <summary>
        /// 事件消息。用于主动通知，不要求一定存在对应响应。
        /// </summary>
        public const string Event = "event";

        /// <summary>
        /// 判断指定消息类型是否为当前协议支持的已知类型。
        /// </summary>
        /// <param name="type">待检查的消息类型字符串。</param>
        /// <returns>属于 request、response 或 event 时返回 <c>true</c>。</returns>
        public static bool IsKnown(string type)
        {
            return string.Equals(type, Request, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, Response, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, Event, StringComparison.OrdinalIgnoreCase);
        }
    }
}
