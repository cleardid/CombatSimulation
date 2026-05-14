using System.Collections.ObjectModel;

namespace CombatSimulation.Models
{
    /// <summary>
    /// 目标信息模型。
    /// 只负责保存目标的基础数据，不负责界面选中状态。
    /// </summary>
    public sealed class TargetInfoItem
    {
        public string Name { get; init; } = string.Empty;

        public string Code { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public ObservableCollection<string> StructureNodes { get; init; } = new();

        public ObservableCollection<string> DamageTreeNodes { get; init; } = new();
    }
}
