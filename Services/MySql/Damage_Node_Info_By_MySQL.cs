using CombatSimulation.MySQLClassBase;

namespace CombatSimulation.Services.MySql;

/// <summary>
/// 毁伤树节点表数据结构。
/// </summary>
/// <remarks>
/// RelationType 使用整数存储：-1=无/叶子节点，0=与门，1=或门，2=表决门。
/// 表决门需要 VoteThreshold；其他关系该字段可为空。
/// </remarks>
public class Damage_Node_Info_By_MySQL : MySQLClassBaseClass
{
    /// <summary>
    /// 毁伤节点表名。
    /// </summary>
    public override string TableName => "t_d_node";

    /// <summary>
    /// 节点唯一标识。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_n_Code", "VARCHAR(255)", true, false)]
    public string NodeCode { get; set; } = string.Empty;

    /// <summary>
    /// 对应的毁伤树唯一标识。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_n_TreeCode", "VARCHAR(255)", false, false)]
    public string DamageTreeCode { get; set; } = string.Empty;

    /// <summary>
    /// 毁伤节点名称。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_n_Name", "VARCHAR(255)", false, false)]
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// 父节点唯一标识。根节点为空。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_n_ParentCode", "VARCHAR(255)", false, true)]
    public string ParentNodeCode { get; set; } = string.Empty;

    /// <summary>
    /// 当前节点与子节点的关系。叶子节点固定为 -1。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_n_RelationType", "INT", false, false)]
    public int RelationType { get; set; }

    /// <summary>
    /// 表决门阈值，仅 RelationType=2 时有效。
    /// 使用 DOUBLE 存储，允许表决阈值为小数。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_n_VoteThreshold", "DOUBLE", false, true)]
    public float? VoteThreshold { get; set; }

    /// <summary>
    /// 对应的目标部件唯一标识。仅叶子节点赋值。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_n_PartCode", "VARCHAR(255)", false, true)]
    public string PartCode { get; set; } = string.Empty;

    /// <summary>
    /// 节点描述，可为空。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_n_Description", "TEXT", false, true)]
    public string NodeDescription { get; set; } = string.Empty;

    /// <summary>
    /// 同一父节点下的显示顺序。该字段是对原始定义的补充，用于保证树节点显示稳定。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_n_SortOrder", "INT", false, false)]
    public int SortOrder { get; set; }

    /// <summary>
    /// 默认构造函数，用于 MySQL 反射读取。
    /// </summary>
    public Damage_Node_Info_By_MySQL() : base() { }

    /// <summary>
    /// 输出关键字段，便于日志调试。
    /// </summary>
    public override string ToString() => $"[Damage_Node_Info_By_MySQL] NodeCode: {NodeCode}, DamageTreeCode: {DamageTreeCode}, NodeName: {NodeName}, ParentNodeCode: {ParentNodeCode}, RelationType: {RelationType}, PartCode: {PartCode}";
}
