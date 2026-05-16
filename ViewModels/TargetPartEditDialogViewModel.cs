using CombatSimulation.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CombatSimulation.ViewModels;
/// <summary>
/// 目标部件添加/修改弹窗 ViewModel。
/// 负责 ComboBox 数据源、动态形状参数、数值校验和编辑结果生成。
/// </summary>
public sealed partial class TargetPartEditDialogViewModel : ObservableObject
{
    private static readonly IReadOnlyList<string> DefaultMaterialOptions = new[]
    {
        "Fe"
    };

    private static readonly IReadOnlyList<string> DefaultShapeTypeOptions = TargetPartShapeParameterDefinitions.ShapeTypes;

    private static readonly IReadOnlyList<string> DefaultColorOptions = new[]
    {
        TargetPartColorFormat.DefaultWpfDisplayColor,
        "#00AEEF",
        "#FFCC00",
        "#FF3300",
        "#FFFFFF",
        "#000000",
        "#00FF66"
    };

    private readonly string _systemCode;
    private readonly Dictionary<int, string> _parameterTextCache = new();
    private bool _isInitialized;

    public TargetPartEditDialogViewModel(TargetPartInfoItem part, bool isEditMode)
    {
        _systemCode = part.SystemCode;
        EditedPart = ClonePart(part);

        if (!isEditMode || string.IsNullOrWhiteSpace(EditedPart.PartCode))
        {
            EditedPart.PartCode = Guid.NewGuid().ToString("N");
        }

        DialogTitle = isEditMode ? "修改目标模型等效部件" : "添加目标模型等效部件";

        InitializeMaterialOptions(part.MaterialId);
        InitializeColorOptions(part.DisplayColor);
        InitializeParameterCache(part.Parameters);

        PartName = part.PartName;
        EquivalentThicknessText = FormatNumber(part.EquivalentThickness);
        VulnerableAreaText = FormatNumber(part.VulnerableArea);
        PartDescription = part.PartDescription;
        CenterXText = FormatNumber(GetCachedParameterValue(part, 1, part.CenterX));
        CenterYText = FormatNumber(GetCachedParameterValue(part, 2, part.CenterY));
        CenterZText = FormatNumber(GetCachedParameterValue(part, 3, part.CenterZ));
        RotationXText = FormatNumber(GetCachedParameterValue(part, 7, part.RotationX));
        RotationYText = FormatNumber(GetCachedParameterValue(part, 8, part.RotationY));
        RotationZText = FormatNumber(GetCachedParameterValue(part, 9, part.RotationZ));
        SelectedMaterial = string.IsNullOrWhiteSpace(part.MaterialId) ? "Fe" : part.MaterialId.Trim();
        SelectedDisplayColor = TargetPartColorFormat.ToWpfDisplayColor(part.DisplayColor);
        SelectedShapeType = TargetPartShapeParameterDefinitions.NormalizeShapeType(part.ShapeType);

        _isInitialized = true;
        RebuildParameterInputs();
    }

    public event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;

    public TargetPartInfoItem EditedPart { get; private set; }

    public ObservableCollection<string> ShapeTypeOptions { get; } = new(DefaultShapeTypeOptions);

    public ObservableCollection<string> MaterialOptions { get; } = new();

    public ObservableCollection<ColorOption> ColorOptions { get; } = new();

    public ObservableCollection<TargetPartParameterInputItem> Parameters { get; } = new();

    /// <summary>
    /// 非六面异形体才显示中心坐标和旋转角度输入区。
    /// 六面异形体的 24 个参数本身就是八个顶点坐标，不再额外维护中心和姿态。
    /// </summary>
    public bool IsTransformVisible => !TargetPartShapeParameterDefinitions.IsHexahedronShape(SelectedShapeType);

    [ObservableProperty]
    private string _dialogTitle = "目标模型等效部件设置";

    [ObservableProperty]
    private string _partName = string.Empty;

    [ObservableProperty]
    private string _selectedMaterial = "Fe";

    [ObservableProperty]
    private string _equivalentThicknessText = "0.00";

    [ObservableProperty]
    private string _selectedDisplayColor = TargetPartColorFormat.DefaultWpfDisplayColor;

    [ObservableProperty]
    private string _vulnerableAreaText = "0.00";

    [ObservableProperty]
    private string _partDescription = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTransformVisible))]
    private string _selectedShapeType = "长方体";

    [ObservableProperty]
    private string _centerXText = "0.00";

    [ObservableProperty]
    private string _centerYText = "0.00";

    [ObservableProperty]
    private string _centerZText = "0.00";

    [ObservableProperty]
    private string _rotationXText = "0.00";

    [ObservableProperty]
    private string _rotationYText = "0.00";

    [ObservableProperty]
    private string _rotationZText = "0.00";

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    partial void OnSelectedShapeTypeChanged(string value)
    {
        if (_isInitialized)
        {
            RebuildParameterInputs();
        }
    }

    /// <summary>
    /// 保存部件输入内容，并请求窗口按确认结果关闭。
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        string partName = PartName.Trim();
        string partCode = string.IsNullOrWhiteSpace(EditedPart.PartCode)
            ? Guid.NewGuid().ToString("N")
            : EditedPart.PartCode;
        string shapeType = TargetPartShapeParameterDefinitions.NormalizeShapeType(SelectedShapeType);
        bool isHexahedron = TargetPartShapeParameterDefinitions.IsHexahedronShape(shapeType);

        if (string.IsNullOrWhiteSpace(partName))
        {
            ValidationMessage = "请输入等效部件名称。";
            return;
        }

        if (!TryReadDouble(EquivalentThicknessText, "等效厚度", out double equivalentThickness) ||
            !TryReadDouble(VulnerableAreaText, "易损面积", out double vulnerableArea))
        {
            return;
        }

        ObservableCollection<TargetPartParameterItem> parameters = new();
        double centerX = 0;
        double centerY = 0;
        double centerZ = 0;
        double rotationX = 0;
        double rotationY = 0;
        double rotationZ = 0;

        if (isHexahedron)
        {
            if (!TryReadParameterInputs(parameters))
            {
                return;
            }
        }
        else
        {
            if (!TryReadDouble(CenterXText, "中心坐标 X", out centerX) ||
                !TryReadDouble(CenterYText, "中心坐标 Y", out centerY) ||
                !TryReadDouble(CenterZText, "中心坐标 Z", out centerZ) ||
                !TryReadParameterInputs(parameters) ||
                !TryReadDouble(RotationXText, "姿态角度 X", out rotationX) ||
                !TryReadDouble(RotationYText, "姿态角度 Y", out rotationY) ||
                !TryReadDouble(RotationZText, "姿态角度 Z", out rotationZ))
            {
                return;
            }

            AddDefinedParameter(parameters, shapeType, 1, centerX);
            AddDefinedParameter(parameters, shapeType, 2, centerY);
            AddDefinedParameter(parameters, shapeType, 3, centerZ);
            AddDefinedParameter(parameters, shapeType, 7, rotationX);
            AddDefinedParameter(parameters, shapeType, 8, rotationY);
            AddDefinedParameter(parameters, shapeType, 9, rotationZ);

            CenterXText = FormatNumber(centerX);
            CenterYText = FormatNumber(centerY);
            CenterZText = FormatNumber(centerZ);
            RotationXText = FormatNumber(rotationX);
            RotationYText = FormatNumber(rotationY);
            RotationZText = FormatNumber(rotationZ);
        }

        EquivalentThicknessText = FormatNumber(equivalentThickness);
        VulnerableAreaText = FormatNumber(vulnerableArea);

        EditedPart = new TargetPartInfoItem
        {
            PartCode = partCode,
            PartName = partName,
            ShapeType = shapeType,
            PartDescription = PartDescription.Trim(),
            MaterialId = string.IsNullOrWhiteSpace(SelectedMaterial) ? "Fe" : SelectedMaterial,
            DisplayColor = TargetPartColorFormat.ToWpfDisplayColor(SelectedDisplayColor),
            EquivalentThickness = equivalentThickness,
            VulnerableArea = vulnerableArea,
            SystemCode = _systemCode,
            CenterX = centerX,
            CenterY = centerY,
            CenterZ = centerZ,
            RotationX = rotationX,
            RotationY = rotationY,
            RotationZ = rotationZ,
            Parameters = new ObservableCollection<TargetPartParameterItem>(parameters.OrderBy(item => item.Index))
        };

        ValidationMessage = string.Empty;
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(dialogResult: true));
    }

    /// <summary>
    /// 取消编辑，并请求窗口按取消结果关闭。
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(dialogResult: false));
    }

    private void InitializeMaterialOptions(string materialId)
    {
        foreach (string item in DefaultMaterialOptions)
        {
            MaterialOptions.Add(item);
        }

        string material = string.IsNullOrWhiteSpace(materialId) ? "Fe" : materialId.Trim();
        if (!MaterialOptions.Any(item => string.Equals(item, material, StringComparison.Ordinal)))
        {
            MaterialOptions.Add(material);
        }
    }

    private void InitializeColorOptions(string displayColor)
    {
        foreach (string color in DefaultColorOptions)
        {
            ColorOptions.Add(new ColorOption(color));
        }

        // 编辑界面只绑定 #RRGGBB；若数据库旧值带 AA，这里统一裁剪后再进入下拉选项。
        string colorValue = TargetPartColorFormat.ToWpfDisplayColor(displayColor);
        if (!ColorOptions.Any(item => string.Equals(item.Value, colorValue, StringComparison.OrdinalIgnoreCase)))
        {
            ColorOptions.Add(new ColorOption(colorValue));
        }
    }

    private void InitializeParameterCache(IEnumerable<TargetPartParameterItem> parameters)
    {
        foreach (TargetPartParameterItem parameter in parameters)
        {
            if (parameter.Index is >= 1 and <= 24)
            {
                _parameterTextCache[parameter.Index] = FormatNumber(parameter.Value);
            }
        }
    }

    private void RebuildParameterInputs()
    {
        CacheCurrentParameterInputs();
        Parameters.Clear();

        string shapeType = TargetPartShapeParameterDefinitions.NormalizeShapeType(SelectedShapeType);

        foreach (TargetPartParameterDefinition definition in TargetPartShapeParameterDefinitions.GetEditableParameterDefinitions(shapeType))
        {
            string valueText = _parameterTextCache.TryGetValue(definition.Index, out string? cachedText)
                ? cachedText
                : FormatNumber(0);

            Parameters.Add(new TargetPartParameterInputItem(definition.Index, definition.Name, definition.Unit, valueText));
        }
    }

    private void CacheCurrentParameterInputs()
    {
        foreach (TargetPartParameterInputItem parameter in Parameters)
        {
            if (parameter.Index is >= 1 and <= 24)
            {
                _parameterTextCache[parameter.Index] = parameter.ValueText;
            }
        }
    }

    private bool TryReadParameterInputs(ObservableCollection<TargetPartParameterItem> parameters)
    {
        foreach (TargetPartParameterInputItem parameterInput in Parameters)
        {
            if (!TryReadDouble(parameterInput.ValueText, parameterInput.Name, out double value))
            {
                return false;
            }

            parameterInput.ValueText = FormatNumber(value);
            parameters.Add(new TargetPartParameterItem
            {
                Index = parameterInput.Index,
                Name = parameterInput.Name,
                Value = value,
                Unit = parameterInput.Unit
            });
        }

        return true;
    }

    private static void AddDefinedParameter(ObservableCollection<TargetPartParameterItem> parameters, string shapeType, int index, double value)
    {
        if (parameters.Any(item => item.Index == index))
        {
            return;
        }

        TargetPartParameterDefinition? definition = TargetPartShapeParameterDefinitions
            .GetParameterDefinitions(shapeType)
            .FirstOrDefault(item => item.Index == index);

        if (definition != null)
        {
            parameters.Add(TargetPartShapeParameterDefinitions.CreateParameterItem(definition, value));
        }
    }

    private bool TryReadDouble(string text, string fieldName, out double value)
    {
        string normalizedText = text.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            value = 0;
            return true;
        }

        if (double.TryParse(normalizedText, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
            double.TryParse(normalizedText, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            value = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            return true;
        }

        ValidationMessage = $"{fieldName} 必须是有效数字。";
        return false;
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.00", CultureInfo.CurrentCulture);
    }

    private static double GetCachedParameterValue(TargetPartInfoItem part, int index, double fallback)
    {
        TargetPartParameterItem? parameter = part.Parameters.FirstOrDefault(item => item.Index == index);
        return parameter?.Value ?? fallback;
    }

    private static TargetPartInfoItem ClonePart(TargetPartInfoItem source)
    {
        TargetPartInfoItem target = new()
        {
            PartCode = source.PartCode,
            PartName = source.PartName,
            ShapeType = source.ShapeType,
            PartDescription = source.PartDescription,
            MaterialId = source.MaterialId,
            DisplayColor = source.DisplayColor,
            EquivalentThickness = source.EquivalentThickness,
            VulnerableArea = source.VulnerableArea,
            SystemCode = source.SystemCode,
            CenterX = source.CenterX,
            CenterY = source.CenterY,
            CenterZ = source.CenterZ,
            RotationX = source.RotationX,
            RotationY = source.RotationY,
            RotationZ = source.RotationZ
        };

        foreach (TargetPartParameterItem parameter in source.Parameters)
        {
            target.Parameters.Add(new TargetPartParameterItem
            {
                Index = parameter.Index,
                Name = parameter.Name,
                Value = parameter.Value,
                Unit = parameter.Unit
            });
        }

        return target;
    }

    public sealed class ColorOption
    {
        public ColorOption(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }
}

/// <summary>
/// 部件形状参数输入行，对应数据库 t_p_EquParam1 ~ t_p_EquParam24 中的一个字段。
/// </summary>
public sealed partial class TargetPartParameterInputItem : ObservableObject
{
    public TargetPartParameterInputItem(int index, string name, string unit, string valueText)
    {
        Index = index;
        Name = name;
        Unit = unit;
        ValueText = valueText;
    }

    public int Index { get; }

    public string Unit { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _valueText = "0.00";

    public string Label => string.IsNullOrWhiteSpace(Unit) ? Name : $"{Name}({Unit})";
}
