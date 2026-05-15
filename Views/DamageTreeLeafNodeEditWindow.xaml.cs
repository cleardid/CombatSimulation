using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CombatSimulation.Views;

/// <summary>
/// 毁伤树叶子节点添加/修改窗口。
/// </summary>
public partial class DamageTreeLeafNodeEditWindow : Window
{
    private readonly DamageTreeLeafNodeEditDialogViewModel _viewModel;

    public DamageTreeLeafNodeEditWindow(DamageTreeNodeItem node, IEnumerable<TargetStructureTreeNode> targetStructureRoots, bool isEditMode)
    {
        InitializeComponent();
        _viewModel = new DamageTreeLeafNodeEditDialogViewModel(node, targetStructureRoots, isEditMode);
        _viewModel.CloseRequested += OnCloseRequested;
        DataContext = _viewModel;
    }

    public DamageTreeNodeItem EditedNode => _viewModel.EditedNode;

    private void OnTargetStructureSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _viewModel.SelectedStructureNode = e.NewValue as TargetStructureTreeNode;
    }

    private void OnCloseRequested(object? sender, DialogCloseRequestedEventArgs e)
    {
        DialogResult = e.DialogResult;
    }
}
