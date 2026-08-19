using System.Globalization;

namespace CombatSimulation.Services.MySql;

/// <summary>
/// 按版本升级 CombatSimulation 使用的 MySQL 表结构。
/// 每个迁移步骤均为幂等操作，只有执行成功后才登记对应版本。
/// </summary>
internal static class DatabaseSchemaMigrator
{
    private const string VersionTableName = "t_schema_version";
    private static readonly object SyncRoot = new();

    public static void Apply(LoadAndWriteDb db)
    {
        ArgumentNullException.ThrowIfNull(db);

        lock (SyncRoot)
        {
            EnsureVersionTable(db);
            int currentVersion = GetCurrentVersion(db);

            ApplyMigration(db, currentVersion, 1, "创建基础业务表", () =>
            {
                db.CreateTable<Target_Info_By_MySQL>();
                db.CreateTable<Target_System_Info_By_MySQL>();
                db.CreateTable<Target_Part_Info_By_MySQL>();
                db.CreateTable<Damage_Tree_Info_By_MySQL>();
                db.CreateTable<Damage_Node_Info_By_MySQL>();
            });

            ApplyMigration(db, currentVersion, 2, "补齐毁伤节点排序字段", () =>
            {
                EnsureColumn(
                    db,
                    "t_d_node",
                    "d_n_SortOrder",
                    "ALTER TABLE `t_d_node` ADD COLUMN `d_n_SortOrder` INT NOT NULL DEFAULT 0;");
            });

            ApplyMigration(db, currentVersion, 3, "补齐关联查询索引", () =>
            {
                EnsureIndex(db, "t_t_system", "ix_t_t_system_target", "`t_s_TargetCode`");
                EnsureIndex(db, "t_t_system", "ix_t_t_system_parent", "`t_s_ParentCode`");
                EnsureIndex(db, "t_t_part", "ix_t_t_part_system", "`t_p_SystemCode`");
                EnsureIndex(db, "t_d_tree", "ix_t_d_tree_target", "`d_t_TargetCode`");
                EnsureIndex(db, "t_d_node", "ix_t_d_node_tree", "`d_n_TreeCode`");
                EnsureIndex(db, "t_d_node", "ix_t_d_node_parent", "`d_n_ParentCode`");
                EnsureIndex(db, "t_d_node", "ix_t_d_node_part", "`d_n_PartCode`");
            });
        }
    }

    private static void EnsureVersionTable(LoadAndWriteDb db)
    {
        db.mySqlCommand_TJ.ExecuteNonQuery(
            $"""
            CREATE TABLE IF NOT EXISTS `{VersionTableName}` (
                `Version` INT NOT NULL PRIMARY KEY,
                `Description` VARCHAR(255) NOT NULL,
                `AppliedUtc` DATETIME NOT NULL
            );
            """);
    }

    private static int GetCurrentVersion(LoadAndWriteDb db)
    {
        object? value = db.mySqlCommand_TJ.ExecuteScalar(
            $"SELECT COALESCE(MAX(`Version`), 0) FROM `{VersionTableName}`;");
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static void ApplyMigration(
        LoadAndWriteDb db,
        int currentVersion,
        int version,
        string description,
        Action migration)
    {
        if (currentVersion >= version)
        {
            return;
        }

        MySqlLog.Log($"开始执行数据库迁移 v{version}：{description}");
        migration();

        db.mySqlCommand_TJ.ExecuteNonQuery(
            $"""
            INSERT INTO `{VersionTableName}` (`Version`, `Description`, `AppliedUtc`)
            VALUES (@Version, @Description, UTC_TIMESTAMP())
            ON DUPLICATE KEY UPDATE
                `Description` = VALUES(`Description`),
                `AppliedUtc` = VALUES(`AppliedUtc`);
            """,
            ("@Version", version),
            ("@Description", description));

        MySqlLog.Log($"数据库迁移 v{version} 执行完成：{description}");
    }

    private static void EnsureColumn(
        LoadAndWriteDb db,
        string tableName,
        string columnName,
        string alterSql)
    {
        object? value = db.mySqlCommand_TJ.ExecuteScalar(
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = @TableName
              AND column_name = @ColumnName;
            """,
            ("@TableName", tableName),
            ("@ColumnName", columnName));

        if (Convert.ToInt32(value, CultureInfo.InvariantCulture) == 0)
        {
            db.mySqlCommand_TJ.ExecuteNonQuery(alterSql);
        }
    }

    private static void EnsureIndex(
        LoadAndWriteDb db,
        string tableName,
        string indexName,
        string columnSql)
    {
        object? value = db.mySqlCommand_TJ.ExecuteScalar(
            """
            SELECT COUNT(*)
            FROM information_schema.statistics
            WHERE table_schema = DATABASE()
              AND table_name = @TableName
              AND index_name = @IndexName;
            """,
            ("@TableName", tableName),
            ("@IndexName", indexName));

        if (Convert.ToInt32(value, CultureInfo.InvariantCulture) == 0)
        {
            db.mySqlCommand_TJ.ExecuteNonQuery(
                $"CREATE INDEX `{indexName}` ON `{tableName}` ({columnSql});");
        }
    }
}

