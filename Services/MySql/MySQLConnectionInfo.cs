using System.IO;
using System.Net;
using System.Text.Json;

/// <summary>
/// 数据库连接信息类。
/// 该类型不依赖 Unity 或 WPF，可在两端共用。
/// </summary>
public sealed class MySQLConnectionInfo
{
    /// <summary>
    /// IP 地址或服务器地址。
    /// </summary>
    public string Server { get; set; } = "127.0.0.1";

    /// <summary>
    /// 兼容原 Unity 代码中的 IPAddress 属性。
    /// </summary>
    public IPAddress IPAddress
    {
        get => IPAddress.TryParse(Server, out IPAddress? address) ? address : IPAddress.Loopback;
        set => Server = value?.ToString() ?? "127.0.0.1";
    }

    /// <summary>
    /// 端口号。
    /// </summary>
    public int Port { get; set; } = 3306;

    /// <summary>
    /// MySQL 用户名。
    /// </summary>
    public string User_Id { get; set; } = "root";

    /// <summary>
    /// MySQL 密码。
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 数据库名。
    /// </summary>
    public string Db_name { get; set; } = string.Empty;

    /// <summary>
    /// 连接超时时间，单位秒。
    /// </summary>
    public int ConnectionTimeout { get; set; } = 5;

    /// <summary>
    /// 将配置转换为 MySQL 连接字符串。
    /// </summary>
    public override string ToString()
    {
        return $"server={Server};port={Port};User Id={User_Id};password={Password};database={Db_name};SslMode=None;Connection Timeout={ConnectionTimeout};Allow User Variables=True;";
    }

    /// <summary>
    /// 从环境变量 TARGET_MYSQL_CONNECTION_STRING 读取完整连接字符串。
    /// </summary>
    public static string? GetConnectionStringFromEnvironment()
    {
        string? connectionString = Environment.GetEnvironmentVariable("TARGET_MYSQL_CONNECTION_STRING");
        return string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
    }

    /// <summary>
    /// 从 JSON 文件读取连接信息。
    /// </summary>
    public static bool TryLoadFromJson(string filePath, out MySQLConnectionInfo connectionInfo)
    {
        connectionInfo = new MySQLConnectionInfo();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            MySQLConnectionInfo? loaded = JsonSerializer.Deserialize<MySQLConnectionInfo>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (loaded == null || string.IsNullOrWhiteSpace(loaded.Db_name))
            {
                return false;
            }

            connectionInfo = loaded;
            return true;
        }
        catch (Exception ex)
        {
            MySqlLog.LogWarning($"读取 MySQL 配置失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 优先从环境变量读取连接字符串；若没有，则尝试读取程序目录下的 target_mysql.json。
    /// </summary>
    public static string? TryBuildDefaultConnectionString()
    {
        string? connectionString = GetConnectionStringFromEnvironment();
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "target_mysql.json");
        return TryLoadFromJson(configPath, out MySQLConnectionInfo connectionInfo)
            ? connectionInfo.ToString()
            : null;
    }
}
