namespace CombatSimulation.MySQLClassBase
{
    /// <summary>
    /// 所有类的基类，仅包含ID
    /// </summary>
    public abstract class MySQLClassBaseClass
    {
        // 共有属性ID
        protected int id;

        // 虚属性，用于获取表名
        public abstract string TableName { get; }
    }
}
