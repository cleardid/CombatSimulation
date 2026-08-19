using CombatSimulation.Models;
using CombatSimulation.Models.Unity;

namespace CombatSimulation.Services.Targets;

/// <summary>
/// 目标结构数据访问契约。ViewModel 仅依赖该接口，运行时默认使用 MySQL 实现。
/// </summary>
public interface ITargetInfoRepository
{
    IReadOnlyList<TargetInfoItem> LoadTargets();
    CombatDatabaseSnapshot CreateDatabaseSnapshot();
    void AddTarget(TargetInfoItem target);
    void UpdateTarget(TargetInfoItem target);
    void DeleteTarget(string targetCode);
    void AddSystem(string targetCode, TargetSystemInfoItem system);
    void UpdateSystem(string targetCode, string originalSystemCode, TargetSystemInfoItem system);
    string? DeleteSystem(string targetCode, string systemCode);
    void AddPart(string targetCode, TargetPartInfoItem part);
    void UpdatePart(string targetCode, string originalPartCode, TargetPartInfoItem part);
    string? DeletePart(string targetCode, string partCode);
}
