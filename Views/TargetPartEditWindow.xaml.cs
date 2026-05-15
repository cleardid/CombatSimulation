using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using System.Windows;

namespace CombatSimulation.Views;

/// <summary>
/// 目标部件添加/修改窗口。
/// 窗口代码后置只负责初始化 DataContext 和接收关闭请求。
/// </summary>
public partial class TargetPartEditWindow : Window
{
    private readonly TargetPartEditDialogViewModel _viewModel;

    public TargetPartEditWindow(TargetPartInfoItem part, bool isEditMode)
    {
        InitializeComponent();

        _viewModel = new TargetPartEditDialogViewModel(part, isEditMode);
        _viewModel.CloseRequested += OnCloseRequested;
        DataContext = _viewModel;
    }

    public TargetPartInfoItem EditedPart => _viewModel.EditedPart;

    private void OnCloseRequested(object? sender, DialogCloseRequestedEventArgs e)
    {
        DialogResult = e.DialogResult;
    }
}
