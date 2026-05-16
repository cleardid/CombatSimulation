using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using CombatSimulation.Views.Dialogs;
using System.Windows;
using System.Windows.Controls;

namespace CombatSimulation.Views;

/// <summary>
/// 毁伤树叶子节点添加/修改窗口。
/// </summary>
public partial class DamageTreeLeafNodeEditWindow : DialogWindowBase
{
    private readonly DamageTreeLeafNodeEditDialogViewModel _viewModel;

    public DamageTreeLeafNodeEditWindow(DamageTreeNodeItem node, IEnumerable<TargetStructureTreeNode> targetStructureRoots, bool isEditMode)
    {
        InitializeComponent();
        _viewModel = new DamageTreeLeafNodeEditDialogViewModel(node, targetStructureRoots, isEditMode);
        BindCloseRequest(_viewModel);
        DataContext = _viewModel;
    }

    public DamageTreeNodeItem EditedNode => _viewModel.EditedNode;

    private void OnTargetStructureSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _viewModel.SelectedStructureNode = e.NewValue as TargetStructureTreeNode;
    }
}
