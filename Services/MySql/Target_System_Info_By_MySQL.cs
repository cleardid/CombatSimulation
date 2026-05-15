using CombatSimulation.MySQLClassBase;

/// <summary>
/// 用于从 MySQL 数据库中获取目标系统信息的数据结构。
/// </summary>
public class Target_System_Info_By_MySQL : MySQLClassBaseClass
{
    // 重写表名。
    public override string TableName => "t_t_system";

    /// <summary>
    /// 系统唯一标识。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_s_Code", "VARCHAR(255)", true, false)]
    public string SystemCode { get; set; } = string.Empty;

    /// <summary>
    /// 系统名称。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_s_Name", "VARCHAR(255)", false, false)]
    public string SystemName { get; set; } = string.Empty;

    /// <summary>
    /// 系统描述。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_s_Description", "VARCHAR(255)", false, true)]
    public string SystemDescription { get; set; } = string.Empty;

    /// <summary>
    /// 是否为顶系统。
    /// 保留原属性名 TargetCategory，避免破坏既有 Unity 端代码；WPF 映射时会转为 IsTopSystem。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_s_IsTop", "BIT", false, false)]
    public bool TargetCategory { get; set; }

    /// <summary>
    /// 父系统唯一标识。
    /// 在非顶系统时有效，否则为空。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_s_ParentCode", "VARCHAR(255)", false, true)]
    public string ParentCode { get; set; } = string.Empty;

    /// <summary>
    /// 所属目标唯一标识。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_s_TargetCode", "VARCHAR(255)", false, false)]
    public string TargetCode { get; set; } = string.Empty;

    /// <summary>
    /// 默认构造函数，用于临时初始化对象，从数据库中获取数据时使用。
    /// </summary>
    public Target_System_Info_By_MySQL() : base() { }

    /// <summary>
    /// 重写 ToString 方法。
    /// </summary>
    public override string ToString() => $"[Target_System_Info_By_MySQL] SystemCode: {SystemCode}, SystemName: {SystemName}, SystemDescription: {SystemDescription}, TargetCategory: {TargetCategory}, ParentCode: {ParentCode}, TargetCode: {TargetCode}";
}
