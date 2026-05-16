namespace CombatSimulation.Models;

/// <summary>
/// 作战推演事件项模型。
/// 用于记录推演过程中的阶段、时间和事件内容。
/// </summary>
public partial class CombatEventItem
{
    /// <summary>
    /// 事件发生时间。
    /// </summary>
    public DateTime Time { get; init; } = DateTime.Now;

    /// <summary>
    /// 供界面显示的时间文本。
    /// </summary>
    public string TimeText => Time.ToString("HH:mm:ss");

    /// <summary>
    /// 当前推演阶段。
    /// </summary>
    public string Phase { get; init; } = string.Empty;

    /// <summary>
    /// 事件描述内容。
    /// </summary>
    public string Content { get; init; } = string.Empty;
}
