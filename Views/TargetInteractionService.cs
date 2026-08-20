using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using System.Windows;

namespace CombatSimulation.Views;

/// <summary>
/// 目标模块的 WPF 弹窗实现。窗口创建细节集中在此处，页面代码后置无需维护事件总线。
/// </summary>
public sealed class TargetInteractionService : ITargetInteractionService
{
    private readonly Func<Window?> _getOwner;

    public TargetInteractionService(Func<Window?> getOwner)
    {
        _getOwner = getOwner ?? throw new ArgumentNullException(nameof(getOwner));
    }

    public void ShowMessage(string message)
    {
        MessageBox.Show(
            _getOwner(),
            string.IsNullOrWhiteSpace(message) ? "操作失败。" : message,
            "提示",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    public bool ConfirmDeletion(string message)
    {
        return MessageBox.Show(
            _getOwner(),
            message,
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public TargetInfoItem? EditTarget(TargetInfoItem draft, bool isEditMode)
    {
        TargetEditWindow window = new(draft, isEditMode);
        SetOwner(window);
        return window.ShowDialog() == true ? window.EditedTarget : null;
    }

    public TargetSystemInfoItem? EditSystem(TargetSystemInfoItem draft, bool isEditMode, IEnumerable<TargetInfoItem> allTargets)
    {
        TargetSystemEditWindow window = new(draft, isEditMode, allTargets);
        SetOwner(window);
        return window.ShowDialog() == true ? window.EditedSystem : null;
    }

    public TargetPartInfoItem? EditPart(TargetPartInfoItem draft, bool isEditMode)
    {
        TargetPartEditWindow window = new(draft, isEditMode);
        SetOwner(window);
        return window.ShowDialog() == true ? window.EditedPart : null;
    }

    public DamageTreeInfoItem? EditDamageTree(DamageTreeInfoItem draft, string targetName, bool isEditMode)
    {
        DamageTreeEditWindow window = new(draft, targetName, isEditMode);
        SetOwner(window);
        return window.ShowDialog() == true ? window.EditedTree : null;
    }

    public DamageTreeNodeItem? EditMiddleDamageNode(DamageTreeNodeItem draft, bool isEditMode)
    {
        DamageTreeMiddleNodeEditWindow window = new(draft, isEditMode);
        SetOwner(window);
        return window.ShowDialog() == true ? window.EditedNode : null;
    }

    public DamageTreeNodeItem? EditLeafDamageNode(
        DamageTreeNodeItem draft,
        IEnumerable<TargetStructureTreeNode> structureRoots,
        bool isEditMode)
    {
        DamageTreeLeafNodeEditWindow window = new(draft, structureRoots, isEditMode);
        SetOwner(window);
        return window.ShowDialog() == true ? window.EditedNode : null;
    }

    private void SetOwner(Window window)
    {
        Window? owner = _getOwner();
        if (owner != null)
        {
            window.Owner = owner;
        }
    }
}