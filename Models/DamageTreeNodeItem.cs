using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace CombatSimulation.Models;

/// <summary>
/// 毁伤树节点模型，对应毁伤节点表中的一条记录。
/// </summary>
public sealed partial class DamageTreeNodeItem : ObservableObject
{
    public DamageTreeNodeItem()
    {
        Children.CollectionChanged += OnChildrenCollectionChanged;
    }

    /// <summary>
    /// 节点唯一标识。数据库主键，统一使用 GUID 字符串。
    /// </summary>
    [ObservableProperty]
    private string _nodeCode = string.Empty;

    /// <summary>
    /// 当前节点所属毁伤树唯一标识。
    /// </summary>
    [ObservableProperty]
    private string _damageTreeCode = string.Empty;

    /// <summary>
    /// 毁伤节点名称，例如“火控系统毁伤”“弹药舱毁伤”。
    /// </summary>
    [ObservableProperty]
    private string _nodeName = string.Empty;

    /// <summary>
    /// 父节点唯一标识。根节点使用空字符串。
    /// </summary>
    [ObservableProperty]
    private string _parentNodeCode = string.Empty;

    /// <summary>
    /// 当前节点与子节点之间的关系。叶子节点固定为 None。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLeafNode))]
    [NotifyPropertyChangedFor(nameof(IsMiddleNode))]
    [NotifyPropertyChangedFor(nameof(RelationDisplayText))]
    [NotifyPropertyChangedFor(nameof(CanAddChild))]
    private DamageNodeRelationType _relationType = DamageNodeRelationType.And;

    /// <summary>
    /// 表决门阈值。只有 RelationType=Vote 时有效。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RelationDisplayText))]
    private int _voteThreshold = 1;

    /// <summary>
    /// 叶子节点绑定的部件唯一标识。中间节点保持为空。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PartDisplayText))]
    private string _partCode = string.Empty;

    /// <summary>
    /// 叶子节点绑定的部件名称。该字段不入库，只由 WPF 根据目标结构表补充显示。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PartDisplayText))]
    private string _partName = string.Empty;

    /// <summary>
    /// 节点描述，可为空。
    /// </summary>
    [ObservableProperty]
    private string _nodeDescription = string.Empty;

    /// <summary>
    /// 同一父节点下的显示顺序。
    /// </summary>
    [ObservableProperty]
    private int _sortOrder;

    /// <summary>
    /// TreeView 展开状态。
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded = true;

    /// <summary>
    /// TreeView 选中状态。
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// 当前节点的父节点。该属性不入库，只用于 UI 树形导航和增删改定位。
    /// </summary>
    public DamageTreeNodeItem? Parent { get; private set; }

    /// <summary>
    /// 子节点集合。叶子节点应保持为空。
    /// </summary>
    public ObservableCollection<DamageTreeNodeItem> Children { get; } = new();

    /// <summary>
    /// 当前节点是否为叶子节点。
    /// </summary>
    public bool IsLeafNode => RelationType == DamageNodeRelationType.None;

    /// <summary>
    /// 当前节点是否为中间节点或根节点。
    /// </summary>
    public bool IsMiddleNode => !IsLeafNode;

    /// <summary>
    /// 当前节点是否允许新增子节点。
    /// </summary>
    public bool CanAddChild => IsMiddleNode;

    /// <summary>
    /// 逻辑关系显示文本。
    /// </summary>
    public string RelationDisplayText => RelationType switch
    {
        DamageNodeRelationType.And => "与门",
        DamageNodeRelationType.Or => "或门",
        DamageNodeRelationType.Vote => $"表决门 ≥ {VoteThreshold}",
        _ => "叶子节点"
    };

    /// <summary>
    /// 叶子节点绑定部件的显示文本。
    /// </summary>
    public string PartDisplayText
    {
        get
        {
            if (!IsLeafNode)
            {
                return "-";
            }

            if (!string.IsNullOrWhiteSpace(PartName))
            {
                return $"{PartName}（{PartCode}）";
            }

            return string.IsNullOrWhiteSpace(PartCode) ? "未绑定部件" : PartCode;
        }
    }

    /// <summary>
    /// 子集合变化时维护 Parent 引用。
    /// </summary>
    private void OnChildrenCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (DamageTreeNodeItem child in e.OldItems.OfType<DamageTreeNodeItem>())
            {
                if (ReferenceEquals(child.Parent, this))
                {
                    child.Parent = null;
                }
            }
        }

        if (e.NewItems != null)
        {
            foreach (DamageTreeNodeItem child in e.NewItems.OfType<DamageTreeNodeItem>())
            {
                child.Parent = this;
                child.ParentNodeCode = NodeCode;
                child.DamageTreeCode = DamageTreeCode;
            }
        }
    }

    /// <summary>
    /// 根据节点类型创建显示用副本，避免弹窗直接修改 TreeView 正在绑定的对象。
    /// </summary>
    public DamageTreeNodeItem CloneShallow()
    {
        return new DamageTreeNodeItem
        {
            NodeCode = NodeCode,
            DamageTreeCode = DamageTreeCode,
            NodeName = NodeName,
            ParentNodeCode = ParentNodeCode,
            RelationType = RelationType,
            VoteThreshold = VoteThreshold,
            PartCode = PartCode,
            PartName = PartName,
            NodeDescription = NodeDescription,
            SortOrder = SortOrder,
            IsExpanded = IsExpanded,
            IsSelected = IsSelected
        };
    }
}
