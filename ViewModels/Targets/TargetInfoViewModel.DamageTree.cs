using CombatSimulation.Models;
using CombatSimulation.Services.DamageTrees;
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
        SelectedDamageTreeNode = _damageTreeEditor.FindPreferredNode(value);
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
            IReadOnlyDictionary<string, TargetPartInfoItem> partLookup = _damageTreeEditor.BuildPartLookup(target);
            IReadOnlyList<DamageTreeInfoItem> storedTrees = await RunRepositoryOperationAsync(
                cancellationToken => _damageTreeRepository.LoadDamageTreesAsync(target.Code, partLookup, cancellationToken),
                loadCts.Token);
            if (loadCts.IsCancellationRequested || !ReferenceEquals(SelectedTarget, target))
            {
                return;
            }

            DamageTreeLoadPreparation preparation = _damageTreeEditor.PrepareLoadedTrees(storedTrees);
            foreach (DamageTreeInfoItem tree in preparation.Trees)
            {
                DamageTrees.Add(tree);
            }

            int skippedTreeCount = preparation.SkippedTreeCount;            SelectedDamageTree = DamageTrees.FirstOrDefault();
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
            await RunRepositoryOperationAsync(() => _damageTreeRepository.AddDamageTreeAsync(newTree));
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
            await RunRepositoryOperationAsync(() => _damageTreeRepository.UpdateDamageTreeAsync(editedTree));

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
            await RunRepositoryOperationAsync(() => _damageTreeRepository.DeleteDamageTreeAsync(tree.DamageTreeCode));
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

            await RunRepositoryOperationAsync(() => _damageTreeRepository.AddNodeAsync(newNode));
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
            await RunRepositoryOperationAsync(() => _damageTreeRepository.UpdateNodeAsync(originalNode.NodeCode, editedNode));
            _damageTreeEditor.ApplyNodeUpdate(originalNode, editedNode);
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
            await RunRepositoryOperationAsync(() => _damageTreeRepository.DeleteNodeAsync(damageTreeCode, nodeCode));
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
    /// 调用领域服务规范化并校验毁伤树，将失败原因投影到界面状态。
    /// </summary>
    private bool NormalizeAndValidateDamageTree(DamageTreeInfoItem tree, DamageTreeInfoItem? except)
    {
        DamageTreeValidationResult result = _damageTreeEditor.NormalizeAndValidateTree(
            tree,
            SelectedTarget,
            DamageTrees,
            except);
        if (!result.IsValid)
        {
            StatusText = result.ErrorMessage;
        }

        return result.IsValid;
    }

    /// <summary>
    /// 调用领域服务规范化并校验毁伤节点，将失败原因投影到界面状态。
    /// </summary>
    private bool NormalizeAndValidateDamageNode(DamageTreeNodeItem node, DamageTreeNodeItem? except)
    {
        DamageTreeValidationResult result = _damageTreeEditor.NormalizeAndValidateNode(
            node,
            SelectedDamageTree,
            SelectedTarget,
            except);
        if (!result.IsValid)
        {
            StatusText = result.ErrorMessage;
        }

        return result.IsValid;
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
}
