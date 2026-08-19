using CombatSimulation.Models;
using CombatSimulation.Services.MySql;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CombatSimulation.ViewModels;

public sealed partial class TargetInfoViewModel
{
    /// <summary>
    /// 选中的毁伤树变化后，自动选中该树的根节点并刷新右侧详情。
    /// </summary>
    partial void OnSelectedDamageTreeChanged(DamageTreeInfoItem? value)
    {
        SelectedDamageTreeNode = FindPreferredDamageNode(value);
    }

    /// <summary>
    /// 选中的毁伤节点变化后，同步节点选中状态并刷新节点详情。
    /// </summary>
    partial void OnSelectedDamageTreeNodeChanged(DamageTreeNodeItem? value)
    {
        SynchronizeDamageNodeSelection(value);
        RefreshDamageNodeDetailRows(value);
    }

    /// <summary>
    /// 清空当前毁伤树界面状态。
    /// </summary>
    private void ClearDamageTreeState()
    {
        SelectedDamageTreeNode = null;
        SelectedDamageTree = null;
        DamageTrees.Clear();
        SelectedDamageNodeDetailRows.Clear();
    }

    /// <summary>
    /// 在后台线程读取当前目标下的毁伤树；快速切换目标时只应用最后一次结果。
    /// </summary>
    private async void LoadDamageTreesForSelectedTarget()
    {
        TargetInfoItem? target = SelectedTarget;
        if (target == null)
        {
            StatusText = "请先选择目标，再查看毁伤树信息";
            return;
        }

        _damageTreeLoadCts?.Cancel();
        CancellationTokenSource loadCts = new();
        _damageTreeLoadCts = loadCts;

        DamageTrees.Clear();
        SelectedDamageTree = null;
        SelectedDamageTreeNode = null;
        SelectedDamageNodeDetailRows.Clear();

        StatusText = $"正在读取目标“{target.Name}”的毁伤树...";

        try
        {
            IReadOnlyDictionary<string, TargetPartInfoItem> partLookup = BuildPartLookup(target);
            IReadOnlyList<DamageTreeInfoItem> storedTrees = await RunRepositoryOperationAsync(
                () => _damageTreeRepository.LoadDamageTrees(target.Code, partLookup));
            if (loadCts.IsCancellationRequested || !ReferenceEquals(SelectedTarget, target))
            {
                return;
            }

            // 当前界面只展示轻度、中度、重度三棵功能毁伤树。
            // 如果数据库中存在历史重复项，只保留同一毁伤等级的第一棵，避免下拉框出现多个“轻度毁伤树”。
            int skippedTreeCount = 0;
            HashSet<string> loadedLevels = new(StringComparer.Ordinal);
            foreach (DamageTreeInfoItem tree in storedTrees)
            {
                tree.DamageLevelInfo = DamageTreeDefaults.NormalizeDamageLevel(tree.DamageLevelInfo);
                tree.DamageTreeType = DamageTreeDefaults.DefaultTreeType;

                if (!DamageTreeDefaults.IsKnownDamageLevel(tree.DamageLevelInfo) || !loadedLevels.Add(tree.DamageLevelInfo))
                {
                    skippedTreeCount++;
                    continue;
                }

                DamageTrees.Add(tree);
            }

            SelectedDamageTree = DamageTrees.FirstOrDefault();
            StatusText = DamageTrees.Count == 0
                ? $"目标“{target.Name}”暂无毁伤树"
                : skippedTreeCount == 0
                    ? $"已读取目标“{target.Name}”的 {DamageTrees.Count} 棵毁伤树"
                    : $"已读取目标“{target.Name}”的 {DamageTrees.Count} 棵毁伤树，已忽略 {skippedTreeCount} 棵非轻中重或重复毁伤树";
        }
        catch (Exception ex)
        {
            if (loadCts.IsCancellationRequested)
            {
                return;
            }

            StatusText = $"读取毁伤树失败：{ex.Message}";
            MySqlLog.LogWarning($"读取毁伤树失败：{ex}");
            Debug.WriteLine($"[TargetInfoViewModel] 读取毁伤树失败：{ex}");
        }
        finally
        {
            if (ReferenceEquals(_damageTreeLoadCts, loadCts))
            {
                _damageTreeLoadCts = null;
            }
            loadCts.Dispose();
        }
    }

    /// <summary>
    /// 创建新增毁伤树弹窗使用的草稿。
    /// </summary>
    public DamageTreeInfoItem CreateDamageTreeDraft()
    {
        string treeCode = Guid.NewGuid().ToString("N");
        string rootNodeCode = Guid.NewGuid().ToString("N");
        string targetName = SelectedTarget?.Name ?? "目标";
        string damageLevel = DamageTreeDefaults.FindFirstAvailableDamageLevel(DamageTrees);

        DamageTreeInfoItem tree = new()
        {
            DamageTreeCode = treeCode,
            DamageTreeName = DamageTreeDefaults.CreateDefaultTreeName(targetName, damageLevel),
            DamageTreeDescription = string.Empty,
            TargetCode = SelectedTarget?.Code ?? string.Empty,
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

    /// <summary>
    /// 创建用于编辑的毁伤树浅拷贝。
    /// </summary>
    private static DamageTreeInfoItem CloneDamageTreeForEdit(DamageTreeInfoItem source)
    {
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

    /// <summary>
    /// 创建新增中间节点草稿。
    /// </summary>
    public DamageTreeNodeItem CreateDamageMiddleNodeDraft(DamageTreeNodeItem parentNode)
    {
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

    /// <summary>
    /// 创建新增叶子节点草稿。
    /// </summary>
    public DamageTreeNodeItem CreateDamageLeafNodeDraft(DamageTreeNodeItem parentNode)
    {
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

    /// <summary>
    /// 创建毁伤节点编辑草稿。
    /// </summary>
    private static DamageTreeNodeItem CloneDamageNodeForEdit(DamageTreeNodeItem source)
    {
        return source.CloneShallow();
    }

    /// <summary>
    /// 新增毁伤树，并同步数据库和界面集合。
    /// </summary>
    public async Task<bool> AddDamageTreeAsync(DamageTreeInfoItem newTree)
    {
        if (!NormalizeAndValidateDamageTree(newTree, except: null))
        {
            return false;
        }

        try
        {
            await RunRepositoryOperationAsync(() => _damageTreeRepository.AddDamageTree(newTree));
            DamageTrees.Add(newTree);
            SelectedDamageTree = newTree;
            NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: false);
            StatusText = $"已添加毁伤树：{newTree.DisplayName}";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"添加毁伤树失败：{ex.Message}";
            Debug.WriteLine($"[TargetInfoViewModel] 添加毁伤树失败：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 修改毁伤树基础信息和根节点名称。
    /// </summary>
    public async Task<bool> UpdateDamageTreeAsync(DamageTreeInfoItem originalTree, DamageTreeInfoItem editedTree)
    {
        if (!DamageTrees.Contains(originalTree))
        {
            StatusText = "当前毁伤树不存在，无法修改";
            return false;
        }

        editedTree.DamageTreeCode = originalTree.DamageTreeCode;
        editedTree.TargetCode = originalTree.TargetCode;
        if (!NormalizeAndValidateDamageTree(editedTree, except: originalTree))
        {
            return false;
        }

        try
        {
            await RunRepositoryOperationAsync(() => _damageTreeRepository.UpdateDamageTree(editedTree));

            originalTree.DamageTreeName = editedTree.DamageTreeName;
            originalTree.DamageTreeDescription = editedTree.DamageTreeDescription;
            originalTree.DamageLevelInfo = editedTree.DamageLevelInfo;
            originalTree.DamageTreeType = editedTree.DamageTreeType;

            // 根节点名称属于节点表，但毁伤树信息弹窗会一并编辑根节点名称，便于用户一次完成树的初始化。
            if (originalTree.RootNodes.Count > 0 && editedTree.RootNodes.Count > 0)
            {
                originalTree.RootNodes[0].NodeName = editedTree.RootNodes[0].NodeName;
                originalTree.RootNodes[0].NodeDescription = editedTree.RootNodes[0].NodeDescription;
            }

            SelectedDamageTree = originalTree;
            RefreshDamageNodeDetailRows(SelectedDamageTreeNode);
            NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: false);
            StatusText = $"已修改毁伤树：{originalTree.DisplayName}";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"修改毁伤树失败：{ex.Message}";
            Debug.WriteLine($"[TargetInfoViewModel] 修改毁伤树失败：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 删除毁伤树及其所有节点。
    /// </summary>
    public async Task<bool> DeleteDamageTreeAsync(DamageTreeInfoItem tree)
    {
        if (!DamageTrees.Contains(tree))
        {
            StatusText = "当前毁伤树不存在，无法删除";
            return false;
        }

        try
        {
            await RunRepositoryOperationAsync(() => _damageTreeRepository.DeleteDamageTree(tree.DamageTreeCode));
            DamageTrees.Remove(tree);
            SelectedDamageTree = DamageTrees.FirstOrDefault();
            NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: false);
            StatusText = $"已删除毁伤树：{tree.DisplayName}";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"删除毁伤树失败：{ex.Message}";
            Debug.WriteLine($"[TargetInfoViewModel] 删除毁伤树失败：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 新增中间节点或叶子节点。
    /// </summary>
    public async Task<bool> AddDamageNodeAsync(DamageTreeNodeItem parentNode, DamageTreeNodeItem newNode)
    {
        if (SelectedDamageTree == null)
        {
            StatusText = "请先选择毁伤树";
            return false;
        }

        if (parentNode.IsLeafNode)
        {
            StatusText = "叶子节点不能继续添加子节点";
            return false;
        }

        if (!NormalizeAndValidateDamageNode(newNode, except: null))
        {
            return false;
        }

        try
        {
            newNode.DamageTreeCode = SelectedDamageTree.DamageTreeCode;
            newNode.ParentNodeCode = parentNode.NodeCode;
            newNode.SortOrder = parentNode.Children.Count;

            await RunRepositoryOperationAsync(() => _damageTreeRepository.AddNode(newNode));
            parentNode.Children.Add(newNode);
            parentNode.IsExpanded = true;
            SelectDamageTreeNode(newNode);
            RefreshDamageNodeDetailRows(newNode);
            NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: false);
            StatusText = $"已添加毁伤节点：{newNode.NodeName}";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"添加毁伤节点失败：{ex.Message}";
            Debug.WriteLine($"[TargetInfoViewModel] 添加毁伤节点失败：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 修改毁伤节点。
    /// </summary>
    public async Task<bool> UpdateDamageNodeAsync(DamageTreeNodeItem originalNode, DamageTreeNodeItem editedNode)
    {
        if (SelectedDamageTree == null)
        {
            StatusText = "请先选择毁伤树";
            return false;
        }

        editedNode.NodeCode = originalNode.NodeCode;
        editedNode.DamageTreeCode = originalNode.DamageTreeCode;
        editedNode.ParentNodeCode = originalNode.ParentNodeCode;
        editedNode.SortOrder = originalNode.SortOrder;

        if (!NormalizeAndValidateDamageNode(editedNode, except: originalNode))
        {
            return false;
        }

        try
        {
            await RunRepositoryOperationAsync(() => _damageTreeRepository.UpdateNode(originalNode.NodeCode, editedNode));
            ApplyDamageNodeUpdate(originalNode, editedNode);
            SelectDamageTreeNode(originalNode);
            RefreshDamageNodeDetailRows(originalNode);
            NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: false);
            StatusText = $"已修改毁伤节点：{originalNode.NodeName}";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"修改毁伤节点失败：{ex.Message}";
            Debug.WriteLine($"[TargetInfoViewModel] 修改毁伤节点失败：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 删除毁伤节点及其子节点。
    /// </summary>
    public async Task<bool> DeleteDamageNodeAsync(DamageTreeNodeItem node)
    {
        if (SelectedDamageTree == null)
        {
            StatusText = "请先选择毁伤树";
            return false;
        }

        if (node.Parent == null)
        {
            StatusText = "不能直接删除毁伤树根节点。如需删除，请删除整棵毁伤树。";
            return false;
        }

        try
        {
            DamageTreeNodeItem parent = node.Parent;
            string damageTreeCode = SelectedDamageTree.DamageTreeCode;
            string nodeCode = node.NodeCode;
            await RunRepositoryOperationAsync(() => _damageTreeRepository.DeleteNode(damageTreeCode, nodeCode));
            parent.Children.Remove(node);
            SelectDamageTreeNode(parent);
            NotifyDatabaseChangedForUnity(refreshCurrentDisplayAfterSync: false);
            StatusText = $"已删除毁伤节点：{node.NodeName}";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"删除毁伤节点失败：{ex.Message}";
            Debug.WriteLine($"[TargetInfoViewModel] 删除毁伤节点失败：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 主动选中毁伤树节点。
    /// </summary>
    public void SelectDamageTreeNode(DamageTreeNodeItem? node)
    {
        SynchronizeDamageNodeSelection(node);

        if (!ReferenceEquals(SelectedDamageTreeNode, node))
        {
            SelectedDamageTreeNode = node;
        }
        else
        {
            RefreshDamageNodeDetailRows(node);
        }
    }

    /// <summary>
    /// 同步毁伤树节点 IsSelected，保证同一棵树中只有一个节点处于选中状态。
    /// </summary>
    private void SynchronizeDamageNodeSelection(DamageTreeNodeItem? node)
    {
        if (_isSynchronizingDamageNodeSelection)
        {
            return;
        }

        _isSynchronizingDamageNodeSelection = true;
        try
        {
            if (SelectedDamageTree != null)
            {
                ClearDamageNodeSelection(SelectedDamageTree.RootNodes);
            }

            if (node != null)
            {
                node.IsSelected = true;
            }
        }
        finally
        {
            _isSynchronizingDamageNodeSelection = false;
        }
    }

    /// <summary>
    /// 刷新毁伤节点详情区域。
    /// </summary>
    private void RefreshDamageNodeDetailRows(DamageTreeNodeItem? node)
    {
        SelectedDamageNodeDetailRows.Clear();

        if (SelectedDamageTree != null)
        {
            AddRows(
                SelectedDamageNodeDetailRows,
                new TargetDetailRow { Label = "毁伤树", Value = SelectedDamageTree.DisplayName },
                new TargetDetailRow { Label = "毁伤等级", Value = SelectedDamageTree.DamageLevelInfo },
                new TargetDetailRow { Label = "毁伤树类型", Value = SelectedDamageTree.DamageTreeType });
        }

        if (node == null)
        {
            AddRows(SelectedDamageNodeDetailRows, new TargetDetailRow { Label = "当前节点", Value = "未选择" });
            return;
        }

        AddRows(
            SelectedDamageNodeDetailRows,
            new TargetDetailRow { Label = "节点名称", Value = node.NodeName },
            new TargetDetailRow { Label = "节点类型", Value = node.IsLeafNode ? "叶子节点" : "中间节点" },
            new TargetDetailRow { Label = "逻辑关系", Value = node.RelationDisplayText },
            new TargetDetailRow { Label = "绑定部件", Value = node.PartDisplayText },
            new TargetDetailRow { Label = "节点描述", Value = string.IsNullOrWhiteSpace(node.NodeDescription) ? "-" : node.NodeDescription });
    }

    /// <summary>
    /// 校验并规范化毁伤树草稿。
    /// </summary>
    private bool NormalizeAndValidateDamageTree(DamageTreeInfoItem tree, DamageTreeInfoItem? except)
    {
        if (SelectedTarget == null)
        {
            StatusText = "请先选择目标";
            return false;
        }

        tree.DamageTreeCode = string.IsNullOrWhiteSpace(tree.DamageTreeCode) ? Guid.NewGuid().ToString("N") : tree.DamageTreeCode.Trim();
        tree.TargetCode = SelectedTarget.Code;
        tree.DamageTreeName = tree.DamageTreeName.Trim();
        tree.DamageTreeDescription = tree.DamageTreeDescription.Trim();
        tree.DamageLevelInfo = DamageTreeDefaults.NormalizeDamageLevel(tree.DamageLevelInfo);
        // 毁伤树类型固定为“功能毁伤”，避免历史“整体毁伤树”等旧值参与唯一性判断。
        tree.DamageTreeType = DamageTreeDefaults.DefaultTreeType;

        if (string.IsNullOrWhiteSpace(tree.DamageTreeName))
        {
            StatusText = "请输入毁伤树名称";
            return false;
        }

        if (!DamageTreeDefaults.IsKnownDamageLevel(tree.DamageLevelInfo))
        {
            StatusText = "毁伤树只能选择轻度毁伤、中度毁伤或重度毁伤";
            return false;
        }

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
        root.NodeCode = string.IsNullOrWhiteSpace(root.NodeCode) ? Guid.NewGuid().ToString("N") : root.NodeCode.Trim();
        root.NodeName = string.IsNullOrWhiteSpace(root.NodeName) ? tree.DamageTreeName : root.NodeName.Trim();
        if (root.IsLeafNode)
        {
            root.RelationType = DamageNodeRelationType.And;
        }

        bool codeUsed = DamageTrees.Any(existingTree =>
            !ReferenceEquals(existingTree, except) &&
            string.Equals(existingTree.DamageTreeCode, tree.DamageTreeCode, StringComparison.Ordinal));
        if (codeUsed)
        {
            StatusText = $"毁伤树唯一标识已存在：{tree.DamageTreeCode}";
            return false;
        }

        bool damageLevelUsed = DamageTrees.Any(existingTree =>
            !ReferenceEquals(existingTree, except) &&
            string.Equals(DamageTreeDefaults.NormalizeDamageLevel(existingTree.DamageLevelInfo), tree.DamageLevelInfo, StringComparison.Ordinal));
        if (damageLevelUsed)
        {
            StatusText = $"当前目标已经存在{tree.DamageLevelInfo}树，轻度、中度、重度各只能保留一棵";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 校验并规范化毁伤节点草稿。
    /// </summary>
    private bool NormalizeAndValidateDamageNode(DamageTreeNodeItem node, DamageTreeNodeItem? except)
    {
        node.NodeCode = string.IsNullOrWhiteSpace(node.NodeCode) ? Guid.NewGuid().ToString("N") : node.NodeCode.Trim();
        node.NodeName = node.NodeName.Trim();
        node.NodeDescription = node.NodeDescription.Trim();
        node.PartCode = node.PartCode.Trim();
        node.PartName = node.PartName.Trim();
        node.VoteThreshold = Math.Max(1f, node.VoteThreshold);

        if (string.IsNullOrWhiteSpace(node.NodeName))
        {
            StatusText = "请输入毁伤节点名称";
            return false;
        }

        if (node.IsLeafNode)
        {
            if (string.IsNullOrWhiteSpace(node.PartCode))
            {
                StatusText = "叶子节点必须绑定目标部件";
                return false;
            }

            if (!BuildPartLookup(SelectedTarget).ContainsKey(node.PartCode))
            {
                StatusText = "叶子节点绑定的部件不存在，请重新选择目标结构中的底层部件";
                return false;
            }
        }
        else
        {
            node.PartCode = string.Empty;
            node.PartName = string.Empty;
        }

        bool codeUsed = SelectedDamageTree != null && EnumerateDamageNodes(SelectedDamageTree.RootNodes)
            .Any(existingNode => !ReferenceEquals(existingNode, except) && string.Equals(existingNode.NodeCode, node.NodeCode, StringComparison.Ordinal));
        if (codeUsed)
        {
            StatusText = $"毁伤节点唯一标识已存在：{node.NodeCode}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 将编辑草稿写回 TreeView 正在绑定的毁伤节点对象。
    /// </summary>
    private static void ApplyDamageNodeUpdate(DamageTreeNodeItem target, DamageTreeNodeItem source)
    {
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

    /// <summary>
    /// 构建当前目标的部件索引，供毁伤树叶子节点绑定校验和显示使用。
    /// </summary>
    private static IReadOnlyDictionary<string, TargetPartInfoItem> BuildPartLookup(TargetInfoItem? target)
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

    /// <summary>
    /// 查找毁伤树默认选中的节点。
    /// </summary>
    private static DamageTreeNodeItem? FindPreferredDamageNode(DamageTreeInfoItem? tree)
    {
        if (tree == null)
        {
            return null;
        }

        return FindDamageNode(tree.RootNodes, node => node.IsSelected)
               ?? tree.RootNodes.FirstOrDefault();
    }

    /// <summary>
    /// 在毁伤树中查找满足条件的第一个节点。
    /// </summary>
    private static DamageTreeNodeItem? FindDamageNode(IEnumerable<DamageTreeNodeItem> nodes, Func<DamageTreeNodeItem, bool> predicate)
    {
        foreach (DamageTreeNodeItem node in nodes)
        {
            if (predicate(node))
            {
                return node;
            }

            DamageTreeNodeItem? child = FindDamageNode(node.Children, predicate);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// 清空毁伤树节点选中状态。
    /// </summary>
    private static void ClearDamageNodeSelection(IEnumerable<DamageTreeNodeItem> nodes)
    {
        foreach (DamageTreeNodeItem node in nodes)
        {
            node.IsSelected = false;
            ClearDamageNodeSelection(node.Children);
        }
    }

    /// <summary>
    /// 深度优先遍历毁伤树节点。
    /// </summary>
    private static IEnumerable<DamageTreeNodeItem> EnumerateDamageNodes(IEnumerable<DamageTreeNodeItem> nodes)
    {
        foreach (DamageTreeNodeItem node in nodes)
        {
            yield return node;

            foreach (DamageTreeNodeItem child in EnumerateDamageNodes(node.Children))
            {
                yield return child;
            }
        }
    }
}
