using System.Diagnostics;
using System.IO;

namespace CombatSimulation.Services.MySql;

/// <summary>
/// MySQL 通用日志入口。
/// Unity 端可以把 MessageLogged / WarningLogged 绑定到 MyDebug，WPF 端默认输出到 Debug、Trace 和本地日志文件。
/// </summary>
public static class MySqlLog
{
    private static readonly object SyncRoot = new();
    private static readonly List<MySqlLogEntry> RecentEntries = new();
    private static string? _logFilePath;

    /// <summary>
    /// 普通日志回调。保留该属性是为了兼容 Unity 端原有接入方式。
    /// </summary>
    public static Action<string>? MessageLogged { get; set; }

    /// <summary>
    /// 警告日志回调。保留该属性是为了兼容 Unity 端原有接入方式。
    /// </summary>
    public static Action<string>? WarningLogged { get; set; }

    /// <summary>
    /// 结构化日志事件。WPF ViewModel 可订阅该事件，把数据库日志显示到界面上。
    /// </summary>
    public static event Action<MySqlLogEntry>? EntryLogged;

    /// <summary>
    /// 当前日志文件路径。默认位于程序输出目录 logs 文件夹下。
    /// </summary>
    public static string LogFilePath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_logFilePath))
            {
                return _logFilePath;
            }

            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDirectory);
            _logFilePath = Path.Combine(logDirectory, $"CombatSimulation_{DateTime.Now:yyyyMMdd}.log");
            return _logFilePath;
        }
    }

    /// <summary>
    /// 获取最近的 MySQL 日志。用于 WPF 界面订阅前补齐启动早期日志。
    /// </summary>
    public static IReadOnlyList<MySqlLogEntry> GetRecentEntries()
    {
        lock (SyncRoot)
        {
            return RecentEntries.ToList();
        }
    }

    /// <summary>
    /// 普通信息日志。
    /// </summary>
    public static void Log(string message)
    {
        Write("Info", message, MessageLogged);
    }

    /// <summary>
    /// 警告信息日志。
    /// </summary>
    public static void LogWarning(string message)
    {
        Write("Warning", message, WarningLogged);
    }

    private static void Write(string level, string message, Action<string>? compatibilityCallback)
    {
        string safeMessage = string.IsNullOrWhiteSpace(message) ? "空日志消息" : message.Trim();
        MySqlLogEntry entry = new(DateTime.Now, level, safeMessage);
        string formattedMessage = entry.ToString();

        AddRecentEntry(entry);
        Debug.WriteLine(formattedMessage);
        Trace.WriteLine(formattedMessage);
        compatibilityCallback?.Invoke(safeMessage);
        EntryLogged?.Invoke(entry);
        AppendToFile(formattedMessage);
    }

    private static void AddRecentEntry(MySqlLogEntry entry)
    {
        lock (SyncRoot)
        {
            RecentEntries.Add(entry);
            while (RecentEntries.Count > 200)
            {
                RecentEntries.RemoveAt(0);
            }
        }
    }

    private static void AppendToFile(string formattedMessage)
    {
        try
        {
            lock (SyncRoot)
            {
                File.AppendAllText(LogFilePath, formattedMessage + Environment.NewLine);
            }
        }
        catch
        {
            // 日志写入失败不能影响数据库业务流程。
        }
    }
}

/// <summary>
/// MySQL 日志消息。
/// </summary>
/// <param name="Time">日志产生时间。</param>
/// <param name="Level">日志级别。</param>
/// <param name="Message">日志内容。</param>
public sealed record MySqlLogEntry(DateTime Time, string Level, string Message)
{
    public override string ToString() => $"{Time:yyyy-MM-dd HH:mm:ss.fff} [MySQL][{Level}] {Message}";
}
