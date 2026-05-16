using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using CombatSimulation.Views.Dialogs;

namespace CombatSimulation.Views;

/// <summary>
/// 目标添加/修改窗口。唯一标识 Code 不在界面展示，由调用方内部生成并保持。
/// 窗口代码后置只负责初始化 DataContext 和接收关闭请求。
/// </summary>
public partial class TargetEditWindow : DialogWindowBase
{
    private readonly TargetEditDialogViewModel _viewModel;

    public TargetEditWindow(TargetInfoItem target, bool isEditMode)
    {
        InitializeComponent();

        _viewModel = new TargetEditDialogViewModel(target, isEditMode);
        BindCloseRequest(_viewModel);
        DataContext = _viewModel;
    }

    public TargetInfoItem EditedTarget => _viewModel.EditedTarget;
}
