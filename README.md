# CombatSimulation

CombatSimulation 是一个基于 .NET 8/WPF 的作战仿真管理端。WPF 负责目标结构和毁伤树的编辑，MySQL 是持久化数据的唯一事实来源，Unity 通过 TCP 接收数据库只读快照并负责三维显示。

## 环境要求

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022（可选，需安装“.NET 桌面开发”工作负载）
- MySQL 8.x
- Unity 客户端（只在联调三维显示时需要）

## MySQL 配置

复制示例文件并填写本机连接信息：

```powershell
Copy-Item target_mysql.example.json target_mysql.json
```

`target_mysql.json` 会在构建时复制到程序输出目录。它包含数据库账号和密码，已被 `.gitignore` 排除，不应提交到 Git。

程序首次连接时会创建数据库结构，并通过 `t_schema_version` 记录迁移版本。当前迁移会创建五张业务表、补齐毁伤节点排序字段及常用关联索引。结构升级应继续新增迁移版本，不要只修改模型类后依赖 `CREATE TABLE IF NOT EXISTS`。

## NuGet 还原与构建

“还原”只负责解析并下载项目声明的依赖，不会重新编译业务代码。以下情况必须重新还原：

- 首次克隆项目；
- `PackageReference` 或目标框架发生变化；
- 删除了 `obj/project.assets.json`；
- 清理了本机 NuGet 全局缓存。

完成一次有效还原后，日常构建可以完全跳过 NuGet 访问：

```powershell
dotnet build CombatSimulation.sln --no-restore
```

需要更新依赖时再显式执行：

```powershell
dotnet restore CombatSimulation.sln
```

如果已在 Visual Studio 中关闭自动还原，构建不会替你访问 nuget.org；但本机必须保留一次成功还原生成的 assets 文件和缓存包。出现 `NU1101`、`NU1102` 时，先用以下命令确认源状态：

```powershell
dotnet nuget list source
```

当前主项目依赖 `CommunityToolkit.Mvvm`、`Newtonsoft.Json` 和 `MySql.Data`。禁用 nuget.org 后，如果这些版本不在本机缓存中，首次还原仍会失败。

## 运行

```powershell
dotnet run --project CombatSimulation.csproj --no-restore
```

启动前确认输出目录存在有效的 `target_mysql.json`。数据库不可用时，目标页会显示具体异常；查询错误不会再被当成空数据处理。

## 数据一致性约定

- 多表新增、修改和删除在单个 MySQL 事务中提交。
- MySQL 是唯一事实来源；数据库提交成功后才更新 WPF 绑定模型并通知 Unity。
- Unity 全量快照使用单连接、`RepeatableRead` 事务读取目标、系统、部件、毁伤树和毁伤节点五张表。
- Unity 暂时离线不会回滚已提交的数据库数据；重连并上报 `server_ready` 后会重新加载完整快照。
- 删除被毁伤树节点引用的部件时返回业务原因，不产生悬空引用。

## 回归测试

`CombatSimulation.Tests` 是不增加第三方测试包的离线测试驱动器，适合当前关闭 NuGet 自动访问的环境。先构建解决方案，再运行：

```powershell
dotnet build CombatSimulation.sln --no-restore
dotnet run --project CombatSimulation.Tests/CombatSimulation.Tests.csproj --no-build --no-restore
```

任何测试失败都会返回非零退出码，可直接接入 CI。当前覆盖颜色格式、毁伤等级规则、默认毁伤等级选择，以及 ViewModel 构造不访问数据库和事件释放行为。

涉及真实事务、迁移和并发快照的测试仍需要独立 MySQL 测试库；不要对生产数据库执行破坏性集成测试。

## 主要目录

- `Models/`：WPF、MySQL 和 Unity 消息模型
- `Services/MySql/`：连接、通用命令、事务和结构迁移
- `Services/Targets/`：目标结构仓储
- `Services/DamageTrees/`：毁伤树仓储
- `Services/Unity/`：Unity TCP 命令适配
- `ViewModels/Targets/`：目标页业务与同步编排
- `CombatSimulation.Tests/`：无第三方测试框架的离线回归测试
