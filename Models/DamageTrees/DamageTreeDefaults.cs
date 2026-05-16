namespace CombatSimulation.Models;

/// <summary>
/// 毁伤树业务约束的集中定义。
/// </summary>
/// <remarks>
/// 当前目标只允许维护轻度、中度、重度三棵功能毁伤树。
/// 将默认值和规范化逻辑集中在这里，避免弹窗、列表加载和保存校验各自维护一套字符串规则。
/// </remarks>
public static class DamageTreeDefaults
{
    public const string LightDamageLevel = "轻度毁伤";
    public const string MediumDamageLevel = "中度毁伤";
    public const string HeavyDamageLevel = "重度毁伤";
    public const string DefaultTreeType = "功能毁伤";

    private static readonly string[] s_damageLevels =
    {
        LightDamageLevel,
        MediumDamageLevel,
        HeavyDamageLevel
    };

    /// <summary>
    /// 可选毁伤等级。界面和校验均使用该集合，保证同一目标最多只有轻、中、重三棵树。
    /// </summary>
    public static IReadOnlyList<string> DamageLevels => s_damageLevels;

    /// <summary>
    /// 将历史录入或手工输入的毁伤等级归一化到固定的轻/中/重三类。
    /// </summary>
    public static string NormalizeDamageLevel(string? value)
    {
        string text = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        foreach (string level in s_damageLevels)
        {
            if (string.Equals(text, level, StringComparison.CurrentCultureIgnoreCase))
            {
                return level;
            }
        }

        if (text.Contains('轻'))
        {
            return LightDamageLevel;
        }

        if (text.Contains('中'))
        {
            return MediumDamageLevel;
        }

        if (text.Contains('重'))
        {
            return HeavyDamageLevel;
        }

        return text;
    }

    /// <summary>
    /// 判断毁伤等级是否属于允许的一档。非轻/中/重的数据不会进入当前界面集合。
    /// </summary>
    public static bool IsKnownDamageLevel(string? value)
    {
        string normalized = NormalizeDamageLevel(value);
        return s_damageLevels.Any(level => string.Equals(level, normalized, StringComparison.Ordinal));
    }

    /// <summary>
    /// 根据当前已有毁伤树推断新增时应使用的第一个空缺等级。
    /// </summary>
    public static string FindFirstAvailableDamageLevel(IEnumerable<DamageTreeInfoItem> existingTrees)
    {
        HashSet<string> usedLevels = existingTrees
            .Select(tree => NormalizeDamageLevel(tree.DamageLevelInfo))
            .Where(IsKnownDamageLevel)
            .ToHashSet(StringComparer.Ordinal);

        return s_damageLevels.FirstOrDefault(level => !usedLevels.Contains(level)) ?? LightDamageLevel;
    }

    /// <summary>
    /// 生成默认毁伤树名称，使下拉框主显示文本稳定为树名称。
    /// </summary>
    public static string CreateDefaultTreeName(string targetName, string damageLevel)
    {
        string normalizedTargetName = string.IsNullOrWhiteSpace(targetName) ? "目标" : targetName.Trim();
        string normalizedDamageLevel = NormalizeDamageLevel(damageLevel);
        return $"{normalizedTargetName}{normalizedDamageLevel}树";
    }

    /// <summary>
    /// 生成默认根节点名称。根节点描述具体毁伤状态，不额外追加“树”。
    /// </summary>
    public static string CreateDefaultRootNodeName(string targetName, string damageLevel)
    {
        string normalizedTargetName = string.IsNullOrWhiteSpace(targetName) ? "目标" : targetName.Trim();
        string normalizedDamageLevel = NormalizeDamageLevel(damageLevel);
        return $"{normalizedTargetName}{normalizedDamageLevel}";
    }
}
