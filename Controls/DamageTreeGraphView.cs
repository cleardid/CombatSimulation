using CombatSimulation.Models;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CombatSimulation.Controls;

/// <summary>
/// 独立的毁伤树逻辑预览控件，负责节点订阅、布局、绘制、缩放和图片导出。
/// </summary>
public sealed class DamageTreeGraphView : Canvas
{
    public static readonly DependencyProperty RootNodesProperty = DependencyProperty.Register(
        nameof(RootNodes),
        typeof(IEnumerable),
        typeof(DamageTreeGraphView),
        new FrameworkPropertyMetadata(null, OnGraphSourceChanged));

    public static readonly DependencyProperty SelectedNodeProperty = DependencyProperty.Register(
        nameof(SelectedNode),
        typeof(DamageTreeNodeItem),
        typeof(DamageTreeGraphView),
        new FrameworkPropertyMetadata(null, OnGraphSourceChanged));

    private const double ZoomMin = 0.25d;
    private const double ZoomMax = 4d;
    private const double ZoomStep = 1.1d;

    private readonly HashSet<DamageTreeNodeItem> _subscribedNodes = new();
    private readonly Dictionary<DamageTreeNodeItem, NotifyCollectionChangedEventHandler> _childrenHandlers = new();
    private readonly ScaleTransform _zoomTransform = new(1d, 1d);
    private INotifyCollectionChanged? _rootCollection;
    private Size _graphSize = new(420d, 260d);
    private bool _refreshQueued;

    public DamageTreeGraphView()
    {
        Background = Brushes.Transparent;
        ClipToBounds = false;
        LayoutTransform = _zoomTransform;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public IEnumerable? RootNodes
    {
        get => (IEnumerable?)GetValue(RootNodesProperty);
        set => SetValue(RootNodesProperty, value);
    }

    public DamageTreeNodeItem? SelectedNode
    {
        get => (DamageTreeNodeItem?)GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public bool HasGraph => Children.Count > 0;

    public bool TryHandleZoom(MouseWheelEventArgs e, ScrollViewer scrollViewer)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return false;
        }

        e.Handled = true;
        double oldZoom = _zoomTransform.ScaleX;
        double factor = e.Delta > 0 ? ZoomStep : 1d / ZoomStep;
        double newZoom = Math.Clamp(oldZoom * factor, ZoomMin, ZoomMax);
        if (Math.Abs(newZoom - oldZoom) < 0.0001d)
        {
            return true;
        }

        Point contentPoint = e.GetPosition(this);
        Point viewportPoint = e.GetPosition(scrollViewer);
        _zoomTransform.ScaleX = newZoom;
        _zoomTransform.ScaleY = newZoom;

        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                UpdateLayout();
                scrollViewer.ScrollToHorizontalOffset(Math.Max(0d, contentPoint.X * newZoom - viewportPoint.X));
                scrollViewer.ScrollToVerticalOffset(Math.Max(0d, contentPoint.Y * newZoom - viewportPoint.Y));
            }),
            DispatcherPriority.Background);
        return true;
    }

    public void ExportToFile(string fileName)
    {
        RefreshGraph();
        UpdateLayout();
        if (!HasGraph || ActualWidth <= 0d || ActualHeight <= 0d)
        {
            throw new InvalidOperationException("当前毁伤树预览图尺寸无效，无法导出。");
        }

        const double dpi = 96d;
        int pixelWidth = Math.Max(1, (int)Math.Ceiling(ActualWidth));
        int pixelHeight = Math.Max(1, (int)Math.Ceiling(ActualHeight));
        DrawingVisual exportVisual = new();
        using (DrawingContext context = exportVisual.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(15, 23, 42)), null, new Rect(0d, 0d, ActualWidth, ActualHeight));
            context.DrawRectangle(new VisualBrush(this), null, new Rect(0d, 0d, ActualWidth, ActualHeight));
        }

        RenderTargetBitmap bitmap = new(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(exportVisual);
        BitmapEncoder encoder = CreateBitmapEncoder(fileName);
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(fileName);
        encoder.Save(stream);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        return _graphSize;
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            double left = GetLeft(child);
            double top = GetTop(child);
            child.Arrange(new Rect(
                double.IsNaN(left) ? 0d : left,
                double.IsNaN(top) ? 0d : top,
                child.DesiredSize.Width,
                child.DesiredSize.Height));
        }

        return arrangeSize;
    }

    private static void OnGraphSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not DamageTreeGraphView view)
        {
            return;
        }

        view.AttachGraphSubscriptions();
        view.QueueRefresh();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachGraphSubscriptions();
        RefreshGraph();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachGraphSubscriptions();
    }

    private void AttachGraphSubscriptions()
    {
        DetachGraphSubscriptions();
        if (!IsLoaded)
        {
            return;
        }

        if (RootNodes is INotifyCollectionChanged collection)
        {
            _rootCollection = collection;
            _rootCollection.CollectionChanged += OnRootCollectionChanged;
        }

        AttachNodeSubscriptions(EnumerateRootNodes());
    }

    private void DetachGraphSubscriptions()
    {
        if (_rootCollection != null)
        {
            _rootCollection.CollectionChanged -= OnRootCollectionChanged;
            _rootCollection = null;
        }

        foreach (DamageTreeNodeItem node in _subscribedNodes)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
            if (_childrenHandlers.TryGetValue(node, out NotifyCollectionChangedEventHandler? handler))
            {
                node.Children.CollectionChanged -= handler;
            }
        }

        _childrenHandlers.Clear();
        _subscribedNodes.Clear();
    }

    private void AttachNodeSubscriptions(IEnumerable<DamageTreeNodeItem> nodes)
    {
        foreach (DamageTreeNodeItem node in nodes)
        {
            if (!_subscribedNodes.Add(node))
            {
                continue;
            }

            node.PropertyChanged += OnNodePropertyChanged;
            NotifyCollectionChangedEventHandler handler = (_, _) =>
            {
                AttachGraphSubscriptions();
                QueueRefresh();
            };
            node.Children.CollectionChanged += handler;
            _childrenHandlers[node] = handler;
            AttachNodeSubscriptions(node.Children);
        }
    }

    private void OnRootCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AttachGraphSubscriptions();
        QueueRefresh();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DamageTreeNodeItem.NodeName)
            or nameof(DamageTreeNodeItem.RelationType)
            or nameof(DamageTreeNodeItem.VoteThreshold)
            or nameof(DamageTreeNodeItem.SortOrder))
        {
            QueueRefresh();
        }
    }

    private IEnumerable<DamageTreeNodeItem> EnumerateRootNodes()
    {
        if (RootNodes == null)
        {
            yield break;
        }

        foreach (object? item in RootNodes)
        {
            if (item is DamageTreeNodeItem node)
            {
                yield return node;
            }
        }
    }

    private DamageTreeNodeItem? ResolveRootNode()
    {
        DamageTreeNodeItem? root = SelectedNode;
        while (root?.Parent != null)
        {
            root = root.Parent;
        }

        DamageTreeNodeItem[] roots = EnumerateRootNodes().ToArray();
        return root != null && roots.Contains(root) ? root : roots.FirstOrDefault();
    }

    private void QueueRefresh()
    {
        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _refreshQueued = false;
            RefreshGraph();
        }), DispatcherPriority.Background);
    }

    private void RefreshGraph()
    {
        Children.Clear();
        DamageTreeNodeItem? rootNode = ResolveRootNode();
        if (rootNode == null)
        {
            _graphSize = new Size(420d, 260d);
            InvalidateMeasure();
            return;
        }

        Color color = Colors.White;
        const double unitWidth = 24d;
        const double unitHeight = 24d;
        Dictionary<int, List<DamageTreeLayerInfo>> infos = new();
        Dictionary<int, List<DamageTreeLayerLineInfo>> lineInfos = new();
        DamageTreeLayerLayout.Convert(0, new[] { rootNode }, infos, lineInfos);
        if (infos.Count == 0 || !infos.TryGetValue(1, out List<DamageTreeLayerInfo>? rootLayer))
        {
            _graphSize = new Size(420d, 260d);
            InvalidateMeasure();
            return;
        }

        List<double> rows = BuildRowWeights(infos);
        int columns = Math.Max(4, rootLayer.Sum(info => info.Include) * 2 + 2);
        _graphSize = new Size(columns * unitWidth, rows.Sum() * unitHeight);
        double[] rowTops = BuildRowTops(rows, unitHeight);
        DrawNodes(infos, color, unitWidth, unitHeight, rows, rowTops);
        DrawLines(lineInfos, color, unitWidth, unitHeight, rows, rowTops);
        InvalidateMeasure();
        InvalidateArrange();
    }

    private static List<double> BuildRowWeights(IReadOnlyDictionary<int, List<DamageTreeLayerInfo>> infos)
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

    private void DrawNodes(
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
                EventRectangle rectangle = new()
                {
                    Color = color,
                    Type = info.HaveUp ? (info.NextCount > 0 ? EventType.Middle : EventType.End) : EventType.Top,
                    Script = info.Content,
                    Vertical = !isRootLayer,
                    Width = nodeWidth,
                    Height = nodeHeight
                };
                Children.Add(rectangle);
                SetLeft(rectangle, centerX - nodeWidth / 2d);
                SetTop(rectangle, rowTops[nodeRow]);

                if (info.NextCount <= 0)
                {
                    continue;
                }

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
                Children.Add(icon);
                SetLeft(icon, centerX - unitWidth / 2d);
                SetTop(icon, rowTops[gateRow]);
            }
        }
    }

    private void DrawLines(
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

                double left = GetNodeCenterX(info.Start, unitWidth);
                ConnectLine line = new()
                {
                    Color = color,
                    Unit = info.Unit,
                    ParentOffset = (info.ParentCenter - info.Start) / Math.Max(1d, info.Unit),
                    Outs = info.Outs.ToArray(),
                    Width = Math.Max(unitWidth, info.Unit * 2d * unitWidth),
                    Height = rows[row] * unitHeight
                };
                Children.Add(line);
                SetLeft(line, left);
                SetTop(line, rowTops[row]);
            }
        }
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

    private static double GetNodeCenterX(double center, double unitWidth) => (2d * center + 1d) * unitWidth;

    private static BitmapEncoder CreateBitmapEncoder(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
            ".bmp" => new BmpBitmapEncoder(),
            ".tif" or ".tiff" => new TiffBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
    }
}