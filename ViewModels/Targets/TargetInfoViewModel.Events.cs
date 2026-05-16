using CombatSimulation.Models;

namespace CombatSimulation.ViewModels;

/// <summary>
/// 请求 View 显示普通提示消息的事件参数。
/// </summary>
public sealed class OperationMessageRequestedEventArgs : EventArgs
{
    public OperationMessageRequestedEventArgs(string message)
    {
        Message = message;
    }

    /// <summary>
    /// 需要显示给用户的提示文本。
    /// </summary>
    public string Message { get; }
}

/// <summary>
/// 请求 View 打开目标新增或修改弹窗的事件参数。
/// </summary>
public sealed class TargetEditRequestedEventArgs : EventArgs
{
    public TargetEditRequestedEventArgs(TargetInfoItem draft, TargetInfoItem? originalTarget, bool isEditMode)
    {
        Draft = draft;
        OriginalTarget = originalTarget;
        IsEditMode = isEditMode;
    }

    /// <summary>
    /// 弹窗绑定的目标草稿。新增时是新对象，修改时通常复制自原目标。
    /// </summary>
    public TargetInfoItem Draft { get; }

    /// <summary>
    /// 被修改的原始目标。新增模式下为 null。
    /// </summary>
    public TargetInfoItem? OriginalTarget { get; }

    /// <summary>
    /// 是否是修改模式。false 表示新增模式。
    /// </summary>
    public bool IsEditMode { get; }
}

/// <summary>
/// 请求 View 确认删除目标的事件参数。
/// </summary>
public sealed class TargetDeleteRequestedEventArgs : EventArgs
{
    public TargetDeleteRequestedEventArgs(TargetInfoItem target)
    {
        Target = target;
    }

    /// <summary>
    /// 待删除的目标。
    /// </summary>
    public TargetInfoItem Target { get; }
}

/// <summary>
/// 请求 View 打开系统或部件修改弹窗的事件参数。
/// </summary>
public sealed class StructureNodeEditRequestedEventArgs : EventArgs
{
    public StructureNodeEditRequestedEventArgs(TargetStructureTreeNode node)
    {
        Node = node;
    }

    /// <summary>
    /// 待修改的结构树节点。
    /// </summary>
    public TargetStructureTreeNode Node { get; }
}

/// <summary>
/// 请求 View 确认删除系统或部件节点的事件参数。
/// </summary>
public sealed class StructureNodeDeleteRequestedEventArgs : EventArgs
{
    public StructureNodeDeleteRequestedEventArgs(TargetStructureTreeNode node)
    {
        Node = node;
    }

    /// <summary>
    /// 待删除的结构树节点。
    /// </summary>
    public TargetStructureTreeNode Node { get; }
}

/// <summary>
/// 请求 View 打开新增子系统弹窗的事件参数。
/// </summary>
public sealed class StructureChildSystemAddRequestedEventArgs : EventArgs
{
    public StructureChildSystemAddRequestedEventArgs(TargetStructureTreeNode parentNode, TargetSystemInfoItem draft)
    {
        ParentNode = parentNode;
        Draft = draft;
    }

    /// <summary>
    /// 新系统的父节点，可以是目标根节点或系统节点。
    /// </summary>
    public TargetStructureTreeNode ParentNode { get; }

    /// <summary>
    /// 新系统编辑草稿。
    /// </summary>
    public TargetSystemInfoItem Draft { get; }
}

/// <summary>
/// 请求 View 打开新增底层部件弹窗的事件参数。
/// </summary>
public sealed class StructureChildPartAddRequestedEventArgs : EventArgs
{
    public StructureChildPartAddRequestedEventArgs(TargetStructureTreeNode parentNode, TargetPartInfoItem draft)
    {
        ParentNode = parentNode;
        Draft = draft;
    }

    /// <summary>
    /// 新部件所属的系统节点。
    /// </summary>
    public TargetStructureTreeNode ParentNode { get; }

    /// <summary>
    /// 新部件编辑草稿。
    /// </summary>
    public TargetPartInfoItem Draft { get; }
}

/// <summary>
/// 请求 View 打开毁伤树新增或修改弹窗的事件参数。
/// </summary>
public sealed class DamageTreeEditRequestedEventArgs : EventArgs
{
    public DamageTreeEditRequestedEventArgs(DamageTreeInfoItem draft, DamageTreeInfoItem? originalTree, bool isEditMode)
    {
        Draft = draft;
        OriginalTree = originalTree;
        IsEditMode = isEditMode;
    }

    /// <summary>
    /// 弹窗绑定的毁伤树草稿。
    /// </summary>
    public DamageTreeInfoItem Draft { get; }

    /// <summary>
    /// 被修改的原始毁伤树。新增模式下为 null。
    /// </summary>
    public DamageTreeInfoItem? OriginalTree { get; }

    /// <summary>
    /// 是否为修改模式。
    /// </summary>
    public bool IsEditMode { get; }
}

/// <summary>
/// 请求 View 确认删除毁伤树的事件参数。
/// </summary>
public sealed class DamageTreeDeleteRequestedEventArgs : EventArgs
{
    public DamageTreeDeleteRequestedEventArgs(DamageTreeInfoItem damageTree)
    {
        DamageTree = damageTree;
    }

    /// <summary>
    /// 待删除的毁伤树。
    /// </summary>
    public DamageTreeInfoItem DamageTree { get; }
}

/// <summary>
/// 请求 View 打开毁伤树中间节点添加或修改弹窗的事件参数。
/// </summary>
public sealed class DamageTreeMiddleNodeEditRequestedEventArgs : EventArgs
{
    public DamageTreeMiddleNodeEditRequestedEventArgs(DamageTreeNodeItem draft, DamageTreeNodeItem? originalNode, bool isEditMode, DamageTreeNodeItem? parentNode = null)
    {
        Draft = draft;
        OriginalNode = originalNode;
        IsEditMode = isEditMode;
        ParentNode = parentNode;
    }

    /// <summary>
    /// 中间节点编辑草稿。
    /// </summary>
    public DamageTreeNodeItem Draft { get; }

    /// <summary>
    /// 被修改的原始节点。新增模式下为 null。
    /// </summary>
    public DamageTreeNodeItem? OriginalNode { get; }

    /// <summary>
    /// 新增节点的父节点。修改模式下通常为 null。
    /// </summary>
    public DamageTreeNodeItem? ParentNode { get; }

    /// <summary>
    /// 是否为修改模式。
    /// </summary>
    public bool IsEditMode { get; }
}

/// <summary>
/// 请求 View 打开毁伤树叶子节点添加或修改弹窗的事件参数。
/// </summary>
public sealed class DamageTreeLeafNodeEditRequestedEventArgs : EventArgs
{
    public DamageTreeLeafNodeEditRequestedEventArgs(DamageTreeNodeItem draft, DamageTreeNodeItem? originalNode, bool isEditMode, DamageTreeNodeItem? parentNode = null)
    {
        Draft = draft;
        OriginalNode = originalNode;
        IsEditMode = isEditMode;
        ParentNode = parentNode;
    }

    /// <summary>
    /// 叶子节点编辑草稿。
    /// </summary>
    public DamageTreeNodeItem Draft { get; }

    /// <summary>
    /// 被修改的原始节点。新增模式下为 null。
    /// </summary>
    public DamageTreeNodeItem? OriginalNode { get; }

    /// <summary>
    /// 新增节点的父节点。修改模式下通常为 null。
    /// </summary>
    public DamageTreeNodeItem? ParentNode { get; }

    /// <summary>
    /// 是否为修改模式。
    /// </summary>
    public bool IsEditMode { get; }
}

/// <summary>
/// 请求 View 确认删除毁伤树节点的事件参数。
/// </summary>
public sealed class DamageTreeNodeDeleteRequestedEventArgs : EventArgs
{
    public DamageTreeNodeDeleteRequestedEventArgs(DamageTreeNodeItem node)
    {
        Node = node;
    }

    /// <summary>
    /// 待删除的毁伤节点。
    /// </summary>
    public DamageTreeNodeItem Node { get; }
}
