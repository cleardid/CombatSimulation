using CombatSimulation.Models;

namespace CombatSimulation.Services.DamageTrees;

/// <summary>
/// 毁伤树编辑领域服务，集中负责草稿创建、历史数据归一化和业务校验。
/// </summary>
public sealed class DamageTreeEditor
{
    public DamageTreeInfoItem CreateTreeDraft(
        TargetInfoItem? target,
        IEnumerable<DamageTreeInfoItem> existingTrees)
    {
        ArgumentNullException.ThrowIfNull(existingTrees);

        string treeCode = Guid.NewGuid().ToString("N");
        string rootNodeCode = Guid.NewGuid().ToString("N");
        string targetName = target?.Name ?? "目标";
        string damageLevel = DamageTreeDefaults.FindFirstAvailableDamageLevel(existingTrees);

        DamageTreeInfoItem tree = new()
        {
            DamageTreeCode = treeCode,
            DamageTreeName = DamageTreeDefaults.CreateDefaultTreeName(targetName, damageLevel),
            DamageTreeDescription = string.Empty,
            TargetCode = target?.Code ?? string.Empty,
            DamageLevelInfo = damageLevel,
            DamageTreeType = DamageTreeDefaults.DefaultTreeType
        };

        tree.RootNodes.Add(new DamageTreeNodeItem
        {
            NodeCode = rootNodeCode,
            DamageTreeCode = treeCode,
            NodeName = DamageTreeDefaults.CreateDefaultRootNodeName(targetName, damageLevel),
            ParentNodeCode = string.Empty,
            RelationType = DamageNodeRelationType.And,
            VoteThreshold = 1,
            NodeDescription = string.Empty,
            SortOrder = 0,
            IsExpanded = true
        });

        return tree;
    }

    public DamageTreeInfoItem CloneTreeForEdit(DamageTreeInfoItem source)
    {
        ArgumentNullException.ThrowIfNull(source);

        DamageTreeInfoItem clone = new()
        {
            DamageTreeCode = source.DamageTreeCode,
            DamageTreeName = source.DamageTreeName,
            DamageTreeDescription = source.DamageTreeDescription,
            TargetCode = source.TargetCode,
            DamageLevelInfo = source.DamageLevelInfo,
            DamageTreeType = source.DamageTreeType
        };

        foreach (DamageTreeNodeItem rootNode in source.RootNodes)
        {
            clone.RootNodes.Add(rootNode.CloneShallow());
        }

        return clone;
    }

    public DamageTreeNodeItem CreateMiddleNodeDraft(DamageTreeNodeItem parentNode)
    {
        ArgumentNullException.ThrowIfNull(parentNode);
        return new DamageTreeNodeItem
        {
            NodeCode = Guid.NewGuid().ToString("N"),
            DamageTreeCode = parentNode.DamageTreeCode,
            NodeName = "新建中间节点",
            ParentNodeCode = parentNode.NodeCode,
            RelationType = DamageNodeRelationType.And,
            VoteThreshold = 1,
            NodeDescription = string.Empty,
            SortOrder = parentNode.Children.Count,
            IsExpanded = true
        };
    }

    public DamageTreeNodeItem CreateLeafNodeDraft(DamageTreeNodeItem parentNode)
    {
        ArgumentNullException.ThrowIfNull(parentNode);
        return new DamageTreeNodeItem
        {
            NodeCode = Guid.NewGuid().ToString("N"),
            DamageTreeCode = parentNode.DamageTreeCode,
            NodeName = "新建叶子节点",
            ParentNodeCode = parentNode.NodeCode,
            RelationType = DamageNodeRelationType.None,
            VoteThreshold = 1,
            PartCode = string.Empty,
            PartName = string.Empty,
            NodeDescription = string.Empty,
            SortOrder = parentNode.Children.Count,
            IsExpanded = false
        };
    }

    public DamageTreeNodeItem CloneNodeForEdit(DamageTreeNodeItem source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.CloneShallow();
    }

    public DamageTreeLoadPreparation PrepareLoadedTrees(IEnumerable<DamageTreeInfoItem> storedTrees)
    {
        ArgumentNullException.ThrowIfNull(storedTrees);

        List<DamageTreeInfoItem> acceptedTrees = new();
        HashSet<string> loadedLevels = new(StringComparer.Ordinal);
        int skippedTreeCount = 0;

        foreach (DamageTreeInfoItem tree in storedTrees)
        {
            tree.DamageLevelInfo = DamageTreeDefaults.NormalizeDamageLevel(tree.DamageLevelInfo);
            tree.DamageTreeType = DamageTreeDefaults.DefaultTreeType;

            if (!DamageTreeDefaults.IsKnownDamageLevel(tree.DamageLevelInfo)
                || !loadedLevels.Add(tree.DamageLevelInfo))
            {
                skippedTreeCount++;
                continue;
            }

            acceptedTrees.Add(tree);
        }

        return new DamageTreeLoadPreparation(acceptedTrees, skippedTreeCount);
    }

    public DamageTreeValidationResult NormalizeAndValidateTree(
        DamageTreeInfoItem tree,
        TargetInfoItem? target,
        IEnumerable<DamageTreeInfoItem> existingTrees,
        DamageTreeInfoItem? except)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(existingTrees);

        if (target == null)
        {
            return DamageTreeValidationResult.Failure("请先选择目标");
        }

        tree.DamageTreeCode = string.IsNullOrWhiteSpace(tree.DamageTreeCode)
            ? Guid.NewGuid().ToString("N")
            : tree.DamageTreeCode.Trim();
        tree.TargetCode = target.Code;
        tree.DamageTreeName = tree.DamageTreeName.Trim();
        tree.DamageTreeDescription = tree.DamageTreeDescription.Trim();
        tree.DamageLevelInfo = DamageTreeDefaults.NormalizeDamageLevel(tree.DamageLevelInfo);
        tree.DamageTreeType = DamageTreeDefaults.DefaultTreeType;

        if (string.IsNullOrWhiteSpace(tree.DamageTreeName))
        {
            return DamageTreeValidationResult.Failure("请输入毁伤树名称");
        }

        if (!DamageTreeDefaults.IsKnownDamageLevel(tree.DamageLevelInfo))
        {
            return DamageTreeValidationResult.Failure("毁伤树只能选择轻度毁伤、中度毁伤或重度毁伤");
        }

        EnsureRootNode(tree);

        bool codeUsed = existingTrees.Any(existingTree =>
            !ReferenceEquals(existingTree, except)
            && string.Equals(existingTree.DamageTreeCode, tree.DamageTreeCode, StringComparison.Ordinal));
        if (codeUsed)
        {
            return DamageTreeValidationResult.Failure($"毁伤树唯一标识已存在：{tree.DamageTreeCode}");
        }

        bool damageLevelUsed = existingTrees.Any(existingTree =>
            !ReferenceEquals(existingTree, except)
            && string.Equals(
                DamageTreeDefaults.NormalizeDamageLevel(existingTree.DamageLevelInfo),
                tree.DamageLevelInfo,
                StringComparison.Ordinal));
        if (damageLevelUsed)
        {
            return DamageTreeValidationResult.Failure(
                $"当前目标已经存在{tree.DamageLevelInfo}树，轻度、中度、重度各只能保留一棵");
        }

        return DamageTreeValidationResult.Success;
    }

    public DamageTreeValidationResult NormalizeAndValidateNode(
        DamageTreeNodeItem node,
        DamageTreeInfoItem? selectedTree,
        TargetInfoItem? target,
        DamageTreeNodeItem? except)
    {
        ArgumentNullException.ThrowIfNull(node);

        node.NodeCode = string.IsNullOrWhiteSpace(node.NodeCode)
            ? Guid.NewGuid().ToString("N")
            : node.NodeCode.Trim();
        node.NodeName = node.NodeName.Trim();
        node.NodeDescription = node.NodeDescription.Trim();
        node.PartCode = node.PartCode.Trim();
        node.PartName = node.PartName.Trim();
        node.VoteThreshold = Math.Max(1f, node.VoteThreshold);

        if (string.IsNullOrWhiteSpace(node.NodeName))
        {
            return DamageTreeValidationResult.Failure("请输入毁伤节点名称");
        }

        if (node.IsLeafNode)
        {
            if (string.IsNullOrWhiteSpace(node.PartCode))
            {
                return DamageTreeValidationResult.Failure("叶子节点必须绑定目标部件");
            }

            if (!BuildPartLookup(target).ContainsKey(node.PartCode))
            {
                return DamageTreeValidationResult.Failure(
                    "叶子节点绑定的部件不存在，请重新选择目标结构中的底层部件");
            }
        }
        else
        {
            node.PartCode = string.Empty;
            node.PartName = string.Empty;
        }

        bool codeUsed = selectedTree != null
            && EnumerateNodes(selectedTree.RootNodes).Any(existingNode =>
                !ReferenceEquals(existingNode, except)
                && string.Equals(existingNode.NodeCode, node.NodeCode, StringComparison.Ordinal));
        if (codeUsed)
        {
            return DamageTreeValidationResult.Failure($"毁伤节点唯一标识已存在：{node.NodeCode}");
        }

        return DamageTreeValidationResult.Success;
    }

    public IReadOnlyDictionary<string, TargetPartInfoItem> BuildPartLookup(TargetInfoItem? target)
    {
        if (target == null)
        {
            return new Dictionary<string, TargetPartInfoItem>(StringComparer.Ordinal);
        }

        return EnumerateSystems(target.Systems)
            .SelectMany(system => system.Parts)
            .Where(part => !string.IsNullOrWhiteSpace(part.PartCode))
            .GroupBy(part => part.PartCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    public DamageTreeNodeItem? FindPreferredNode(DamageTreeInfoItem? tree)
    {
        if (tree == null)
        {
            return null;
        }

        return FindNode(tree.RootNodes, node => node.IsSelected)
               ?? tree.RootNodes.FirstOrDefault();
    }

    public bool HasAllDamageLevels(IEnumerable<DamageTreeInfoItem> trees)
    {
        ArgumentNullException.ThrowIfNull(trees);
        return DamageTreeDefaults.DamageLevels.All(level =>
            trees.Any(tree => string.Equals(
                DamageTreeDefaults.NormalizeDamageLevel(tree.DamageLevelInfo),
                level,
                StringComparison.Ordinal)));
    }

    public void ApplyNodeUpdate(DamageTreeNodeItem target, DamageTreeNodeItem source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        target.NodeName = source.NodeName;
        target.RelationType = source.RelationType;
        target.VoteThreshold = source.VoteThreshold;
        target.PartCode = source.PartCode;
        target.PartName = source.PartName;
        target.NodeDescription = source.NodeDescription;

        if (target.IsLeafNode)
        {
            target.Children.Clear();
        }
    }

    private static void EnsureRootNode(DamageTreeInfoItem tree)
    {
        if (tree.RootNodes.Count == 0)
        {
            tree.RootNodes.Add(new DamageTreeNodeItem
            {
                NodeCode = Guid.NewGuid().ToString("N"),
                DamageTreeCode = tree.DamageTreeCode,
                NodeName = tree.DamageTreeName,
                ParentNodeCode = string.Empty,
                RelationType = DamageNodeRelationType.And,
                VoteThreshold = 1,
                SortOrder = 0,
                IsExpanded = true
            });
        }

        DamageTreeNodeItem root = tree.RootNodes[0];
        root.DamageTreeCode = tree.DamageTreeCode;
        root.ParentNodeCode = string.Empty;
        root.NodeCode = string.IsNullOrWhiteSpace(root.NodeCode)
            ? Guid.NewGuid().ToString("N")
            : root.NodeCode.Trim();
        root.NodeName = string.IsNullOrWhiteSpace(root.NodeName)
            ? tree.DamageTreeName
            : root.NodeName.Trim();
        if (root.IsLeafNode)
        {
            root.RelationType = DamageNodeRelationType.And;
        }
    }

    private static IEnumerable<TargetSystemInfoItem> EnumerateSystems(
        IEnumerable<TargetSystemInfoItem> systems)
    {
        foreach (TargetSystemInfoItem system in systems)
        {
            yield return system;

            foreach (TargetSystemInfoItem childSystem in EnumerateSystems(system.ChildSystems))
            {
                yield return childSystem;
            }
        }
    }

    private static DamageTreeNodeItem? FindNode(
        IEnumerable<DamageTreeNodeItem> nodes,
        Func<DamageTreeNodeItem, bool> predicate)
    {
        foreach (DamageTreeNodeItem node in nodes)
        {
            if (predicate(node))
            {
                return node;
            }

            DamageTreeNodeItem? child = FindNode(node.Children, predicate);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    private static IEnumerable<DamageTreeNodeItem> EnumerateNodes(
        IEnumerable<DamageTreeNodeItem> nodes)
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

public readonly record struct DamageTreeValidationResult(bool IsValid, string ErrorMessage)
{
    public static DamageTreeValidationResult Success { get; } = new(true, string.Empty);

    public static DamageTreeValidationResult Failure(string errorMessage) =>
        new(false, errorMessage);
}

public sealed record DamageTreeLoadPreparation(
    IReadOnlyList<DamageTreeInfoItem> Trees,
    int SkippedTreeCount);
