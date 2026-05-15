using CombatSimulation.Services.MySql;

namespace CombatSimulation.Models.Unity;

/// <summary>
/// 发送给 Unity 的数据库快照。
/// </summary>
/// <remarks>
/// Unity 只把该对象作为运行期只读缓存使用；数据库写入仍由 WPF 完成。
/// 属性名称需要与 Unity 端 SceneControl.Data.CombatDatabaseSnapshot 保持一致，
/// 这样 JSON 反序列化时才能直接映射到 Unity 侧的表缓存结构。
/// </remarks>
public sealed class CombatDatabaseSnapshot
{
    /// <summary>
    /// 目标主表记录集合，对应 t_t_info。
    /// </summary>
    public List<Target_Info_By_MySQL> Targets { get; } = new();

    /// <summary>
    /// 目标系统表记录集合，对应 t_t_system。
    /// </summary>
    public List<Target_System_Info_By_MySQL> TargetSystems { get; } = new();

    /// <summary>
    /// 目标部件表记录集合，对应 t_t_part。
    /// </summary>
    public List<Target_Part_Info_By_MySQL> TargetParts { get; } = new();

    /// <summary>
    /// 毁伤树主表记录集合，对应 t_d_tree。
    /// </summary>
    public List<Damage_Tree_Info_By_MySQL> DamageTrees { get; } = new();

    /// <summary>
    /// 毁伤树节点表记录集合，对应 t_d_node。
    /// </summary>
    public List<Damage_Node_Info_By_MySQL> DamageNodes { get; } = new();
}
