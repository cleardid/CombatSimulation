using CombatSimulation.Models;

namespace CombatSimulation.ViewModels;

/// <summary>
/// 目标模块需要的弹窗、确认框和提示交互。
/// ViewModel 只依赖该契约，不再通过多组事件反向驱动 View。
/// </summary>
public interface ITargetInteractionService
{
    void ShowMessage(string message);
    bool ConfirmDeletion(string message);
    TargetInfoItem? EditTarget(TargetInfoItem draft, bool isEditMode);
    TargetSystemInfoItem? EditSystem(TargetSystemInfoItem draft, bool isEditMode, IEnumerable<TargetInfoItem> allTargets);
    TargetPartInfoItem? EditPart(TargetPartInfoItem draft, bool isEditMode);
    DamageTreeInfoItem? EditDamageTree(DamageTreeInfoItem draft, string targetName, bool isEditMode);
    DamageTreeNodeItem? EditMiddleDamageNode(DamageTreeNodeItem draft, bool isEditMode);
    DamageTreeNodeItem? EditLeafDamageNode(DamageTreeNodeItem draft, IEnumerable<TargetStructureTreeNode> structureRoots, bool isEditMode);
}

internal sealed class NullTargetInteractionService : ITargetInteractionService
{
    public static NullTargetInteractionService Instance { get; } = new();

    private NullTargetInteractionService()
    {
    }

    public void ShowMessage(string message)
    {
    }

    public bool ConfirmDeletion(string message) => false;
    public TargetInfoItem? EditTarget(TargetInfoItem draft, bool isEditMode) => null;
    public TargetSystemInfoItem? EditSystem(TargetSystemInfoItem draft, bool isEditMode, IEnumerable<TargetInfoItem> allTargets) => null;
    public TargetPartInfoItem? EditPart(TargetPartInfoItem draft, bool isEditMode) => null;
    public DamageTreeInfoItem? EditDamageTree(DamageTreeInfoItem draft, string targetName, bool isEditMode) => null;
    public DamageTreeNodeItem? EditMiddleDamageNode(DamageTreeNodeItem draft, bool isEditMode) => null;
    public DamageTreeNodeItem? EditLeafDamageNode(DamageTreeNodeItem draft, IEnumerable<TargetStructureTreeNode> structureRoots, bool isEditMode) => null;
}