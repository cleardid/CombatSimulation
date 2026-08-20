using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace CombatSimulation.Views;

/// <summary>
/// 目标毁伤树信息子视图，只处理 TreeView 选择、上下文菜单和文件保存对话框。
/// 预览图的订阅、布局、绘制、缩放与编码由 DamageTreeGraphView 负责。
/// </summary>
public partial class TargetDamageTreeInfoView : UserControl
{
    public TargetDamageTreeInfoView()
    {
        InitializeComponent();
    }

    private void OnDamageTreePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        DamageTreeShow.TryHandleZoom(e, DamageTreePreviewScrollViewer);
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

        if (DataContext is TargetInfoViewModel viewModel && button.DataContext is DamageTreeNodeItem node)
        {
            viewModel.SelectDamageTreeNode(node);
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }

    private void OnExportDamageTreeImageClicked(object sender, RoutedEventArgs e)
    {
        if (!DamageTreeShow.HasGraph)
        {
            MessageBox.Show("当前没有可导出的毁伤树预览图。", "导出图片", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SaveFileDialog dialog = new()
        {
            Title = "导出毁伤树预览图",
            Filter = "PNG 图片 (*.png)|*.png|JPEG 图片 (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP 图片 (*.bmp)|*.bmp|TIFF 图片 (*.tif;*.tiff)|*.tif;*.tiff",
            FileName = $"{(DataContext as TargetInfoViewModel)?.SelectedDamageTree?.DisplayName ?? "DamageTree"}_逻辑预览图.png",
            AddExtension = true,
            DefaultExt = ".png",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        try
        {
            DamageTreeShow.ExportToFile(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出毁伤树预览图失败：{ex.Message}", "导出图片", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}