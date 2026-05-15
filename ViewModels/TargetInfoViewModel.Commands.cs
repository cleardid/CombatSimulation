using CombatSimulation.Models;
using CommunityToolkit.Mvvm.Input;

namespace CombatSimulation.ViewModels;

public sealed partial class TargetInfoViewModel
{
    /// <summary>
    /// 目标选中项变化后的处理入口。
    /// </summary>
    partial void OnSelectedTargetChanged(TargetInfoItem? value)
    {
        // 切换目标时，先解除旧目标树节点事件，再订阅新目标树节点事件，避免旧目标勾选变化继续触发 Unity 同步。
        DetachCheckStateHandlers(_checkStateSubscriptionTarget);
        _checkStateSubscriptionTarget = value;
        AttachCheckStateHandlers(value);

        StatusText = value == null
            ? "未选择目标"
            : $"当前目标：{value.Name}";

        // 切换目标时清空毁伤树状态，避免新目标加载前仍显示旧目标的毁伤节点。
        ClearDamageTreeState();
        if (IsDamageTreeInfoSelected)
        {
            LoadDamageTreesForSelectedTarget();
        }

        // 快速切换目标时取消上一次 Unity 显示命令，只保留最新目标。
        _targetSelectionCts?.Cancel();
        _targetSelectionCts = new CancellationTokenSource();
        RunUnityCommandInBackground(token => SendSelectedTargetToUnityAsync(value, token), _targetSelectionCts.Token);

        // 切换目标只发送“显示目标”命令。
        // 勾选状态同步仅在用户实际勾选/取消结构树节点时触发，避免每次切换目标都发送大量部件标识。
        SelectedStructureNode = FindPreferredStructureNode(value);
    }

    /// <summary>
    /// 结构树选中节点变化后的处理入口。
    /// </summary>
    partial void OnSelectedStructureNodeChanged(TargetStructureTreeNode? value)
    {
        SynchronizeStructureNodeSelection(value);
        RefreshSelectedDetailRows(value);

        // 部件高亮命令只对部件节点有效。系统和目标根节点不发送高亮命令。
        _partHighlightCts?.Cancel();
        if (value?.Part != null)
        {
            _partHighlightCts = new CancellationTokenSource();
            RunUnityCommandInBackground(token => SendHighlightedPartToUnityAsync(value.Part, token), _partHighlightCts.Token);
        }
    }

    /// <summary>
    /// 目标结构/毁伤树模块切换后的状态文本更新。
    /// </summary>
    partial void OnSelectedInfoPanelChanged(string value)
    {
        StatusText = IsStructureInfoSelected
            ? "当前位于目标结构信息模块"
            : "当前位于毁伤树信息模块";

        if (IsDamageTreeInfoSelected)
        {
            LoadDamageTreesForSelectedTarget();
        }
    }

    /// <summary>
    /// 切换到目标结构信息模块。
    /// </summary>
    [RelayCommand]
    private void ShowStructureInfo()
    {
        SelectedInfoPanel = StructureInfoPanel;
    }

    /// <summary>
    /// 切换到目标毁伤树信息模块。
    /// </summary>
    [RelayCommand]
    private void ShowDamageTreeInfo()
    {
        SelectedInfoPanel = DamageTreeInfoPanel;
    }

    /// <summary>
    /// 请求 View 打开新增目标弹窗。
    /// </summary>
    [RelayCommand]
    private void RequestAddTarget()
    {
        TargetEditRequested?.Invoke(this, new TargetEditRequestedEventArgs(CreateTargetDraft(), originalTarget: null, isEditMode: false));
    }

    /// <summary>
    /// 请求 View 打开修改目标弹窗。
    /// </summary>
    [RelayCommand]
    private void RequestModifyTarget()
    {
        if (SelectedTarget == null)
        {
            RequestOperationMessage("请选择需要修改的目标。");
            return;
        }

        TargetEditRequested?.Invoke(this, new TargetEditRequestedEventArgs(SelectedTarget, SelectedTarget, isEditMode: true));
    }

    /// <summary>
    /// 请求 View 确认删除当前目标。
    /// </summary>
    [RelayCommand]
    private void RequestDeleteTarget()
    {
        if (SelectedTarget == null)
        {
            RequestOperationMessage("请选择需要删除的目标。");
            return;
        }

        TargetDeleteRequested?.Invoke(this, new TargetDeleteRequestedEventArgs(SelectedTarget));
    }

    /// <summary>
    /// 请求 View 打开系统或部件修改弹窗。
    /// </summary>
    [RelayCommand]
    private void RequestModifyStructureNode(TargetStructureTreeNode? node)
    {
        if (node == null || node.Target != null)
        {
            return;
        }

        SelectStructureNode(node);
        StructureNodeEditRequested?.Invoke(this, new StructureNodeEditRequestedEventArgs(node));
    }

    /// <summary>
    /// 请求 View 确认删除系统或部件节点。
    /// </summary>
    [RelayCommand]
    private void RequestDeleteStructureNode(TargetStructureTreeNode? node)
    {
        if (node == null || node.Target != null)
        {
            return;
        }

        SelectStructureNode(node);
        StructureNodeDeleteRequested?.Invoke(this, new StructureNodeDeleteRequestedEventArgs(node));
    }

    /// <summary>
    /// 请求 View 打开新增子系统弹窗。
    /// </summary>
    [RelayCommand]
    private void RequestAddChildSystem(TargetStructureTreeNode? parentNode)
    {
        if (parentNode?.System == null && parentNode?.Target == null)
        {
            RequestOperationMessage("请选择目标根节点或系统节点后再添加子系统。");
            return;
        }

        SelectStructureNode(parentNode);
        StructureChildSystemAddRequested?.Invoke(this, new StructureChildSystemAddRequestedEventArgs(parentNode, CreateChildSystemDraft(parentNode)));
    }

    /// <summary>
    /// 请求 View 打开新增底层部件弹窗。
    /// </summary>
    [RelayCommand]
    private void RequestAddChildPart(TargetStructureTreeNode? parentNode)
    {
        if (parentNode?.System == null)
        {
            RequestOperationMessage("请选择系统节点后再添加底层部件。");
            return;
        }

        SelectStructureNode(parentNode);
        StructureChildPartAddRequested?.Invoke(this, new StructureChildPartAddRequestedEventArgs(parentNode, CreateChildPartDraft(parentNode)));
    }

    /// <summary>
    /// 请求 View 打开新增毁伤树弹窗。
    /// </summary>
    [RelayCommand]
    private void RequestAddDamageTree()
    {
        if (SelectedTarget == null)
        {
            RequestOperationMessage("请先选择目标，再添加毁伤树。");
            return;
        }

        DamageTreeEditRequested?.Invoke(this, new DamageTreeEditRequestedEventArgs(CreateDamageTreeDraft(), originalTree: null, isEditMode: false));
    }

    /// <summary>
    /// 请求 View 打开修改毁伤树弹窗。
    /// </summary>
    [RelayCommand]
    private void RequestModifyDamageTree()
    {
        if (SelectedDamageTree == null)
        {
            RequestOperationMessage("请选择需要修改的毁伤树。");
            return;
        }

        DamageTreeEditRequested?.Invoke(this, new DamageTreeEditRequestedEventArgs(CloneDamageTreeForEdit(SelectedDamageTree), SelectedDamageTree, isEditMode: true));
    }

    /// <summary>
    /// 请求 View 确认删除当前毁伤树。
    /// </summary>
    [RelayCommand]
    private void RequestDeleteDamageTree()
    {
        if (SelectedDamageTree == null)
        {
            RequestOperationMessage("请选择需要删除的毁伤树。");
            return;
        }

        DamageTreeDeleteRequested?.Invoke(this, new DamageTreeDeleteRequestedEventArgs(SelectedDamageTree));
    }

    /// <summary>
    /// 请求 View 打开毁伤树节点修改弹窗。
    /// </summary>
    [RelayCommand]
    private void RequestModifyDamageTreeNode(DamageTreeNodeItem? node)
    {
        if (node == null)
        {
            return;
        }

        SelectDamageTreeNode(node);
        if (node.IsLeafNode)
        {
            DamageTreeLeafNodeEditRequested?.Invoke(this, new DamageTreeLeafNodeEditRequestedEventArgs(CloneDamageNodeForEdit(node), node, isEditMode: true));
        }
        else
        {
            DamageTreeMiddleNodeEditRequested?.Invoke(this, new DamageTreeMiddleNodeEditRequestedEventArgs(CloneDamageNodeForEdit(node), node, isEditMode: true));
        }
    }

    /// <summary>
    /// 请求 View 确认删除毁伤树节点。
    /// </summary>
    [RelayCommand]
    private void RequestDeleteDamageTreeNode(DamageTreeNodeItem? node)
    {
        if (node == null)
        {
            return;
        }

        SelectDamageTreeNode(node);
        DamageTreeNodeDeleteRequested?.Invoke(this, new DamageTreeNodeDeleteRequestedEventArgs(node));
    }

    /// <summary>
    /// 请求 View 打开新增中间毁伤节点弹窗。
    /// </summary>
    [RelayCommand]
    private void RequestAddDamageMiddleNode(DamageTreeNodeItem? parentNode)
    {
        if (parentNode == null || parentNode.IsLeafNode)
        {
            RequestOperationMessage("请选择毁伤树中间节点后再添加子节点。");
            return;
        }

        SelectDamageTreeNode(parentNode);
        DamageTreeMiddleNodeEditRequested?.Invoke(this, new DamageTreeMiddleNodeEditRequestedEventArgs(CreateDamageMiddleNodeDraft(parentNode), originalNode: null, isEditMode: false, parentNode));
    }

    /// <summary>
    /// 请求 View 打开新增叶子毁伤节点弹窗。
    /// </summary>
    [RelayCommand]
    private void RequestAddDamageLeafNode(DamageTreeNodeItem? parentNode)
    {
        if (parentNode == null || parentNode.IsLeafNode)
        {
            RequestOperationMessage("请选择毁伤树中间节点后再添加叶子节点。");
            return;
        }

        SelectDamageTreeNode(parentNode);
        DamageTreeLeafNodeEditRequested?.Invoke(this, new DamageTreeLeafNodeEditRequestedEventArgs(CreateDamageLeafNodeDraft(parentNode), originalNode: null, isEditMode: false, parentNode));
    }

    /// <summary>
    /// 将 ViewModel 中的提示消息转交给 View，由 View 决定以 MessageBox 或其他方式展示。
    /// </summary>
    private void RequestOperationMessage(string message)
    {
        OperationMessageRequested?.Invoke(this, new OperationMessageRequestedEventArgs(message));
    }
}
