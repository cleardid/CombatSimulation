using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace CombatSimulation.Models
{
    /// <summary>
    /// 目标系统信息表。
    /// 字段对应：系统唯一标识、系统名称、系统描述、是否为顶系统、父系统、所属目标标识。
    /// </summary>
    public sealed partial class TargetSystemInfoItem : ObservableObject
    {
        [ObservableProperty]
        private string _systemCode = string.Empty;

        [ObservableProperty]
        private string _systemName = string.Empty;

        [ObservableProperty]
        private string _systemDescription = string.Empty;

        [ObservableProperty]
        private bool _isTopSystem;

        [ObservableProperty]
        private string _parentSystemCode = "-1";

        [ObservableProperty]
        private string _targetCode = string.Empty;

        [ObservableProperty]
        private ObservableCollection<TargetSystemInfoItem> _childSystems = new();

        [ObservableProperty]
        private ObservableCollection<TargetPartInfoItem> _parts = new();
    }
}
