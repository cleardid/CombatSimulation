using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using System.Windows;

namespace CombatSimulation.Views;

/// <summary>
/// 毁伤树中间节点添加/修改窗口。
/// </summary>
public partial class DamageTreeMiddleNodeEditWindow : Window
{
    private readonly DamageTreeMiddleNodeEditDialogViewModel _viewModel;

    public DamageTreeMiddleNodeEditWindow(DamageTreeNodeItem node, bool isEditMode)
    {
        InitializeComponent();
        _viewModel = new DamageTreeMiddleNodeEditDialogViewModel(node, isEditMode);
        _viewModel.CloseRequested += OnCloseRequested;
        DataContext = _viewModel;
    }

    public DamageTreeNodeItem EditedNode => _viewModel.EditedNode;

    private void OnCloseRequested(object? sender, DialogCloseRequestedEventArgs e)
    {
        DialogResult = e.DialogResult;
    }
}
