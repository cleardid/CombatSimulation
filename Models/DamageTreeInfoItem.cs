using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace CombatSimulation.Models;

/// <summary>
/// 毁伤树信息模型，对应毁伤树主表中的一条记录。
/// </summary>
public sealed partial class DamageTreeInfoItem : ObservableObject
{
    /// <summary>
    /// 毁伤树唯一标识。数据库主键，统一使用 GUID 字符串。
    /// </summary>
    [ObservableProperty]
    private string _damageTreeCode = string.Empty;

    /// <summary>
    /// 毁伤树名称。该字段不是算法必需项，但用于下拉列表和弹窗标题显示。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _damageTreeName = string.Empty;

    /// <summary>
    /// 毁伤树描述，可为空。
    /// </summary>
    [ObservableProperty]
    private string _damageTreeDescription = string.Empty;

    /// <summary>
    /// 当前毁伤树所属目标的唯一标识。
    /// </summary>
    [ObservableProperty]
    private string _targetCode = string.Empty;

    /// <summary>
    /// 毁伤等级信息，例如轻度毁伤、中度毁伤、重度毁伤。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _damageLevelInfo = string.Empty;

    /// <summary>
    /// 毁伤树类型，例如整体毁伤树、功能毁伤树、任务毁伤树。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _damageTreeType = string.Empty;

    /// <summary>
    /// 当前毁伤树的根节点集合。正常业务下通常只有一个根节点。
    /// 使用集合是为了容错显示历史数据中可能出现的多个根节点。
    /// </summary>
    public ObservableCollection<DamageTreeNodeItem> RootNodes { get; } = new();

    /// <summary>
    /// 供列表显示使用的名称。
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(DamageTreeName))
            {
                return DamageTreeName;
            }

            string level = string.IsNullOrWhiteSpace(DamageLevelInfo) ? "未设置等级" : DamageLevelInfo;
            string type = string.IsNullOrWhiteSpace(DamageTreeType) ? "未设置类型" : DamageTreeType;
            return $"{level} / {type}";
        }
    }
}
