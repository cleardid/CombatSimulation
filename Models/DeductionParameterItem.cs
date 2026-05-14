namespace CombatSimulation.Models
{
    /// <summary>
    /// 推演参数项模型。
    /// 只负责描述一条推演参数，不处理界面逻辑。
    /// </summary>
    public partial class DeductionParameterItem
    {
        public string Name { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;

        public string Unit { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;
    }
}