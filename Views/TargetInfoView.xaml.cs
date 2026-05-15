using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CombatSimulation.Views
{
    /// <summary>
    /// 负责初始化 TargetInfoView，并承接必须由 View 完成的弹窗和确认框交互。
    /// 按钮本身通过 Command 绑定到 TargetInfoViewModel，不再在 XAML 中直接挂业务 Click。
    /// </summary>
    public partial class TargetInfoView : UserControl
    {
        private TargetInfoViewModel? _attachedViewModel;

        public TargetInfoView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            AttachViewModel(DataContext as TargetInfoViewModel);
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            DetachViewModel(_attachedViewModel);
            AttachViewModel(e.NewValue as TargetInfoViewModel);
        }

        private void AttachViewModel(TargetInfoViewModel? viewModel)
        {
            if (viewModel == null)
            {
                _attachedViewModel = null;
                return;
            }

            _attachedViewModel = viewModel;
            viewModel.OperationMessageRequested += OnOperationMessageRequested;
            viewModel.TargetEditRequested += OnTargetEditRequested;
            viewModel.TargetDeleteRequested += OnTargetDeleteRequested;
            viewModel.StructureNodeEditRequested += OnStructureNodeEditRequested;
            viewModel.StructureNodeDeleteRequested += OnStructureNodeDeleteRequested;
            viewModel.StructureChildSystemAddRequested += OnStructureChildSystemAddRequested;
            viewModel.StructureChildPartAddRequested += OnStructureChildPartAddRequested;
        }

        private void DetachViewModel(TargetInfoViewModel? viewModel)
        {
            if (viewModel == null)
            {
                return;
            }

            viewModel.OperationMessageRequested -= OnOperationMessageRequested;
            viewModel.TargetEditRequested -= OnTargetEditRequested;
            viewModel.TargetDeleteRequested -= OnTargetDeleteRequested;
            viewModel.StructureNodeEditRequested -= OnStructureNodeEditRequested;
            viewModel.StructureNodeDeleteRequested -= OnStructureNodeDeleteRequested;
            viewModel.StructureChildSystemAddRequested -= OnStructureChildSystemAddRequested;
            viewModel.StructureChildPartAddRequested -= OnStructureChildPartAddRequested;
        }

        private void OnOperationMessageRequested(object? sender, OperationMessageRequestedEventArgs e)
        {
            ShowOperationMessage(e.Message);
        }

        private async void OnTargetEditRequested(object? sender, TargetEditRequestedEventArgs e)
        {
            if (sender is not TargetInfoViewModel viewModel)
            {
                return;
            }

            TargetEditWindow window = new(e.Draft, e.IsEditMode)
            {
                Owner = Window.GetWindow(this)
            };

            if (window.ShowDialog() != true)
            {
                return;
            }

            bool succeeded = e.OriginalTarget == null
                ? await viewModel.AddTargetAsync(window.EditedTarget)
                : await viewModel.UpdateTargetAsync(e.OriginalTarget, window.EditedTarget);

            if (!succeeded)
            {
                ShowOperationMessage(viewModel.StatusText);
            }
        }

        private async void OnTargetDeleteRequested(object? sender, TargetDeleteRequestedEventArgs e)
        {
            if (sender is not TargetInfoViewModel viewModel)
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                Window.GetWindow(this),
                $"确定删除目标“{e.Target.Name}”及其全部系统、部件和毁伤树信息？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes && !(await viewModel.DeleteTargetAsync(e.Target)))
            {
                ShowOperationMessage(viewModel.StatusText);
            }
        }

        private async void OnStructureNodeEditRequested(object? sender, StructureNodeEditRequestedEventArgs e)
        {
            if (sender is not TargetInfoViewModel viewModel)
            {
                return;
            }

            TargetStructureTreeNode node = e.Node;
            if (node.System != null)
            {
                TargetSystemEditWindow window = new(node.System, isEditMode: true)
                {
                    Owner = Window.GetWindow(this)
                };

                if (window.ShowDialog() == true && !(await viewModel.UpdateSystemAsync(node, window.EditedSystem)))
                {
                    ShowOperationMessage(viewModel.StatusText);
                }

                return;
            }

            if (node.Part != null)
            {
                TargetPartEditWindow window = new(node.Part, isEditMode: true)
                {
                    Owner = Window.GetWindow(this)
                };

                if (window.ShowDialog() == true && !(await viewModel.UpdatePartAsync(node, window.EditedPart)))
                {
                    ShowOperationMessage(viewModel.StatusText);
                }
            }
        }

        private async void OnStructureNodeDeleteRequested(object? sender, StructureNodeDeleteRequestedEventArgs e)
        {
            if (sender is not TargetInfoViewModel viewModel)
            {
                return;
            }

            string message = e.Node.System != null
                ? $"确定删除系统“{e.Node.Name}”及其全部子系统和底层部件？"
                : $"确定删除部件“{e.Node.Name}”？";

            MessageBoxResult result = MessageBox.Show(
                Window.GetWindow(this),
                message,
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes && !(await viewModel.DeleteStructureNodeAsync(e.Node)))
            {
                ShowOperationMessage(viewModel.StatusText);
            }
        }

        private async void OnStructureChildSystemAddRequested(object? sender, StructureChildSystemAddRequestedEventArgs e)
        {
            if (sender is not TargetInfoViewModel viewModel)
            {
                return;
            }

            TargetSystemEditWindow window = new(e.Draft, isEditMode: false)
            {
                Owner = Window.GetWindow(this)
            };

            if (window.ShowDialog() == true && !(await viewModel.AddChildSystemAsync(e.ParentNode, window.EditedSystem)))
            {
                ShowOperationMessage(viewModel.StatusText);
            }
        }

        private async void OnStructureChildPartAddRequested(object? sender, StructureChildPartAddRequestedEventArgs e)
        {
            if (sender is not TargetInfoViewModel viewModel)
            {
                return;
            }

            TargetPartEditWindow window = new(e.Draft, isEditMode: false)
            {
                Owner = Window.GetWindow(this)
            };

            if (window.ShowDialog() == true && !(await viewModel.AddChildPartAsync(e.ParentNode, window.EditedPart)))
            {
                ShowOperationMessage(viewModel.StatusText);
            }
        }

        private void ShowOperationMessage(string message)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                string.IsNullOrWhiteSpace(message) ? "操作失败。" : message,
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
