using System.Diagnostics;

/// <summary>
/// MySQL 通用日志入口。
/// Unity 端可以把 MessageLogged / WarningLogged 绑定到 MyDebug，WPF 端默认输出到 Debug。
/// </summary>
public static class MySqlLog
{
    public static Action<string>? MessageLogged { get; set; }

    public static Action<string>? WarningLogged { get; set; }

    public static void Log(string message)
    {
        if (MessageLogged != null)
        {
            MessageLogged.Invoke(message);
            return;
        }

        Debug.WriteLine($"[MySQL] {message}");
    }

    public static void LogWarning(string message)
    {
        if (WarningLogged != null)
        {
            WarningLogged.Invoke(message);
            return;
        }

        Debug.WriteLine($"[MySQL][Warning] {message}");
    }
}
