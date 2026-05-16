using System.Collections.ObjectModel;

namespace CombatSimulation.Models;
/// <summary>
/// 部件形状与 24 个等效参数字段的统一定义。
/// </summary>
/// <remarks>
/// 数据库字段 t_p_EquParam1 ~ t_p_EquParam24 没有独立语义，必须结合形状解释。
/// 非六面异形体统一使用：1~3 为中心坐标，4~6 为尺寸参数，7~9 为旋转角度；
/// 六面异形体统一使用：1~24 为八个顶点的 XYZ 坐标。
/// </remarks>
public static class TargetPartShapeParameterDefinitions
{
    public const string DefaultShapeType = "长方体";
    public const string HexahedronShapeType = "六面异形体";

    private static readonly IReadOnlyList<TargetPartParameterDefinition> BoxDefinitions = new[]
    {
        Definition(1, "中心X", "mm", TargetPartParameterRole.Position),
        Definition(2, "中心Y", "mm", TargetPartParameterRole.Position),
        Definition(3, "中心Z", "mm", TargetPartParameterRole.Position),
        Definition(4, "长", "mm", TargetPartParameterRole.Size),
        Definition(5, "宽", "mm", TargetPartParameterRole.Size),
        Definition(6, "高", "mm", TargetPartParameterRole.Size),
        Definition(7, "旋转X", "°", TargetPartParameterRole.Rotation),
        Definition(8, "旋转Y", "°", TargetPartParameterRole.Rotation),
        Definition(9, "旋转Z", "°", TargetPartParameterRole.Rotation)
    };

    private static readonly IReadOnlyList<TargetPartParameterDefinition> CylinderDefinitions = new[]
    {
        Definition(1, "中心X", "mm", TargetPartParameterRole.Position),
        Definition(2, "中心Y", "mm", TargetPartParameterRole.Position),
        Definition(3, "中心Z", "mm", TargetPartParameterRole.Position),
        Definition(4, "半径", "mm", TargetPartParameterRole.Size),
        Definition(5, "高", "mm", TargetPartParameterRole.Size),
        Definition(7, "旋转X", "°", TargetPartParameterRole.Rotation),
        Definition(8, "旋转Y", "°", TargetPartParameterRole.Rotation),
        Definition(9, "旋转Z", "°", TargetPartParameterRole.Rotation)
    };

    private static readonly IReadOnlyList<TargetPartParameterDefinition> SphereDefinitions = new[]
    {
        Definition(1, "中心X", "mm", TargetPartParameterRole.Position),
        Definition(2, "中心Y", "mm", TargetPartParameterRole.Position),
        Definition(3, "中心Z", "mm", TargetPartParameterRole.Position),
        Definition(4, "半径", "mm", TargetPartParameterRole.Size),
        Definition(7, "旋转X", "°", TargetPartParameterRole.Rotation),
        Definition(8, "旋转Y", "°", TargetPartParameterRole.Rotation),
        Definition(9, "旋转Z", "°", TargetPartParameterRole.Rotation)
    };

    private static readonly IReadOnlyList<TargetPartParameterDefinition> HexahedronDefinitions = CreateHexahedronDefinitions();

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<TargetPartParameterDefinition>> ParameterDefinitionMap =
        new Dictionary<string, IReadOnlyList<TargetPartParameterDefinition>>
        {
            ["长方体"] = BoxDefinitions,
            ["圆柱体"] = CylinderDefinitions,
            ["球形"] = SphereDefinitions,
            [HexahedronShapeType] = HexahedronDefinitions
        };

    /// <summary>
    /// 可选择的形状类型。圆筒当前不参与业务，因此不作为可选项暴露。
    /// </summary>
    public static IReadOnlyList<string> ShapeTypes => ParameterDefinitionMap.Keys.ToArray();

    /// <summary>
    /// 获取指定形状的全部有效参数定义。未知形状按长方体处理。
    /// </summary>
    public static IReadOnlyList<TargetPartParameterDefinition> GetParameterDefinitions(string? shapeType)
    {
        string normalizedShapeType = NormalizeShapeType(shapeType);
        return ParameterDefinitionMap[normalizedShapeType];
    }

    /// <summary>
    /// 获取部件编辑弹窗中“形状参数”区域需要直接输入的参数。
    /// 非六面异形体的中心坐标和旋转角度由独立区域编辑，因此这里只返回尺寸参数。
    /// 六面异形体没有中心和姿态的独立定义，因此返回全部 24 个顶点坐标。
    /// </summary>
    public static IReadOnlyList<TargetPartParameterDefinition> GetEditableParameterDefinitions(string? shapeType)
    {
        string normalizedShapeType = NormalizeShapeType(shapeType);
        IReadOnlyList<TargetPartParameterDefinition> definitions = GetParameterDefinitions(normalizedShapeType);

        if (IsHexahedronShape(normalizedShapeType))
        {
            return definitions;
        }

        return definitions.Where(item => item.Role == TargetPartParameterRole.Size).ToArray();
    }

    /// <summary>
    /// 获取右侧“结构信息”区域显示的形状参数。
    /// </summary>
    public static IReadOnlyList<TargetPartParameterDefinition> GetStructureParameterDefinitions(string? shapeType)
    {
        return GetEditableParameterDefinitions(shapeType);
    }

    /// <summary>
    /// 获取指定形状的参数名称。保留该方法用于兼容旧代码。
    /// </summary>
    public static IReadOnlyList<string> GetParameterNames(string? shapeType)
    {
        return GetParameterDefinitions(shapeType).Select(item => item.Name).ToArray();
    }

    /// <summary>
    /// 根据形状创建默认参数集合，所有数值默认为 0。
    /// </summary>
    public static ObservableCollection<TargetPartParameterItem> CreateDefaultParameters(string? shapeType)
    {
        ObservableCollection<TargetPartParameterItem> parameters = new();
        foreach (TargetPartParameterDefinition definition in GetParameterDefinitions(shapeType))
        {
            parameters.Add(CreateParameterItem(definition, 0));
        }

        return parameters;
    }

    /// <summary>
    /// 创建与定义一致的参数模型。
    /// </summary>
    public static TargetPartParameterItem CreateParameterItem(TargetPartParameterDefinition definition, double value)
    {
        return new TargetPartParameterItem
        {
            Index = definition.Index,
            Name = definition.Name,
            Value = value,
            Unit = definition.Unit
        };
    }

    /// <summary>
    /// 判断形状是否为六面异形体。
    /// </summary>
    public static bool IsHexahedronShape(string? shapeType)
    {
        return string.Equals(NormalizeShapeType(shapeType), HexahedronShapeType, StringComparison.Ordinal);
    }

    /// <summary>
    /// 统一形状名称。未知或空值均回退为长方体。
    /// </summary>
    public static string NormalizeShapeType(string? shapeType)
    {
        if (!string.IsNullOrWhiteSpace(shapeType) && ParameterDefinitionMap.ContainsKey(shapeType.Trim()))
        {
            return shapeType.Trim();
        }

        return DefaultShapeType;
    }

    private static TargetPartParameterDefinition Definition(int index, string name, string unit, TargetPartParameterRole role)
    {
        return new TargetPartParameterDefinition(index, name, unit, role);
    }

    private static IReadOnlyList<TargetPartParameterDefinition> CreateHexahedronDefinitions()
    {
        List<TargetPartParameterDefinition> definitions = new(capacity: 24);
        int index = 1;
        for (int pointIndex = 1; pointIndex <= 8; pointIndex++)
        {
            definitions.Add(Definition(index++, $"点{pointIndex}_X", "mm", TargetPartParameterRole.VertexCoordinate));
            definitions.Add(Definition(index++, $"点{pointIndex}_Y", "mm", TargetPartParameterRole.VertexCoordinate));
            definitions.Add(Definition(index++, $"点{pointIndex}_Z", "mm", TargetPartParameterRole.VertexCoordinate));
        }

        return definitions;
    }
}

/// <summary>
/// 部件参数在 24 个数据库字段中的业务角色。
/// </summary>
public enum TargetPartParameterRole
{
    Position,
    Size,
    Rotation,
    VertexCoordinate
}

/// <summary>
/// 单个数据库参数字段的业务定义。
/// </summary>
public sealed class TargetPartParameterDefinition
{
    public TargetPartParameterDefinition(int index, string name, string unit, TargetPartParameterRole role)
    {
        Index = index;
        Name = name;
        Unit = unit;
        Role = role;
    }

    /// <summary>
    /// 对应 t_p_EquParam1 ~ t_p_EquParam24 的序号。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 当前形状下该参数的业务名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 显示单位。
    /// </summary>
    public string Unit { get; }

    /// <summary>
    /// 参数角色，用于界面分区和详情显示。
    /// </summary>
    public TargetPartParameterRole Role { get; }
}
