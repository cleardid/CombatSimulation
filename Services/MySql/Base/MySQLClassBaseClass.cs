namespace CombatSimulation.MySQLClassBase;

/// <summary>
/// 所有类的基类，仅包含ID
/// </summary>
public abstract class MySQLClassBaseClass
{
    // 共有属性 ID。当前 MySQL 映射主要使用业务主键，此字段保留用于兼容原 Unity 数据结构。
    protected int id;

    /// <summary>
    /// 当前数据类型映射到 MySQL 后使用的表名。
    /// </summary>
    public abstract string TableName { get; }
}
