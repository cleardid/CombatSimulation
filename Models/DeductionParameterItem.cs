namespace CombatSimulation.Models;

/// <summary>
/// 推演参数项模型。
/// 只负责描述一条推演参数，不处理界面逻辑。
/// </summary>
public partial class DeductionParameterItem
{
    /// <summary>
    /// 参数名称。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 参数值，保留为字符串以兼容数值、枚举和编码类参数。
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// 参数单位，无单位时使用空字符串或“-”。
    /// </summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>
    /// 参数说明。
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
