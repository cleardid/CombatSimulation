using CombatSimulation.Models;
using CommunityToolkit.Mvvm.Input;

namespace CombatSimulation.ViewModels;

public sealed partial class TargetInfoViewModel
{
    partial void OnSelectedTargetChanged(TargetInfoItem? value)
    {
        DetachCheckStateHandlers(_checkStateSubscriptionTarget);
        _checkStateSubscriptionTarget = value;
        AttachCheckStateHandlers(value);

        StatusText = value == null ? "未选择目标" : $"当前目标：{value.Name}";
        ClearDamageTreeState();
        if (IsDamageTreeInfoSelected)
        {
            LoadDamageTreesForSelectedTarget();
        }

        _unitySyncCoordinator.ShowTarget(value?.Code);
        SelectedStructureNode = FindPreferredStructureNode(value);
    }

    partial void OnSelectedStructureNodeChanged(TargetStructureTreeNode? value)
    {
        SynchronizeStructureNodeSelection(value);
        RefreshSelectedDetailRows(value);
        _unitySyncCoordinator.HighlightPart(value?.Part?.PartCode);

    }

    partial void OnSelectedInfoPanelChanged(string value)
    {
        StatusText = IsStructureInfoSelected ? "当前位于目标结构信息模块" : "当前位于毁伤树信息模块";
        if (IsDamageTreeInfoSelected)
        {
            LoadDamageTreesForSelectedTarget();
        }
    }

    [RelayCommand]
    private void ShowStructureInfo() => SelectedInfoPanel = StructureInfoPanel;

    [RelayCommand]
    private void ShowDamageTreeInfo() => SelectedInfoPanel = DamageTreeInfoPanel;

    [RelayCommand]
    private async Task RequestAddTargetAsync()
    {
        TargetInfoItem? edited = _interactionService.EditTarget(_targetStructureEditor.CreateTargetDraft(Targets), isEditMode: false);
        if (edited != null)
        {
            ShowOperationFailure(await AddTargetAsync(edited));
        }
    }

    [RelayCommand]
    private async Task RequestModifyTargetAsync()
    {
        TargetInfoItem? target = SelectedTarget;
        if (target == null)
        {
            RequestOperationMessage("请选择需要修改的目标。");
            return;
        }

        TargetInfoItem? edited = _interactionService.EditTarget(target, isEditMode: true);
        if (edited != null)
        {
            ShowOperationFailure(await UpdateTargetAsync(target, edited));
        }
    }

    [RelayCommand]
    private async Task RequestDeleteTargetAsync()
    {
        TargetInfoItem? target = SelectedTarget;
        if (target == null)
        {
            RequestOperationMessage("请选择需要删除的目标。");
            return;
        }

        if (_interactionService.ConfirmDeletion($"确定删除目标“{target.Name}”及其全部系统、部件和毁伤树信息？"))
        {
            ShowOperationFailure(await DeleteTargetAsync(target));
        }
    }

    [RelayCommand]
    private async Task RequestModifyStructureNodeAsync(TargetStructureTreeNode? node)
    {
        if (node == null || node.Target != null)
        {
            return;
        }

        SelectStructureNode(node);
        if (node.System != null)
        {
            TargetSystemInfoItem? edited = _interactionService.EditSystem(node.System, isEditMode: true, Targets);
            if (edited != null)
            {
                ShowOperationFailure(await UpdateSystemAsync(node, edited));
            }
            return;
        }

        if (node.Part != null)
        {
            TargetPartInfoItem? edited = _interactionService.EditPart(node.Part, isEditMode: true);
            if (edited != null)
            {
                ShowOperationFailure(await UpdatePartAsync(node, edited));
            }
        }
    }

    [RelayCommand]
    private async Task RequestDeleteStructureNodeAsync(TargetStructureTreeNode? node)
    {
        if (node == null || node.Target != null)
        {
            return;
        }

        SelectStructureNode(node);
        string message = node.System != null
            ? $"确定删除系统“{node.Name}”及其全部子系统和底层部件？"
            : $"确定删除部件“{node.Name}”？";
        if (_interactionService.ConfirmDeletion(message))
        {
            ShowOperationFailure(await DeleteStructureNodeAsync(node));
        }
    }

    [RelayCommand]
    private async Task RequestAddChildSystemAsync(TargetStructureTreeNode? parentNode)
    {
        if (parentNode?.System == null && parentNode?.Target == null)
        {
            RequestOperationMessage("请选择目标根节点或系统节点后再添加子系统。");
            return;
        }

        SelectStructureNode(parentNode);
        TargetSystemInfoItem? edited = _interactionService.EditSystem(_targetStructureEditor.CreateSystemDraft(parentNode, SelectedTarget), isEditMode: false, Targets);
        if (edited != null)
        {
            ShowOperationFailure(await AddChildSystemAsync(parentNode, edited));
        }
    }

    [RelayCommand]
    private async Task RequestAddChildPartAsync(TargetStructureTreeNode? parentNode)
    {
        if (parentNode?.System == null)
        {
            RequestOperationMessage("请选择系统节点后再添加底层部件。");
            return;
        }

        SelectStructureNode(parentNode);
        TargetPartInfoItem? edited = _interactionService.EditPart(_targetStructureEditor.CreatePartDraft(parentNode, SelectedTarget), isEditMode: false);
        if (edited != null)
        {
            ShowOperationFailure(await AddChildPartAsync(parentNode, edited));
        }
    }

    [RelayCommand]
    private async Task RequestAddDamageTreeAsync()
    {
        if (SelectedTarget == null)
        {
            RequestOperationMessage("请先选择目标，再添加毁伤树。");
            return;
        }

        if (_damageTreeEditor.HasAllDamageLevels(DamageTrees))
        {
            RequestOperationMessage("当前目标已存在轻度、中度、重度三棵毁伤树，不能继续添加。");
            return;
        }

        DamageTreeInfoItem? edited = _interactionService.EditDamageTree(_damageTreeEditor.CreateTreeDraft(SelectedTarget, DamageTrees), SelectedTarget.Name, isEditMode: false);
        if (edited != null)
        {
            ShowOperationFailure(await AddDamageTreeAsync(edited));
        }
    }

    [RelayCommand]
    private async Task RequestModifyDamageTreeAsync()
    {
        DamageTreeInfoItem? tree = SelectedDamageTree;
        if (tree == null)
        {
            RequestOperationMessage("请选择需要修改的毁伤树。");
            return;
        }

        DamageTreeInfoItem? edited = _interactionService.EditDamageTree(
            _damageTreeEditor.CloneTreeForEdit(tree),
            SelectedTarget?.Name ?? string.Empty,
            isEditMode: true);
        if (edited != null)
        {
            ShowOperationFailure(await UpdateDamageTreeAsync(tree, edited));
        }
    }

    [RelayCommand]
    private async Task RequestDeleteDamageTreeAsync()
    {
        DamageTreeInfoItem? tree = SelectedDamageTree;
        if (tree == null)
        {
            RequestOperationMessage("请选择需要删除的毁伤树。");
            return;
        }

        if (_interactionService.ConfirmDeletion($"确定删除毁伤树“{tree.DisplayName}”及其全部毁伤节点？"))
        {
            ShowOperationFailure(await DeleteDamageTreeAsync(tree));
        }
    }

    [RelayCommand]
    private async Task RequestModifyDamageTreeNodeAsync(DamageTreeNodeItem? node)
    {
        if (node == null)
        {
            return;
        }

        SelectDamageTreeNode(node);
        DamageTreeNodeItem? edited = node.IsLeafNode
            ? _interactionService.EditLeafDamageNode(
                _damageTreeEditor.CloneNodeForEdit(node),
                SelectedTarget?.StructureTreeNodes ?? Enumerable.Empty<TargetStructureTreeNode>(),
                isEditMode: true)
            : _interactionService.EditMiddleDamageNode(_damageTreeEditor.CloneNodeForEdit(node), isEditMode: true);
        if (edited != null)
        {
            ShowOperationFailure(await UpdateDamageNodeAsync(node, edited));
        }
    }

    [RelayCommand]
    private async Task RequestDeleteDamageTreeNodeAsync(DamageTreeNodeItem? node)
    {
        if (node == null)
        {
            return;
        }

        SelectDamageTreeNode(node);
        string message = node.IsLeafNode
            ? $"确定删除叶子节点“{node.NodeName}”？"
            : $"确定删除中间节点“{node.NodeName}”及其全部子节点？";
        if (_interactionService.ConfirmDeletion(message))
        {
            ShowOperationFailure(await DeleteDamageNodeAsync(node));
        }
    }

    [RelayCommand]
    private async Task RequestAddDamageMiddleNodeAsync(DamageTreeNodeItem? parentNode)
    {
        if (parentNode == null || parentNode.IsLeafNode)
        {
            RequestOperationMessage("请选择毁伤树中间节点后再添加子节点。");
            return;
        }

        SelectDamageTreeNode(parentNode);
        DamageTreeNodeItem? edited = _interactionService.EditMiddleDamageNode(_damageTreeEditor.CreateMiddleNodeDraft(parentNode), isEditMode: false);
        if (edited != null)
        {
            ShowOperationFailure(await AddDamageNodeAsync(parentNode, edited));
        }
    }

    [RelayCommand]
    private async Task RequestAddDamageLeafNodeAsync(DamageTreeNodeItem? parentNode)
    {
        if (parentNode == null || parentNode.IsLeafNode)
        {
            RequestOperationMessage("请选择毁伤树中间节点后再添加叶子节点。");
            return;
        }

        SelectDamageTreeNode(parentNode);
        DamageTreeNodeItem? edited = _interactionService.EditLeafDamageNode(
            _damageTreeEditor.CreateLeafNodeDraft(parentNode),
            SelectedTarget?.StructureTreeNodes ?? Enumerable.Empty<TargetStructureTreeNode>(),
            isEditMode: false);
        if (edited != null)
        {
            ShowOperationFailure(await AddDamageNodeAsync(parentNode, edited));
        }
    }

    private void ShowOperationFailure(bool succeeded)
    {
        if (!succeeded)
        {
            RequestOperationMessage(StatusText);
        }
    }

    private void RequestOperationMessage(string message)
    {
        _interactionService.ShowMessage(message);
    }
}