namespace CombatSimulation.Models;

/// <summary>
/// 目标结构详情表格中的一行键值数据。
/// </summary>
public sealed class TargetDetailRow
{
    /// <summary>
    /// 详情项名称。
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// 详情项显示值。
    /// </summary>
    public string Value { get; init; } = string.Empty;
}
