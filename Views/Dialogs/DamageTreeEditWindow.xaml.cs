using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using CombatSimulation.Views.Dialogs;

namespace CombatSimulation.Views;

/// <summary>
/// 毁伤树基础信息添加/修改窗口。
/// </summary>
public partial class DamageTreeEditWindow : DialogWindowBase
{
    private readonly DamageTreeEditDialogViewModel _viewModel;

    public DamageTreeEditWindow(DamageTreeInfoItem tree, string targetName, bool isEditMode)
    {
        InitializeComponent();
        _viewModel = new DamageTreeEditDialogViewModel(tree, targetName, isEditMode);
        BindCloseRequest(_viewModel);
        DataContext = _viewModel;
    }

    public DamageTreeInfoItem EditedTree => _viewModel.EditedTree;
}
