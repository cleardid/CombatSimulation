using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using System.Windows;

namespace CombatSimulation.Views;

/// <summary>
/// 毁伤树基础信息添加/修改窗口。
/// </summary>
public partial class DamageTreeEditWindow : Window
{
    private readonly DamageTreeEditDialogViewModel _viewModel;

    public DamageTreeEditWindow(DamageTreeInfoItem tree, string targetName, bool isEditMode)
    {
        InitializeComponent();
        _viewModel = new DamageTreeEditDialogViewModel(tree, targetName, isEditMode);
        _viewModel.CloseRequested += OnCloseRequested;
        DataContext = _viewModel;
    }

    public DamageTreeInfoItem EditedTree => _viewModel.EditedTree;

    private void OnCloseRequested(object? sender, DialogCloseRequestedEventArgs e)
    {
        DialogResult = e.DialogResult;
    }
}
