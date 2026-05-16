using CombatSimulation.Controls;
using CombatSimulation.Models;
using CombatSimulation.ViewModels;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
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

    private void OnDamageTreeShowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 预览区尺寸变化会影响自适应/滚动绘制方式，因此需要强制重绘。
        QueueShowTree(refresh: true);
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
    private void ShowTree(bool? zoom = null, bool refresh = true)
    {
        if (DamageTreeShow == null || DamageTreeShow.ActualWidth <= 0 || DamageTreeShow.ActualHeight <= 0)
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

        Grid drawGrid = DamageTreeShow;
        if (zoom == null)
        {
            zoom = columns < 20 && rows.Count < 10;
        }

        if (zoom == true)
        {
            BuildFitGrid(drawGrid, columns, rows, ref unitWidth, ref unitHeight);
        }
        else
        {
            // 大树采用 ScrollViewer，避免为了强行压缩而导致事件框和逻辑门不可读。
            ScrollViewer scrollViewer = new()
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                // 当树宽度小于可视区时让内容居中；树过宽时仍保持正常横向滚动。
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Top
            };
            DamageTreeShow.Children.Add(scrollViewer);

            drawGrid = new Grid
            {
                // ScrollViewer 的 HorizontalContentAlignment 在部分模板下不会作用到内容，显式设置可保证小型毁伤树在滚动模式下仍居中。
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };
            scrollViewer.Content = drawGrid;
            BuildScrollableGrid(drawGrid, columns, rows, unitWidth, unitHeight);
        }

        DrawPreviewNodes(drawGrid, infos, color, unitWidth, columns);
        DrawPreviewLines(drawGrid, lineInfos, color);
    }

    private void ClearDamageTreeShow()
    {
        DamageTreeShow.Children.Clear();
        DamageTreeShow.RowDefinitions.Clear();
        DamageTreeShow.ColumnDefinitions.Clear();
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

    private void BuildFitGrid(Grid grid, int columns, IReadOnlyList<double> rows, ref double unitWidth, ref double unitHeight)
    {
        foreach (int _ in Enumerable.Range(0, columns))
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        }

        if (DamageTreeShow.ActualWidth / columns < unitWidth)
        {
            unitWidth = DamageTreeShow.ActualWidth / columns;
        }

        if (DamageTreeShow.ActualHeight / rows.Sum() < unitHeight)
        {
            unitHeight = DamageTreeShow.ActualHeight / rows.Sum();
        }

        if (unitWidth > unitHeight)
        {
            unitWidth = unitHeight;
        }
        else if (unitWidth < unitHeight)
        {
            unitHeight = unitWidth;
        }

        foreach (double row in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(row * unitHeight, GridUnitType.Pixel) });
        }
    }

    private static void BuildScrollableGrid(Grid grid, int columns, IEnumerable<double> rows, double unitWidth, double unitHeight)
    {
        foreach (int _ in Enumerable.Range(0, columns))
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(unitWidth, GridUnitType.Pixel) });
        }

        foreach (double row in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(row * unitHeight, GridUnitType.Pixel) });
        }
    }

    private static void DrawPreviewNodes(Grid grid, IReadOnlyDictionary<int, List<DamageTreeLayerInfo>> infos, Color color, double unitWidth, int totalColumns)
    {
        foreach (KeyValuePair<int, List<DamageTreeLayerInfo>> item in infos.OrderBy(item => item.Key))
        {
            foreach (DamageTreeLayerInfo info in item.Value)
            {
                bool isRootLayer = item.Key == 1;
                EventRectangle rectangle = new()
                {
                    Color = color,
                    Type = info.HaveUp ? (info.NextCount > 0 ? EventType.Middle : EventType.End) : EventType.Top,
                    Script = info.Content,
                    Vertical = !isRootLayer,
                    Width = isRootLayer ? (info.Content.Length + 2d) * unitWidth : unitWidth,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                bool shouldCenterSingleRoot = isRootLayer && item.Value.Count == 1;
                int column = shouldCenterSingleRoot ? 0 : info.Start * 2 + 1;
                int columnSpan = shouldCenterSingleRoot ? Math.Max(1, totalColumns) : info.Include * 2;

                grid.Children.Add(rectangle);
                Grid.SetRow(rectangle, isRootLayer ? 1 : item.Key * 3 - 2);
                Grid.SetColumn(rectangle, column);
                Grid.SetColumnSpan(rectangle, columnSpan);

                if (info.NextCount > 0)
                {
                    GateIcon icon = new()
                    {
                        Color = color,
                        Type = info.NextCount < 2 ? -1 : info.Type,
                        Rate = info.Ratio,
                        Width = unitWidth,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    grid.Children.Add(icon);
                    Grid.SetRow(icon, isRootLayer ? 2 : item.Key * 3 - 1);
                    Grid.SetColumn(icon, column);
                    Grid.SetColumnSpan(icon, columnSpan);
                }
            }
        }
    }

    private static void DrawPreviewLines(Grid grid, IReadOnlyDictionary<int, List<DamageTreeLayerLineInfo>> lineInfos, Color color)
    {
        foreach (KeyValuePair<int, List<DamageTreeLayerLineInfo>> item in lineInfos.OrderBy(item => item.Key))
        {
            foreach (DamageTreeLayerLineInfo info in item.Value)
            {
                ConnectLine line = new()
                {
                    Color = color,
                    Unit = info.Unit,
                    Outs = info.Outs.ToArray()
                };

                grid.Children.Add(line);
                Grid.SetRow(line, item.Key * 3);
                Grid.SetColumn(line, info.Start * 2 + 1);
                Grid.SetColumnSpan(line, info.Unit * 2);
            }
        }
    }

    private static double PreviewUnitSize(double value) => value;
}
