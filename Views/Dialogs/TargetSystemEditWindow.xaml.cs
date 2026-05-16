using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using CombatSimulation.Views.Dialogs;

namespace CombatSimulation.Views;

/// <summary>
/// 目标系统添加/修改窗口。
/// 窗口代码后置只负责初始化 DataContext 和接收关闭请求。
/// </summary>
public partial class TargetSystemEditWindow : DialogWindowBase
{
    private readonly TargetSystemEditDialogViewModel _viewModel;

    public TargetSystemEditWindow(TargetSystemInfoItem system, bool isEditMode, IEnumerable<TargetInfoItem>? allTargets = null)
    {
        InitializeComponent();

        _viewModel = new TargetSystemEditDialogViewModel(system, isEditMode, allTargets);
        BindCloseRequest(_viewModel);
        DataContext = _viewModel;
    }

    public TargetSystemInfoItem EditedSystem => _viewModel.EditedSystem;
}
