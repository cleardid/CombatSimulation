using CombatSimulation.Models;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CombatSimulation.Controls;

/// <summary>
/// 毁伤树逻辑图预览控件。
/// </summary>
/// <remarks>
/// 左侧 TreeView 用于编辑节点，本控件只负责把同一批 DamageTreeNodeItem 绘制成“方框 + 连线 + 逻辑门”的结构图。
/// 布局规则为：叶节点按固定间距排列；非叶父节点横向位置取首个子节点和末个子节点中心点的中点。
/// </remarks>
public sealed class DamageTreeGraphView : FrameworkElement
{
    public static readonly DependencyProperty RootNodesProperty = DependencyProperty.Register(
        nameof(RootNodes),
        typeof(IEnumerable),
        typeof(DamageTreeGraphView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure, OnRootNodesChanged));

    private const double HorizontalGap = 34d;
    private const double LeafPitch = VerticalNodeWidth + HorizontalGap;
    private const double LevelConnectorGap = 78d;
    private const double TopMargin = 20d;
    private const double LeftMargin = 20d;
    private const double RightMargin = 20d;
    private const double BottomMargin = 30d;
    private const double HorizontalNodeWidth = 180d;
    private const double HorizontalNodeHeight = 30d;
    private const double VerticalNodeWidth = 38d;
    private const double VerticalNodeMinHeight = 92d;
    private const double VerticalNodeMaxHeight = 180d;
    private const double GateSize = 28d;

    private static readonly Pen s_linePen = new(new SolidColorBrush(Color.FromArgb(190, 191, 212, 245)), 1d);
    private static readonly Pen s_boxPen = new(new SolidColorBrush(Color.FromArgb(235, 210, 226, 250)), 1d);
    private static readonly Pen s_gatePen = new(new SolidColorBrush(Color.FromArgb(235, 210, 226, 250)), 1d);
    private static readonly Brush s_nodeBrush = new SolidColorBrush(Color.FromArgb(80, 19, 50, 76));
    private static readonly Brush s_gateBrush = new SolidColorBrush(Color.FromArgb(72, 15, 23, 42));
    private static readonly Brush s_textBrush = new SolidColorBrush(Color.FromRgb(234, 246, 255));
    private static readonly Brush s_gateTextBrush = new SolidColorBrush(Color.FromRgb(246, 232, 74));
    private static readonly Brush s_emptyTextBrush = new SolidColorBrush(Color.FromRgb(191, 212, 245));

    private readonly HashSet<DamageTreeNodeItem> _subscribedNodes = new();
    private readonly Dictionary<DamageTreeNodeItem, NotifyCollectionChangedEventHandler> _childrenHandlers = new();

    private LayoutNode[] _layoutRoots = Array.Empty<LayoutNode>();
    private Size _desiredGraphSize = new(420d, 260d);

    public IEnumerable? RootNodes
    {
        get => (IEnumerable?)GetValue(RootNodesProperty);
        set => SetValue(RootNodesProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateLayoutTree();
        return _desiredGraphSize;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        UpdateLayoutTree();

        if (_layoutRoots.Length == 0)
        {
            DrawEmptyText(drawingContext);
            return;
        }

        foreach (LayoutNode root in _layoutRoots)
        {
            DrawConnectors(drawingContext, root);
        }

        foreach (LayoutNode root in _layoutRoots)
        {
            DrawNodeRecursive(drawingContext, root);
        }
    }

    private static void OnRootNodesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not DamageTreeGraphView view)
        {
            return;
        }

        if (e.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= view.OnRootCollectionChanged;
        }

        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += view.OnRootCollectionChanged;
        }

        view.DetachNodeSubscriptions();
        view.AttachNodeSubscriptions(view.EnumerateCurrentRootNodes());
        view.InvalidateMeasure();
        view.InvalidateVisual();
    }

    private void OnRootCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 根节点集合变化同样会影响图尺寸；通常只有一个根节点，但这里保留历史数据容错。
        DetachNodeSubscriptions();
        AttachNodeSubscriptions(EnumerateCurrentRootNodes());
        InvalidateMeasure();
        InvalidateVisual();
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
                // 子节点集合变化会影响图的横向布局，必须重新订阅并重新测量。
                DetachNodeSubscriptions();
                AttachNodeSubscriptions(EnumerateCurrentRootNodes());
                InvalidateMeasure();
                InvalidateVisual();
            };

            node.Children.CollectionChanged += handler;
            _childrenHandlers[node] = handler;
            AttachNodeSubscriptions(node.Children);
        }
    }

    private void DetachNodeSubscriptions()
    {
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

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DamageTreeNodeItem.NodeName)
            or nameof(DamageTreeNodeItem.RelationType)
            or nameof(DamageTreeNodeItem.VoteThreshold)
            or nameof(DamageTreeNodeItem.SortOrder))
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private IEnumerable<DamageTreeNodeItem> EnumerateCurrentRootNodes()
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

    private void UpdateLayoutTree()
    {
        List<LayoutNode> roots = EnumerateCurrentRootNodes()
            .OrderBy(node => node.SortOrder)
            .Select(node => BuildLayout(node, depth: 0))
            .ToList();

        if (roots.Count == 0)
        {
            _layoutRoots = Array.Empty<LayoutNode>();
            _desiredGraphSize = new Size(420d, 260d);
            return;
        }

        Dictionary<int, double> levelTops = BuildLevelTops(roots);
        double nextLeafCenterX = LeftMargin + VerticalNodeWidth / 2d;
        double nextRootLeft = LeftMargin;
        GraphBounds graphBounds = GraphBounds.Empty;

        foreach (LayoutNode root in roots)
        {
            AssignHorizontalPositions(root, ref nextLeafCenterX);
            ApplyLevelTops(root, levelTops);

            GraphBounds rootBounds = GetBounds(root);
            double offsetX = Math.Max(0d, nextRootLeft - rootBounds.Left);
            if (offsetX > 0d)
            {
                MoveLayout(root, offsetX);
                nextLeafCenterX += offsetX;
                rootBounds = GetBounds(root);
            }

            graphBounds = graphBounds.Include(rootBounds);
            nextRootLeft = rootBounds.Right + HorizontalGap;
            nextLeafCenterX = Math.Max(nextLeafCenterX, nextRootLeft + VerticalNodeWidth / 2d);
        }

        double offsetToMargin = Math.Max(0d, LeftMargin - graphBounds.Left);
        if (offsetToMargin > 0d)
        {
            foreach (LayoutNode root in roots)
            {
                MoveLayout(root, offsetToMargin);
            }

            graphBounds = GraphBounds.Empty;
            foreach (LayoutNode root in roots)
            {
                graphBounds = graphBounds.Include(GetBounds(root));
            }
        }

        _layoutRoots = roots.ToArray();
        _desiredGraphSize = new Size(
            Math.Max(420d, graphBounds.Right + RightMargin),
            Math.Max(260d, graphBounds.Bottom + BottomMargin));
    }

    private static LayoutNode BuildLayout(DamageTreeNodeItem node, int depth)
    {
        Size nodeSize = GetNodeSize(node, depth);
        LayoutNode layout = new(node, depth, nodeSize.Width, nodeSize.Height);
        foreach (DamageTreeNodeItem child in node.Children.OrderBy(child => child.SortOrder))
        {
            layout.Children.Add(BuildLayout(child, depth + 1));
        }

        return layout;
    }

    private static Dictionary<int, double> BuildLevelTops(IEnumerable<LayoutNode> roots)
    {
        Dictionary<int, double> maxHeightByDepth = new();
        foreach (LayoutNode root in roots)
        {
            CollectMaxHeightByDepth(root, maxHeightByDepth);
        }

        Dictionary<int, double> levelTops = new() { [0] = TopMargin };
        int maxDepth = maxHeightByDepth.Keys.Max();
        for (int depth = 1; depth <= maxDepth; depth++)
        {
            double previousTop = levelTops[depth - 1];
            double previousHeight = maxHeightByDepth.TryGetValue(depth - 1, out double height)
                ? height
                : VerticalNodeMinHeight;
            levelTops[depth] = previousTop + previousHeight + LevelConnectorGap;
        }

        return levelTops;
    }

    private static void CollectMaxHeightByDepth(LayoutNode layout, IDictionary<int, double> maxHeightByDepth)
    {
        if (!maxHeightByDepth.TryGetValue(layout.Depth, out double current) || layout.Height > current)
        {
            maxHeightByDepth[layout.Depth] = layout.Height;
        }

        foreach (LayoutNode child in layout.Children)
        {
            CollectMaxHeightByDepth(child, maxHeightByDepth);
        }
    }

    private static void AssignHorizontalPositions(LayoutNode layout, ref double nextLeafCenterX)
    {
        if (layout.Children.Count == 0)
        {
            layout.CenterX = nextLeafCenterX;
            nextLeafCenterX += LeafPitch;
            return;
        }

        foreach (LayoutNode child in layout.Children)
        {
            AssignHorizontalPositions(child, ref nextLeafCenterX);
        }

        // 关键：父节点放在首、末子节点中心点的中点，而不是放在整个子树矩形宽度中心。
        // 当某个子节点自身拥有更宽的子树时，这可以避免父节点被宽子树拖偏。
        layout.CenterX = (layout.Children.First().CenterX + layout.Children.Last().CenterX) / 2d;
    }

    private static void ApplyLevelTops(LayoutNode layout, IReadOnlyDictionary<int, double> levelTops)
    {
        layout.Top = levelTops.TryGetValue(layout.Depth, out double top) ? top : TopMargin;
        foreach (LayoutNode child in layout.Children)
        {
            ApplyLevelTops(child, levelTops);
        }
    }

    private static void MoveLayout(LayoutNode layout, double offsetX)
    {
        layout.CenterX += offsetX;
        foreach (LayoutNode child in layout.Children)
        {
            MoveLayout(child, offsetX);
        }
    }

    private static GraphBounds GetBounds(LayoutNode layout)
    {
        GraphBounds bounds = GraphBounds.FromRect(layout.NodeRect);

        if (layout.Children.Count > 0)
        {
            Point parentBottom = layout.BottomCenter;
            double gateCenterY = parentBottom.Y + 28d;
            double branchY = gateCenterY + 30d;
            bounds = bounds.Include(new Rect(layout.CenterX - GateSize / 2d, gateCenterY - GateSize / 2d, GateSize, GateSize));
            bounds = bounds.Include(new Point(layout.CenterX, branchY));
            bounds = bounds.Include(new Point(layout.Children.First().CenterX, branchY));
            bounds = bounds.Include(new Point(layout.Children.Last().CenterX, branchY));
        }

        foreach (LayoutNode child in layout.Children)
        {
            bounds = bounds.Include(GetBounds(child));
        }

        return bounds;
    }

    private static Size GetNodeSize(DamageTreeNodeItem node, int depth)
    {
        if (depth == 0)
        {
            return new Size(HorizontalNodeWidth, HorizontalNodeHeight);
        }

        // 非根节点使用竖排矩形，视觉上接近原毁伤树示意图中的纵向节点框。
        int verticalTextLength = Math.Max(4, node.NodeName?.Trim().Length ?? 0);
        double height = Math.Clamp(verticalTextLength * 18d + 18d, VerticalNodeMinHeight, VerticalNodeMaxHeight);
        return new Size(VerticalNodeWidth, height);
    }

    private static void DrawConnectors(DrawingContext drawingContext, LayoutNode layout)
    {
        if (layout.Children.Count == 0)
        {
            return;
        }

        Point parentBottom = layout.BottomCenter;
        Point gateCenter = new(layout.CenterX, parentBottom.Y + 28d);
        Rect gateRect = new(gateCenter.X - GateSize / 2d, gateCenter.Y - GateSize / 2d, GateSize, GateSize);
        double branchY = gateCenter.Y + 30d;

        drawingContext.DrawLine(s_linePen, parentBottom, new Point(gateCenter.X, gateRect.Top));
        drawingContext.DrawRoundedRectangle(s_gateBrush, s_gatePen, gateRect, 2d, 2d);
        DrawCenteredText(drawingContext, GetGateText(layout.Node), gateRect, s_gateTextBrush, 15d, FontWeights.SemiBold);
        drawingContext.DrawLine(s_linePen, new Point(gateCenter.X, gateRect.Bottom), new Point(gateCenter.X, branchY));

        double firstChildX = layout.Children.First().CenterX;
        double lastChildX = layout.Children.Last().CenterX;
        drawingContext.DrawLine(s_linePen, new Point(firstChildX, branchY), new Point(lastChildX, branchY));

        foreach (LayoutNode child in layout.Children)
        {
            drawingContext.DrawLine(s_linePen, new Point(child.CenterX, branchY), child.TopCenter);
            DrawConnectors(drawingContext, child);
        }
    }

    private static void DrawNodeRecursive(DrawingContext drawingContext, LayoutNode layout)
    {
        Rect nodeRect = layout.NodeRect;
        drawingContext.DrawRectangle(s_nodeBrush, s_boxPen, nodeRect);

        string nodeName = string.IsNullOrWhiteSpace(layout.Node.NodeName) ? "未命名" : layout.Node.NodeName.Trim();
        if (layout.Depth == 0)
        {
            DrawCenteredText(drawingContext, nodeName, nodeRect, s_textBrush, 15d, FontWeights.Bold);
        }
        else
        {
            DrawCenteredText(drawingContext, ToVerticalText(nodeName), nodeRect, s_textBrush, 15d, FontWeights.SemiBold);
        }

        foreach (LayoutNode child in layout.Children)
        {
            DrawNodeRecursive(drawingContext, child);
        }
    }

    private void DrawEmptyText(DrawingContext drawingContext)
    {
        Rect bounds = new(0d, 0d, Math.Max(RenderSize.Width, 420d), Math.Max(RenderSize.Height, 260d));
        DrawCenteredText(drawingContext, "当前毁伤树暂无节点", bounds, s_emptyTextBrush, 16d, FontWeights.SemiBold);
    }

    private static string GetGateText(DamageTreeNodeItem node)
    {
        return node.RelationType switch
        {
            DamageNodeRelationType.And => "+",
            DamageNodeRelationType.Or => "或",
            DamageNodeRelationType.Vote => node.VoteThreshold.ToString("0.###", CultureInfo.CurrentCulture),
            _ => "-"
        };
    }

    private static string ToVerticalText(string text)
    {
        return string.Join(Environment.NewLine, text.Where(ch => !char.IsWhiteSpace(ch)));
    }

    private static void DrawCenteredText(DrawingContext drawingContext, string text, Rect bounds, Brush brush, double fontSize, FontWeight fontWeight)
    {
        double pixelsPerDip = Application.Current?.MainWindow is Visual visual
            ? VisualTreeHelper.GetDpi(visual).PixelsPerDip
            : 1d;

        FormattedText formattedText = new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Microsoft YaHei"), FontStyles.Normal, fontWeight, FontStretches.Normal),
            fontSize,
            brush,
            pixelsPerDip)
        {
            TextAlignment = TextAlignment.Center,
            MaxTextWidth = Math.Max(1d, bounds.Width - 6d),
            MaxTextHeight = Math.Max(1d, bounds.Height - 4d),
            Trimming = TextTrimming.CharacterEllipsis
        };

        Point origin = new(bounds.Left + (bounds.Width - formattedText.Width) / 2d, bounds.Top + (bounds.Height - formattedText.Height) / 2d);
        drawingContext.DrawText(formattedText, origin);
    }

    private sealed class LayoutNode
    {
        public LayoutNode(DamageTreeNodeItem node, int depth, double width, double height)
        {
            Node = node;
            Depth = depth;
            Width = width;
            Height = height;
        }

        public DamageTreeNodeItem Node { get; }

        public int Depth { get; }

        public double Width { get; }

        public double Height { get; }

        public double CenterX { get; set; }

        public double Top { get; set; }

        public List<LayoutNode> Children { get; } = new();

        public Rect NodeRect => new(CenterX - Width / 2d, Top, Width, Height);

        public Point TopCenter => new(CenterX, Top);

        public Point BottomCenter => new(CenterX, Top + Height);
    }

    private readonly record struct GraphBounds(double Left, double Top, double Right, double Bottom)
    {
        public static GraphBounds Empty => new(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

        public static GraphBounds FromRect(Rect rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

        public GraphBounds Include(GraphBounds other)
        {
            if (double.IsPositiveInfinity(Left))
            {
                return other;
            }

            if (double.IsPositiveInfinity(other.Left))
            {
                return this;
            }

            return new GraphBounds(
                Math.Min(Left, other.Left),
                Math.Min(Top, other.Top),
                Math.Max(Right, other.Right),
                Math.Max(Bottom, other.Bottom));
        }

        public GraphBounds Include(Rect rect) => Include(FromRect(rect));

        public GraphBounds Include(Point point)
        {
            GraphBounds pointBounds = new(point.X, point.Y, point.X, point.Y);
            return Include(pointBounds);
        }
    }
}
