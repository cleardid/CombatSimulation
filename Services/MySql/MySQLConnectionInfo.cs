using System.IO;
using System.Net;
using System.Text.Json;
using MySql.Data.MySqlClient;

namespace CombatSimulation.Services.MySql;

/// <summary>
/// 数据库连接信息类。
/// 该类型不依赖 Unity 或 WPF，可在两端共用。
/// </summary>
public sealed class MySQLConnectionInfo
{
    /// <summary>
    /// 默认 JSON 配置文件名称。
    /// </summary>
    public const string DefaultConfigFileName = "target_mysql.json";

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
    /// 程序运行时默认读取的 JSON 配置路径。
    /// 注意：WPF 运行时目录通常是 bin/Debug/net8.0-windows，而不是项目源码目录。
    /// </summary>
    public static string DefaultConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultConfigFileName);

    /// <summary>
    /// 将配置转换为 MySQL 连接字符串。
    /// </summary>
    public override string ToString()
    {
        return $"server={Server};port={Port};User Id={User_Id};password={Password};database={Db_name};Connection Timeout={ConnectionTimeout};Allow User Variables=True;";
    }

    /// <summary>
    /// 从 JSON 文件读取连接信息。
    /// </summary>
    public static bool TryLoadFromJson(string filePath, out MySQLConnectionInfo connectionInfo)
    {
        connectionInfo = new MySQLConnectionInfo();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            MySqlLog.LogWarning($"未找到 MySQL 配置文件：{filePath}");
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
                MySqlLog.LogWarning($"MySQL 配置文件无效或缺少 Db_name：{filePath}");
                return false;
            }

            connectionInfo = loaded;
            MySqlLog.Log($"已读取 MySQL 配置文件：{filePath}，数据库：{connectionInfo.Db_name}");
            return true;
        }
        catch (Exception ex)
        {
            MySqlLog.LogWarning($"读取 MySQL 配置失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 尝试从程序运行目录下的 target_mysql.json 读取连接信息并生成连接字符串。
    /// </summary>
    public static string? TryBuildDefaultConnectionString()
    {
        return TryBuildDefaultConnectionString(out _, out _);
    }

    /// <summary>
    /// 尝试从程序运行目录下的 target_mysql.json 读取连接信息，并返回配置来源和诊断信息。
    /// 默认配置统一收敛到 JSON 文件，避免多个配置来源造成不一致。
    /// </summary>
    public static string? TryBuildDefaultConnectionString(out string source, out string diagnosticMessage)
    {
        string configPath = DefaultConfigPath;
        if (TryLoadFromJson(configPath, out MySQLConnectionInfo connectionInfo))
        {
            source = configPath;
            diagnosticMessage = $"已从 JSON 配置读取 MySQL 连接信息：{configPath}";
            return connectionInfo.ToString();
        }

        source = string.Empty;
        diagnosticMessage = $"未找到 MySQL 连接信息。已检查配置文件：{configPath}";
        MySqlLog.LogWarning(diagnosticMessage);
        return null;
    }

    /// <summary>
    /// 根据连接字符串确认数据库是否存在；不存在时尝试创建数据库。
    /// 该方法会先移除连接字符串中的 database 字段，再连接 MySQL 服务器执行 CREATE DATABASE IF NOT EXISTS。
    /// </summary>
    public static void EnsureDatabaseExists(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        try
        {
            var builder = new MySqlConnectionStringBuilder(connectionString);
            string databaseName = builder.Database;
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                MySqlLog.LogWarning("连接字符串中没有 database/Database 字段，跳过数据库自动创建。 ");
                return;
            }

            builder.Database = string.Empty;
            using MySqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            using MySqlCommand command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{EscapeIdentifier(databaseName)}` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;";
            command.ExecuteNonQuery();
            MySqlLog.Log($"已确认 MySQL 数据库存在：{databaseName}");
        }
        catch (Exception ex)
        {
            MySqlLog.LogWarning($"确认或创建 MySQL 数据库失败：{ex.Message}");
        }
    }

    private static string EscapeIdentifier(string identifier)
    {
        return identifier.Replace("`", "``");
    }
}
