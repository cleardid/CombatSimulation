namespace CombatSimulation.Models.Unity;

/// <summary>
/// 发送给 Unity 的数据库快照协议。DTO 只描述 JSON 契约，不依赖 MySQL 实体或映射特性。
/// </summary>
public sealed class CombatDatabaseSnapshot
{
    public List<UnityTargetRecord> Targets { get; } = new();
    public List<UnityTargetSystemRecord> TargetSystems { get; } = new();
    public List<UnityTargetPartRecord> TargetParts { get; } = new();
    public List<UnityDamageTreeRecord> DamageTrees { get; } = new();
    public List<UnityDamageNodeRecord> DamageNodes { get; } = new();
}

public sealed class UnityTargetRecord
{
    public string TargetCode { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string TargetCategory { get; set; } = string.Empty;
    public string TargetDescription { get; set; } = string.Empty;
}

public sealed class UnityTargetSystemRecord
{
    public string SystemCode { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public string SystemDescription { get; set; } = string.Empty;
    public bool TargetCategory { get; set; }
    public string ParentCode { get; set; } = string.Empty;
    public string TargetCode { get; set; } = string.Empty;
}

public sealed class UnityTargetPartRecord
{
    public string PartCode { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string PartShape { get; set; } = string.Empty;
    public string PartDescription { get; set; } = string.Empty;
    public string PartMaterial { get; set; } = string.Empty;
    public float PartVulnerableArea { get; set; }
    public float PartEquThickness { get; set; }
    public string PartColor { get; set; } = "#8080800F";
    public string PartSystemCode { get; set; } = string.Empty;
    public float PartEquParam1 { get; set; }
    public float PartEquParam2 { get; set; }
    public float PartEquParam3 { get; set; }
    public float PartEquParam4 { get; set; }
    public float PartEquParam5 { get; set; }
    public float PartEquParam6 { get; set; }
    public float PartEquParam7 { get; set; }
    public float PartEquParam8 { get; set; }
    public float PartEquParam9 { get; set; }
    public float PartEquParam10 { get; set; }
    public float PartEquParam11 { get; set; }
    public float PartEquParam12 { get; set; }
    public float PartEquParam13 { get; set; }
    public float PartEquParam14 { get; set; }
    public float PartEquParam15 { get; set; }
    public float PartEquParam16 { get; set; }
    public float PartEquParam17 { get; set; }
    public float PartEquParam18 { get; set; }
    public float PartEquParam19 { get; set; }
    public float PartEquParam20 { get; set; }
    public float PartEquParam21 { get; set; }
    public float PartEquParam22 { get; set; }
    public float PartEquParam23 { get; set; }
    public float PartEquParam24 { get; set; }
}

public sealed class UnityDamageTreeRecord
{
    public string DamageTreeCode { get; set; } = string.Empty;
    public string DamageTreeName { get; set; } = string.Empty;
    public string DamageTreeDescription { get; set; } = string.Empty;
    public string TargetCode { get; set; } = string.Empty;
    public string DamageLevelInfo { get; set; } = string.Empty;
    public string DamageTreeType { get; set; } = string.Empty;
}

public sealed class UnityDamageNodeRecord
{
    public string NodeCode { get; set; } = string.Empty;
    public string DamageTreeCode { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string ParentNodeCode { get; set; } = string.Empty;
    public int RelationType { get; set; }
    public float? VoteThreshold { get; set; }
    public string PartCode { get; set; } = string.Empty;
    public string NodeDescription { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
