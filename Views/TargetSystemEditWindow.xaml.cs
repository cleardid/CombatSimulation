using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using System.Windows;

namespace CombatSimulation.Views
{
    /// <summary>
    /// 目标系统添加/修改窗口。
    /// 窗口代码后置只负责初始化 DataContext 和接收关闭请求。
    /// </summary>
    public partial class TargetSystemEditWindow : Window
    {
        private readonly TargetSystemEditDialogViewModel _viewModel;

        public TargetSystemEditWindow(TargetSystemInfoItem system, bool isEditMode)
        {
            InitializeComponent();

            _viewModel = new TargetSystemEditDialogViewModel(system, isEditMode);
            _viewModel.CloseRequested += OnCloseRequested;
            DataContext = _viewModel;
        }

        public TargetSystemInfoItem EditedSystem => _viewModel.EditedSystem;

        private void OnCloseRequested(object? sender, DialogCloseRequestedEventArgs e)
        {
            DialogResult = e.DialogResult;
        }
    }
}
