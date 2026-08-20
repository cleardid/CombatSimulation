using CombatSimulation.Models;
using CombatSimulation.Models.Unity;

namespace CombatSimulation.Services.Targets;

/// <summary>
/// 目标结构数据访问契约。ViewModel 仅依赖该接口，运行时默认使用 MySQL 实现。
/// </summary>
public interface ITargetInfoRepository
{
    Task<IReadOnlyList<TargetInfoItem>> LoadTargetsAsync(CancellationToken cancellationToken = default);
    Task<CombatDatabaseSnapshot> CreateDatabaseSnapshotAsync(CancellationToken cancellationToken = default);
    Task AddTargetAsync(TargetInfoItem target, CancellationToken cancellationToken = default);
    Task UpdateTargetAsync(TargetInfoItem target, CancellationToken cancellationToken = default);
    Task DeleteTargetAsync(string targetCode, CancellationToken cancellationToken = default);
    Task AddSystemAsync(string targetCode, TargetSystemInfoItem system, CancellationToken cancellationToken = default);
    Task UpdateSystemAsync(string targetCode, string originalSystemCode, TargetSystemInfoItem system, CancellationToken cancellationToken = default);
    Task<string?> DeleteSystemAsync(string targetCode, string systemCode, CancellationToken cancellationToken = default);
    Task AddPartAsync(string targetCode, TargetPartInfoItem part, CancellationToken cancellationToken = default);
    Task UpdatePartAsync(string targetCode, string originalPartCode, TargetPartInfoItem part, CancellationToken cancellationToken = default);
    Task<string?> DeletePartAsync(string targetCode, string partCode, CancellationToken cancellationToken = default);
}
