using CombatSimulation.MySQLClassBase;

/// <summary>
/// 用于从 MySQL 数据库中获取目标部件信息的数据结构。
/// </summary>
public class Target_Part_Info_By_MySQL : MySQLClassBaseClass, ICloneable
{
    // 重写表名。
    public override string TableName => "t_t_part";

    /// <summary>
    /// 部件唯一标识。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_Code", "VARCHAR(255)", true, false)]
    public string PartCode { get; set; } = string.Empty;

    /// <summary>
    /// 部件名称。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_Name", "VARCHAR(255)", false, false)]
    public string PartName { get; set; } = string.Empty;

    /// <summary>
    /// 部件形状。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_Shape", "VARCHAR(255)", false, false)]
    public string PartShape { get; set; } = string.Empty;

    /// <summary>
    /// 部件描述。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_Description", "VARCHAR(255)", false, true)]
    public string PartDescription { get; set; } = string.Empty;

    /// <summary>
    /// 部件材料。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_Material", "VARCHAR(255)", false, false)]
    public string PartMaterial { get; set; } = string.Empty;

    /// <summary>
    /// 部件易损面积。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_VulnerableArea", "DOUBLE", false, false)]
    public float PartVulnerableArea { get; set; }

    /// <summary>
    /// 部件等效厚度。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquThickness", "DOUBLE", false, false)]
    public float PartEquThickness { get; set; }

    /// <summary>
    /// 部件颜色，例如 #808080。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_Color", "VARCHAR(255)", false, false)]
    public string PartColor { get; set; } = "#808080";

    /// <summary>
    /// 部件所属系统唯一标识。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_SystemCode", "VARCHAR(255)", false, false)]
    public string PartSystemCode { get; set; } = string.Empty;

    /// <summary>
    /// 部件等效参数 1。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam1", "DOUBLE", false, true)]
    public float PartEquParam1 { get; set; }

    /// <summary>
    /// 部件等效参数 2。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam2", "DOUBLE", false, true)]
    public float PartEquParam2 { get; set; }

    /// <summary>
    /// 部件等效参数 3。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam3", "DOUBLE", false, true)]
    public float PartEquParam3 { get; set; }

    /// <summary>
    /// 部件等效参数 4。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam4", "DOUBLE", false, true)]
    public float PartEquParam4 { get; set; }

    /// <summary>
    /// 部件等效参数 5。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam5", "DOUBLE", false, true)]
    public float PartEquParam5 { get; set; }

    /// <summary>
    /// 部件等效参数 6。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam6", "DOUBLE", false, true)]
    public float PartEquParam6 { get; set; }

    /// <summary>
    /// 部件等效参数 7。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam7", "DOUBLE", false, true)]
    public float PartEquParam7 { get; set; }

    /// <summary>
    /// 部件等效参数 8。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam8", "DOUBLE", false, true)]
    public float PartEquParam8 { get; set; }

    /// <summary>
    /// 部件等效参数 9。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam9", "DOUBLE", false, true)]
    public float PartEquParam9 { get; set; }

    /// <summary>
    /// 部件等效参数 10。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam10", "DOUBLE", false, true)]
    public float PartEquParam10 { get; set; }

    /// <summary>
    /// 部件等效参数 11。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam11", "DOUBLE", false, true)]
    public float PartEquParam11 { get; set; }

    /// <summary>
    /// 部件等效参数 12。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam12", "DOUBLE", false, true)]
    public float PartEquParam12 { get; set; }

    /// <summary>
    /// 部件等效参数 13。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam13", "DOUBLE", false, true)]
    public float PartEquParam13 { get; set; }

    /// <summary>
    /// 部件等效参数 14。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam14", "DOUBLE", false, true)]
    public float PartEquParam14 { get; set; }

    /// <summary>
    /// 部件等效参数 15。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam15", "DOUBLE", false, true)]
    public float PartEquParam15 { get; set; }

    /// <summary>
    /// 部件等效参数 16。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam16", "DOUBLE", false, true)]
    public float PartEquParam16 { get; set; }

    /// <summary>
    /// 部件等效参数 17。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam17", "DOUBLE", false, true)]
    public float PartEquParam17 { get; set; }

    /// <summary>
    /// 部件等效参数 18。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam18", "DOUBLE", false, true)]
    public float PartEquParam18 { get; set; }

    /// <summary>
    /// 部件等效参数 19。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam19", "DOUBLE", false, true)]
    public float PartEquParam19 { get; set; }

    /// <summary>
    /// 部件等效参数 20。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam20", "DOUBLE", false, true)]
    public float PartEquParam20 { get; set; }

    /// <summary>
    /// 部件等效参数 21。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam21", "DOUBLE", false, true)]
    public float PartEquParam21 { get; set; }

    /// <summary>
    /// 部件等效参数 22。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam22", "DOUBLE", false, true)]
    public float PartEquParam22 { get; set; }

    /// <summary>
    /// 部件等效参数 23。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam23", "DOUBLE", false, true)]
    public float PartEquParam23 { get; set; }

    /// <summary>
    /// 部件等效参数 24。
    /// </summary>
    [MySQLClassHelpAttribute(true, "t_p_EquParam24", "DOUBLE", false, true)]
    public float PartEquParam24 { get; set; }

    /// <summary>
    /// 默认构造函数，用于临时初始化对象，从数据库中获取数据时使用。
    /// </summary>
    public Target_Part_Info_By_MySQL() : base() { }

    /// <summary>
    /// 克隆当前部件数据。
    /// </summary>
    public object Clone() => MemberwiseClone();
}
