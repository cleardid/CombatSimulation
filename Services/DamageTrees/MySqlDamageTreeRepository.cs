using CombatSimulation.Models;
using CombatSimulation.Services.MySql;

namespace CombatSimulation.Services.DamageTrees;

/// <summary>
/// 基于 MySQL 的毁伤树仓储。
/// </summary>
/// <remarks>
/// 该类只负责毁伤树主表和毁伤节点表的增删改查，不包含 Unity 通信逻辑。
/// WPF 端使用 DamageTreeInfoItem / DamageTreeNodeItem；数据库端使用 Damage_Tree_Info_By_MySQL / Damage_Node_Info_By_MySQL。
/// </remarks>
public sealed class MySqlDamageTreeRepository : IDamageTreeRepository
{
    private readonly string _connectionString;
    private bool _databaseAndTableChecked;

    /// <summary>
    /// 默认从程序运行目录下的 target_mysql.json 读取连接信息。
    /// </summary>
    public MySqlDamageTreeRepository()
    {
        _connectionString = ResolveDefaultConnectionString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            MySqlLog.LogWarning("毁伤树仓储未获得有效连接字符串，后续读取或写入会失败。 ");
        }
    }

    /// <summary>
    /// 使用指定 MySQL 连接字符串创建仓储。
    /// </summary>
    public MySqlDamageTreeRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("MySQL 连接字符串不能为空。", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    /// <summary>
    /// 使用连接配置对象创建仓储。
    /// </summary>
    public MySqlDamageTreeRepository(MySQLConnectionInfo connectionInfo)
        : this(connectionInfo?.ToString() ?? throw new ArgumentNullException(nameof(connectionInfo)))
    {
    }

    /// <summary>
    /// 读取指定目标下的全部毁伤树。
    /// </summary>
    /// <param name="targetCode">目标唯一标识。</param>
    /// <param name="partsByCode">目标部件索引，用于把叶子节点的 PartCode 翻译为部件名称。</param>
    public Task<IReadOnlyList<DamageTreeInfoItem>> LoadDamageTreesAsync(
        string targetCode,
        IReadOnlyDictionary<string, TargetPartInfoItem>? partsByCode = null,
        CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => LoadDamageTrees(targetCode, partsByCode), cancellationToken);

    private IReadOnlyList<DamageTreeInfoItem> LoadDamageTrees(string targetCode, IReadOnlyDictionary<string, TargetPartInfoItem>? partsByCode)
    {
        if (string.IsNullOrWhiteSpace(targetCode))
        {
            return Array.Empty<DamageTreeInfoItem>();
        }

        using LoadAndWriteDb db = OpenDb();
        List<Damage_Tree_Info_By_MySQL> treeRows = db.mySqlCommand_TJ.SelectByField<Damage_Tree_Info_By_MySQL>("d_t_TargetCode", targetCode);
        List<Damage_Node_Info_By_MySQL> allNodeRows = db.GetTableData<Damage_Node_Info_By_MySQL>();

        return BuildDamageTrees(treeRows, allNodeRows, partsByCode);
    }

    /// <summary>
    /// 新增毁伤树及其根节点。
    /// </summary>
    public Task AddDamageTreeAsync(DamageTreeInfoItem tree, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => AddDamageTree(tree), cancellationToken);

    private void AddDamageTree(DamageTreeInfoItem tree)
    {
        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        db.ExecuteInTransaction(() =>
        {

        db.mySqlCommand_TJ.Insert(ToMySqlTree(tree));
        foreach (DamageTreeNodeItem node in EnumerateNodes(tree.RootNodes))
        {
            db.mySqlCommand_TJ.Insert(ToMySqlNode(node));
        }
        });
    }

    /// <summary>
    /// 修改毁伤树基础信息。根节点名称若在草稿中被修改，也会同步写回节点表。
    /// </summary>
    public Task UpdateDamageTreeAsync(DamageTreeInfoItem tree, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => UpdateDamageTree(tree), cancellationToken);

    private void UpdateDamageTree(DamageTreeInfoItem tree)
    {
        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        db.ExecuteInTransaction(() =>
        {

        db.mySqlCommand_TJ.Insert(ToMySqlTree(tree));
        foreach (DamageTreeNodeItem node in EnumerateNodes(tree.RootNodes))
        {
            db.mySqlCommand_TJ.Insert(ToMySqlNode(node));
        }
        });
    }

    /// <summary>
    /// 删除毁伤树及其所有节点。
    /// </summary>
    public Task DeleteDamageTreeAsync(string damageTreeCode, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => DeleteDamageTree(damageTreeCode), cancellationToken);

    private void DeleteDamageTree(string damageTreeCode)
    {
        if (string.IsNullOrWhiteSpace(damageTreeCode))
        {
            return;
        }

        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        db.ExecuteInTransaction(() =>
        {

        db.mySqlCommand_TJ.DeleteByField<Damage_Node_Info_By_MySQL>("d_n_TreeCode", damageTreeCode);
        db.mySqlCommand_TJ.DeleteByID<Damage_Tree_Info_By_MySQL>(damageTreeCode);
        });
    }

    /// <summary>
    /// 新增毁伤节点。
    /// </summary>
    public Task AddNodeAsync(DamageTreeNodeItem node, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => AddNode(node), cancellationToken);

    private void AddNode(DamageTreeNodeItem node)
    {
        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        db.mySqlCommand_TJ.Insert(ToMySqlNode(node));
    }

    /// <summary>
    /// 修改毁伤节点。如果节点主键变化，会同步修正子节点的父节点引用。
    /// </summary>
    public Task UpdateNodeAsync(string originalNodeCode, DamageTreeNodeItem node, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => UpdateNode(originalNodeCode, node), cancellationToken);

    private void UpdateNode(string originalNodeCode, DamageTreeNodeItem node)
    {
        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        db.ExecuteInTransaction(() =>
        {

        if (!string.Equals(originalNodeCode, node.NodeCode, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(originalNodeCode))
        {
            db.mySqlCommand_TJ.Insert(ToMySqlNode(node));

            List<Damage_Node_Info_By_MySQL> childRows = db.mySqlCommand_TJ.SelectByField<Damage_Node_Info_By_MySQL>("d_n_ParentCode", originalNodeCode);
            foreach (Damage_Node_Info_By_MySQL childRow in childRows)
            {
                childRow.ParentNodeCode = node.NodeCode;
                db.mySqlCommand_TJ.Insert(childRow);
            }

            db.mySqlCommand_TJ.DeleteByID<Damage_Node_Info_By_MySQL>(originalNodeCode);
            return;
        }

        db.mySqlCommand_TJ.Insert(ToMySqlNode(node));
        });
    }

    /// <summary>
    /// 删除节点及其全部后代节点。
    /// </summary>
    public Task DeleteNodeAsync(string damageTreeCode, string nodeCode, CancellationToken cancellationToken = default) =>
        BackgroundRepositoryOperation.RunAsync(() => DeleteNode(damageTreeCode, nodeCode), cancellationToken);

    private void DeleteNode(string damageTreeCode, string nodeCode)
    {
        if (string.IsNullOrWhiteSpace(damageTreeCode) || string.IsNullOrWhiteSpace(nodeCode))
        {
            return;
        }

        using LoadAndWriteDb db = OpenDb();
        EnsureTables(db);
        db.ExecuteInTransaction(() =>
        {

        List<Damage_Node_Info_By_MySQL> allRows = db.mySqlCommand_TJ.SelectByField<Damage_Node_Info_By_MySQL>("d_n_TreeCode", damageTreeCode);
        HashSet<string> codesToDelete = CollectDescendantNodeCodes(nodeCode, allRows);
        foreach (string code in codesToDelete)
        {
            db.mySqlCommand_TJ.DeleteByID<Damage_Node_Info_By_MySQL>(code);
        }
        });
    }

    /// <summary>
    /// 创建毁伤树相关数据表。
    /// </summary>
    public static void EnsureDamageTreeTables(LoadAndWriteDb db)
    {
        db.CreateTable<Damage_Tree_Info_By_MySQL>();
        db.CreateTable<Damage_Node_Info_By_MySQL>();
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
            MySqlLog.Log($"毁伤树 MySQL 连接配置来源：{source}");
        }
        else
        {
            MySqlLog.LogWarning(diagnosticMessage);
        }

        return connectionString;
    }

    private static void EnsureTables(LoadAndWriteDb db)
    {
        MySqlLog.Log("开始检查并创建毁伤树信息表。 ");
        EnsureDamageTreeTables(db);
        MySqlLog.Log("毁伤树信息表检查完成：t_d_tree、t_d_node。 ");
    }

    private static IReadOnlyList<DamageTreeInfoItem> BuildDamageTrees(
        List<Damage_Tree_Info_By_MySQL> treeRows,
        List<Damage_Node_Info_By_MySQL> allNodeRows,
        IReadOnlyDictionary<string, TargetPartInfoItem>? partsByCode)
    {
        Dictionary<string, List<Damage_Node_Info_By_MySQL>> nodesByTree = allNodeRows
            .Where(item => !string.IsNullOrWhiteSpace(item.DamageTreeCode))
            .GroupBy(item => item.DamageTreeCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        List<DamageTreeInfoItem> result = new();
        foreach (Damage_Tree_Info_By_MySQL treeRow in treeRows.OrderBy(item => item.DamageLevelInfo, StringComparer.CurrentCulture).ThenBy(item => item.DamageTreeType, StringComparer.CurrentCulture))
        {
            DamageTreeInfoItem tree = FromMySqlTree(treeRow);
            if (nodesByTree.TryGetValue(tree.DamageTreeCode, out List<Damage_Node_Info_By_MySQL>? nodeRows))
            {
                foreach (DamageTreeNodeItem rootNode in BuildRootNodes(nodeRows, partsByCode))
                {
                    tree.RootNodes.Add(rootNode);
                }
            }

            result.Add(tree);
        }

        return result;
    }

    private static IEnumerable<DamageTreeNodeItem> BuildRootNodes(List<Damage_Node_Info_By_MySQL> nodeRows, IReadOnlyDictionary<string, TargetPartInfoItem>? partsByCode)
    {
        Dictionary<string, List<Damage_Node_Info_By_MySQL>> childRowsByParent = nodeRows
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentNodeCode))
            .GroupBy(item => item.ParentNodeCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        List<Damage_Node_Info_By_MySQL> rootRows = nodeRows
            .Where(item => string.IsNullOrWhiteSpace(item.ParentNodeCode))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.NodeName, StringComparer.CurrentCulture)
            .ToList();

        HashSet<string> visited = new(StringComparer.Ordinal);
        foreach (Damage_Node_Info_By_MySQL rootRow in rootRows)
        {
            yield return BuildNode(rootRow, childRowsByParent, partsByCode, visited);
        }

        // 容错处理：如果历史数据 ParentCode 错误导致某些节点无法挂到根下，则作为根节点显示。
        foreach (Damage_Node_Info_By_MySQL orphanRow in nodeRows.Where(item => !visited.Contains(item.NodeCode)))
        {
            yield return BuildNode(orphanRow, childRowsByParent, partsByCode, visited);
        }
    }

    private static DamageTreeNodeItem BuildNode(
        Damage_Node_Info_By_MySQL row,
        Dictionary<string, List<Damage_Node_Info_By_MySQL>> childRowsByParent,
        IReadOnlyDictionary<string, TargetPartInfoItem>? partsByCode,
        HashSet<string> visited)
    {
        DamageTreeNodeItem node = FromMySqlNode(row, partsByCode);
        if (!visited.Add(node.NodeCode))
        {
            return node;
        }

        if (childRowsByParent.TryGetValue(node.NodeCode, out List<Damage_Node_Info_By_MySQL>? childRows))
        {
            foreach (Damage_Node_Info_By_MySQL childRow in childRows.OrderBy(item => item.SortOrder).ThenBy(item => item.NodeName, StringComparer.CurrentCulture))
            {
                node.Children.Add(BuildNode(childRow, childRowsByParent, partsByCode, visited));
            }
        }

        return node;
    }

    private static DamageTreeInfoItem FromMySqlTree(Damage_Tree_Info_By_MySQL row)
    {
        return new DamageTreeInfoItem
        {
            DamageTreeCode = row.DamageTreeCode ?? string.Empty,
            DamageTreeName = row.DamageTreeName ?? string.Empty,
            DamageTreeDescription = row.DamageTreeDescription ?? string.Empty,
            TargetCode = row.TargetCode ?? string.Empty,
            DamageLevelInfo = DamageTreeDefaults.NormalizeDamageLevel(row.DamageLevelInfo),
            DamageTreeType = string.IsNullOrWhiteSpace(row.DamageTreeType) ? DamageTreeDefaults.DefaultTreeType : row.DamageTreeType.Trim()
        };
    }

    private static DamageTreeNodeItem FromMySqlNode(Damage_Node_Info_By_MySQL row, IReadOnlyDictionary<string, TargetPartInfoItem>? partsByCode)
    {
        string partCode = row.PartCode ?? string.Empty;

        // 叶子节点通过 PartCode 关联目标结构中的底层部件。
        // partsByCode 可能为空，因此这里必须先显式初始化 part，避免条件访问 ?.TryGetValue
        // 导致编译器认为 out 变量在后续 PartName 赋值处可能尚未赋值。
        TargetPartInfoItem? part = null;
        if (!string.IsNullOrWhiteSpace(partCode) && partsByCode is not null)
        {
            partsByCode.TryGetValue(partCode, out part);
        }

        DamageNodeRelationType relationType = Enum.IsDefined(typeof(DamageNodeRelationType), row.RelationType)
            ? (DamageNodeRelationType)row.RelationType
            : DamageNodeRelationType.None;

        return new DamageTreeNodeItem
        {
            NodeCode = row.NodeCode ?? string.Empty,
            DamageTreeCode = row.DamageTreeCode ?? string.Empty,
            NodeName = row.NodeName ?? string.Empty,
            ParentNodeCode = row.ParentNodeCode ?? string.Empty,
            RelationType = relationType,
            VoteThreshold = Math.Max(1f, row.VoteThreshold ?? 1f),
            PartCode = partCode,
            PartName = part?.PartName ?? string.Empty,
            NodeDescription = row.NodeDescription ?? string.Empty,
            SortOrder = row.SortOrder,
            IsExpanded = true
        };
    }

    private static Damage_Tree_Info_By_MySQL ToMySqlTree(DamageTreeInfoItem tree)
    {
        return new Damage_Tree_Info_By_MySQL
        {
            DamageTreeCode = tree.DamageTreeCode?.Trim() ?? string.Empty,
            DamageTreeName = tree.DamageTreeName?.Trim() ?? string.Empty,
            DamageTreeDescription = tree.DamageTreeDescription?.Trim() ?? string.Empty,
            TargetCode = tree.TargetCode?.Trim() ?? string.Empty,
            DamageLevelInfo = DamageTreeDefaults.NormalizeDamageLevel(tree.DamageLevelInfo),
            DamageTreeType = string.IsNullOrWhiteSpace(tree.DamageTreeType) ? DamageTreeDefaults.DefaultTreeType : tree.DamageTreeType.Trim()
        };
    }

    private static Damage_Node_Info_By_MySQL ToMySqlNode(DamageTreeNodeItem node)
    {
        DamageNodeRelationType relationType = node.IsLeafNode ? DamageNodeRelationType.None : node.RelationType;
        return new Damage_Node_Info_By_MySQL
        {
            NodeCode = node.NodeCode?.Trim() ?? string.Empty,
            DamageTreeCode = node.DamageTreeCode?.Trim() ?? string.Empty,
            NodeName = node.NodeName?.Trim() ?? string.Empty,
            ParentNodeCode = node.ParentNodeCode?.Trim() ?? string.Empty,
            RelationType = (int)relationType,
            // VoteThreshold 在数据库中使用 DOUBLE；保存时保留小数，只对下限做保护。
            VoteThreshold = relationType == DamageNodeRelationType.Vote ? Math.Max(1f, node.VoteThreshold) : null,
            PartCode = relationType == DamageNodeRelationType.None ? node.PartCode?.Trim() ?? string.Empty : string.Empty,
            NodeDescription = node.NodeDescription?.Trim() ?? string.Empty,
            SortOrder = node.SortOrder
        };
    }

    private static HashSet<string> CollectDescendantNodeCodes(string rootNodeCode, List<Damage_Node_Info_By_MySQL> allNodes)
    {
        Dictionary<string, List<Damage_Node_Info_By_MySQL>> childRowsByParent = allNodes
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentNodeCode))
            .GroupBy(item => item.ParentNodeCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        HashSet<string> result = new(StringComparer.Ordinal) { rootNodeCode };
        Queue<string> queue = new();
        queue.Enqueue(rootNodeCode);

        while (queue.Count > 0)
        {
            string currentCode = queue.Dequeue();
            if (!childRowsByParent.TryGetValue(currentCode, out List<Damage_Node_Info_By_MySQL>? children))
            {
                continue;
            }

            foreach (Damage_Node_Info_By_MySQL child in children)
            {
                if (result.Add(child.NodeCode))
                {
                    queue.Enqueue(child.NodeCode);
                }
            }
        }

        return result;
    }

    private static IEnumerable<DamageTreeNodeItem> EnumerateNodes(IEnumerable<DamageTreeNodeItem> nodes)
    {
        foreach (DamageTreeNodeItem node in nodes)
        {
            yield return node;

            foreach (DamageTreeNodeItem child in EnumerateNodes(node.Children))
            {
                yield return child;
            }
        }
    }
}
