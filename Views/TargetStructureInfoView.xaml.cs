using CombatSimulation.Controls;
using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CombatSimulation.Views;

/// <summary>
/// 目标结构信息子视图。Unity 原生窗口生命周期由 UnityHostLifecycleCoordinator 管理。
/// </summary>
public partial class TargetStructureInfoView : UserControl
{
    private readonly UnityHostLifecycleCoordinator _unityHostLifecycleCoordinator;

    public TargetStructureInfoView()
    {
        InitializeComponent();
        _unityHostLifecycleCoordinator = new UnityHostLifecycleCoordinator(this, UnityNativeHost);
    }

    private void OnStructureTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is TargetInfoViewModel viewModel && e.NewValue is TargetStructureTreeNode node)
        {
            viewModel.SelectStructureNode(node);
        }
    }

    private void OnOpenAddContextMenuClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not Button button || button.ContextMenu == null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }
}
