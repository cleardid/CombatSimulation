using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Text;
using CombatSimulation.MySQLClassBase;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

namespace CombatSimulation.Services.MySql;

/// <summary>
/// 数据库操作类，用于执行 MySQL 数据库的增删改查操作。
/// 该实现保留原有泛型反射模型，但移除了 Unity 依赖，并修正了字符串主键建表、插入与更新逻辑。
/// </summary>
public sealed class MySqlCommand_TJ : MySqlConnect
{
    // 操作命令。
    private MySqlCommand? _sqlComm;

    // 反射结果缓存。
    private static readonly ConcurrentDictionary<Type, List<PropertyInfo>> PropertyCache = new();

    // 检测冗余调用。
    private bool _disposed;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public MySqlCommand_TJ(string connectionString) : base(connectionString)
    {
        _sqlComm = new MySqlCommand
        {
            Connection = _sqlConn
        };
    }

    /// <summary>
    /// 获取创建的表名。
    /// </summary>
    private static string GetTableName<T>() where T : MySQLClassBaseClass
    {
        var objectInstance = Activator.CreateInstance<T>();
        return objectInstance.TableName;
    }

    /// <summary>
    /// 创建表，如果表已存在则不创建。
    /// 字符串主键不会自动添加 AUTO_INCREMENT，只有整型主键才会自动递增。
    /// </summary>
    public int CreateTable<T>() where T : MySQLClassBaseClass
    {
        EnsureCommandReady();

        var sql = new StringBuilder();
        sql.Append($"CREATE TABLE IF NOT EXISTS `{GetTableName<T>()}` (");

        List<PropertyInfo> properties = GetCachedProperties<T>();
        foreach (PropertyInfo prop in properties)
        {
            MySQLClassHelpAttribute attribute = GetAttribute(prop);
            sql.Append($"`{attribute.FieldName}` {attribute.TypeName} ");

            if (attribute.IsPrimaryKey)
            {
                sql.Append("PRIMARY KEY ");
                if (IsAutoIncrementType(prop.PropertyType))
                {
                    sql.Append("AUTO_INCREMENT ");
                }
            }

            sql.Append(attribute.IsCanBeNull ? "NULL" : "NOT NULL");
            sql.Append(',');
        }

        if (properties.Count == 0)
        {
            throw new InvalidOperationException($"类型 {typeof(T).Name} 没有可创建的 MySQL 字段。 ");
        }

        sql.Length -= 1;
        sql.Append(");");

        _sqlComm!.CommandText = sql.ToString();
        _sqlComm.Parameters.Clear();
        return _sqlComm.ExecuteNonQuery();
    }

    /// <summary>
    /// 删除指定类型的表。
    /// </summary>
    public int DeleteTable<T>() where T : MySQLClassBaseClass
    {
        EnsureCommandReady();
        _sqlComm!.CommandText = $"DROP TABLE IF EXISTS `{GetTableName<T>()}`;";
        _sqlComm.Parameters.Clear();
        return _sqlComm.ExecuteNonQuery();
    }

    /// <summary>
    /// 检查表是否存在。
    /// </summary>
    public bool IsTableExist<T>() where T : MySQLClassBaseClass
    {
        EnsureCommandReady();
        _sqlComm!.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @TableName;";
        _sqlComm.Parameters.Clear();
        _sqlComm.Parameters.AddWithValue("@TableName", GetTableName<T>());
        return Convert.ToInt32(_sqlComm.ExecuteScalar()) != 0;
    }

    /// <summary>
    /// 插入或更新单条数据，根据主键进行替换。
    /// </summary>
    public int Insert<T>(T data) where T : MySQLClassBaseClass
    {
        if (data == null)
        {
            MySqlLog.LogWarning("Insert() 参数错误：data 为 null");
            return -1;
        }

        EnsureCommandReady();
        CreateTable<T>();

        List<PropertyInfo> properties = GetCachedProperties<T>();
        List<string> columnNames = properties.Select(p => GetAttribute(p).FieldName).ToList();
        List<string> paramNames = columnNames.Select(name => "@" + name).ToList();
        List<string> updateColumnNames = properties
            .Where(p => !GetAttribute(p).IsPrimaryKey)
            .Select(p => GetAttribute(p).FieldName)
            .ToList();

        string columns = string.Join(", ", columnNames.Select(name => $"`{name}`"));
        string parameters = string.Join(", ", paramNames);
        string updateClause = updateColumnNames.Count == 0
            ? string.Empty
            : " ON DUPLICATE KEY UPDATE " + string.Join(", ", updateColumnNames.Select(name => $"`{name}` = VALUES(`{name}`)"));

        _sqlComm!.CommandText = $"INSERT INTO `{GetTableName<T>()}` ({columns}) VALUES ({parameters}){updateClause};";
        _sqlComm.Parameters.Clear();

        foreach (PropertyInfo prop in properties)
        {
            MySQLClassHelpAttribute attr = GetAttribute(prop);
            _sqlComm.Parameters.AddWithValue("@" + attr.FieldName, NormalizeParameterValue(prop.GetValue(data)));
        }

        return _sqlComm.ExecuteNonQuery();
    }

    /// <summary>
    /// 批量插入或更新数据（同步）。
    /// </summary>
    public int Insert<T>(List<T> datas) where T : MySQLClassBaseClass
    {
        if (datas == null || datas.Count == 0)
        {
            MySqlLog.LogWarning("Insert(List) 参数错误：datas 为 null 或为空");
            return -1;
        }

        EnsureCommandReady();
        CreateTable<T>();

        int result = 0;
        using MySqlTransaction transaction = _sqlConn.BeginTransaction();
        try
        {
            foreach (T data in datas)
            {
                _sqlComm!.Transaction = transaction;
                result += InsertWithoutOwnTransaction(data);
            }

            transaction.Commit();
            _sqlComm!.Transaction = null;
            return result;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _sqlComm!.Transaction = null;
            MySqlLog.LogWarning($"Insert(List) 插入出错：{ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// 批量插入或更新数据（异步）。
    /// </summary>
    public Task<int> InsertAsync<T>(List<T> datas) where T : MySQLClassBaseClass
    {
        return Task.Run(() => Insert(datas));
    }

    /// <summary>
    /// 根据主键更新多条记录。
    /// </summary>
    public int UpdateByIds<T>(List<T> datas) where T : MySQLClassBaseClass
    {
        return Insert(datas);
    }

    /// <summary>
    /// 通过主键删除单条记录。
    /// </summary>
    public int DeleteByID<T>(object id) where T : MySQLClassBaseClass
    {
        EnsureCommandReady();
        PropertyInfo primaryKey = GetPrimaryKeyProperty<T>();
        string primaryFieldName = GetAttribute(primaryKey).FieldName;

        _sqlComm!.CommandText = $"DELETE FROM `{GetTableName<T>()}` WHERE `{primaryFieldName}` = @ID;";
        _sqlComm.Parameters.Clear();
        _sqlComm.Parameters.AddWithValue("@ID", NormalizeParameterValue(id));
        return _sqlComm.ExecuteNonQuery();
    }

    /// <summary>
    /// 通过主键删除多条记录（批量）。
    /// </summary>
    public int DeleteByID<T>(IEnumerable<object> ids) where T : MySQLClassBaseClass
    {
        if (ids == null)
        {
            MySqlLog.LogWarning("DeleteByID(IEnumerable) 参数错误：ids 为 null");
            return -1;
        }

        EnsureCommandReady();

        int result = 0;
        using MySqlTransaction transaction = _sqlConn.BeginTransaction();
        try
        {
            foreach (object id in ids)
            {
                _sqlComm!.Transaction = transaction;
                result += DeleteByID<T>(id);
            }

            transaction.Commit();
            _sqlComm!.Transaction = null;
            return result;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _sqlComm!.Transaction = null;
            MySqlLog.LogWarning($"DeleteByID(IEnumerable) 删除出错：{ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// 通过字段和值删除记录。
    /// </summary>
    public int DeleteByField<T>(string fieldName, object? value) where T : MySQLClassBaseClass
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            MySqlLog.LogWarning("DeleteByField() 参数错误：fieldName 为空");
            return -1;
        }

        EnsureCommandReady();
        _sqlComm!.CommandText = $"DELETE FROM `{GetTableName<T>()}` WHERE `{fieldName}` = @Value;";
        _sqlComm.Parameters.Clear();
        _sqlComm.Parameters.AddWithValue("@Value", NormalizeParameterValue(value));
        return _sqlComm.ExecuteNonQuery();
    }

    /// <summary>
    /// 通过自定义 SQL 条件删除记录。
    /// 调用方应只传入可信条件；外部输入建议优先使用 DeleteByField。
    /// </summary>
    public int DeleteBySql<T>(string sqlCondition) where T : MySQLClassBaseClass
    {
        if (string.IsNullOrWhiteSpace(sqlCondition))
        {
            MySqlLog.LogWarning("DeleteBySql() 参数错误：sqlCondition 为空");
            return -1;
        }

        EnsureCommandReady();
        _sqlComm!.CommandText = $"DELETE FROM `{GetTableName<T>()}` WHERE {sqlCondition};";
        _sqlComm.Parameters.Clear();
        return _sqlComm.ExecuteNonQuery();
    }

    /// <summary>
    /// 删除表中所有记录。
    /// </summary>
    public int DeleteAll<T>() where T : MySQLClassBaseClass
    {
        EnsureCommandReady();
        _sqlComm!.CommandText = $"DELETE FROM `{GetTableName<T>()}`;";
        _sqlComm.Parameters.Clear();
        return _sqlComm.ExecuteNonQuery();
    }

    /// <summary>
    /// 根据主键查询单条记录。
    /// </summary>
    public T? SelectByID<T>(object id) where T : MySQLClassBaseClass
    {
        EnsureCommandReady();
        PropertyInfo primaryKey = GetPrimaryKeyProperty<T>();
        string primaryFieldName = GetAttribute(primaryKey).FieldName;

        _sqlComm!.CommandText = $"SELECT * FROM `{GetTableName<T>()}` WHERE `{primaryFieldName}` = @ID;";
        _sqlComm.Parameters.Clear();
        _sqlComm.Parameters.AddWithValue("@ID", NormalizeParameterValue(id));

        using MySqlDataReader dr = _sqlComm.ExecuteReader();
        return dr.Read() ? DataReaderToData<T>(dr) : null;
    }

    /// <summary>
    /// 异步根据条件查询数据。
    /// </summary>
    public Task<List<T>> SelectBySqlAsync<T>(string sqlCondition = "") where T : MySQLClassBaseClass
    {
        return Task.Run(() => SelectBySql<T>(sqlCondition));
    }

    /// <summary>
    /// 同步根据条件查询数据。
    /// 在查询字符串值时确保值的内容通过引号包裹在内；外部输入建议优先使用 SelectByField。
    /// </summary>
    public List<T> SelectBySql<T>(string sqlCondition = "") where T : MySQLClassBaseClass
    {
        EnsureCommandReady();
        CreateTable<T>();

        _sqlComm!.CommandText = string.IsNullOrWhiteSpace(sqlCondition)
            ? $"SELECT * FROM `{GetTableName<T>()}`;"
            : $"SELECT * FROM `{GetTableName<T>()}` WHERE {sqlCondition};";
        _sqlComm.Parameters.Clear();

        var list = new List<T>();
        try
        {
            using MySqlDataReader dr = _sqlComm.ExecuteReader();
            while (dr.Read())
            {
                T? item = DataReaderToData<T>(dr);
                if (item != null)
                {
                    list.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            MySqlLog.LogWarning($"SelectBySql<{GetTableName<T>()}> 出错：{ex.Message}");
        }

        return list;
    }

    /// <summary>
    /// 按字段和值查询数据。
    /// </summary>
    public List<T> SelectByField<T>(string fieldName, object? value) where T : MySQLClassBaseClass
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return new List<T>();
        }

        EnsureCommandReady();
        CreateTable<T>();

        _sqlComm!.CommandText = $"SELECT * FROM `{GetTableName<T>()}` WHERE `{fieldName}` = @Value;";
        _sqlComm.Parameters.Clear();
        _sqlComm.Parameters.AddWithValue("@Value", NormalizeParameterValue(value));

        var list = new List<T>();
        using MySqlDataReader dr = _sqlComm.ExecuteReader();
        while (dr.Read())
        {
            T? item = DataReaderToData<T>(dr);
            if (item != null)
            {
                list.Add(item);
            }
        }

        return list;
    }

    /// <summary>
    /// 根据存储过程查询数据。
    /// </summary>
    public List<T> CallBySqlToGetData<T>(string name) where T : MySQLClassBaseClass
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new List<T>();
        }

        EnsureCommandReady();
        _sqlComm!.CommandText = $"CALL {name}";
        _sqlComm.Parameters.Clear();

        var list = new List<T>();
        try
        {
            using MySqlDataReader dr = _sqlComm.ExecuteReader();
            while (dr.Read())
            {
                T? item = DataReaderToData<T>(dr);
                if (item != null)
                {
                    list.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            MySqlLog.LogWarning($"CALL {name} 出错：{ex.Message}");
        }

        return list;
    }

    /// <summary>
    /// 根据存储过程添加数据。
    /// </summary>
    public int CallBySqlToAddData<T>(string param) where T : MySQLClassBaseClass
    {
        if (string.IsNullOrWhiteSpace(param))
        {
            return -1;
        }

        EnsureCommandReady();
        _sqlComm!.CommandText = $"CALL {param}";
        _sqlComm.Parameters.Clear();

        try
        {
            using MySqlDataReader dr = _sqlComm.ExecuteReader();
            return dr.Read() ? dr.GetInt32(0) : -1;
        }
        catch (Exception ex)
        {
            MySqlLog.LogWarning($"CALL {param} 出错：{ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// 根据存储过程获取 T 类型对应表中的数据索引最大值。
    /// </summary>
    public int GetMaxIndex<T>() where T : MySQLClassBaseClass
    {
        EnsureCommandReady();
        string tableName = GetTableName<T>();
        _sqlComm!.CommandText = "CALL Proc_S_S_returnMaxID(@TableName);";
        _sqlComm.Parameters.Clear();
        _sqlComm.Parameters.AddWithValue("@TableName", tableName);

        try
        {
            using MySqlDataReader dr = _sqlComm.ExecuteReader();
            return dr.Read() ? dr.GetInt32(0) : -1;
        }
        catch (Exception ex)
        {
            MySqlLog.LogWarning($"GetMaxIndex 出错：{ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// 根据存储过程获取 T 类型的破片数据。
    /// </summary>
    public List<T> GetFragments<T>(string param) where T : MySQLClassBaseClass
    {
        if (string.IsNullOrWhiteSpace(param))
        {
            return new List<T>();
        }

        EnsureCommandReady();
        _sqlComm!.CommandText = $"CALL {param}";
        _sqlComm.Parameters.Clear();

        var list = new List<T>();
        try
        {
            using MySqlDataReader dr = _sqlComm.ExecuteReader();
            while (dr.Read())
            {
                string? jsonString = dr[0]?.ToString();
                if (!string.IsNullOrWhiteSpace(jsonString))
                {
                    list = JsonConvert.DeserializeObject<List<T>>(jsonString) ?? new List<T>();
                }
            }
        }
        catch (Exception ex)
        {
            MySqlLog.LogWarning($"GetFragments 出错：{ex.Message}");
        }

        return list;
    }

    /// <summary>
    /// 将数据读取器转换为数据对象。
    /// </summary>
    private static T? DataReaderToData<T>(IDataRecord dr) where T : MySQLClassBaseClass
    {
        try
        {
            HashSet<string> fieldNames = Enumerable.Range(0, dr.FieldCount)
                .Select(dr.GetName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var data = Activator.CreateInstance<T>();
            foreach (PropertyInfo prop in GetCachedProperties<T>())
            {
                MySQLClassHelpAttribute attr = GetAttribute(prop);
                if (!prop.CanWrite || !attr.IsCreated || !fieldNames.Contains(attr.FieldName))
                {
                    continue;
                }

                object? value = dr[attr.FieldName] != DBNull.Value ? dr[attr.FieldName] : null;
                prop.SetValue(data, ConvertValue(value, prop.PropertyType));
            }

            return data;
        }
        catch (Exception ex)
        {
            MySqlLog.LogWarning($"DataReaderToData() 转换出错，类型 {typeof(T).Name}，具体消息为：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取并缓存类型 T 的可创建属性。
    /// </summary>
    private static List<PropertyInfo> GetCachedProperties<T>() where T : MySQLClassBaseClass
    {
        Type type = typeof(T);
        return PropertyCache.GetOrAdd(type, static currentType => currentType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<MySQLClassHelpAttribute>() is { IsCreated: true })
            .ToList());
    }

    private static PropertyInfo GetPrimaryKeyProperty<T>() where T : MySQLClassBaseClass
    {
        return GetCachedProperties<T>()
            .FirstOrDefault(p => GetAttribute(p).IsPrimaryKey)
            ?? throw new InvalidOperationException($"类型 {typeof(T).Name} 没有定义主键字段。 ");
    }

    private static MySQLClassHelpAttribute GetAttribute(PropertyInfo property)
    {
        return property.GetCustomAttribute<MySQLClassHelpAttribute>()
            ?? throw new InvalidOperationException($"属性 {property.Name} 缺少 MySQLClassHelpAttribute。 ");
    }

    private static bool IsAutoIncrementType(Type type)
    {
        Type normalizedType = Nullable.GetUnderlyingType(type) ?? type;
        return normalizedType == typeof(int) || normalizedType == typeof(long) || normalizedType == typeof(uint) || normalizedType == typeof(ulong);
    }

    private static object NormalizeParameterValue(object? value)
    {
        return value ?? DBNull.Value;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
        {
            Type normalizedTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            return normalizedTargetType.IsValueType ? Activator.CreateInstance(normalizedTargetType) : null;
        }

        Type actualTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (actualTargetType == typeof(string))
        {
            return Convert.ToString(value);
        }

        if (actualTargetType == typeof(bool))
        {
            if (value is bool boolValue)
            {
                return boolValue;
            }

            if (value is byte[] bytes && bytes.Length > 0)
            {
                return bytes[0] != 0;
            }

            return Convert.ToBoolean(value);
        }

        if (actualTargetType.IsEnum)
        {
            return Enum.Parse(actualTargetType, Convert.ToString(value) ?? string.Empty);
        }

        return Convert.ChangeType(value, actualTargetType);
    }

    private int InsertWithoutOwnTransaction<T>(T data) where T : MySQLClassBaseClass
    {
        List<PropertyInfo> properties = GetCachedProperties<T>();
        List<string> columnNames = properties.Select(p => GetAttribute(p).FieldName).ToList();
        List<string> paramNames = columnNames.Select(name => "@" + name).ToList();
        List<string> updateColumnNames = properties
            .Where(p => !GetAttribute(p).IsPrimaryKey)
            .Select(p => GetAttribute(p).FieldName)
            .ToList();

        string columns = string.Join(", ", columnNames.Select(name => $"`{name}`"));
        string parameters = string.Join(", ", paramNames);
        string updateClause = updateColumnNames.Count == 0
            ? string.Empty
            : " ON DUPLICATE KEY UPDATE " + string.Join(", ", updateColumnNames.Select(name => $"`{name}` = VALUES(`{name}`)"));

        _sqlComm!.CommandText = $"INSERT INTO `{GetTableName<T>()}` ({columns}) VALUES ({parameters}){updateClause};";
        _sqlComm.Parameters.Clear();

        foreach (PropertyInfo prop in properties)
        {
            MySQLClassHelpAttribute attr = GetAttribute(prop);
            _sqlComm.Parameters.AddWithValue("@" + attr.FieldName, NormalizeParameterValue(prop.GetValue(data)));
        }

        return _sqlComm.ExecuteNonQuery();
    }

    private void EnsureCommandReady()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MySqlCommand_TJ));
        }

        EnsureConnectionOpen();
        _sqlComm ??= new MySqlCommand { Connection = _sqlConn };
        _sqlComm.Connection ??= _sqlConn;
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _sqlComm?.Dispose();
            _sqlComm = null;
        }

        _disposed = true;
        base.Dispose(disposing);
    }
}
