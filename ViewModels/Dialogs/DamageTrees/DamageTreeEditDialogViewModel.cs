using CombatSimulation.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CombatSimulation.ViewModels;

/// <summary>
/// 毁伤树基础信息添加/修改弹窗 ViewModel。
/// </summary>
public sealed partial class DamageTreeEditDialogViewModel : ObservableObject, IDialogRequestClose
{
    public DamageTreeEditDialogViewModel(DamageTreeInfoItem tree, string targetName, bool isEditMode)
    {
        EditedTree = CloneTree(tree);
        DialogTitle = isEditMode ? "修改毁伤树信息" : "添加毁伤树信息";
        TargetDisplayName = targetName;
        DamageTreeName = EditedTree.DamageTreeName;
        DamageLevelInfo = DamageTreeDefaults.NormalizeDamageLevel(EditedTree.DamageLevelInfo);
        // 当前业务只维护功能毁伤树，弹窗中固定展示该默认类型，避免用户录入“整体毁伤树”等旧值后破坏轻/中/重唯一性。
        DamageTreeType = DamageTreeDefaults.DefaultTreeType;
        DamageTreeDescription = EditedTree.DamageTreeDescription;
        RootNodeName = EditedTree.RootNodes.FirstOrDefault()?.NodeName ?? EditedTree.DamageTreeName;
        RootNodeDescription = EditedTree.RootNodes.FirstOrDefault()?.NodeDescription ?? string.Empty;
    }

    public event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;

    /// <summary>
    /// 毁伤等级固定为轻度、中度、重度三档；新增或修改时只能从这里选择。
    /// </summary>
    public IReadOnlyList<string> DamageLevelOptions => DamageTreeDefaults.DamageLevels;

    /// <summary>
    /// 保存成功后由调用方读取的毁伤树结果。
    /// </summary>
    public DamageTreeInfoItem EditedTree { get; private set; }

    [ObservableProperty]
    private string _dialogTitle = "毁伤树信息设置";

    [ObservableProperty]
    private string _targetDisplayName = string.Empty;

    [ObservableProperty]
    private string _damageTreeName = string.Empty;

    [ObservableProperty]
    private string _damageLevelInfo = string.Empty;

    [ObservableProperty]
    private string _damageTreeType = string.Empty;

    [ObservableProperty]
    private string _damageTreeDescription = string.Empty;

    [ObservableProperty]
    private string _rootNodeName = string.Empty;

    [ObservableProperty]
    private string _rootNodeDescription = string.Empty;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    /// <summary>
    /// 校验并保存毁伤树输入内容。
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        string treeName = DamageTreeName.Trim();
        string damageLevel = DamageTreeDefaults.NormalizeDamageLevel(DamageLevelInfo);
        string treeType = DamageTreeDefaults.DefaultTreeType;
        string rootName = RootNodeName.Trim();

        if (string.IsNullOrWhiteSpace(treeName))
        {
            ValidationMessage = "请输入毁伤树名称。";
            return;
        }

        if (!DamageTreeDefaults.IsKnownDamageLevel(damageLevel))
        {
            ValidationMessage = "毁伤等级只能选择轻度毁伤、中度毁伤或重度毁伤。";
            return;
        }

        if (string.IsNullOrWhiteSpace(rootName))
        {
            ValidationMessage = "请输入根节点名称。";
            return;
        }

        EditedTree.DamageTreeName = treeName;
        EditedTree.DamageLevelInfo = damageLevel;
        EditedTree.DamageTreeType = treeType;
        EditedTree.DamageTreeDescription = DamageTreeDescription.Trim();

        DamageTreeNodeItem root = EnsureRootNode(EditedTree);
        root.NodeName = rootName;
        root.NodeDescription = RootNodeDescription.Trim();
        root.RelationType = root.IsLeafNode ? DamageNodeRelationType.And : root.RelationType;

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

    private static DamageTreeInfoItem CloneTree(DamageTreeInfoItem source)
    {
        DamageTreeInfoItem clone = new()
        {
            DamageTreeCode = source.DamageTreeCode,
            DamageTreeName = source.DamageTreeName,
            DamageTreeDescription = source.DamageTreeDescription,
            TargetCode = source.TargetCode,
            DamageLevelInfo = source.DamageLevelInfo,
            DamageTreeType = source.DamageTreeType
        };

        foreach (DamageTreeNodeItem rootNode in source.RootNodes)
        {
            clone.RootNodes.Add(rootNode.CloneShallow());
        }

        EnsureRootNode(clone);
        return clone;
    }

    private static DamageTreeNodeItem EnsureRootNode(DamageTreeInfoItem tree)
    {
        if (tree.RootNodes.Count == 0)
        {
            tree.RootNodes.Add(new DamageTreeNodeItem
            {
                NodeCode = Guid.NewGuid().ToString("N"),
                DamageTreeCode = tree.DamageTreeCode,
                ParentNodeCode = string.Empty,
                RelationType = DamageNodeRelationType.And,
                VoteThreshold = 1,
                SortOrder = 0,
                IsExpanded = true
            });
        }

        return tree.RootNodes[0];
    }
}
