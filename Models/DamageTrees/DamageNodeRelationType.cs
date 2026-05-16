namespace CombatSimulation.Models;

/// <summary>
/// 毁伤节点与其子节点之间的逻辑关系。
/// </summary>
/// <remarks>
/// 叶子节点没有子节点关系，固定使用 None=-1；中间节点只能使用 And、Or 或 Vote。
/// 数据库存储时使用对应的整数值，便于 Unity、算法模块和 WPF 共享同一套枚举定义。
/// </remarks>
public enum DamageNodeRelationType
{
    /// <summary>
    /// 无子节点关系。仅叶子节点使用。
    /// </summary>
    None = -1,

    /// <summary>
    /// 与门：所有子节点成立时当前节点成立。
    /// </summary>
    And = 0,

    /// <summary>
    /// 或门：任一子节点成立时当前节点成立。
    /// </summary>
    Or = 1,

    /// <summary>
    /// 表决门：满足阈值数量的子节点成立时当前节点成立。
    /// </summary>
    Vote = 2
}
