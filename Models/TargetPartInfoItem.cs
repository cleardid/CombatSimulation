using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace CombatSimulation.Models
{
    /// <summary>
    /// 目标部件信息表。
    /// 参数集合用于承接数据库中的 Parameter01 ~ Parameter30，按不同形状解释其含义。
    /// </summary>
    public sealed partial class TargetPartInfoItem : ObservableObject
    {
        [ObservableProperty]
        private string _partCode = string.Empty;

        [ObservableProperty]
        private string _shapeType = string.Empty;

        [ObservableProperty]
        private string _partName = string.Empty;

        [ObservableProperty]
        private string _partDescription = string.Empty;

        [ObservableProperty]
        private string _materialId = string.Empty;

        [ObservableProperty]
        private string _displayColor = "#808080";

        [ObservableProperty]
        private double _equivalentThickness;

        [ObservableProperty]
        private double _vulnerableArea;

        [ObservableProperty]
        private string _systemCode = string.Empty;

        [ObservableProperty]
        private double _centerX;

        [ObservableProperty]
        private double _centerY;

        [ObservableProperty]
        private double _centerZ;

        [ObservableProperty]
        private double _rotationX;

        [ObservableProperty]
        private double _rotationY;

        [ObservableProperty]
        private double _rotationZ;

        [ObservableProperty]
        private ObservableCollection<TargetPartParameterItem> _parameters = new();
    }
}
