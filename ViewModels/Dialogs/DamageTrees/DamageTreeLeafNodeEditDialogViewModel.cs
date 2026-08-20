using CombatSimulation.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CombatSimulation.ViewModels;

/// <summary>
/// 毁伤树叶子节点添加/修改弹窗 ViewModel。
/// </summary>
public sealed partial class DamageTreeLeafNodeEditDialogViewModel : ObservableObject, IDialogRequestClose, IDialogRequestMessage
{
    private bool _suppressPartSelectionNameSync;

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

        // 修改模式打开时需要保留原节点名称；只有用户后续重新选择部件时才同步为部件名称。
        _suppressPartSelectionNameSync = true;
        SelectedStructureNode = FindStructureNodeByPartCode(TargetStructureRoots, EditedNode.PartCode);
        _suppressPartSelectionNameSync = false;
    }

    public event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;
    public event EventHandler<DialogMessageRequestedEventArgs>? MessageRequested;

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

    partial void OnSelectedStructureNodeChanged(TargetStructureTreeNode? value)
    {
        if (_suppressPartSelectionNameSync || value?.Part == null)
        {
            return;
        }

        // 叶子节点表示某个部件的毁伤事件，因此选择部件后默认生成“部件名称 + 毁伤”作为节点名。
        string partName = value.Part.PartName.Trim();

        // 避免重复追加“毁伤”。
        NodeName = partName.EndsWith("毁伤", StringComparison.Ordinal)
            ? partName
            : $"{partName}毁伤";
    }

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
            ShowValidationMessage("请输入节点名称。");
            return;
        }

        if (SelectedStructureNode?.Part == null)
        {
            ShowValidationMessage("请选择目标结构树中的底层部件，不能选择目标、系统或子系统。");
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

    private void ShowValidationMessage(string message)
    {
        ValidationMessage = message;
        MessageRequested?.Invoke(this, new DialogMessageRequestedEventArgs(message, DialogTitle));
    }
}
