using CombatSimulation.MySQLClassBase;

namespace CombatSimulation.Services.MySql;

/// <summary>
/// 毁伤树主表数据结构。
/// </summary>
/// <remarks>
/// 一条记录描述某个目标的一棵毁伤树，例如 M1A2 的轻度毁伤树、重度毁伤树等。
/// 具体树形结构保存在 Damage_Node_Info_By_MySQL 对应的毁伤节点表中。
/// </remarks>
public class Damage_Tree_Info_By_MySQL : MySQLClassBaseClass
{
    /// <summary>
    /// 毁伤树表名。
    /// </summary>
    public override string TableName => "t_d_tree";

    /// <summary>
    /// 毁伤树唯一标识。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_t_Code", "VARCHAR(255)", true, false)]
    public string DamageTreeCode { get; set; } = string.Empty;

    /// <summary>
    /// 毁伤树名称，用于 WPF 下拉列表和弹窗标题显示。
    /// 该字段是对原始表定义的补充，避免只靠等级和类型难以区分多棵树。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_t_Name", "VARCHAR(255)", false, false)]
    public string DamageTreeName { get; set; } = string.Empty;

    /// <summary>
    /// 毁伤树描述，可为空。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_t_Description", "TEXT", false, true)]
    public string DamageTreeDescription { get; set; } = string.Empty;

    /// <summary>
    /// 对应目标唯一标识。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_t_TargetCode", "VARCHAR(255)", false, false)]
    public string TargetCode { get; set; } = string.Empty;

    /// <summary>
    /// 毁伤等级信息，例如轻度毁伤、中度毁伤、重度毁伤。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_t_DamageLevel", "VARCHAR(255)", false, false)]
    public string DamageLevelInfo { get; set; } = string.Empty;

    /// <summary>
    /// 毁伤树类型，例如整体毁伤树、功能毁伤树、任务毁伤树。
    /// </summary>
    [MySQLClassHelpAttribute(true, "d_t_Type", "VARCHAR(255)", false, false)]
    public string DamageTreeType { get; set; } = string.Empty;

    /// <summary>
    /// 默认构造函数，用于 MySQL 反射读取。
    /// </summary>
    public Damage_Tree_Info_By_MySQL() : base() { }

    /// <summary>
    /// 输出关键字段，便于日志调试。
    /// </summary>
    public override string ToString() => $"[Damage_Tree_Info_By_MySQL] DamageTreeCode: {DamageTreeCode}, DamageTreeName: {DamageTreeName}, TargetCode: {TargetCode}, DamageLevelInfo: {DamageLevelInfo}, DamageTreeType: {DamageTreeType}";
}
