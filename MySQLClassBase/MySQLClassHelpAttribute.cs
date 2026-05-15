namespace CombatSimulation.MySQLClassBase
{
    /// <summary>
    /// 用于设定与数据库相关的各数据类的特性
    /// 属性依次为：是否创建、对应的字段名、类型名、是否为主键、是否可以为空
    /// AttributeTargets.Property 表示该属性用于类中的属性
    /// Inherited = false 表示该属性不继承给子类
    /// AllowMultiple = false 表示该属性不能多次使用
    /// </summary>
    /// <remarks>
    /// 属性设置
    /// </remarks>
    /// <param name="isCreated">是否创建列</param>
    /// <param name="fieldName">列名</param>
    /// <param name="typeName">字段类型，如VARCHAR(255)</param>
    /// <param name="isPrimaryKey">是否为主键</param>
    /// <param name="isCanBeNull">是否可为空</param>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class MySQLClassHelpAttribute(bool isCreated, string fieldName, string typeName, bool isPrimaryKey, bool isCanBeNull = false) : Attribute
    {
        /// <summary>
        /// 是否可以创建
        /// </summary>
        public bool IsCreated { get; set; } = isCreated;

        /// <summary>
        /// 对应的字段名
        /// </summary>
        public string FieldName { get; set; } = fieldName;

        /// <summary>
        /// 对应的类型名
        /// </summary>
        public string TypeName { get; set; } = typeName;

        /// <summary>
        /// 是否为主键
        /// </summary>
        public bool IsPrimaryKey { get; set; } = isPrimaryKey;

        /// <summary>
        /// 是否可以为空
        /// </summary>
        public bool IsCanBeNull { get; set; } = isCanBeNull;     // 默认不为空
    }
}
