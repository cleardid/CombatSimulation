namespace CombatSimulation.Models;

/// <summary>
/// 目标部件颜色格式转换工具。
/// </summary>
/// <remarks>
/// WPF 编辑界面只展示 #RRGGBB；MySQL 与 Unity 快照使用 #RRGGBBAA。
/// 将转换规则集中在这里，避免 ViewModel、Repository 各自维护一套重复逻辑。
/// </remarks>
public static class TargetPartColorFormat
{
    public const string DefaultWpfDisplayColor = "#808080";

    /// <summary>
    /// Unity 端固定透明度：0F，即 alpha = 15/255。
    /// </summary>
    public const string UnityAlphaSuffix = "0F";

    private const int RgbHexDigitCount = 6;
    private const int RgbaHexDigitCount = 8;

    /// <summary>
    /// 转换为 WPF 绑定和展示使用的 #RRGGBB。
    /// 如果传入数据库中的 #RRGGBBAA，会丢弃末尾 AA，避免界面下拉项显示透明度。
    /// </summary>
    public static string ToWpfDisplayColor(string? value)
    {
        string colorText = NormalizeColorText(value);
        if (IsHexColor(colorText, RgbaHexDigitCount))
        {
            colorText = "#" + colorText.Substring(1, RgbHexDigitCount);
        }

        return IsHexColor(colorText, RgbHexDigitCount) ? colorText : DefaultWpfDisplayColor;
    }

    /// <summary>
    /// 转换为 MySQL/Unity 使用的 #RRGGBBAA，并强制 AA = 0F。
    /// 这样即使调用方传入 #RRGGBB 或旧数据 #RRGGBBAA，最终写库格式也保持一致。
    /// </summary>
    public static string ToUnityDatabaseColor(string? value)
    {
        return ToWpfDisplayColor(value) + UnityAlphaSuffix;
    }

    private static string NormalizeColorText(string? value)
    {
        string colorText = string.IsNullOrWhiteSpace(value) ? DefaultWpfDisplayColor : value.Trim();
        if (!colorText.StartsWith("#", StringComparison.Ordinal))
        {
            colorText = "#" + colorText;
        }

        return colorText.ToUpperInvariant();
    }

    private static bool IsHexColor(string colorText, int hexDigitCount)
    {
        if (colorText.Length != hexDigitCount + 1 || !colorText.StartsWith("#", StringComparison.Ordinal))
        {
            return false;
        }

        for (int i = 1; i < colorText.Length; i++)
        {
            if (!Uri.IsHexDigit(colorText[i]))
            {
                return false;
            }
        }

        return true;
    }
}
