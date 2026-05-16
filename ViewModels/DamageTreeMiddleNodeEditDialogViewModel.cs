using CombatSimulation.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CombatSimulation.ViewModels;

/// <summary>
/// 毁伤树中间节点添加/修改弹窗 ViewModel。
/// </summary>
public sealed partial class DamageTreeMiddleNodeEditDialogViewModel : ObservableObject
{
    public DamageTreeMiddleNodeEditDialogViewModel(DamageTreeNodeItem node, bool isEditMode)
    {
        EditedNode = node.CloneShallow();
        if (EditedNode.IsLeafNode)
        {
            EditedNode.RelationType = DamageNodeRelationType.And;
        }

        DialogTitle = isEditMode ? "修改毁伤树中间节点" : "添加毁伤树中间节点";
        NodeName = EditedNode.NodeName;
        SelectedRelationOption = RelationOptions.FirstOrDefault(item => item.RelationType == EditedNode.RelationType) ?? RelationOptions[0];
        VoteThreshold = Math.Max(1f, EditedNode.VoteThreshold);
        NodeDescription = EditedNode.NodeDescription;
    }

    public event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;

    /// <summary>
    /// 可选逻辑关系列表。
    /// </summary>
    public ObservableCollection<DamageRelationOption> RelationOptions { get; } = new()
    {
        new DamageRelationOption("与门", DamageNodeRelationType.And),
        new DamageRelationOption("或门", DamageNodeRelationType.Or),
        new DamageRelationOption("表决门", DamageNodeRelationType.Vote)
    };

    /// <summary>
    /// 保存成功后由调用方读取的中间节点结果。
    /// </summary>
    public DamageTreeNodeItem EditedNode { get; private set; }

    [ObservableProperty]
    private string _dialogTitle = "毁伤树中间节点设置";

    [ObservableProperty]
    private string _nodeName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVoteRelation))]
    private DamageRelationOption? _selectedRelationOption;

    [ObservableProperty]
    private float _voteThreshold = 1f;

    [ObservableProperty]
    private string _nodeDescription = string.Empty;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    /// <summary>
    /// 当前是否选中了表决门。
    /// </summary>
    public bool IsVoteRelation => SelectedRelationOption?.RelationType == DamageNodeRelationType.Vote;

    /// <summary>
    /// 校验并保存中间节点输入内容。
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        string nodeName = NodeName.Trim();
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            ValidationMessage = "请输入节点名称。";
            return;
        }

        DamageNodeRelationType relationType = SelectedRelationOption?.RelationType ?? DamageNodeRelationType.And;
        if (relationType == DamageNodeRelationType.Vote && VoteThreshold < 1f)
        {
            ValidationMessage = "表决门阈值必须大于或等于 1。";
            return;
        }

        EditedNode.NodeName = nodeName;
        EditedNode.RelationType = relationType;
        EditedNode.VoteThreshold = relationType == DamageNodeRelationType.Vote ? VoteThreshold : 1;
        EditedNode.PartCode = string.Empty;
        EditedNode.PartName = string.Empty;
        EditedNode.NodeDescription = NodeDescription.Trim();
        ValidationMessage = string.Empty;

        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(dialogResult: true));
    }

    /// <summary>
    /// 取消编辑。
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(dialogResult: false));
    }
}

/// <summary>
/// 中间节点逻辑关系下拉选项。
/// </summary>
public sealed record DamageRelationOption(string DisplayName, DamageNodeRelationType RelationType)
{
    /// <summary>
    /// ComboBox 使用自定义模板时会读取 SelectionBoxItem；重写 ToString 可确保选中项仍显示“与门/或门/表决门”，而不是 record 的完整类型文本。
    /// </summary>
    public override string ToString() => DisplayName;
}
