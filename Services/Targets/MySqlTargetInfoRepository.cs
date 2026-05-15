using CombatSimulation.Models;
using CombatSimulation.Services.MySql;

namespace CombatSimulation.Services.Targets
{
    /// <summary>
    /// 基于 MySQL 的目标结构仓储实现。
    /// </summary>
    /// <remarks>
    /// 该类负责在 WPF 模型 TargetInfoItem / TargetSystemInfoItem / TargetPartInfoItem
    /// 与原 MySQL 数据结构 Target_Info_By_MySQL / Target_System_Info_By_MySQL / Target_Part_Info_By_MySQL 之间转换。
    /// MySQL 基础访问类不依赖 WPF，因此仍可由 Unity 端直接复用。
    /// </remarks>
    public sealed class MySqlTargetInfoRepository
    {
        private static string? _configuredConnectionString;

        private readonly string _connectionString;

        /// <summary>
        /// 创建默认 MySQL 仓储。
        /// 优先使用显式配置的连接字符串，其次使用环境变量或 target_mysql.json。
        /// </summary>
        public MySqlTargetInfoRepository()
        {
            _connectionString = ResolveDefaultConnectionString() ?? string.Empty;
        }

        /// <summary>
        /// 显式配置默认 MySQL 连接字符串，供 WPF 启动流程或 Unity 端初始化时调用。
        /// </summary>
        public static void ConfigureDefaultConnectionString(string? connectionString)
        {
            _configuredConnectionString = string.IsNullOrWhiteSpace(connectionString) ? null : connectionString.Trim();
        }

        /// <summary>
        /// 使用 MySQL 连接字符串创建仓储。
        /// </summary>
        public MySqlTargetInfoRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("MySQL 连接字符串不能为空。", nameof(connectionString));
            }

            _connectionString = connectionString;
        }

        /// <summary>
        /// 使用 MySQL 连接信息创建仓储。
        /// </summary>
        public MySqlTargetInfoRepository(MySQLConnectionInfo connectionInfo)
            : this(connectionInfo?.ToString() ?? throw new ArgumentNullException(nameof(connectionInfo)))
        {
        }

        /// <summary>
        /// 读取全部目标及其系统、部件结构。
        /// </summary>
        public IReadOnlyList<TargetInfoItem> LoadTargets()
        {
            using LoadAndWriteDb db = OpenDb();
            EnsureTables(db);

            List<Target_Info_By_MySQL> targetRows = db.GetTableData<Target_Info_By_MySQL>();
            List<Target_System_Info_By_MySQL> systemRows = db.GetTableData<Target_System_Info_By_MySQL>();
            List<Target_Part_Info_By_MySQL> partRows = db.GetTableData<Target_Part_Info_By_MySQL>();

            return BuildTargets(targetRows, systemRows, partRows);
        }

        /// <summary>
        /// 新增目标信息。
        /// </summary>
        public void AddTarget(TargetInfoItem target)
        {
            using LoadAndWriteDb db = OpenDb();
            EnsureTables(db);
            db.mySqlCommand_TJ.Insert(ToMySqlTarget(target));
        }

        /// <summary>
        /// 更新目标信息。
        /// </summary>
        public void UpdateTarget(TargetInfoItem target)
        {
            using LoadAndWriteDb db = OpenDb();
            EnsureTables(db);
            db.mySqlCommand_TJ.Insert(ToMySqlTarget(target));
        }

        /// <summary>
        /// 删除目标及其全部系统、部件和毁伤树信息。
        /// </summary>
        public void DeleteTarget(string targetCode)
        {
            if (string.IsNullOrWhiteSpace(targetCode))
            {
                return;
            }

            using LoadAndWriteDb db = OpenDb();
            EnsureTables(db);

            List<Target_System_Info_By_MySQL> systems = db.mySqlCommand_TJ.SelectByField<Target_System_Info_By_MySQL>("t_s_TargetCode", targetCode);
            foreach (string systemCode in systems.Select(item => item.SystemCode).Where(code => !string.IsNullOrWhiteSpace(code)).Distinct(StringComparer.Ordinal))
            {
                db.mySqlCommand_TJ.DeleteByField<Target_Part_Info_By_MySQL>("t_p_SystemCode", systemCode);
                db.mySqlCommand_TJ.DeleteByID<Target_System_Info_By_MySQL>(systemCode);
            }

            db.mySqlCommand_TJ.DeleteByID<Target_Info_By_MySQL>(targetCode);
        }

        /// <summary>
        /// 新增目标系统。
        /// </summary>
        public void AddSystem(string targetCode, TargetSystemInfoItem system)
        {
            using LoadAndWriteDb db = OpenDb();
            EnsureTables(db);
            db.mySqlCommand_TJ.Insert(ToMySqlSystem(system));
        }

        /// <summary>
        /// 更新目标系统。
        /// </summary>
        public void UpdateSystem(string targetCode, string originalSystemCode, TargetSystemInfoItem system)
        {
            using LoadAndWriteDb db = OpenDb();
            EnsureTables(db);

            if (!string.Equals(originalSystemCode, system.SystemCode, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(originalSystemCode))
            {
                // 如果系统主键变更，先写入新主键行，再同步子系统和部件外键，最后删除旧主键行。
                // 该顺序可兼容后续数据库增加外键约束的场景。
                db.mySqlCommand_TJ.Insert(ToMySqlSystem(system));

                List<Target_System_Info_By_MySQL> childSystems = db.mySqlCommand_TJ.SelectByField<Target_System_Info_By_MySQL>("t_s_ParentCode", originalSystemCode);
                foreach (Target_System_Info_By_MySQL childSystem in childSystems)
                {
                    childSystem.ParentCode = system.SystemCode;
                    db.mySqlCommand_TJ.Insert(childSystem);
                }

                List<Target_Part_Info_By_MySQL> parts = db.mySqlCommand_TJ.SelectByField<Target_Part_Info_By_MySQL>("t_p_SystemCode", originalSystemCode);
                foreach (Target_Part_Info_By_MySQL part in parts)
                {
                    part.PartSystemCode = system.SystemCode;
                    db.mySqlCommand_TJ.Insert(part);
                }

                db.mySqlCommand_TJ.DeleteByID<Target_System_Info_By_MySQL>(originalSystemCode);
                return;
            }

            db.mySqlCommand_TJ.Insert(ToMySqlSystem(system));
        }

        /// <summary>
        /// 删除目标系统及其子系统和底层部件。
        /// </summary>
        public void DeleteSystem(string targetCode, string systemCode)
        {
            if (string.IsNullOrWhiteSpace(systemCode))
            {
                return;
            }

            using LoadAndWriteDb db = OpenDb();
            EnsureTables(db);

            List<Target_System_Info_By_MySQL> allSystems = db.mySqlCommand_TJ.SelectByField<Target_System_Info_By_MySQL>("t_s_TargetCode", targetCode);
            HashSet<string> codesToDelete = CollectDescendantSystemCodes(systemCode, allSystems);

            foreach (string code in codesToDelete)
            {
                db.mySqlCommand_TJ.DeleteByField<Target_Part_Info_By_MySQL>("t_p_SystemCode", code);
            }

            foreach (string code in codesToDelete)
            {
                db.mySqlCommand_TJ.DeleteByID<Target_System_Info_By_MySQL>(code);
            }
        }

        /// <summary>
        /// 新增目标部件。
        /// </summary>
        public void AddPart(string targetCode, TargetPartInfoItem part)
        {
            using LoadAndWriteDb db = OpenDb();
            EnsureTables(db);
            db.mySqlCommand_TJ.Insert(ToMySqlPart(part));
        }

        /// <summary>
        /// 更新目标部件。
        /// </summary>
        public void UpdatePart(string targetCode, string originalPartCode, TargetPartInfoItem part)
        {
            using LoadAndWriteDb db = OpenDb();
            EnsureTables(db);

            if (!string.Equals(originalPartCode, part.PartCode, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(originalPartCode))
            {
                db.mySqlCommand_TJ.Insert(ToMySqlPart(part));
                db.mySqlCommand_TJ.DeleteByID<Target_Part_Info_By_MySQL>(originalPartCode);
                return;
            }

            db.mySqlCommand_TJ.Insert(ToMySqlPart(part));
        }

        /// <summary>
        /// 删除目标部件。
        /// </summary>
        public void DeletePart(string targetCode, string partCode)
        {
            if (string.IsNullOrWhiteSpace(partCode))
            {
                return;
            }

            using LoadAndWriteDb db = OpenDb();
            EnsureTables(db);
            db.mySqlCommand_TJ.DeleteByID<Target_Part_Info_By_MySQL>(partCode);
        }

        private LoadAndWriteDb OpenDb()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("未配置 MySQL 连接信息。请设置 TARGET_MYSQL_CONNECTION_STRING，或在程序输出目录放置 target_mysql.json。");
            }

            return new LoadAndWriteDb(_connectionString);
        }

        private static string? ResolveDefaultConnectionString()
        {
            return _configuredConnectionString ?? MySQLConnectionInfo.TryBuildDefaultConnectionString();
        }

        private static void EnsureTables(LoadAndWriteDb db)
        {
            db.IsTableExist<Target_Info_By_MySQL>();
            db.IsTableExist<Target_System_Info_By_MySQL>();
            db.IsTableExist<Target_Part_Info_By_MySQL>();
        }

        private static IReadOnlyList<TargetInfoItem> BuildTargets(
            List<Target_Info_By_MySQL> targetRows,
            List<Target_System_Info_By_MySQL> systemRows,
            List<Target_Part_Info_By_MySQL> partRows)
        {
            Dictionary<string, List<Target_System_Info_By_MySQL>> systemsByTarget = systemRows
                .Where(item => !string.IsNullOrWhiteSpace(item.TargetCode))
                .GroupBy(item => item.TargetCode, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            Dictionary<string, List<Target_Part_Info_By_MySQL>> partsBySystem = partRows
                .Where(item => !string.IsNullOrWhiteSpace(item.PartSystemCode))
                .GroupBy(item => item.PartSystemCode, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            List<TargetInfoItem> targets = new();
            foreach (Target_Info_By_MySQL targetRow in targetRows.OrderBy(item => item.TargetName, StringComparer.CurrentCulture))
            {
                TargetInfoItem target = FromMySqlTarget(targetRow);
                if (systemsByTarget.TryGetValue(target.Code, out List<Target_System_Info_By_MySQL>? targetSystems))
                {
                    foreach (TargetSystemInfoItem system in BuildTopSystems(targetSystems, partsBySystem))
                    {
                        target.Systems.Add(system);
                    }
                }

                targets.Add(target);
            }

            return targets;
        }

        private static IEnumerable<TargetSystemInfoItem> BuildTopSystems(
            List<Target_System_Info_By_MySQL> targetSystems,
            Dictionary<string, List<Target_Part_Info_By_MySQL>> partsBySystem)
        {
            Dictionary<string, List<Target_System_Info_By_MySQL>> childRowsByParent = targetSystems
                .Where(item => !string.IsNullOrWhiteSpace(item.ParentCode) && item.ParentCode != "-1")
                .GroupBy(item => item.ParentCode, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            List<Target_System_Info_By_MySQL> topRows = targetSystems
                .Where(item => item.TargetCategory || string.IsNullOrWhiteSpace(item.ParentCode) || item.ParentCode == "-1")
                .OrderBy(item => item.SystemName, StringComparer.CurrentCulture)
                .ToList();

            HashSet<string> visited = new(StringComparer.Ordinal);
            foreach (Target_System_Info_By_MySQL topRow in topRows)
            {
                yield return BuildSystem(topRow, childRowsByParent, partsBySystem, visited);
            }

            // 防止数据库里存在 ParentCode 错误导致系统无法显示。
            foreach (Target_System_Info_By_MySQL orphanRow in targetSystems.Where(item => !visited.Contains(item.SystemCode)))
            {
                yield return BuildSystem(orphanRow, childRowsByParent, partsBySystem, visited);
            }
        }

        private static TargetSystemInfoItem BuildSystem(
            Target_System_Info_By_MySQL row,
            Dictionary<string, List<Target_System_Info_By_MySQL>> childRowsByParent,
            Dictionary<string, List<Target_Part_Info_By_MySQL>> partsBySystem,
            HashSet<string> visited)
        {
            TargetSystemInfoItem system = FromMySqlSystem(row);
            if (!visited.Add(system.SystemCode))
            {
                return system;
            }

            if (childRowsByParent.TryGetValue(system.SystemCode, out List<Target_System_Info_By_MySQL>? childRows))
            {
                foreach (Target_System_Info_By_MySQL childRow in childRows.OrderBy(item => item.SystemName, StringComparer.CurrentCulture))
                {
                    system.ChildSystems.Add(BuildSystem(childRow, childRowsByParent, partsBySystem, visited));
                }
            }

            if (partsBySystem.TryGetValue(system.SystemCode, out List<Target_Part_Info_By_MySQL>? partRows))
            {
                foreach (Target_Part_Info_By_MySQL partRow in partRows.OrderBy(item => item.PartName, StringComparer.CurrentCulture))
                {
                    system.Parts.Add(FromMySqlPart(partRow));
                }
            }

            return system;
        }

        private static HashSet<string> CollectDescendantSystemCodes(string rootSystemCode, List<Target_System_Info_By_MySQL> allSystems)
        {
            Dictionary<string, List<Target_System_Info_By_MySQL>> childRowsByParent = allSystems
                .Where(item => !string.IsNullOrWhiteSpace(item.ParentCode))
                .GroupBy(item => item.ParentCode, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            HashSet<string> result = new(StringComparer.Ordinal) { rootSystemCode };
            Queue<string> queue = new();
            queue.Enqueue(rootSystemCode);

            while (queue.Count > 0)
            {
                string currentCode = queue.Dequeue();
                if (!childRowsByParent.TryGetValue(currentCode, out List<Target_System_Info_By_MySQL>? children))
                {
                    continue;
                }

                foreach (Target_System_Info_By_MySQL child in children)
                {
                    if (result.Add(child.SystemCode))
                    {
                        queue.Enqueue(child.SystemCode);
                    }
                }
            }

            return result;
        }

        private static TargetInfoItem FromMySqlTarget(Target_Info_By_MySQL row)
        {
            return new TargetInfoItem
            {
                Code = row.TargetCode ?? string.Empty,
                Name = row.TargetName ?? string.Empty,
                Category = row.TargetCategory ?? string.Empty,
                Description = row.TargetDescription ?? string.Empty
            };
        }

        private static TargetSystemInfoItem FromMySqlSystem(Target_System_Info_By_MySQL row)
        {
            return new TargetSystemInfoItem
            {
                SystemCode = row.SystemCode ?? string.Empty,
                SystemName = row.SystemName ?? string.Empty,
                SystemDescription = row.SystemDescription ?? string.Empty,
                IsTopSystem = row.TargetCategory,
                ParentSystemCode = string.IsNullOrWhiteSpace(row.ParentCode) ? "-1" : row.ParentCode,
                TargetCode = row.TargetCode ?? string.Empty
            };
        }

        private static TargetPartInfoItem FromMySqlPart(Target_Part_Info_By_MySQL row)
        {
            string shapeType = TargetPartShapeParameterDefinitions.NormalizeShapeType(row.PartShape);
            TargetPartInfoItem part = new()
            {
                PartCode = row.PartCode ?? string.Empty,
                PartName = row.PartName ?? string.Empty,
                ShapeType = shapeType,
                PartDescription = row.PartDescription ?? string.Empty,
                MaterialId = row.PartMaterial ?? string.Empty,
                DisplayColor = string.IsNullOrWhiteSpace(row.PartColor) ? "#808080" : row.PartColor,
                EquivalentThickness = row.PartEquThickness,
                VulnerableArea = row.PartVulnerableArea,
                SystemCode = row.PartSystemCode ?? string.Empty
            };

            IReadOnlyList<string> parameterNames = TargetPartShapeParameterDefinitions.GetParameterNames(shapeType);

            for (int index = 1; index <= parameterNames.Count && index <= 24; index++)
            {
                part.Parameters.Add(new TargetPartParameterItem
                {
                    Index = index,
                    Name = parameterNames[index - 1],
                    Value = GetPartParameterValue(row, index),
                    Unit = "mm"
                });
            }

            return part;
        }

        private static Target_Info_By_MySQL ToMySqlTarget(TargetInfoItem target)
        {
            return new Target_Info_By_MySQL
            {
                TargetCode = target.Code?.Trim() ?? string.Empty,
                TargetName = target.Name?.Trim() ?? string.Empty,
                TargetCategory = target.Category?.Trim() ?? string.Empty,
                TargetDescription = target.Description?.Trim() ?? string.Empty
            };
        }

        private static Target_System_Info_By_MySQL ToMySqlSystem(TargetSystemInfoItem system)
        {
            return new Target_System_Info_By_MySQL
            {
                SystemCode = system.SystemCode?.Trim() ?? string.Empty,
                SystemName = system.SystemName?.Trim() ?? string.Empty,
                SystemDescription = system.SystemDescription?.Trim() ?? string.Empty,
                TargetCategory = system.IsTopSystem,
                ParentCode = string.IsNullOrWhiteSpace(system.ParentSystemCode) ? "-1" : system.ParentSystemCode.Trim(),
                TargetCode = system.TargetCode?.Trim() ?? string.Empty
            };
        }

        private static Target_Part_Info_By_MySQL ToMySqlPart(TargetPartInfoItem part)
        {
            Target_Part_Info_By_MySQL row = new()
            {
                PartCode = part.PartCode?.Trim() ?? string.Empty,
                PartName = part.PartName?.Trim() ?? string.Empty,
                PartShape = TargetPartShapeParameterDefinitions.NormalizeShapeType(part.ShapeType),
                PartDescription = part.PartDescription?.Trim() ?? string.Empty,
                PartMaterial = string.IsNullOrWhiteSpace(part.MaterialId) ? "Fe" : part.MaterialId.Trim(),
                PartVulnerableArea = Convert.ToSingle(part.VulnerableArea),
                PartEquThickness = Convert.ToSingle(part.EquivalentThickness),
                PartColor = string.IsNullOrWhiteSpace(part.DisplayColor) ? "#808080" : part.DisplayColor.Trim(),
                PartSystemCode = part.SystemCode?.Trim() ?? string.Empty
            };

            foreach (TargetPartParameterItem parameter in part.Parameters.OrderBy(item => item.Index).Take(24))
            {
                SetPartParameterValue(row, parameter.Index, Convert.ToSingle(parameter.Value));
            }

            return row;
        }

        private static float GetPartParameterValue(Target_Part_Info_By_MySQL part, int index)
        {
            return index switch
            {
                1 => part.PartEquParam1,
                2 => part.PartEquParam2,
                3 => part.PartEquParam3,
                4 => part.PartEquParam4,
                5 => part.PartEquParam5,
                6 => part.PartEquParam6,
                7 => part.PartEquParam7,
                8 => part.PartEquParam8,
                9 => part.PartEquParam9,
                10 => part.PartEquParam10,
                11 => part.PartEquParam11,
                12 => part.PartEquParam12,
                13 => part.PartEquParam13,
                14 => part.PartEquParam14,
                15 => part.PartEquParam15,
                16 => part.PartEquParam16,
                17 => part.PartEquParam17,
                18 => part.PartEquParam18,
                19 => part.PartEquParam19,
                20 => part.PartEquParam20,
                21 => part.PartEquParam21,
                22 => part.PartEquParam22,
                23 => part.PartEquParam23,
                24 => part.PartEquParam24,
                _ => 0
            };
        }

        private static void SetPartParameterValue(Target_Part_Info_By_MySQL part, int index, float value)
        {
            switch (index)
            {
                case 1: part.PartEquParam1 = value; break;
                case 2: part.PartEquParam2 = value; break;
                case 3: part.PartEquParam3 = value; break;
                case 4: part.PartEquParam4 = value; break;
                case 5: part.PartEquParam5 = value; break;
                case 6: part.PartEquParam6 = value; break;
                case 7: part.PartEquParam7 = value; break;
                case 8: part.PartEquParam8 = value; break;
                case 9: part.PartEquParam9 = value; break;
                case 10: part.PartEquParam10 = value; break;
                case 11: part.PartEquParam11 = value; break;
                case 12: part.PartEquParam12 = value; break;
                case 13: part.PartEquParam13 = value; break;
                case 14: part.PartEquParam14 = value; break;
                case 15: part.PartEquParam15 = value; break;
                case 16: part.PartEquParam16 = value; break;
                case 17: part.PartEquParam17 = value; break;
                case 18: part.PartEquParam18 = value; break;
                case 19: part.PartEquParam19 = value; break;
                case 20: part.PartEquParam20 = value; break;
                case 21: part.PartEquParam21 = value; break;
                case 22: part.PartEquParam22 = value; break;
                case 23: part.PartEquParam23 = value; break;
                case 24: part.PartEquParam24 = value; break;
            }
        }
    }
}
