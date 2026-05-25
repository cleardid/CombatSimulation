using CombatSimulation.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CombatSimulation.Controls;

/// <summary>
/// 毁伤树预览节点矩形的连线类型。
/// </summary>
public enum EventType
{
    Top,
    Middle,
    End
}

/// <summary>
/// 毁伤树预览中的事件矩形。
/// </summary>
/// <remarks>
/// 该控件用于还原旧版预览区中“事件框”的绘制方式。根节点横排显示，非根节点通常由调用方设置为竖排显示。
/// </remarks>
public sealed class EventRectangle : FrameworkElement
{
    private static readonly Brush s_fillBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
    private static readonly Brush s_textBrush = Brushes.White;

    public Color Color { get; set; } = Colors.White;

    public EventType Type { get; set; } = EventType.Top;

    public string Script { get; set; } = string.Empty;

    public bool Vertical { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsNaN(Width) ? 24d : Width;
        return new Size(width, availableSize.Height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        Pen pen = CreatePen(Color);
        Rect rect = new(0.5d, 0.5d, Math.Max(0d, ActualWidth - 1d), Math.Max(0d, ActualHeight - 1d));
        drawingContext.DrawRectangle(s_fillBrush, pen, rect);

        string text = string.IsNullOrWhiteSpace(Script) ? "未命名" : Script.Trim();
        DrawCenteredText(drawingContext, Vertical ? ToVerticalText(text) : text, rect, s_textBrush, 15d, FontWeights.SemiBold);
    }

    private static string ToVerticalText(string text)
    {
        return string.Join(Environment.NewLine, text.Where(ch => !char.IsWhiteSpace(ch)));
    }

    internal static Pen CreatePen(Color color)
    {
        Pen pen = new(new SolidColorBrush(color), 1d);
        pen.Freeze();
        return pen;
    }

    internal static void DrawCenteredText(DrawingContext drawingContext, string text, Rect bounds, Brush brush, double fontSize, FontWeight fontWeight)
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

        // TextAlignment.Center 已经会在 MaxTextWidth 内水平居中；X 坐标固定到矩形内边距，避免二次偏移导致根节点文字靠右。
        Point origin = new(
            bounds.Left + 3d,
            bounds.Top + (bounds.Height - formattedText.Height) / 2d);
        drawingContext.DrawText(formattedText, origin);
    }
}

/// <summary>
/// 毁伤树预览中的逻辑门图标。
/// </summary>
/// <remarks>
/// Type=-1 表示单子节点透传关系，只画竖向连线，不显示逻辑门符号；其他值与 DamageNodeRelationType 枚举值保持一致。
/// </remarks>
public sealed class GateIcon : FrameworkElement
{
    private static readonly Brush s_fillBrush = new SolidColorBrush(Color.FromArgb(35, 15, 23, 42));
    private static readonly Brush s_textBrush = new SolidColorBrush(Color.FromRgb(246, 232, 74));

    public Color Color { get; set; } = Colors.White;

    public int Type { get; set; }

    public float Rate { get; set; } = 1f;

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsNaN(Width) ? 24d : Width;
        return new Size(width, availableSize.Height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        Pen pen = EventRectangle.CreatePen(Color);
        double centerX = ActualWidth / 2d;
        double centerY = ActualHeight / 2d;

        if (Type < 0)
        {
            drawingContext.DrawLine(pen, new Point(centerX, 0d), new Point(centerX, ActualHeight));
            return;
        }

        double iconSize = Math.Min(Math.Min(ActualWidth, ActualHeight) - 2d, 24d);
        iconSize = Math.Max(12d, iconSize);
        Rect iconRect = new(centerX - iconSize / 2d, centerY - iconSize / 2d, iconSize, iconSize);

        drawingContext.DrawLine(pen, new Point(centerX, 0d), new Point(centerX, iconRect.Top));
        drawingContext.DrawRoundedRectangle(s_fillBrush, pen, iconRect, 2d, 2d);
        EventRectangle.DrawCenteredText(drawingContext, GetGateText(), iconRect, s_textBrush, 13d, FontWeights.SemiBold);
        drawingContext.DrawLine(pen, new Point(centerX, iconRect.Bottom), new Point(centerX, ActualHeight));
    }

    private string GetGateText()
    {
        return (DamageNodeRelationType)Type switch
        {
            DamageNodeRelationType.And => "+",
            DamageNodeRelationType.Or => "或",
            DamageNodeRelationType.Vote => Rate.ToString("0.###", CultureInfo.CurrentCulture),
            _ => "-"
        };
    }
}

/// <summary>
/// 毁伤树预览中的父子连接线。
/// </summary>
/// <remarks>
/// Unit 表示当前连线横跨的叶子单元数量；Outs 表示各子节点中心点相对于当前子树起点的单元偏移。
/// </remarks>
public sealed class ConnectLine : FrameworkElement
{
    public Color Color { get; set; } = Colors.White;

    public int Unit { get; set; } = 1;

    /// <summary>
    /// 父节点中心点在当前连线区域内的相对位置，取值范围通常为 0~1。
    /// 旧实现固定使用 0.5，会导致宽度不一致的子树下父节点略微偏移。
    /// </summary>
    public double ParentOffset { get; set; } = 0.5d;

    public double[] Outs { get; set; } = Array.Empty<double>();

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (ActualWidth <= 0 || ActualHeight <= 0 || Unit <= 0 || Outs.Length == 0)
        {
            return;
        }

        Pen pen = EventRectangle.CreatePen(Color);
        double parentX = Math.Clamp(ParentOffset, 0d, 1d) * ActualWidth;
        double branchY = Math.Max(1d, ActualHeight * 0.42d);
        double[] childXs = Outs.Select(offset => Math.Clamp(offset / Unit, 0d, 1d) * ActualWidth).ToArray();

        drawingContext.DrawLine(pen, new Point(parentX, 0d), new Point(parentX, branchY));

        if (childXs.Length == 1)
        {
            drawingContext.DrawLine(pen, new Point(parentX, branchY), new Point(childXs[0], branchY));
            drawingContext.DrawLine(pen, new Point(childXs[0], branchY), new Point(childXs[0], ActualHeight));
            return;
        }

        double minX = childXs.Min();
        double maxX = childXs.Max();
        drawingContext.DrawLine(pen, new Point(minX, branchY), new Point(maxX, branchY));
        foreach (double childX in childXs)
        {
            drawingContext.DrawLine(pen, new Point(childX, branchY), new Point(childX, ActualHeight));
        }
    }
}

/// <summary>
/// 将 DamageTreeNodeItem 树转换为按层绘制所需的信息。
/// </summary>
internal static class DamageTreeLayerLayout
{
    public static void Convert(
        int start,
        IEnumerable<DamageTreeNodeItem> roots,
        IDictionary<int, List<DamageTreeLayerInfo>> layerInfos,
        IDictionary<int, List<DamageTreeLayerLineInfo>> lineInfos)
    {
        int cursor = start;
        foreach (DamageTreeNodeItem root in roots)
        {
            Build(root, layer: 1, ref cursor, layerInfos, lineInfos);
        }
    }

    private static DamageTreeNodeLayout Build(
        DamageTreeNodeItem node,
        int layer,
        ref int cursor,
        IDictionary<int, List<DamageTreeLayerInfo>> layerInfos,
        IDictionary<int, List<DamageTreeLayerLineInfo>> lineInfos)
    {
        List<DamageTreeNodeLayout> childLayouts = new();
        foreach (DamageTreeNodeItem child in node.Children.OrderBy(child => child.SortOrder))
        {
            childLayouts.Add(Build(child, layer + 1, ref cursor, layerInfos, lineInfos));
        }

        int nodeStart;
        int include;
        double center;
        if (childLayouts.Count == 0)
        {
            nodeStart = cursor++;
            include = 1;
            center = nodeStart + 0.5d;
        }
        else
        {
            nodeStart = childLayouts.Min(child => child.Start);
            int nodeEnd = childLayouts.Max(child => child.Start + child.Include);
            include = Math.Max(1, nodeEnd - nodeStart);

            // 父节点横向位置取首个子节点中心与末个子节点中心的中点。
            // 这样在左右两侧子树宽度不同的情况下，父节点仍然位于其直接子节点组的视觉中心。
            center = (childLayouts.First().Center + childLayouts.Last().Center) / 2d;
        }

        AddLayerInfo(layerInfos, layer, new DamageTreeLayerInfo
        {
            Start = nodeStart,
            Include = Math.Max(1, include),
            Center = center,
            Content = string.IsNullOrWhiteSpace(node.NodeName) ? "未命名" : node.NodeName.Trim(),
            HaveUp = layer > 1,
            NextCount = childLayouts.Count,
            Type = (int)node.RelationType,
            Ratio = node.VoteThreshold
        });

        if (childLayouts.Count > 0)
        {
            AddLineInfo(lineInfos, layer, new DamageTreeLayerLineInfo
            {
                Start = nodeStart,
                Unit = Math.Max(1, include),
                ParentCenter = center,
                Outs = childLayouts
                    .Select(child => child.Center - nodeStart)
                    .ToList()
            });
        }

        return new DamageTreeNodeLayout(nodeStart, Math.Max(1, include), center);
    }

    private static void AddLayerInfo(IDictionary<int, List<DamageTreeLayerInfo>> layerInfos, int layer, DamageTreeLayerInfo info)
    {
        if (!layerInfos.TryGetValue(layer, out List<DamageTreeLayerInfo>? values))
        {
            values = new List<DamageTreeLayerInfo>();
            layerInfos[layer] = values;
        }

        values.Add(info);
    }

    private static void AddLineInfo(IDictionary<int, List<DamageTreeLayerLineInfo>> lineInfos, int layer, DamageTreeLayerLineInfo info)
    {
        if (!lineInfos.TryGetValue(layer, out List<DamageTreeLayerLineInfo>? values))
        {
            values = new List<DamageTreeLayerLineInfo>();
            lineInfos[layer] = values;
        }

        values.Add(info);
    }

    private sealed record DamageTreeNodeLayout(int Start, int Include, double Center);
}

internal sealed class DamageTreeLayerInfo
{
    public int Start { get; init; }

    public int Include { get; init; }

    /// <summary>
    /// 节点中心点，单位为叶子节点跨度。
    /// </summary>
    public double Center { get; init; }

    public string Content { get; init; } = string.Empty;

    public bool HaveUp { get; init; }

    public int NextCount { get; init; }

    public int Type { get; init; }

    public float Ratio { get; init; }
}

internal sealed class DamageTreeLayerLineInfo
{
    public int Start { get; init; }

    public int Unit { get; init; }

    /// <summary>
    /// 父节点中心点，单位为叶子节点跨度。
    /// </summary>
    public double ParentCenter { get; init; }

    public List<double> Outs { get; init; } = new();
}
