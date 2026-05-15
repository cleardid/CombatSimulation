using CombatSimulation.MySQLClassBase;

/// <summary>
/// 用于从 MySQL 数据库中获取目标整体信息的数据结构。
/// </summary>
public class Target_Info_By_MySQL : MySQLClassBaseClass
{
    /// <summary>
    /// 重写表名。
    /// </summary>
    public override string TableName => "t_t_info";

    /// <summary>
    /// 目标唯一标识。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_t_Code", "VARCHAR(255)", true, false)]
    public string TargetCode { get; set; } = string.Empty;

    /// <summary>
    /// 目标名称。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_t_Name", "VARCHAR(255)", false, false)]
    public string TargetName { get; set; } = string.Empty;

    /// <summary>
    /// 目标类型。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_t_Category", "VARCHAR(255)", false, false)]
    public string TargetCategory { get; set; } = string.Empty;

    /// <summary>
    /// 目标描述。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_t_Description", "VARCHAR(255)", false, true)]
    public string TargetDescription { get; set; } = string.Empty;

    /// <summary>
    /// 默认构造函数，用于临时初始化对象，从数据库中获取数据时使用。
    /// </summary>
    public Target_Info_By_MySQL() : base() { }

    /// <summary>
    /// 重写 ToString 方法。
    /// </summary>
    public override string ToString() => $"[Target_Info_By_MySQL] TargetCode: {TargetCode}, TargetName: {TargetName}, TargetCategory: {TargetCategory}, TargetDescription: {TargetDescription}";
}
