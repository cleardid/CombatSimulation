using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using CombatSimulation.Views.Dialogs;

namespace CombatSimulation.Views;

/// <summary>
/// 目标部件添加/修改窗口。
/// 窗口代码后置只负责初始化 DataContext 和接收关闭请求。
/// </summary>
public partial class TargetPartEditWindow : DialogWindowBase
{
    private readonly TargetPartEditDialogViewModel _viewModel;

    public TargetPartEditWindow(TargetPartInfoItem part, bool isEditMode)
    {
        InitializeComponent();

        _viewModel = new TargetPartEditDialogViewModel(part, isEditMode);
        BindCloseRequest(_viewModel);
        DataContext = _viewModel;
    }

    public TargetPartInfoItem EditedPart => _viewModel.EditedPart;
}
