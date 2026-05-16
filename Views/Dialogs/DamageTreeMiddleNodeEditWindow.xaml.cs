using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using CombatSimulation.Views.Dialogs;

namespace CombatSimulation.Views;

/// <summary>
/// 毁伤树中间节点添加/修改窗口。
/// </summary>
public partial class DamageTreeMiddleNodeEditWindow : DialogWindowBase
{
    private readonly DamageTreeMiddleNodeEditDialogViewModel _viewModel;

    public DamageTreeMiddleNodeEditWindow(DamageTreeNodeItem node, bool isEditMode)
    {
        InitializeComponent();
        _viewModel = new DamageTreeMiddleNodeEditDialogViewModel(node, isEditMode);
        BindCloseRequest(_viewModel);
        DataContext = _viewModel;
    }

    public DamageTreeNodeItem EditedNode => _viewModel.EditedNode;
}
