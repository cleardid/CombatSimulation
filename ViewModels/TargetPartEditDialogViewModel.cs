using CombatSimulation.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;

namespace CombatSimulation.ViewModels
{
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

        private static readonly IReadOnlyList<ColorOption> DefaultColorOptions = new[]
        {
            new ColorOption("#808080"),
            new ColorOption("#00AEEF"),
            new ColorOption("#FFCC00"),
            new ColorOption("#FF3300"),
            new ColorOption("#FFFFFF"),
            new ColorOption("#000000"),
            new ColorOption("#00FF66")
        };

        private readonly string _systemCode;
        private readonly Dictionary<string, string> _parameterTextCache = new();
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
            CenterXText = FormatNumber(part.CenterX);
            CenterYText = FormatNumber(part.CenterY);
            CenterZText = FormatNumber(part.CenterZ);
            RotationXText = FormatNumber(part.RotationX);
            RotationYText = FormatNumber(part.RotationY);
            RotationZText = FormatNumber(part.RotationZ);
            SelectedMaterial = string.IsNullOrWhiteSpace(part.MaterialId) ? "Fe" : part.MaterialId.Trim();
            SelectedDisplayColor = NormalizeColorValue(part.DisplayColor);
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

        public bool IsTransformVisible => SelectedShapeType != "六面异形体";

        [ObservableProperty]
        private string _dialogTitle = "目标模型等效部件设置";

        [ObservableProperty]
        private string _partName = string.Empty;

        [ObservableProperty]
        private string _selectedMaterial = "Fe";

        [ObservableProperty]
        private string _equivalentThicknessText = "0.00";

        [ObservableProperty]
        private string _selectedDisplayColor = "#808080";

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
            int parameterIndex = 1;
            foreach (TargetPartParameterInputItem parameterInput in Parameters)
            {
                if (!TryReadDouble(parameterInput.ValueText, parameterInput.Name, out double value))
                {
                    return;
                }

                parameterInput.ValueText = FormatNumber(value);
                parameters.Add(new TargetPartParameterItem
                {
                    Index = parameterIndex++,
                    Name = parameterInput.Name,
                    Value = value,
                    Unit = "mm"
                });
            }

            double centerX = 0;
            double centerY = 0;
            double centerZ = 0;
            double rotationX = 0;
            double rotationY = 0;
            double rotationZ = 0;

            if (shapeType != "六面异形体")
            {
                if (!TryReadDouble(CenterXText, "中心坐标 X", out centerX) ||
                    !TryReadDouble(CenterYText, "中心坐标 Y", out centerY) ||
                    !TryReadDouble(CenterZText, "中心坐标 Z", out centerZ) ||
                    !TryReadDouble(RotationXText, "姿态角度 X", out rotationX) ||
                    !TryReadDouble(RotationYText, "姿态角度 Y", out rotationY) ||
                    !TryReadDouble(RotationZText, "姿态角度 Z", out rotationZ))
                {
                    return;
                }

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
                DisplayColor = NormalizeColorValue(SelectedDisplayColor),
                EquivalentThickness = equivalentThickness,
                VulnerableArea = vulnerableArea,
                SystemCode = _systemCode,
                CenterX = centerX,
                CenterY = centerY,
                CenterZ = centerZ,
                RotationX = rotationX,
                RotationY = rotationY,
                RotationZ = rotationZ,
                Parameters = parameters
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
            foreach (ColorOption color in DefaultColorOptions)
            {
                ColorOptions.Add(new ColorOption(color.Value));
            }

            string colorValue = NormalizeColorValue(displayColor);
            if (!ColorOptions.Any(item => string.Equals(item.Value, colorValue, StringComparison.OrdinalIgnoreCase)))
            {
                ColorOptions.Add(new ColorOption(colorValue));
            }
        }

        private void InitializeParameterCache(IEnumerable<TargetPartParameterItem> parameters)
        {
            foreach (TargetPartParameterItem parameter in parameters)
            {
                _parameterTextCache[parameter.Name] = FormatNumber(parameter.Value);
            }
        }

        private void RebuildParameterInputs()
        {
            CacheCurrentParameterInputs();
            Parameters.Clear();

            string shapeType = TargetPartShapeParameterDefinitions.NormalizeShapeType(SelectedShapeType);

            foreach (string parameterName in TargetPartShapeParameterDefinitions.GetParameterNames(shapeType))
            {
                string valueText = _parameterTextCache.TryGetValue(parameterName, out string? cachedText)
                    ? cachedText
                    : FormatNumber(0);

                Parameters.Add(new TargetPartParameterInputItem(parameterName, valueText));
            }
        }

        private void CacheCurrentParameterInputs()
        {
            foreach (TargetPartParameterInputItem parameter in Parameters)
            {
                _parameterTextCache[parameter.Name] = parameter.ValueText;
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

        private static string NormalizeColorValue(string? value)
        {
            string colorText = string.IsNullOrWhiteSpace(value) ? "#808080" : value.Trim();
            if (!colorText.StartsWith("#", StringComparison.Ordinal))
            {
                colorText = "#" + colorText;
            }

            try
            {
                _ = ColorConverter.ConvertFromString(colorText);
                return colorText.ToUpperInvariant();
            }
            catch (FormatException)
            {
                return "#808080";
            }
            catch (NotSupportedException)
            {
                return "#808080";
            }
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
    /// 部件形状参数输入行。
    /// </summary>
    public sealed partial class TargetPartParameterInputItem : ObservableObject
    {
        public TargetPartParameterInputItem(string name, string valueText)
        {
            Name = name;
            ValueText = valueText;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Label))]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _valueText = "0.00";

        public string Label => $"{Name}(mm)";
    }
}
