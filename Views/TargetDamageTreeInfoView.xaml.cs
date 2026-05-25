using CombatSimulation.Controls;
using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using Microsoft.Win32;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CombatSimulation.Views;

/// <summary>
/// 目标毁伤树信息子视图。
/// 不设置 DataContext，继承父级 TargetInfoViewModel。
/// </summary>
public partial class TargetDamageTreeInfoView : UserControl
{
    private TargetInfoViewModel? _attachedViewModel;
    private DamageTreeInfoItem? _attachedTree;
    private DamageTreeNodeItem? _currentShow;
    private bool _previewRefreshQueued;
    private bool _pendingPreviewRefresh;

    // 预览图缩放比例。使用 LayoutTransform 缩放滚动内容，使 ScrollViewer 的滚动范围随缩放同步更新。
    private const double PreviewZoomMin = 0.25d;
    private const double PreviewZoomMax = 4.0d;
    private const double PreviewZoomStep = 1.1d;
    private double _previewZoom = 1.0d;

    private readonly HashSet<DamageTreeNodeItem> _subscribedPreviewNodes = new();
    private readonly Dictionary<DamageTreeNodeItem, NotifyCollectionChangedEventHandler> _childrenCollectionHandlers = new();

    public TargetDamageTreeInfoView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel(DataContext as TargetInfoViewModel);
        QueueShowTree(refresh: true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModel();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachViewModel(e.NewValue as TargetInfoViewModel);
        QueueShowTree(refresh: true);
    }

    private void OnDamageTreePreviewViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 预览区尺寸变化会影响滚动内容尺寸，因此需要强制重绘。
        QueueShowTree(refresh: true);
    }

    private void OnDamageTreePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 普通滚轮仍然交给 ScrollViewer 做纵向滚动；只有按住 Ctrl 时才进行缩放。
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        e.Handled = true;

        if (DamageTreePreviewScrollViewer == null || DamageTreePreviewCanvas == null)
        {
            return;
        }

        double oldZoom = _previewZoom;
        double zoomFactor = e.Delta > 0 ? PreviewZoomStep : 1d / PreviewZoomStep;
        double newZoom = Math.Clamp(oldZoom * zoomFactor, PreviewZoomMin, PreviewZoomMax);
        if (Math.Abs(newZoom - oldZoom) < 0.0001d)
        {
            return;
        }

        Point contentPoint = e.GetPosition(DamageTreePreviewCanvas);
        Point viewportPoint = e.GetPosition(DamageTreePreviewScrollViewer);

        _previewZoom = newZoom;
        ApplyPreviewZoomTransform();

        // 保持鼠标指向的逻辑位置尽量不变，缩放后不至于突然跳到左上角。
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                DamageTreePreviewCanvas.UpdateLayout();

                double targetHorizontalOffset = contentPoint.X * newZoom - viewportPoint.X;
                double targetVerticalOffset = contentPoint.Y * newZoom - viewportPoint.Y;

                DamageTreePreviewScrollViewer.ScrollToHorizontalOffset(Math.Max(0d, targetHorizontalOffset));
                DamageTreePreviewScrollViewer.ScrollToVerticalOffset(Math.Max(0d, targetVerticalOffset));
            }),
            DispatcherPriority.Background);
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

        // 与目标结构模块保持一致：点击节点右侧“添加”按钮时，先把该节点设为当前节点，再打开菜单。
        if (DataContext is TargetInfoViewModel viewModel && button.DataContext is DamageTreeNodeItem node)
        {
            viewModel.SelectDamageTreeNode(node);
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }

    private void AttachViewModel(TargetInfoViewModel? viewModel)
    {
        if (ReferenceEquals(_attachedViewModel, viewModel))
        {
            AttachSelectedTree(viewModel?.SelectedDamageTree);
            return;
        }

        DetachViewModel();
        _attachedViewModel = viewModel;
        if (_attachedViewModel == null)
        {
            return;
        }

        _attachedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _attachedViewModel.DamageTrees.CollectionChanged += OnDamageTreesCollectionChanged;
        AttachSelectedTree(_attachedViewModel.SelectedDamageTree);
    }

    private void DetachViewModel()
    {
        if (_attachedViewModel != null)
        {
            _attachedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _attachedViewModel.DamageTrees.CollectionChanged -= OnDamageTreesCollectionChanged;
        }

        AttachSelectedTree(null);
        _attachedViewModel = null;
    }

    private void AttachSelectedTree(DamageTreeInfoItem? tree)
    {
        if (ReferenceEquals(_attachedTree, tree))
        {
            return;
        }

        if (_attachedTree != null)
        {
            _attachedTree.RootNodes.CollectionChanged -= OnRootNodesCollectionChanged;
        }

        DetachPreviewNodeSubscriptions();
        _attachedTree = tree;

        if (_attachedTree != null)
        {
            _attachedTree.RootNodes.CollectionChanged += OnRootNodesCollectionChanged;
            AttachPreviewNodeSubscriptions(_attachedTree.RootNodes);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_attachedViewModel == null)
        {
            return;
        }

        if (e.PropertyName == nameof(TargetInfoViewModel.SelectedDamageTree))
        {
            AttachSelectedTree(_attachedViewModel.SelectedDamageTree);
            QueueShowTree(refresh: true);
            return;
        }

        if (e.PropertyName == nameof(TargetInfoViewModel.SelectedDamageTreeNode)
            || e.PropertyName == nameof(TargetInfoViewModel.DamageTreeInfoVisibility))
        {
            QueueShowTree(refresh: false);
        }
    }

    private void OnDamageTreesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AttachSelectedTree(_attachedViewModel?.SelectedDamageTree);
        QueueShowTree(refresh: true);
    }

    private void OnRootNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DetachPreviewNodeSubscriptions();
        if (_attachedTree != null)
        {
            AttachPreviewNodeSubscriptions(_attachedTree.RootNodes);
        }

        QueueShowTree(refresh: true);
    }

    private void AttachPreviewNodeSubscriptions(IEnumerable<DamageTreeNodeItem> nodes)
    {
        foreach (DamageTreeNodeItem node in nodes)
        {
            if (!_subscribedPreviewNodes.Add(node))
            {
                continue;
            }

            node.PropertyChanged += OnPreviewNodePropertyChanged;

            NotifyCollectionChangedEventHandler handler = (_, _) =>
            {
                // 节点增删会改变每个节点的叶子跨度和连线位置，必须重新订阅并重绘。
                DetachPreviewNodeSubscriptions();
                if (_attachedTree != null)
                {
                    AttachPreviewNodeSubscriptions(_attachedTree.RootNodes);
                }

                QueueShowTree(refresh: true);
            };
            node.Children.CollectionChanged += handler;
            _childrenCollectionHandlers[node] = handler;

            AttachPreviewNodeSubscriptions(node.Children);
        }
    }

    private void DetachPreviewNodeSubscriptions()
    {
        foreach (DamageTreeNodeItem node in _subscribedPreviewNodes)
        {
            node.PropertyChanged -= OnPreviewNodePropertyChanged;
            if (_childrenCollectionHandlers.TryGetValue(node, out NotifyCollectionChangedEventHandler? handler))
            {
                node.Children.CollectionChanged -= handler;
            }
        }

        _childrenCollectionHandlers.Clear();
        _subscribedPreviewNodes.Clear();
    }

    private void OnPreviewNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DamageTreeNodeItem.NodeName)
            or nameof(DamageTreeNodeItem.RelationType)
            or nameof(DamageTreeNodeItem.VoteThreshold)
            or nameof(DamageTreeNodeItem.SortOrder))
        {
            QueueShowTree(refresh: true);
        }
    }

    private void QueueShowTree(bool refresh)
    {
        _pendingPreviewRefresh |= refresh;
        if (_previewRefreshQueued)
        {
            return;
        }

        _previewRefreshQueued = true;
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                _previewRefreshQueued = false;
                bool shouldRefresh = _pendingPreviewRefresh;
                _pendingPreviewRefresh = false;
                ShowTree(refresh: shouldRefresh);
            }),
            DispatcherPriority.Background);
    }

    /// <summary>
    /// 按旧版 DamageTreeShow 的方式绘制毁伤树预览：先把树转换为层级信息，再在 Grid 中放置事件框、逻辑门和连线。
    /// </summary>
    private void ShowTree(bool refresh = true)
    {
        if (DamageTreePreviewScrollViewer == null || DamageTreePreviewCanvas == null || DamageTreeShow == null)
        {
            return;
        }

        if (DamageTreePreviewScrollViewer.ActualWidth <= 0 || DamageTreePreviewScrollViewer.ActualHeight <= 0)
        {
            return;
        }

        DamageTreeNodeItem? selectedNode = _attachedViewModel?.SelectedDamageTreeNode
            ?? _attachedViewModel?.SelectedDamageTree?.RootNodes.FirstOrDefault();
        if (selectedNode == null)
        {
            _currentShow = null;
            ClearDamageTreeShow();
            return;
        }

        DamageTreeNodeItem rootNode = selectedNode;
        while (rootNode.Parent != null)
        {
            rootNode = rootNode.Parent;
        }

        if (ReferenceEquals(_currentShow, rootNode) && !refresh)
        {
            return;
        }

        _currentShow = rootNode;
        ClearDamageTreeShow();

        Color color = Colors.White;
        double unitWidth = PreviewUnitSize(24d);
        double unitHeight = PreviewUnitSize(24d);

        Dictionary<int, List<DamageTreeLayerInfo>> infos = new();
        Dictionary<int, List<DamageTreeLayerLineInfo>> lineInfos = new();
        DamageTreeLayerLayout.Convert(0, new[] { rootNode }, infos, lineInfos);
        if (infos.Count == 0 || !infos.TryGetValue(1, out List<DamageTreeLayerInfo>? rootLayer))
        {
            return;
        }

        List<double> rows = BuildPreviewRowWeights(infos);
        int columns = Math.Max(4, rootLayer.Sum(info => info.Include) * 2 + 2);

        double contentWidth = columns * unitWidth;
        double contentHeight = rows.Sum() * unitHeight;
        double viewportWidth = Math.Max(0d, DamageTreePreviewScrollViewer.ActualWidth - SystemParameters.VerticalScrollBarWidth);

        // Canvas 宽度至少等于可视区宽度。
        // 内容小于可视区时，DamageTreeShow 通过 HorizontalAlignment=Center 居中；内容大于可视区时，横向滚动范围稳定存在。
        DamageTreePreviewCanvas.Width = Math.Max(contentWidth, viewportWidth);
        DamageTreePreviewCanvas.Height = contentHeight;
        DamageTreeShow.Width = contentWidth;
        DamageTreeShow.Height = contentHeight;

        ApplyPreviewZoomTransform();

        double[] rowTops = BuildRowTops(rows, unitHeight);
        DrawPreviewNodes(DamageTreeShow, infos, color, unitWidth, unitHeight, rows, rowTops);
        DrawPreviewLines(DamageTreeShow, lineInfos, color, unitWidth, unitHeight, rows, rowTops);
    }

    private void ClearDamageTreeShow()
    {
        DamageTreeShow.Children.Clear();
        // DamageTreeShow 使用 Canvas 绝对定位，清空子元素即可。
        DamageTreeShow.Width = double.NaN;
        DamageTreeShow.Height = double.NaN;

        if (DamageTreePreviewCanvas != null)
        {
            DamageTreePreviewCanvas.Width = double.NaN;
            DamageTreePreviewCanvas.Height = double.NaN;
            ApplyPreviewZoomTransform();
        }
    }

    private void ApplyPreviewZoomTransform()
    {
        if (DamageTreePreviewCanvas == null)
        {
            return;
        }

        DamageTreePreviewCanvas.LayoutTransform = new ScaleTransform(_previewZoom, _previewZoom);
    }

    private static List<double> BuildPreviewRowWeights(IReadOnlyDictionary<int, List<DamageTreeLayerInfo>> infos)
    {
        List<double> rows = new() { 1d };
        foreach (KeyValuePair<int, List<DamageTreeLayerInfo>> item in infos.OrderBy(item => item.Key))
        {
            if (item.Key == 1)
            {
                rows.Add(1.5d);
            }
            else
            {
                rows.Add(1.5d);
                rows.Add(1d);
                rows.Add(item.Value.Max(info => Math.Max(1, info.Content.Length)) + 1d);
            }
        }

        rows.Add(1d);
        return rows;
    }

    private void OnExportDamageTreeImageClicked(object sender, RoutedEventArgs e)
    {
        if (DamageTreeShow == null || DamageTreeShow.Children.Count == 0)
        {
            MessageBox.Show("当前没有可导出的毁伤树预览图。", "导出图片", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 导出前强制刷新一次，确保当前选中的毁伤树已经完整绘制。
        ShowTree(refresh: true);
        DamageTreeShow.UpdateLayout();

        double width = DamageTreeShow.Width;
        double height = DamageTreeShow.Height;
        if (double.IsNaN(width) || width <= 0d || double.IsNaN(height) || height <= 0d)
        {
            MessageBox.Show("当前毁伤树预览图尺寸无效，无法导出。", "导出图片", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveFileDialog dialog = new()
        {
            Title = "导出毁伤树预览图",
            Filter = "PNG 图片 (*.png)|*.png|JPEG 图片 (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP 图片 (*.bmp)|*.bmp|TIFF 图片 (*.tif;*.tiff)|*.tif;*.tiff",
            FileName = $"{(_attachedViewModel?.SelectedDamageTree?.DisplayName ?? "DamageTree")}_逻辑预览图.png",
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
            ExportDamageTreePreview(dialog.FileName, width, height);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出毁伤树预览图失败：{ex.Message}", "导出图片", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportDamageTreePreview(string fileName, double width, double height)
    {
        const double dpi = 96d;
        int pixelWidth = Math.Max(1, (int)Math.Ceiling(width));
        int pixelHeight = Math.Max(1, (int)Math.Ceiling(height));

        DrawingVisual exportVisual = new();
        using (DrawingContext drawingContext = exportVisual.RenderOpen())
        {
            // 导出完整逻辑图，而不是当前滚动窗口中的可视区域。
            drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(15, 23, 42)), null, new Rect(0d, 0d, width, height));
            drawingContext.DrawRectangle(new VisualBrush(DamageTreeShow), null, new Rect(0d, 0d, width, height));
        }

        RenderTargetBitmap bitmap = new(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(exportVisual);

        BitmapEncoder encoder = CreateBitmapEncoder(fileName);
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using FileStream stream = File.Create(fileName);
        encoder.Save(stream);
    }

    private static BitmapEncoder CreateBitmapEncoder(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
            ".bmp" => new BmpBitmapEncoder(),
            ".tif" or ".tiff" => new TiffBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
    }

    private static double[] BuildRowTops(IReadOnlyList<double> rows, double unitHeight)
    {
        double[] rowTops = new double[rows.Count];
        double currentTop = 0d;
        for (int index = 0; index < rows.Count; index++)
        {
            rowTops[index] = currentTop;
            currentTop += rows[index] * unitHeight;
        }

        return rowTops;
    }

    private static double GetNodeCenterX(double center, double unitWidth)
    {
        // 与旧版 Grid 布局保持同一套横向单位：左侧保留 1 个 unitWidth 的空列，
        // 每个叶子节点占 2 个 unitWidth，节点中心位于叶子槽中心。
        return (2d * center + 1d) * unitWidth;
    }

    private static void DrawPreviewNodes(
        Canvas canvas,
        IReadOnlyDictionary<int, List<DamageTreeLayerInfo>> infos,
        Color color,
        double unitWidth,
        double unitHeight,
        IReadOnlyList<double> rows,
        IReadOnlyList<double> rowTops)
    {
        foreach (KeyValuePair<int, List<DamageTreeLayerInfo>> item in infos.OrderBy(item => item.Key))
        {
            foreach (DamageTreeLayerInfo info in item.Value)
            {
                bool isRootLayer = item.Key == 1;
                int nodeRow = isRootLayer ? 1 : item.Key * 3 - 2;
                if (nodeRow < 0 || nodeRow >= rows.Count)
                {
                    continue;
                }

                double nodeWidth = isRootLayer ? (info.Content.Length + 2d) * unitWidth : unitWidth;
                double nodeHeight = rows[nodeRow] * unitHeight;
                double centerX = GetNodeCenterX(info.Center, unitWidth);

                // 保持父节点位于其直接子节点组的中心，不再额外按内容区宽度二次居中。

                EventRectangle rectangle = new()
                {
                    Color = color,
                    Type = info.HaveUp ? (info.NextCount > 0 ? EventType.Middle : EventType.End) : EventType.Top,
                    Script = info.Content,
                    Vertical = !isRootLayer,
                    Width = nodeWidth,
                    Height = nodeHeight
                };

                canvas.Children.Add(rectangle);
                Canvas.SetLeft(rectangle, centerX - nodeWidth / 2d);
                Canvas.SetTop(rectangle, rowTops[nodeRow]);

                if (info.NextCount > 0)
                {
                    int gateRow = isRootLayer ? 2 : item.Key * 3 - 1;
                    if (gateRow < 0 || gateRow >= rows.Count)
                    {
                        continue;
                    }

                    GateIcon icon = new()
                    {
                        Color = color,
                        Type = info.NextCount < 2 ? -1 : info.Type,
                        Rate = info.Ratio,
                        Width = unitWidth,
                        Height = rows[gateRow] * unitHeight
                    };

                    canvas.Children.Add(icon);
                    Canvas.SetLeft(icon, centerX - unitWidth / 2d);
                    Canvas.SetTop(icon, rowTops[gateRow]);
                }
            }
        }
    }

    private static void DrawPreviewLines(
        Canvas canvas,
        IReadOnlyDictionary<int, List<DamageTreeLayerLineInfo>> lineInfos,
        Color color,
        double unitWidth,
        double unitHeight,
        IReadOnlyList<double> rows,
        IReadOnlyList<double> rowTops)
    {
        foreach (KeyValuePair<int, List<DamageTreeLayerLineInfo>> item in lineInfos.OrderBy(item => item.Key))
        {
            foreach (DamageTreeLayerLineInfo info in item.Value)
            {
                int row = item.Key * 3;
                if (row < 0 || row >= rows.Count)
                {
                    continue;
                }

                double left = (2d * info.Start + 1d) * unitWidth;
                double width = Math.Max(unitWidth, info.Unit * 2d * unitWidth);

                ConnectLine line = new()
                {
                    Color = color,
                    Unit = info.Unit,
                    ParentOffset = (info.ParentCenter - info.Start) / Math.Max(1d, info.Unit),
                    Outs = info.Outs.ToArray(),
                    Width = width,
                    Height = rows[row] * unitHeight
                };

                canvas.Children.Add(line);
                Canvas.SetLeft(line, left);
                Canvas.SetTop(line, rowTops[row]);
            }
        }
    }

    private static double PreviewUnitSize(double value) => value;
}
