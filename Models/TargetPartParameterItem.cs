using CommunityToolkit.Mvvm.ComponentModel;

namespace CombatSimulation.Models;
/// <summary>
/// 部件参数项。
/// Index 对应数据库中的 t_p_EquParam1 ~ t_p_EquParam24，Name 是该形状下的业务含义。
/// </summary>
public sealed partial class TargetPartParameterItem : ObservableObject
{
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private double _value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private string _unit = "mm";

    public string DisplayText => $"{Value:0.###}{(string.IsNullOrWhiteSpace(Unit) ? string.Empty : " " + Unit)}";
}
