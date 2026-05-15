using System.Data;
using MySql.Data.MySqlClient;

/// <summary>
/// 基础的 MySQL 连接管理类。
/// 不依赖 UnityEngine 或 WPF，可在 Unity 与 WPF 中共用。
/// </summary>
public class MySqlConnect : IDisposable
{
    // MySQL 连接的数据变量。
    protected MySqlConnection _sqlConn;

    // 检测冗余调用。
    private bool _disposed;

    /// <summary>
    /// 构造函数，初始化 MySQL 连接。
    /// </summary>
    /// <param name="connectionString">数据库连接字符串。</param>
    public MySqlConnect(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("MySQL 连接字符串不能为空。", nameof(connectionString));
        }

        _sqlConn = new MySqlConnection(connectionString);
        ConnectMySql();
    }

    /// <summary>
    /// 当前连接是否处于可用状态。
    /// </summary>
    public bool IsConnected => _sqlConn.State == ConnectionState.Open;

    /// <summary>
    /// 连接 MySQL 数据库。
    /// </summary>
    /// <returns>是否成功连接。</returns>
    protected bool ConnectMySql()
    {
        try
        {
            if (_sqlConn.State != ConnectionState.Open)
            {
                _sqlConn.Open();
            }

            return true;
        }
        catch (Exception ex)
        {
            MySqlLog.LogWarning("MySQL 连接失败：" + ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 确保连接已经打开。
    /// </summary>
    protected void EnsureConnectionOpen()
    {
        if (_sqlConn.State != ConnectionState.Open)
        {
            ConnectMySql();
        }
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 实现 IDisposable。
    /// </summary>
    /// <param name="disposing">是否释放托管资源。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                if (_sqlConn.State != ConnectionState.Closed)
                {
                    _sqlConn.Close();
                    MySqlLog.Log("MySQL 连接已关闭");
                }

                _sqlConn.Dispose();
            }
            catch (Exception ex)
            {
                MySqlLog.LogWarning("MySQL 连接关闭失败：" + ex.Message);
            }
        }

        _disposed = true;
    }

    /// <summary>
    /// 析构函数，确保资源被释放。
    /// </summary>
    ~MySqlConnect()
    {
        Dispose(false);
    }
}
