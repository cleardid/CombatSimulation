using CombatSimulation.Models;
using CombatSimulation.Models.Unity;
using CombatSimulation.Services.MySql;
using System.Data;

namespace CombatSimulation.Services.Targets;
/// <summary>
/// 基于 MySQL 的目标结构仓储实现。
/// </summary>
/// <remarks>
/// 该类负责在 WPF 模型 TargetInfoItem / TargetSystemInfoItem / TargetPartInfoItem
/// 与原 MySQL 数据结构 Target_Info_By_MySQL / Target_System_Info_By_MySQL / Target_Part_Info_By_MySQL 之间转换。
/// MySQL 基础访问类不依赖 WPF，因此仍可由 Unity 端直接复用。
/// </remarks>
public sealed class MySqlTargetInfoRepository : ITargetInfoRepository
{
    private readonly string _connectionString;
    private bool _databaseAndTableChecked;

    /// <summary>
    /// 创建默认 MySQL 仓储。
    /// 默认流程只从程序运行目录下的 target_mysql.json 读取连接信息。
    /// </summary>
    public MySqlTargetInfoRepository()
    {
        _connectionString = ResolveDefaultConnectionString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            MySqlLog.LogWarning("目标仓储未获得有效连接字符串，后续读取或写入会失败。 ");
        }
    }

    /// <summary>
    /// 使用 MySQL 连接字符串创建仓储。
    /// </summary>
    public MySqlTargetInfoRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("MySQL 连接字符串不能为空。", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    /// <summary>
    /// 使用 MySQL 连接信息创建仓储。
    /// </summary>
    public MySqlTargetInfoRepository(MySQLConnectionInfo connectionInfo)
        : this(connectionInfo?.ToString() ?? throw new ArgumentNullException(nameof(connectionInfo)))
    {
    }

    /// <summary>
    /// 读取全部目标及其系统、部件结构。
    /// </summary>
    public Task<IReadOnlyList<TargetInfoItem>> LoadTargetsAsync(CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(LoadTargets, cancellationToken);

    private IReadOnlyList<TargetInfoItem> LoadTargets()
    {
        MySqlLog.Log("开始读取目标信息。 ");

        using LoadAndWriteDb db = OpenDb();
        List<Target_Info_By_MySQL> targetRows = db.GetTableData<Target_Info_By_MySQL>();
        List<Target_System_Info_By_MySQL> systemRows = db.GetTableData<Target_System_Info_By_MySQL>();
        List<Target_Part_Info_By_MySQL> partRows = db.GetTableData<Target_Part_Info_By_MySQL>();

        MySqlLog.Log($"目标信息读取完成：目标 {targetRows.Count} 条，系统 {systemRows.Count} 条，部件 {partRows.Count} 条。 ");
        return BuildTargets(targetRows, systemRows, partRows);
    }

    /// <summary>
    /// 在同一可重复读事务中读取目标、系统、部件、毁伤树和毁伤节点五张表。
    /// </summary>
    /// <remarks>
    /// 直接读取 MySQL 原始表结构，避免界面模型二次转换遗漏字段；
    /// 单连接事务保证 Unity 不会收到跨写入时点拼接出的混合快照。
    /// </remarks>
    public Task<CombatDatabaseSnapshot> CreateDatabaseSnapshotAsync(CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(CreateDatabaseSnapshot, cancellationToken);

    private CombatDatabaseSnapshot CreateDatabaseSnapshot()
    {
        using LoadAndWriteDb db = OpenDb();

        return db.ExecuteInTransaction(IsolationLevel.RepeatableRead, () =>
        {
            CombatDatabaseSnapshot snapshot = new();
            snapshot.Targets.AddRange(db.GetTableData<Target_Info_By_MySQL>().Select(ToUnityTargetRecord));
            snapshot.TargetSystems.AddRange(db.GetTableData<Target_System_Info_By_MySQL>().Select(ToUnityTargetSystemRecord));
            snapshot.TargetParts.AddRange(db.GetTableData<Target_Part_Info_By_MySQL>().Select(ToUnityTargetPartRecord));
            snapshot.DamageTrees.AddRange(db.GetTableData<Damage_Tree_Info_By_MySQL>().Select(ToUnityDamageTreeRecord));
            snapshot.DamageNodes.AddRange(db.GetTableData<Damage_Node_Info_By_MySQL>().Select(ToUnityDamageNodeRecord));
            return snapshot;
        });
    }

    /// <summary>
    /// 新增目标信息。
    /// </summary>
    public Task AddTargetAsync(TargetInfoItem target, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => AddTarget(target), cancellationToken);

    private void AddTarget(TargetInfoItem target)
    {
        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        db.mySqlCommand_TJ.Insert(ToMySqlTarget(target));
    }

    /// <summary>
    /// 更新目标信息。
    /// </summary>
    public Task UpdateTargetAsync(TargetInfoItem target, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => UpdateTarget(target), cancellationToken);

    private void UpdateTarget(TargetInfoItem target)
    {
        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        db.mySqlCommand_TJ.Insert(ToMySqlTarget(target));
    }

    /// <summary>
    /// 删除目标及其全部系统、部件和毁伤树信息。
    /// </summary>
    public Task DeleteTargetAsync(string targetCode, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => DeleteTarget(targetCode), cancellationToken);

    private void DeleteTarget(string targetCode)
    {
        if (string.IsNullOrWhiteSpace(targetCode))
        {
            return;
        }

        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        db.ExecuteInTransaction(() =>
        {

        List<Target_System_Info_By_MySQL> systems = db.mySqlCommand_TJ.SelectByField<Target_System_Info_By_MySQL>("t_s_TargetCode", targetCode);
        foreach (string systemCode in systems.Select(item => item.SystemCode).Where(code => !string.IsNullOrWhiteSpace(code)).Distinct(StringComparer.Ordinal))
        {
            db.mySqlCommand_TJ.DeleteByField<Target_Part_Info_By_MySQL>("t_p_SystemCode", systemCode);
            db.mySqlCommand_TJ.DeleteByID<Target_System_Info_By_MySQL>(systemCode);
        }

        // 目标删除后，对应毁伤树已经失去目标外键，需要同步删除，避免 Unity 快照中出现孤立毁伤树。
        List<Damage_Tree_Info_By_MySQL> damageTrees = db.mySqlCommand_TJ.SelectByField<Damage_Tree_Info_By_MySQL>("d_t_TargetCode", targetCode);
        foreach (Damage_Tree_Info_By_MySQL damageTree in damageTrees)
        {
            if (!string.IsNullOrWhiteSpace(damageTree.DamageTreeCode))
            {
                db.mySqlCommand_TJ.DeleteByField<Damage_Node_Info_By_MySQL>("d_n_TreeCode", damageTree.DamageTreeCode);
                db.mySqlCommand_TJ.DeleteByID<Damage_Tree_Info_By_MySQL>(damageTree.DamageTreeCode);
            }
        }

        db.mySqlCommand_TJ.DeleteByID<Target_Info_By_MySQL>(targetCode);
        });
    }

    /// <summary>
    /// 新增目标系统。
    /// </summary>
    public Task AddSystemAsync(string targetCode, TargetSystemInfoItem system, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => AddSystem(targetCode, system), cancellationToken);

    private void AddSystem(string targetCode, TargetSystemInfoItem system)
    {
        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        db.mySqlCommand_TJ.Insert(ToMySqlSystem(system));
    }

    /// <summary>
    /// 更新目标系统。
    /// </summary>
    public Task UpdateSystemAsync(string targetCode, string originalSystemCode, TargetSystemInfoItem system, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => UpdateSystem(targetCode, originalSystemCode, system), cancellationToken);

    private void UpdateSystem(string targetCode, string originalSystemCode, TargetSystemInfoItem system)
    {
        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        db.ExecuteInTransaction(() =>
        {

        if (!string.Equals(originalSystemCode, system.SystemCode, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(originalSystemCode))
        {
            // 如果系统主键变更，先写入新主键行，再同步子系统和部件外键，最后删除旧主键行。
            // 该顺序可兼容后续数据库增加外键约束的场景。
            db.mySqlCommand_TJ.Insert(ToMySqlSystem(system));

            List<Target_System_Info_By_MySQL> childSystems = db.mySqlCommand_TJ.SelectByField<Target_System_Info_By_MySQL>("t_s_ParentCode", originalSystemCode);
            foreach (Target_System_Info_By_MySQL childSystem in childSystems)
            {
                childSystem.ParentCode = system.SystemCode;
                db.mySqlCommand_TJ.Insert(childSystem);
            }

            List<Target_Part_Info_By_MySQL> parts = db.mySqlCommand_TJ.SelectByField<Target_Part_Info_By_MySQL>("t_p_SystemCode", originalSystemCode);
            foreach (Target_Part_Info_By_MySQL part in parts)
            {
                part.PartSystemCode = system.SystemCode;
                // 主键迁移时直接复用数据库行，仍需补齐颜色透明度，避免旧数据继续以 #RRGGBB 写回。
                part.PartColor = TargetPartColorFormat.ToUnityDatabaseColor(part.PartColor);
                db.mySqlCommand_TJ.Insert(part);
            }

            db.mySqlCommand_TJ.DeleteByID<Target_System_Info_By_MySQL>(originalSystemCode);
            return;
        }

        db.mySqlCommand_TJ.Insert(ToMySqlSystem(system));
        });
    }

    /// <summary>
    /// 删除目标系统及其子系统和底层部件。
    /// </summary>
    public Task<string?> DeleteSystemAsync(string targetCode, string systemCode, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => DeleteSystem(targetCode, systemCode), cancellationToken);

    private string? DeleteSystem(string targetCode, string systemCode)
    {
        if (string.IsNullOrWhiteSpace(systemCode))
        {
            return null;
        }

        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        return db.ExecuteInTransaction(() =>
        {

        List<Target_System_Info_By_MySQL> allSystems = db.mySqlCommand_TJ.SelectByField<Target_System_Info_By_MySQL>("t_s_TargetCode", targetCode);
        HashSet<string> codesToDelete = CollectDescendantSystemCodes(systemCode, allSystems);

        List<Target_Part_Info_By_MySQL> partsToDelete = new();
        foreach (string code in codesToDelete)
        {
            partsToDelete.AddRange(
                db.mySqlCommand_TJ.SelectByField<Target_Part_Info_By_MySQL>("t_p_SystemCode", code));
        }

        string? referenceBlockReason = GetDamageTreeReferenceBlockReason(
            db,
            partsToDelete.Select(part => part.PartCode),
            "删除系统");
        if (referenceBlockReason != null)
        {
            return referenceBlockReason;
        }

        foreach (string code in codesToDelete)
        {
            db.mySqlCommand_TJ.DeleteByField<Target_Part_Info_By_MySQL>("t_p_SystemCode", code);
        }

        foreach (string code in codesToDelete)
        {
            db.mySqlCommand_TJ.DeleteByID<Target_System_Info_By_MySQL>(code);
        }

        return null;
        });
    }

    /// <summary>
    /// 新增目标部件。
    /// </summary>
    public Task AddPartAsync(string targetCode, TargetPartInfoItem part, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => AddPart(targetCode, part), cancellationToken);

    private void AddPart(string targetCode, TargetPartInfoItem part)
    {
        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        db.mySqlCommand_TJ.Insert(ToMySqlPart(part));
    }

    /// <summary>
    /// 更新目标部件。
    /// </summary>
    public Task UpdatePartAsync(string targetCode, string originalPartCode, TargetPartInfoItem part, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => UpdatePart(targetCode, originalPartCode, part), cancellationToken);

    private void UpdatePart(string targetCode, string originalPartCode, TargetPartInfoItem part)
    {
        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        db.ExecuteInTransaction(() =>
        {

        string normalizedOriginalPartCode = originalPartCode?.Trim() ?? string.Empty;
        string normalizedNewPartCode = part.PartCode?.Trim() ?? string.Empty;
        bool isPartCodeChanged =
            !string.Equals(normalizedOriginalPartCode, normalizedNewPartCode, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(normalizedOriginalPartCode);

        if (isPartCodeChanged)
        {
            string? referenceBlockReason = GetDamageTreeReferenceBlockReason(
                db,
                new[] { normalizedOriginalPartCode },
                "修改部件唯一标识");
            if (referenceBlockReason != null)
            {
                throw new InvalidOperationException(referenceBlockReason);
            }
        }

        // ToMySqlPart 已集中处理颜色透明度、数值类型和参数字段映射，更新时只保留一次转换和一次写入。
        db.mySqlCommand_TJ.Insert(ToMySqlPart(part));

        if (isPartCodeChanged)
        {
            db.mySqlCommand_TJ.DeleteByID<Target_Part_Info_By_MySQL>(normalizedOriginalPartCode);
        }
        });
    }


    /// <summary>
    /// 删除目标部件。
    /// </summary>
    public Task<string?> DeletePartAsync(string targetCode, string partCode, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => DeletePart(targetCode, partCode), cancellationToken);

    private string? DeletePart(string targetCode, string partCode)
    {
        if (string.IsNullOrWhiteSpace(partCode))
        {
            return null;
        }

        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        string? referenceBlockReason = GetDamageTreeReferenceBlockReason(db, new[] { partCode }, "删除部件");
        if (referenceBlockReason != null)
        {
            return referenceBlockReason;
        }

        db.mySqlCommand_TJ.DeleteByID<Target_Part_Info_By_MySQL>(partCode);
        return null;
    }

    /// <summary>
    /// 检查删除或改号是否会影响仍被毁伤树节点引用的部件。
    /// </summary>
    /// <returns>允许操作时返回 null，否则返回可直接展示给用户的禁止原因。</returns>
    private static string? GetDamageTreeReferenceBlockReason(
        LoadAndWriteDb db,
        IEnumerable<string?> partCodes,
        string operationName)
    {
        List<string> normalizedPartCodes = partCodes
            .Select(code => code?.Trim() ?? string.Empty)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedPartCodes.Count == 0)
        {
            return null;
        }

        List<Damage_Node_Info_By_MySQL> referencedNodes = new();
        foreach (string partCode in normalizedPartCodes)
        {
            referencedNodes.AddRange(
                db.mySqlCommand_TJ.SelectByField<Damage_Node_Info_By_MySQL>("d_n_PartCode", partCode));
        }

        if (referencedNodes.Count == 0)
        {
            return null;
        }

        int referencedPartCount = referencedNodes
            .Select(node => node.PartCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.Ordinal)
            .Count();
        string nodeNames = string.Join(
            "、",
            referencedNodes
                .Select(node => string.IsNullOrWhiteSpace(node.NodeName) ? node.NodeCode : node.NodeName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .Take(3));
        string nodeSummary = string.IsNullOrWhiteSpace(nodeNames) ? string.Empty : $"（节点：{nodeNames}）";

        return $"不能{operationName}：其中 {referencedPartCount} 个部件正被 {referencedNodes.Count} 个毁伤树节点引用{nodeSummary}。请先删除或修改相关毁伤节点。";
    }

    private LoadAndWriteDb OpenDb()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException($"未配置 MySQL 连接信息。请在程序输出目录放置 {MySQLConnectionInfo.DefaultConfigFileName}。当前运行目录：{AppDomain.CurrentDomain.BaseDirectory}");
        }

        if (!_databaseAndTableChecked)
        {
            MySQLConnectionInfo.EnsureDatabaseExists(_connectionString);
            using LoadAndWriteDb bootstrapDb = new(_connectionString);
            DatabaseSchemaMigrator.Apply(bootstrapDb);
            _databaseAndTableChecked = true;
        }

        return new LoadAndWriteDb(_connectionString);
    }

    private static string? ResolveDefaultConnectionString()
    {
        string? connectionString = MySQLConnectionInfo.TryBuildDefaultConnectionString(out string source, out string diagnosticMessage);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            MySqlLog.Log($"MySQL 连接配置来源：{source}");
        }
        else
        {
            MySqlLog.LogWarning(diagnosticMessage);
        }

        return connectionString;
    }

    private static void EnsureTables(LoadAndWriteDb db)
    {
        MySqlLog.Log("开始检查并创建目标信息表。 ");
        db.CreateTable<Target_Info_By_MySQL>();
        db.CreateTable<Target_System_Info_By_MySQL>();
        db.CreateTable<Target_Part_Info_By_MySQL>();
        db.CreateTable<Damage_Tree_Info_By_MySQL>();
        db.CreateTable<Damage_Node_Info_By_MySQL>();
        MySqlLog.Log("目标信息表检查完成：t_t_info、t_t_system、t_t_part、t_d_tree、t_d_node。 ");
    }

    private static IReadOnlyList<TargetInfoItem> BuildTargets(
        List<Target_Info_By_MySQL> targetRows,
        List<Target_System_Info_By_MySQL> systemRows,
        List<Target_Part_Info_By_MySQL> partRows)
    {
        Dictionary<string, List<Target_System_Info_By_MySQL>> systemsByTarget = systemRows
            .Where(item => !string.IsNullOrWhiteSpace(item.TargetCode))
            .GroupBy(item => item.TargetCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        Dictionary<string, List<Target_Part_Info_By_MySQL>> partsBySystem = partRows
            .Where(item => !string.IsNullOrWhiteSpace(item.PartSystemCode))
            .GroupBy(item => item.PartSystemCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        List<TargetInfoItem> targets = new();
        foreach (Target_Info_By_MySQL targetRow in targetRows.OrderBy(item => item.TargetName, StringComparer.CurrentCulture))
        {
            TargetInfoItem target = FromMySqlTarget(targetRow);
            if (systemsByTarget.TryGetValue(target.Code, out List<Target_System_Info_By_MySQL>? targetSystems))
            {
                foreach (TargetSystemInfoItem system in BuildTopSystems(targetSystems, partsBySystem))
                {
                    target.Systems.Add(system);
                }
            }

            targets.Add(target);
        }

        return targets;
    }

    private static IEnumerable<TargetSystemInfoItem> BuildTopSystems(
        List<Target_System_Info_By_MySQL> targetSystems,
        Dictionary<string, List<Target_Part_Info_By_MySQL>> partsBySystem)
    {
        Dictionary<string, List<Target_System_Info_By_MySQL>> childRowsByParent = targetSystems
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentCode) && item.ParentCode != "-1")
            .GroupBy(item => item.ParentCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        List<Target_System_Info_By_MySQL> topRows = targetSystems
            .Where(item => item.TargetCategory || string.IsNullOrWhiteSpace(item.ParentCode) || item.ParentCode == "-1")
            .OrderBy(item => item.SystemName, StringComparer.CurrentCulture)
            .ToList();

        HashSet<string> visited = new(StringComparer.Ordinal);
        foreach (Target_System_Info_By_MySQL topRow in topRows)
        {
            yield return BuildSystem(topRow, childRowsByParent, partsBySystem, visited);
        }

        // 防止数据库里存在 ParentCode 错误导致系统无法显示。
        foreach (Target_System_Info_By_MySQL orphanRow in targetSystems.Where(item => !visited.Contains(item.SystemCode)))
        {
            yield return BuildSystem(orphanRow, childRowsByParent, partsBySystem, visited);
        }
    }

    private static TargetSystemInfoItem BuildSystem(
        Target_System_Info_By_MySQL row,
        Dictionary<string, List<Target_System_Info_By_MySQL>> childRowsByParent,
        Dictionary<string, List<Target_Part_Info_By_MySQL>> partsBySystem,
        HashSet<string> visited)
    {
        TargetSystemInfoItem system = FromMySqlSystem(row);
        if (!visited.Add(system.SystemCode))
        {
            return system;
        }

        if (childRowsByParent.TryGetValue(system.SystemCode, out List<Target_System_Info_By_MySQL>? childRows))
        {
            foreach (Target_System_Info_By_MySQL childRow in childRows.OrderBy(item => item.SystemName, StringComparer.CurrentCulture))
            {
                system.ChildSystems.Add(BuildSystem(childRow, childRowsByParent, partsBySystem, visited));
            }
        }

        if (partsBySystem.TryGetValue(system.SystemCode, out List<Target_Part_Info_By_MySQL>? partRows))
        {
            foreach (Target_Part_Info_By_MySQL partRow in partRows.OrderBy(item => item.PartName, StringComparer.CurrentCulture))
            {
                system.Parts.Add(FromMySqlPart(partRow));
            }
        }

        return system;
    }

    private static HashSet<string> CollectDescendantSystemCodes(string rootSystemCode, List<Target_System_Info_By_MySQL> allSystems)
    {
        Dictionary<string, List<Target_System_Info_By_MySQL>> childRowsByParent = allSystems
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentCode))
            .GroupBy(item => item.ParentCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        HashSet<string> result = new(StringComparer.Ordinal) { rootSystemCode };
        Queue<string> queue = new();
        queue.Enqueue(rootSystemCode);

        while (queue.Count > 0)
        {
            string currentCode = queue.Dequeue();
            if (!childRowsByParent.TryGetValue(currentCode, out List<Target_System_Info_By_MySQL>? children))
            {
                continue;
            }

            foreach (Target_System_Info_By_MySQL child in children)
            {
                if (result.Add(child.SystemCode))
                {
                    queue.Enqueue(child.SystemCode);
                }
            }
        }

        return result;
    }

    private static TargetInfoItem FromMySqlTarget(Target_Info_By_MySQL row)
    {
        return new TargetInfoItem
        {
            Code = row.TargetCode ?? string.Empty,
            Name = row.TargetName ?? string.Empty,
            Category = row.TargetCategory ?? string.Empty,
            Description = row.TargetDescription ?? string.Empty
        };
    }

    private static TargetSystemInfoItem FromMySqlSystem(Target_System_Info_By_MySQL row)
    {
        return new TargetSystemInfoItem
        {
            SystemCode = row.SystemCode ?? string.Empty,
            SystemName = row.SystemName ?? string.Empty,
            SystemDescription = row.SystemDescription ?? string.Empty,
            IsTopSystem = row.TargetCategory,
            ParentSystemCode = string.IsNullOrWhiteSpace(row.ParentCode) ? "-1" : row.ParentCode,
            TargetCode = row.TargetCode ?? string.Empty
        };
    }

    private static TargetPartInfoItem FromMySqlPart(Target_Part_Info_By_MySQL row)
    {
        string shapeType = TargetPartShapeParameterDefinitions.NormalizeShapeType(row.PartShape);
        bool isHexahedron = TargetPartShapeParameterDefinitions.IsHexahedronShape(shapeType);
        TargetPartInfoItem part = new()
        {
            PartCode = row.PartCode ?? string.Empty,
            PartName = row.PartName ?? string.Empty,
            ShapeType = shapeType,
            PartDescription = row.PartDescription ?? string.Empty,
            MaterialId = row.PartMaterial ?? string.Empty,
            DisplayColor = TargetPartColorFormat.ToWpfDisplayColor(row.PartColor),
            EquivalentThickness = row.PartEquThickness,
            VulnerableArea = row.PartVulnerableArea,
            SystemCode = row.PartSystemCode ?? string.Empty
        };

        foreach (TargetPartParameterDefinition definition in TargetPartShapeParameterDefinitions.GetParameterDefinitions(shapeType))
        {
            part.Parameters.Add(TargetPartShapeParameterDefinitions.CreateParameterItem(definition, GetPartParameterValue(row, definition.Index)));
        }

        if (!isHexahedron)
        {
            // 非六面异形体固定约定：1~3 为中心坐标，7~9 为旋转角度。
            part.CenterX = GetPartParameterValue(row, 1);
            part.CenterY = GetPartParameterValue(row, 2);
            part.CenterZ = GetPartParameterValue(row, 3);
            part.RotationX = GetPartParameterValue(row, 7);
            part.RotationY = GetPartParameterValue(row, 8);
            part.RotationZ = GetPartParameterValue(row, 9);
        }

        return part;
    }

    private static Target_Info_By_MySQL ToMySqlTarget(TargetInfoItem target)
    {
        return new Target_Info_By_MySQL
        {
            TargetCode = target.Code?.Trim() ?? string.Empty,
            TargetName = target.Name?.Trim() ?? string.Empty,
            TargetCategory = target.Category?.Trim() ?? string.Empty,
            TargetDescription = target.Description?.Trim() ?? string.Empty
        };
    }

    private static Target_System_Info_By_MySQL ToMySqlSystem(TargetSystemInfoItem system)
    {
        return new Target_System_Info_By_MySQL
        {
            SystemCode = system.SystemCode?.Trim() ?? string.Empty,
            SystemName = system.SystemName?.Trim() ?? string.Empty,
            SystemDescription = system.SystemDescription?.Trim() ?? string.Empty,
            TargetCategory = system.IsTopSystem,
            ParentCode = string.IsNullOrWhiteSpace(system.ParentSystemCode) ? "-1" : system.ParentSystemCode.Trim(),
            TargetCode = system.TargetCode?.Trim() ?? string.Empty
        };
    }

    private static Target_Part_Info_By_MySQL ToMySqlPart(TargetPartInfoItem part)
    {
        string shapeType = TargetPartShapeParameterDefinitions.NormalizeShapeType(part.ShapeType);
        Target_Part_Info_By_MySQL row = new()
        {
            PartCode = part.PartCode?.Trim() ?? string.Empty,
            PartName = part.PartName?.Trim() ?? string.Empty,
            PartShape = shapeType,
            PartDescription = part.PartDescription?.Trim() ?? string.Empty,
            PartMaterial = string.IsNullOrWhiteSpace(part.MaterialId) ? "Fe" : part.MaterialId.Trim(),
            PartVulnerableArea = Convert.ToSingle(part.VulnerableArea),
            PartEquThickness = Convert.ToSingle(part.EquivalentThickness),
            PartColor = TargetPartColorFormat.ToUnityDatabaseColor(part.DisplayColor),
            PartSystemCode = part.SystemCode?.Trim() ?? string.Empty
        };

        foreach (TargetPartParameterItem parameter in part.Parameters.Where(item => item.Index is >= 1 and <= 24).OrderBy(item => item.Index))
        {
            SetPartParameterValue(row, parameter.Index, Convert.ToSingle(parameter.Value));
        }

        if (!TargetPartShapeParameterDefinitions.IsHexahedronShape(shapeType))
        {
            // 非六面异形体固定写回：1~3 为中心坐标，7~9 为旋转角度。
            // 这样即使调用方只更新了 Center/Rotation 属性，也不会丢失数据库中的姿态语义。
            SetPartParameterValue(row, 1, Convert.ToSingle(part.CenterX));
            SetPartParameterValue(row, 2, Convert.ToSingle(part.CenterY));
            SetPartParameterValue(row, 3, Convert.ToSingle(part.CenterZ));
            SetPartParameterValue(row, 7, Convert.ToSingle(part.RotationX));
            SetPartParameterValue(row, 8, Convert.ToSingle(part.RotationY));
            SetPartParameterValue(row, 9, Convert.ToSingle(part.RotationZ));
        }

        return row;
    }

    private static UnityTargetRecord ToUnityTargetRecord(Target_Info_By_MySQL row) => new()
    {
        TargetCode = row.TargetCode,
        TargetName = row.TargetName,
        TargetCategory = row.TargetCategory,
        TargetDescription = row.TargetDescription
    };

    private static UnityTargetSystemRecord ToUnityTargetSystemRecord(Target_System_Info_By_MySQL row) => new()
    {
        SystemCode = row.SystemCode,
        SystemName = row.SystemName,
        SystemDescription = row.SystemDescription,
        TargetCategory = row.TargetCategory,
        ParentCode = row.ParentCode,
        TargetCode = row.TargetCode
    };

    private static UnityTargetPartRecord ToUnityTargetPartRecord(Target_Part_Info_By_MySQL row) => new()
    {
        PartCode = row.PartCode,
        PartName = row.PartName,
        PartShape = row.PartShape,
        PartDescription = row.PartDescription,
        PartMaterial = row.PartMaterial,
        PartVulnerableArea = row.PartVulnerableArea,
        PartEquThickness = row.PartEquThickness,
        PartColor = TargetPartColorFormat.ToUnityDatabaseColor(row.PartColor),
        PartSystemCode = row.PartSystemCode,
        PartEquParam1 = row.PartEquParam1,
        PartEquParam2 = row.PartEquParam2,
        PartEquParam3 = row.PartEquParam3,
        PartEquParam4 = row.PartEquParam4,
        PartEquParam5 = row.PartEquParam5,
        PartEquParam6 = row.PartEquParam6,
        PartEquParam7 = row.PartEquParam7,
        PartEquParam8 = row.PartEquParam8,
        PartEquParam9 = row.PartEquParam9,
        PartEquParam10 = row.PartEquParam10,
        PartEquParam11 = row.PartEquParam11,
        PartEquParam12 = row.PartEquParam12,
        PartEquParam13 = row.PartEquParam13,
        PartEquParam14 = row.PartEquParam14,
        PartEquParam15 = row.PartEquParam15,
        PartEquParam16 = row.PartEquParam16,
        PartEquParam17 = row.PartEquParam17,
        PartEquParam18 = row.PartEquParam18,
        PartEquParam19 = row.PartEquParam19,
        PartEquParam20 = row.PartEquParam20,
        PartEquParam21 = row.PartEquParam21,
        PartEquParam22 = row.PartEquParam22,
        PartEquParam23 = row.PartEquParam23,
        PartEquParam24 = row.PartEquParam24
    };

    private static UnityDamageTreeRecord ToUnityDamageTreeRecord(Damage_Tree_Info_By_MySQL row) => new()
    {
        DamageTreeCode = row.DamageTreeCode,
        DamageTreeName = row.DamageTreeName,
        DamageTreeDescription = row.DamageTreeDescription,
        TargetCode = row.TargetCode,
        DamageLevelInfo = row.DamageLevelInfo,
        DamageTreeType = row.DamageTreeType
    };

    private static UnityDamageNodeRecord ToUnityDamageNodeRecord(Damage_Node_Info_By_MySQL row) => new()
    {
        NodeCode = row.NodeCode,
        DamageTreeCode = row.DamageTreeCode,
        NodeName = row.NodeName,
        ParentNodeCode = row.ParentNodeCode,
        RelationType = row.RelationType,
        VoteThreshold = row.VoteThreshold,
        PartCode = row.PartCode,
        NodeDescription = row.NodeDescription,
        SortOrder = row.SortOrder
    };
    private static float GetPartParameterValue(Target_Part_Info_By_MySQL part, int index)
    {
        return index switch
        {
            1 => part.PartEquParam1,
            2 => part.PartEquParam2,
            3 => part.PartEquParam3,
            4 => part.PartEquParam4,
            5 => part.PartEquParam5,
            6 => part.PartEquParam6,
            7 => part.PartEquParam7,
            8 => part.PartEquParam8,
            9 => part.PartEquParam9,
            10 => part.PartEquParam10,
            11 => part.PartEquParam11,
            12 => part.PartEquParam12,
            13 => part.PartEquParam13,
            14 => part.PartEquParam14,
            15 => part.PartEquParam15,
            16 => part.PartEquParam16,
            17 => part.PartEquParam17,
            18 => part.PartEquParam18,
            19 => part.PartEquParam19,
            20 => part.PartEquParam20,
            21 => part.PartEquParam21,
            22 => part.PartEquParam22,
            23 => part.PartEquParam23,
            24 => part.PartEquParam24,
            _ => 0
        };
    }

    private static void SetPartParameterValue(Target_Part_Info_By_MySQL part, int index, float value)
    {
        switch (index)
        {
            case 1: part.PartEquParam1 = value; break;
            case 2: part.PartEquParam2 = value; break;
            case 3: part.PartEquParam3 = value; break;
            case 4: part.PartEquParam4 = value; break;
            case 5: part.PartEquParam5 = value; break;
            case 6: part.PartEquParam6 = value; break;
            case 7: part.PartEquParam7 = value; break;
            case 8: part.PartEquParam8 = value; break;
            case 9: part.PartEquParam9 = value; break;
            case 10: part.PartEquParam10 = value; break;
            case 11: part.PartEquParam11 = value; break;
            case 12: part.PartEquParam12 = value; break;
            case 13: part.PartEquParam13 = value; break;
            case 14: part.PartEquParam14 = value; break;
            case 15: part.PartEquParam15 = value; break;
            case 16: part.PartEquParam16 = value; break;
            case 17: part.PartEquParam17 = value; break;
            case 18: part.PartEquParam18 = value; break;
            case 19: part.PartEquParam19 = value; break;
            case 20: part.PartEquParam20 = value; break;
            case 21: part.PartEquParam21 = value; break;
            case 22: part.PartEquParam22 = value; break;
            case 23: part.PartEquParam23 = value; break;
            case 24: part.PartEquParam24 = value; break;
        }
    }
}
