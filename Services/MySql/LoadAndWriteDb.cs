using CombatSimulation.MySQLClassBase;
using Newtonsoft.Json;

namespace CombatSimulation.Services.MySql;

/// <summary>
/// 数据库的读写方法。
/// 原 Unity 版依赖 Singleton、GlobalStaticInfos 和 MyDebug；当前版本改为显式传入连接字符串，因此可被 Unity 与 WPF 同时复用。
/// </summary>
public sealed class LoadAndWriteDb : IDisposable
{
    // 数据库连接信息。
    private readonly string _connectionString;

    public MySqlCommand_TJ mySqlCommand_TJ { get; }

    /// <summary>
    /// 默认构造函数。
    /// 仅从程序运行目录下的 target_mysql.json 读取连接信息。
    /// </summary>
    public LoadAndWriteDb()
        : this(MySQLConnectionInfo.TryBuildDefaultConnectionString()
            ?? throw new InvalidOperationException("未找到 MySQL 连接信息。请在程序运行目录放置 target_mysql.json。"))
    {
    }

    /// <summary>
    /// 使用连接信息初始化数据库访问对象。
    /// </summary>
    public LoadAndWriteDb(MySQLConnectionInfo connectionInfo)
        : this(connectionInfo?.ToString() ?? throw new ArgumentNullException(nameof(connectionInfo)))
    {
    }

    /// <summary>
    /// 使用完整连接字符串初始化数据库访问对象。
    /// </summary>
    public LoadAndWriteDb(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("MySQL 连接字符串不能为空。", nameof(connectionString));
        }

        _connectionString = connectionString;
        mySqlCommand_TJ = new MySqlCommand_TJ(_connectionString);
    }

    /// <summary>
    /// 创建某个类型对应的数据表。
    /// MySqlCommand_TJ.CreateTable 内部使用 CREATE TABLE IF NOT EXISTS，因此表已存在时不会重复创建。
    /// </summary>
    public void CreateTable<T>() where T : MySQLClassBaseClass
    {
        mySqlCommand_TJ.CreateTable<T>();
    }

    /// <summary>
    /// 在单个 MySQL 事务内执行一个原子业务操作。
    /// </summary>
    public void ExecuteInTransaction(Action operation)
    {
        mySqlCommand_TJ.ExecuteInTransaction(operation);
    }

    /// <summary>
    /// 在单个 MySQL 事务内执行一个带返回值的原子业务操作。
    /// </summary>
    public TResult ExecuteInTransaction<TResult>(Func<TResult> operation)
    {
        return mySqlCommand_TJ.ExecuteInTransaction(operation);
    }

    /// <summary>
    /// 判断某个表是否存在，不存在则添加，存在则跳过。
    /// 保留该方法名是为了兼容原 Unity 端调用；新代码建议直接使用 CreateTable。
    /// </summary>
    public void IsTableExist<T>() where T : MySQLClassBaseClass
    {
        CreateTable<T>();
    }

    /// <summary>
    /// 异步添加数据，但需要等待其完成。
    /// </summary>
    /// <param name="data">数据列表。</param>
    /// <typeparam name="T">继承于 MySQLClassBaseClass 的类。</typeparam>
    /// <returns>插入了多少条数据。</returns>
    public Task<int> AddDataToTabelAsync<T>(List<T> data) where T : MySQLClassBaseClass
    {
        IsTableExist<T>();
        return mySqlCommand_TJ.InsertAsync(data);
    }

    /// <summary>
    /// 清空数据库中指定类型对应的表。
    /// </summary>
    public void ClearDb<T>() where T : MySQLClassBaseClass
    {
        mySqlCommand_TJ.DeleteAll<T>();
    }

    /// <summary>
    /// 根据 SQL 条件删除指定数据。
    /// </summary>
    public void DeleteDataBySql<T>(string sql) where T : MySQLClassBaseClass
    {
        mySqlCommand_TJ.DeleteBySql<T>(sql);
    }

    /// <summary>
    /// 获取表中的所有数据，若没有指定 SQL 条件则默认为获取全部数据。
    /// </summary>
    /// <param name="sql">SQL 条件，不包含 WHERE。</param>
    /// <typeparam name="T">查询的表。</typeparam>
    /// <returns>返回结果。</returns>
    public List<T> GetTableData<T>(string sql = "") where T : MySQLClassBaseClass
    {
        return mySqlCommand_TJ.SelectBySql<T>(sql);
    }

    /// <summary>
    /// 调用存储过程获取数据。
    /// </summary>
    /// <param name="name">存储过程名及参数。</param>
    /// <typeparam name="T">接收类型。</typeparam>
    /// <returns>结果列表。</returns>
    public List<T> Test<T>(string name) where T : MySQLClassBaseClass
    {
        return mySqlCommand_TJ.CallBySqlToGetData<T>(name);
    }

    /// <summary>
    /// 调用存储过程存储数据。
    /// </summary>
    /// <param name="name">存储过程名及参数。</param>
    /// <param name="datas">存储的数据。</param>
    /// <typeparam name="T">存储类型。</typeparam>
    /// <returns>返回结果。</returns>
    public int InsertData<T>(string name, List<T> datas) where T : MySQLClassBaseClass
    {
        string message = JsonConvert.SerializeObject(datas);
        string param = $"{name} ('{message.Replace("'", "''")}');";
        return mySqlCommand_TJ.CallBySqlToAddData<T>(param);
    }

    /// <summary>
    /// 对外接口：获取此类对应的表中的当前数据的索引最大值。
    /// </summary>
    /// <typeparam name="T">此类对应的表。</typeparam>
    /// <returns>索引最大值；若为 -1 则表示此表中没有数据或查询失败。</returns>
    public int GetMaxIndex<T>() where T : MySQLClassBaseClass
    {
        return mySqlCommand_TJ.GetMaxIndex<T>();
    }

    /// <summary>
    /// 对外接口：获取静态或动态杀爆弹破片数据。
    /// </summary>
    /// <param name="name">存储过程名。</param>
    /// <param name="index">选取的杀爆弹 ID。</param>
    /// <typeparam name="T">存储类型。</typeparam>
    /// <returns>返回结果。</returns>
    public List<T> GetFragments<T>(string name, int index) where T : MySQLClassBaseClass
    {
        string param = $"{name} ({index});";
        return mySqlCommand_TJ.GetFragments<T>(param);
    }

    /// <summary>
    /// 主线程退出时需要释放连接资源。
    /// Unity 端可在 OnApplicationQuit 中调用，WPF 端可在 App.OnExit 中调用。
    /// </summary>
    public void Dispose()
    {
        mySqlCommand_TJ.Dispose();
    }
}
