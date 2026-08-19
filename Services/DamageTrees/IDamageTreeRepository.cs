using CombatSimulation.Models;

namespace CombatSimulation.Services.DamageTrees;

/// <summary>
/// 毁伤树数据访问契约。ViewModel 仅依赖该接口，运行时默认使用 MySQL 实现。
/// </summary>
public interface IDamageTreeRepository
{
    IReadOnlyList<DamageTreeInfoItem> LoadDamageTrees(
        string targetCode,
        IReadOnlyDictionary<string, TargetPartInfoItem>? partsByCode = null);
    void AddDamageTree(DamageTreeInfoItem tree);
    void UpdateDamageTree(DamageTreeInfoItem tree);
    void DeleteDamageTree(string damageTreeCode);
    void AddNode(DamageTreeNodeItem node);
    void UpdateNode(string originalNodeCode, DamageTreeNodeItem node);
    void DeleteNode(string damageTreeCode, string nodeCode);
}
