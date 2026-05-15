using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CombatSimulation.Views;

/// <summary>
/// 目标毁伤树信息子视图。
/// 不设置 DataContext，继承父级 TargetInfoViewModel。
/// </summary>
public partial class TargetDamageTreeInfoView : UserControl
{
    public TargetDamageTreeInfoView()
    {
        InitializeComponent();
    }

    private void OnDamageTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is TargetInfoViewModel viewModel && e.NewValue is DamageTreeNodeItem node)
        {
            viewModel.SelectDamageTreeNode(node);
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
