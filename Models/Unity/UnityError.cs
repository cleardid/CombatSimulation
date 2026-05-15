using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CombatSimulation.Models.Unity;

/// <summary>
/// 表示 Unity 响应失败时返回的错误对象。
/// </summary>
/// <remarks>
/// 当 <c>UnityMessage.Type</c> 为 <c>response</c> 且 <c>UnityMessage.Success</c> 为 <c>false</c> 时，
/// 该对象用于承载错误码、错误描述和可选的扩展错误数据。
/// </remarks>
public sealed class UnityError
{
    /// <summary>
    /// 错误码。
    /// </summary>
    /// <remarks>
    /// 建议使用 <see cref="UnityErrorCodes"/> 中定义的常量，便于 WPF 和 Unity 双端统一处理。
    /// </remarks>
    [JsonProperty("code")]
    public string Code { get; set; } = UnityErrorCodes.UnknownError;

    /// <summary>
    /// 面向开发或日志记录的错误描述。
    /// </summary>
    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 扩展错误信息。
    /// </summary>
    /// <remarks>
    /// 可用于携带字段校验结果、异常上下文、Unity 对象标识等额外数据。
    /// </remarks>
    [JsonProperty("details")]
    public JToken? Details { get; set; }
}
