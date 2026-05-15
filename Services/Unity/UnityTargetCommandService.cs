using CombatSimulation.Models;
using CombatSimulation.Models.Unity;

namespace CombatSimulation.Services.Unity
{
    /// <summary>
    /// 目标结构界面 Unity 命令发送服务。
    /// </summary>
    /// <remarks>
    /// 显示类命令使用 event，不阻塞 UI；修改目标命令使用 request，并检查 Unity 返回结果。
    /// </remarks>
    public sealed class UnityTargetCommandService : IUnityTargetCommandService
    {
        private readonly UnityService _unityService;

        public UnityTargetCommandService(UnityService unityService)
        {
            _unityService = unityService;
        }

        /// <summary>
        /// 显示指定目标。
        /// </summary>
        public Task ShowTargetAsync(string targetCode, CancellationToken cancellationToken = default)
        {
            return _unityService.SendEventAsync(UnityCommandNames.ShowTarget, targetCode, cancellationToken);
        }

        /// <summary>
        /// 高亮指定部件。
        /// </summary>
        public Task HighlightPartAsync(string partCode, CancellationToken cancellationToken = default)
        {
            return _unityService.SendEventAsync(UnityCommandNames.HighlightPart, partCode, cancellationToken);
        }

        /// <summary>
        /// 显示当前勾选部件集合。
        /// </summary>
        public Task ShowPartsAsync(IReadOnlyList<string> partCodes, CancellationToken cancellationToken = default)
        {
            return _unityService.SendEventAsync(UnityCommandNames.ShowParts, partCodes.ToList(), cancellationToken);
        }

        /// <summary>
        /// 修改目标中的单个部件。
        /// </summary>
        public async Task UpdateTargetPartAsync(TargetPartInfoItem part, CancellationToken cancellationToken = default)
        {
            UnityMessage response = await _unityService
                .SendRequestAsync(UnityCommandNames.UpdateTarget, part, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (response.Success == false)
            {
                string message = response.Error?.Message ?? "Unity 修改目标失败。";
                throw new InvalidOperationException(message);
            }
        }
    }
}
