using CombatSimulation.Models;

namespace CombatSimulation.Services.DamageTrees;

/// <summary>
/// 毁伤树数据访问契约。ViewModel 仅依赖该接口，运行时默认使用 MySQL 实现。
/// </summary>
public interface IDamageTreeRepository
{
    Task<IReadOnlyList<DamageTreeInfoItem>> LoadDamageTreesAsync(
        string targetCode,
        IReadOnlyDictionary<string, TargetPartInfoItem>? partsByCode = null,
        CancellationToken cancellationToken = default);
    Task AddDamageTreeAsync(DamageTreeInfoItem tree, CancellationToken cancellationToken = default);
    Task UpdateDamageTreeAsync(DamageTreeInfoItem tree, CancellationToken cancellationToken = default);
    Task DeleteDamageTreeAsync(string damageTreeCode, CancellationToken cancellationToken = default);
    Task AddNodeAsync(DamageTreeNodeItem node, CancellationToken cancellationToken = default);
    Task UpdateNodeAsync(string originalNodeCode, DamageTreeNodeItem node, CancellationToken cancellationToken = default);
    Task DeleteNodeAsync(string damageTreeCode, string nodeCode, CancellationToken cancellationToken = default);
}
