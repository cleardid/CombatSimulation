namespace CombatSimulation.Models
{
    /// <summary>
    /// 作战推演事件项模型。
    /// 用于记录推演过程中的阶段、时间和事件内容。
    /// </summary>
    public partial class CombatEventItem
    {
        public DateTime Time { get; init; } = DateTime.Now;

        public string TimeText => Time.ToString("HH:mm:ss");

        public string Phase { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;
    }
}