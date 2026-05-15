using CombatSimulation.Models;

namespace CombatSimulation.Services.Unity
{
    /// <summary>
    /// 目标结构界面向 Unity 发送业务命令的服务接口。
    /// </summary>
    public interface IUnityTargetCommandService
    {
        /// <summary>
        /// 通知 Unity 显示指定目标。
        /// data 为目标唯一标识。
        /// </summary>
        Task ShowTargetAsync(string targetCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// 通知 Unity 高亮指定部件。
        /// data 为部件唯一标识。
        /// </summary>
        Task HighlightPartAsync(string partCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// 通知 Unity 显示当前勾选的全部部件。
        /// data 为部件唯一标识列表。
        /// </summary>
        Task ShowPartsAsync(IReadOnlyList<string> partCodes, CancellationToken cancellationToken = default);

        /// <summary>
        /// 通知 Unity 修改目标中的单个部件。
        /// data 为修改后的部件数据。
        /// </summary>
        Task UpdateTargetPartAsync(TargetPartInfoItem part, CancellationToken cancellationToken = default);
    }
}
