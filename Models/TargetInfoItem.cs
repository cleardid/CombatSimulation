using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace CombatSimulation.Models
{
    /// <summary>
    /// 目标信息表。
    /// 字段对应：目标名称、唯一标识 Code、种类、描述。
    /// </summary>
    public sealed partial class TargetInfoItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _code = string.Empty;

        [ObservableProperty]
        private string _category = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        /// <summary>
        /// 目标系统信息表集合。
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<TargetSystemInfoItem> _systems = new();

        /// <summary>
        /// 结构树 UI 根节点。通常只有一个根节点，即当前目标本身。
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<TargetStructureTreeNode> _structureTreeNodes = new();

        /// <summary>
        /// 毁伤树节点暂保留原有字符串结构，避免影响毁伤树模块。
        /// 后续可以按同样方式改成实体节点。
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _damageTreeNodes = new();
    }
}
