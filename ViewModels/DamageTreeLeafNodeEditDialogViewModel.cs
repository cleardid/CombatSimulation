using CombatSimulation.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CombatSimulation.ViewModels;

/// <summary>
/// 毁伤树叶子节点添加/修改弹窗 ViewModel。
/// </summary>
public sealed partial class DamageTreeLeafNodeEditDialogViewModel : ObservableObject
{
    public DamageTreeLeafNodeEditDialogViewModel(DamageTreeNodeItem node, IEnumerable<TargetStructureTreeNode> targetStructureRoots, bool isEditMode)
    {
        EditedNode = node.CloneShallow();
        EditedNode.RelationType = DamageNodeRelationType.None;
        DialogTitle = isEditMode ? "修改毁伤树叶子节点" : "添加毁伤树叶子节点";
        NodeName = EditedNode.NodeName;
        NodeDescription = EditedNode.NodeDescription;

        foreach (TargetStructureTreeNode root in targetStructureRoots)
        {
            TargetStructureRoots.Add(root);
        }

        SelectedStructureNode = FindStructureNodeByPartCode(TargetStructureRoots, EditedNode.PartCode);
    }

    public event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;

    /// <summary>
    /// 目标结构树根节点集合，用于让用户选择底层部件。
    /// </summary>
    public ObservableCollection<TargetStructureTreeNode> TargetStructureRoots { get; } = new();

    /// <summary>
    /// 保存成功后由调用方读取的叶子节点结果。
    /// </summary>
    public DamageTreeNodeItem EditedNode { get; private set; }

    [ObservableProperty]
    private string _dialogTitle = "毁伤树叶子节点设置";

    [ObservableProperty]
    private string _nodeName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPartDisplayText))]
    private TargetStructureTreeNode? _selectedStructureNode;

    [ObservableProperty]
    private string _nodeDescription = string.Empty;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    /// <summary>
    /// 当前选中的部件显示文本。
    /// </summary>
    public string SelectedPartDisplayText
    {
        get
        {
            if (SelectedStructureNode?.Part == null)
            {
                return "未选择底层部件";
            }

            return $"{SelectedStructureNode.Part.PartName}（{SelectedStructureNode.Part.PartCode}）";
        }
    }

    /// <summary>
    /// 校验并保存叶子节点输入内容。
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

        if (SelectedStructureNode?.Part == null)
        {
            ValidationMessage = "请选择目标结构树中的底层部件，不能选择目标、系统或子系统。";
            return;
        }

        TargetPartInfoItem selectedPart = SelectedStructureNode.Part;
        EditedNode.NodeName = nodeName;
        EditedNode.RelationType = DamageNodeRelationType.None;
        EditedNode.PartCode = selectedPart.PartCode;
        EditedNode.PartName = selectedPart.PartName;
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

    private static TargetStructureTreeNode? FindStructureNodeByPartCode(IEnumerable<TargetStructureTreeNode> nodes, string partCode)
    {
        if (string.IsNullOrWhiteSpace(partCode))
        {
            return null;
        }

        foreach (TargetStructureTreeNode node in nodes)
        {
            if (node.Part != null && string.Equals(node.Part.PartCode, partCode, StringComparison.Ordinal))
            {
                return node;
            }

            TargetStructureTreeNode? child = FindStructureNodeByPartCode(node.Children, partCode);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }
}
