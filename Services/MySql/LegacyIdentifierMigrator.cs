using CombatSimulation.Models;
using CombatSimulation.MySQLClassBase;
using System.Security.Cryptography;
using System.Text;

namespace CombatSimulation.Services.MySql;

/// <summary>
/// 将旧版非 GUID 主键和跨表引用一次性迁移为稳定 GUID。
/// </summary>
internal static class LegacyIdentifierMigrator
{
    public static void Apply(LoadAndWriteDb db)
    {
        ArgumentNullException.ThrowIfNull(db);

        db.ExecuteInTransaction(() =>
        {
            List<Target_Info_By_MySQL> targetRows = db.GetTableData<Target_Info_By_MySQL>();
            List<Target_System_Info_By_MySQL> systemRows = db.GetTableData<Target_System_Info_By_MySQL>();
            List<Target_Part_Info_By_MySQL> partRows = db.GetTableData<Target_Part_Info_By_MySQL>();
            List<Damage_Tree_Info_By_MySQL> treeRows = db.GetTableData<Damage_Tree_Info_By_MySQL>();
            List<Damage_Node_Info_By_MySQL> nodeRows = db.GetTableData<Damage_Node_Info_By_MySQL>();

            Dictionary<string, string> targetCodeMap = CreateGuidCodeMap(
                "target",
                targetRows.Select(item => item.TargetCode),
                systemRows.Select(item => item.TargetCode),
                treeRows.Select(item => item.TargetCode));
            Dictionary<string, string> systemCodeMap = CreateGuidCodeMap(
                "system",
                systemRows.Select(item => item.SystemCode),
                systemRows.Select(item => item.ParentCode),
                partRows.Select(item => item.PartSystemCode));
            Dictionary<string, string> partCodeMap = CreateGuidCodeMap(
                "part",
                partRows.Select(item => item.PartCode),
                nodeRows.Select(item => item.PartCode));
            Dictionary<string, string> treeCodeMap = CreateGuidCodeMap(
                "damage-tree",
                treeRows.Select(item => item.DamageTreeCode),
                nodeRows.Select(item => item.DamageTreeCode));
            Dictionary<string, string> nodeCodeMap = CreateGuidCodeMap(
                "damage-node",
                nodeRows.Select(item => item.NodeCode),
                nodeRows.Select(item => item.ParentNodeCode));

            int changedCodeCount = targetCodeMap.Count
                + systemCodeMap.Count
                + partCodeMap.Count
                + treeCodeMap.Count
                + nodeCodeMap.Count;
            if (changedCodeCount == 0)
            {
                return;
            }

            MySqlLog.Log(
                $"检测到旧版非 GUID 标识，开始统一迁移：目标 {targetCodeMap.Count}，系统 {systemCodeMap.Count}，" +
                $"部件 {partCodeMap.Count}，毁伤树 {treeCodeMap.Count}，节点 {nodeCodeMap.Count}。 ");

            foreach (Target_Info_By_MySQL row in targetRows)
            {
                ReplacePrimaryCode<Target_Info_By_MySQL>(db, row.TargetCode, ConvertCode(row.TargetCode, targetCodeMap), value => row.TargetCode = value);
                db.mySqlCommand_TJ.Insert(row);
            }

            foreach (Target_System_Info_By_MySQL row in systemRows)
            {
                ReplacePrimaryCode<Target_System_Info_By_MySQL>(db, row.SystemCode, ConvertCode(row.SystemCode, systemCodeMap), value => row.SystemCode = value);
                row.TargetCode = ConvertCode(row.TargetCode, targetCodeMap);
                row.ParentCode = ConvertReferenceCode(row.ParentCode, systemCodeMap, "-1");
                db.mySqlCommand_TJ.Insert(row);
            }

            foreach (Target_Part_Info_By_MySQL row in partRows)
            {
                ReplacePrimaryCode<Target_Part_Info_By_MySQL>(db, row.PartCode, ConvertCode(row.PartCode, partCodeMap), value => row.PartCode = value);
                row.PartSystemCode = ConvertCode(row.PartSystemCode, systemCodeMap);
                row.PartColor = TargetPartColorFormat.ToUnityDatabaseColor(row.PartColor);
                db.mySqlCommand_TJ.Insert(row);
            }

            foreach (Damage_Tree_Info_By_MySQL row in treeRows)
            {
                ReplacePrimaryCode<Damage_Tree_Info_By_MySQL>(db, row.DamageTreeCode, ConvertCode(row.DamageTreeCode, treeCodeMap), value => row.DamageTreeCode = value);
                row.TargetCode = ConvertCode(row.TargetCode, targetCodeMap);
                db.mySqlCommand_TJ.Insert(row);
            }

            foreach (Damage_Node_Info_By_MySQL row in nodeRows)
            {
                ReplacePrimaryCode<Damage_Node_Info_By_MySQL>(db, row.NodeCode, ConvertCode(row.NodeCode, nodeCodeMap), value => row.NodeCode = value);
                row.DamageTreeCode = ConvertCode(row.DamageTreeCode, treeCodeMap);
                row.ParentNodeCode = ConvertReferenceCode(row.ParentNodeCode, nodeCodeMap, string.Empty);
                row.PartCode = ConvertReferenceCode(row.PartCode, partCodeMap, string.Empty);
                db.mySqlCommand_TJ.Insert(row);
            }

            MySqlLog.Log("旧版非 GUID 标识及跨表引用迁移完成。 ");
        });
    }

    private static void ReplacePrimaryCode<T>(
        LoadAndWriteDb db,
        string? oldCode,
        string newCode,
        Action<string> assignNewCode)
        where T : MySQLClassBaseClass
    {
        string normalizedOldCode = NormalizeCode(oldCode);
        if (string.Equals(normalizedOldCode, newCode, StringComparison.Ordinal))
        {
            return;
        }

        db.mySqlCommand_TJ.DeleteByID<T>(normalizedOldCode);
        assignNewCode(newCode);
    }

    private static Dictionary<string, string> CreateGuidCodeMap(
        string scope,
        params IEnumerable<string?>[] codeGroups)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (IEnumerable<string?> codes in codeGroups)
        {
            foreach (string? rawCode in codes)
            {
                string code = NormalizeCode(rawCode);
                if (string.IsNullOrWhiteSpace(code)
                    || code == "-1"
                    || Guid.TryParse(code, out _)
                    || result.ContainsKey(code))
                {
                    continue;
                }

                result.Add(code, CreateDeterministicGuid(scope, code));
            }
        }

        return result;
    }

    private static string ConvertCode(string? rawCode, IReadOnlyDictionary<string, string> codeMap)
    {
        string code = NormalizeCode(rawCode);
        return codeMap.TryGetValue(code, out string? convertedCode) ? convertedCode : code;
    }

    private static string ConvertReferenceCode(
        string? rawCode,
        IReadOnlyDictionary<string, string> codeMap,
        string emptyValue)
    {
        string code = NormalizeCode(rawCode);
        if (string.IsNullOrWhiteSpace(code))
        {
            return emptyValue;
        }

        if (code == "-1")
        {
            return "-1";
        }

        return codeMap.TryGetValue(code, out string? convertedCode) ? convertedCode : code;
    }

    private static string NormalizeCode(string? code) => code?.Trim() ?? string.Empty;

    private static string CreateDeterministicGuid(string scope, string legacyCode)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes($"CombatSimulation:{scope}:{legacyCode}"));
        return new Guid(hash).ToString("N");
    }
}
