namespace CombatSimulation.Models
{
    /// <summary>
    /// 部件形状与等效参数名称的统一定义。
    /// MySQL 读写和 WPF 编辑弹窗共用该定义，避免两处维护导致字段顺序不一致。
    /// </summary>
    public static class TargetPartShapeParameterDefinitions
    {
        public const string DefaultShapeType = "长方体";

        private static readonly IReadOnlyDictionary<string, string[]> ParameterNameMap = new Dictionary<string, string[]>
        {
            ["长方体"] = new[] { "长", "宽", "高" },
            ["圆柱体"] = new[] { "半径", "长" },
            ["圆筒"] = new[] { "外半径", "内半径", "长" },
            ["球形"] = new[] { "半径" },
            ["六面异形体"] = new[]
            {
                "点1_X", "点1_Y", "点1_Z",
                "点2_X", "点2_Y", "点2_Z",
                "点3_X", "点3_Y", "点3_Z",
                "点4_X", "点4_Y", "点4_Z",
                "点5_X", "点5_Y", "点5_Z",
                "点6_X", "点6_Y", "点6_Z",
                "点7_X", "点7_Y", "点7_Z",
                "点8_X", "点8_Y", "点8_Z"
            }
        };

        /// <summary>
        /// 可选择的形状类型。
        /// </summary>
        public static IReadOnlyList<string> ShapeTypes => ParameterNameMap.Keys.ToArray();

        /// <summary>
        /// 获取指定形状的参数名称。未知形状按长方体处理。
        /// </summary>
        public static IReadOnlyList<string> GetParameterNames(string? shapeType)
        {
            string normalizedShapeType = NormalizeShapeType(shapeType);
            return ParameterNameMap[normalizedShapeType];
        }

        /// <summary>
        /// 统一形状名称。未知或空值均回退为长方体。
        /// </summary>
        public static string NormalizeShapeType(string? shapeType)
        {
            if (!string.IsNullOrWhiteSpace(shapeType) && ParameterNameMap.ContainsKey(shapeType.Trim()))
            {
                return shapeType.Trim();
            }

            return DefaultShapeType;
        }
    }
}
