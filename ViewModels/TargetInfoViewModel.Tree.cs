using CombatSimulation.Models;

namespace CombatSimulation.ViewModels;

public sealed partial class TargetInfoViewModel
{
    /// <summary>
    /// 从 View 或 ViewModel 主动选中结构树节点。
    /// </summary>
    public void SelectStructureNode(TargetStructureTreeNode? node)
    {
        SynchronizeStructureNodeSelection(node);

        if (!ReferenceEquals(SelectedStructureNode, node))
        {
            SelectedStructureNode = node;
        }
        else
        {
            // 同一个节点再次被选中时，属性回调不会触发，因此需要主动刷新详情。
            RefreshSelectedDetailRows(node);
        }
    }

    /// <summary>
    /// 同步 TargetStructureTreeNode.IsSelected，确保模型树中只有一个选中节点。
    /// </summary>
    private void SynchronizeStructureNodeSelection(TargetStructureTreeNode? node)
    {
        if (_isSynchronizingStructureNodeSelection)
        {
            return;
        }

        _isSynchronizingStructureNodeSelection = true;
        try
        {
            if (SelectedTarget != null)
            {
                ClearSelection(SelectedTarget.StructureTreeNodes);
            }

            if (node != null)
            {
                node.IsSelected = true;
            }
        }
        finally
        {
            _isSynchronizingStructureNodeSelection = false;
        }
    }

    /// <summary>
    /// 递归清空结构树节点选中状态。
    /// </summary>
    private static void ClearSelection(IEnumerable<TargetStructureTreeNode> nodes)
    {
        foreach (TargetStructureTreeNode node in nodes)
        {
            node.IsSelected = false;
            ClearSelection(node.Children);
        }
    }

    /// <summary>
    /// 查找指定结构节点的父节点。
    /// </summary>
    private TargetStructureTreeNode? FindParentNode(IEnumerable<TargetStructureTreeNode> nodes, TargetStructureTreeNode targetNode)
    {
        foreach (TargetStructureTreeNode node in nodes)
        {
            if (node.Children.Contains(targetNode))
            {
                return node;
            }

            TargetStructureTreeNode? childParent = FindParentNode(node.Children, targetNode);
            if (childParent != null)
            {
                return childParent;
            }
        }

        return null;
    }

    /// <summary>
    /// 创建未被当前目标占用的系统 GUID。
    /// </summary>
    private string CreateUniqueSystemCode()
    {
        string code;
        do
        {
            code = Guid.NewGuid().ToString("N");
        }
        while (IsSystemCodeUsed(code, except: null));

        return code;
    }

    /// <summary>
    /// 创建未被当前目标占用的部件 GUID。
    /// </summary>
    private string CreateUniquePartCode()
    {
        string code;
        do
        {
            code = Guid.NewGuid().ToString("N");
        }
        while (IsPartCodeUsed(code, except: null));

        return code;
    }

    /// <summary>
    /// 创建未被目标列表占用的目标 GUID。
    /// </summary>
    private string CreateUniqueTargetCode()
    {
        string code;
        do
        {
            code = Guid.NewGuid().ToString("N");
        }
        while (Targets.Any(target => string.Equals(target.Code, code, StringComparison.Ordinal)));

        return code;
    }

    /// <summary>
    /// 判断系统编号是否已被当前目标的其他系统占用。
    /// </summary>
    private bool IsSystemCodeUsed(string systemCode, TargetSystemInfoItem? except)
    {
        if (SelectedTarget == null || string.IsNullOrWhiteSpace(systemCode))
        {
            return false;
        }

        return EnumerateSystems(SelectedTarget.Systems)
            .Any(system => !ReferenceEquals(system, except) && string.Equals(system.SystemCode, systemCode, StringComparison.Ordinal));
    }

    /// <summary>
    /// 判断部件编号是否已被当前目标的其他部件占用。
    /// </summary>
    private bool IsPartCodeUsed(string partCode, TargetPartInfoItem? except)
    {
        if (SelectedTarget == null || string.IsNullOrWhiteSpace(partCode))
        {
            return false;
        }

        return EnumerateSystems(SelectedTarget.Systems)
            .SelectMany(system => system.Parts)
            .Any(part => !ReferenceEquals(part, except) && string.Equals(part.PartCode, partCode, StringComparison.Ordinal));
    }

    /// <summary>
    /// 深度优先遍历系统模型树。
    /// </summary>
    private static IEnumerable<TargetSystemInfoItem> EnumerateSystems(IEnumerable<TargetSystemInfoItem> systems)
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

    /// <summary>
    /// 为目标选择一个默认结构节点。
    /// </summary>
    private static TargetStructureTreeNode? FindPreferredStructureNode(TargetInfoItem? target)
    {
        if (target == null)
        {
            return null;
        }

        TargetStructureTreeNode? selectedNode = FindNode(target.StructureTreeNodes, node => node.IsSelected);
        if (selectedNode != null)
        {
            return selectedNode;
        }

        // 优先选择第一个部件节点，方便切换目标后右侧直接显示可编辑部件信息。
        TargetStructureTreeNode? firstPartNode = FindNode(target.StructureTreeNodes, node => node.Part != null);
        if (firstPartNode != null)
        {
            firstPartNode.IsSelected = true;
            return firstPartNode;
        }

        return target.StructureTreeNodes.FirstOrDefault();
    }

    /// <summary>
    /// 在结构树中查找第一个满足条件的节点。
    /// </summary>
    private static TargetStructureTreeNode? FindNode(IEnumerable<TargetStructureTreeNode> nodes, Func<TargetStructureTreeNode, bool> predicate)
    {
        foreach (TargetStructureTreeNode node in nodes)
        {
            if (predicate(node))
            {
                return node;
            }

            TargetStructureTreeNode? childResult = FindNode(node.Children, predicate);
            if (childResult != null)
            {
                return childResult;
            }
        }

        return null;
    }

    /// <summary>
    /// 统计目标下的系统总数。
    /// </summary>
    private static int CountSystems(TargetInfoItem target)
    {
        return target.Systems.Sum(system => 1 + CountChildSystems(system));
    }

    /// <summary>
    /// 统计系统下的子系统总数。
    /// </summary>
    private static int CountChildSystems(TargetSystemInfoItem system)
    {
        return system.ChildSystems.Sum(child => 1 + CountChildSystems(child));
    }

    /// <summary>
    /// 统计目标下的部件总数。
    /// </summary>
    private static int CountParts(TargetInfoItem target)
    {
        return target.Systems.Sum(CountParts);
    }

    /// <summary>
    /// 统计系统及其子系统下的部件总数。
    /// </summary>
    private static int CountParts(TargetSystemInfoItem system)
    {
        return system.Parts.Count + system.ChildSystems.Sum(CountParts);
    }

    /// <summary>
    /// 创建结构树中的系统节点，并递归添加子系统和部件节点。
    /// </summary>
    private static TargetStructureTreeNode CreateSystemNode(TargetSystemInfoItem system)
    {
        TargetStructureTreeNode node = TargetStructureTreeNode.ForSystem(system);

        foreach (TargetSystemInfoItem childSystem in system.ChildSystems)
        {
            node.Children.Add(CreateSystemNode(childSystem));
        }

        foreach (TargetPartInfoItem part in system.Parts)
        {
            node.Children.Add(TargetStructureTreeNode.ForPart(part));
        }

        return node;
    }
}
