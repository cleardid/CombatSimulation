using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using System.Windows;

namespace CombatSimulation.Views;

/// <summary>
/// 目标添加/修改窗口。唯一标识 Code 不在界面展示，由调用方内部生成并保持。
/// 窗口代码后置只负责初始化 DataContext 和接收关闭请求。
/// </summary>
public partial class TargetEditWindow : Window
{
    private readonly TargetEditDialogViewModel _viewModel;

    public TargetEditWindow(TargetInfoItem target, bool isEditMode)
    {
        InitializeComponent();

        _viewModel = new TargetEditDialogViewModel(target, isEditMode);
        _viewModel.CloseRequested += OnCloseRequested;
        DataContext = _viewModel;
    }

    public TargetInfoItem EditedTarget => _viewModel.EditedTarget;

    private void OnCloseRequested(object? sender, DialogCloseRequestedEventArgs e)
    {
        DialogResult = e.DialogResult;
    }
}
