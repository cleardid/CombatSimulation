using CombatSimulation.Models;
using CombatSimulation.Models.Unity;
using CombatSimulation.Services.MySql;
using System.Diagnostics;
using System.Windows;

namespace CombatSimulation.ViewModels;

public sealed partial class TargetInfoViewModel
{
    /// <summary>
    /// Unity 主动事件入口。
    /// </summary>
    /// <remarks>
    /// 当前重点处理 server_ready：Unity 已经完成 TCP Server 启动并接受 WPF 连接，
    /// 此时 WPF 需要把数据库快照推送过去，保证后续 show_target / highlight_part 只依赖唯一标识即可执行。
    /// </remarks>
    private void OnUnityEventReceived(object? sender, UnityMessage message)
    {
        if (message == null)
        {
            return;
        }

        if (string.Equals(message.Command, UnityCommandNames.ServerReady, StringComparison.OrdinalIgnoreCase))
        {
            // Unity 可能在每次客户端重连时都发送 server_ready。
            // 这里始终重新推送快照，保证 Unity 场景缓存和当前 MySQL 数据一致。
            ScheduleDatabaseSnapshotUnitySync(refreshCurrentDisplayAfterSync: true, delayMilliseconds: 0);
            return;
        }

        if (string.Equals(message.Command, UnityCommandNames.RequestDatabaseSnapshot, StringComparison.OrdinalIgnoreCase))
        {
            // 兜底扩展：如果 Unity 主动发现缓存为空，也可以要求 WPF 重新推送一次全量快照。
            ScheduleDatabaseSnapshotUnitySync(refreshCurrentDisplayAfterSync: true, delayMilliseconds: 0);
        }
    }

    /// <summary>
    /// 向 Unity 发送“显示目标”命令。
    /// </summary>
    /// <remarks>
    /// 该方法只发送目标唯一标识。目标完整数据由 load_database_snapshot 提前同步。
    /// </remarks>
    private async Task SendSelectedTargetToUnityAsync(TargetInfoItem? target, CancellationToken cancellationToken)
    {
        if (target == null || string.IsNullOrWhiteSpace(target.Code))
        {
            return;
        }

        try
        {
            await _unityCommandService.ShowTargetAsync(target.Code, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 用户快速切换目标时，旧目标命令会被取消。这属于正常流程，不应提示错误。
        }
        catch (Exception ex)
        {
            SetStatusTextOnUiThread($"Unity 显示目标命令发送失败：{ex.Message}");
            Debug.WriteLine($"[TargetInfoViewModel] Unity 显示目标失败：{ex}");
        }
    }

    /// <summary>
    /// 向 Unity 发送“高亮部件”命令。
    /// </summary>
    private async Task SendHighlightedPartToUnityAsync(TargetPartInfoItem part, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(part.PartCode))
        {
            return;
        }

        try
        {
            await _unityCommandService.HighlightPartAsync(part.PartCode, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 结构树选中项变化很频繁，旧部件高亮命令被取消时不需要处理。
        }
        catch (Exception ex)
        {
            SetStatusTextOnUiThread($"Unity 高亮部件命令发送失败：{ex.Message}");
            Debug.WriteLine($"[TargetInfoViewModel] Unity 高亮部件失败：{ex}");
        }
    }

    /// <summary>
    /// 安排一次数据库快照同步。
    /// </summary>
    /// <param name="refreshCurrentDisplayAfterSync">
    /// 快照成功送达后，是否重新发送当前目标、当前勾选部件和当前高亮部件。
    /// server_ready 场景必须为 true，因为 Unity 刚启动时缓存为空，需要先收快照再执行显示命令。
    /// </param>
    /// <param name="delayMilliseconds">
    /// 延迟时间。数据库连续写入时使用小延迟合并多次全量快照发送。
    /// </param>
    private void ScheduleDatabaseSnapshotUnitySync(bool refreshCurrentDisplayAfterSync, int delayMilliseconds = 250)
    {
        _databaseSnapshotSyncCts?.Cancel();

        UnityDisplayRefreshState displayState = CaptureCurrentUnityDisplayState();
        CancellationTokenSource cts = new();
        _databaseSnapshotSyncCts = cts;

        RunUnityCommandInBackground(
            token => SendDatabaseSnapshotToUnityAsync(displayState, refreshCurrentDisplayAfterSync, delayMilliseconds, cts, token),
            cts.Token);
    }

    /// <summary>
    /// 数据库写入成功后调用。负责按统一策略刷新 Unity 运行期数据库缓存。
    /// </summary>
    /// <remarks>
    /// 目标、系统、部件、毁伤树、毁伤节点发生新增、修改、删除后，WPF 是数据库唯一写入方，
    /// Unity 只通过该全量快照同步自己的内存表。小延迟用于合并一次弹窗保存过程中可能产生的连续写库操作。
    /// </remarks>
    private void NotifyDatabaseChangedForUnity(bool refreshCurrentDisplayAfterSync = true)
    {
        ScheduleDatabaseSnapshotUnitySync(refreshCurrentDisplayAfterSync, delayMilliseconds: 250);
    }

    /// <summary>
    /// 实际创建数据库快照并发送给 Unity。
    /// </summary>
    private async Task SendDatabaseSnapshotToUnityAsync(
        UnityDisplayRefreshState displayState,
        bool refreshCurrentDisplayAfterSync,
        int delayMilliseconds,
        CancellationTokenSource cts,
        CancellationToken cancellationToken)
    {
        try
        {
            if (delayMilliseconds > 0)
            {
                await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            CombatDatabaseSnapshot snapshot = CreateUnityDatabaseSnapshot();
            await _unityCommandService.LoadDatabaseSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);

            SetStatusTextOnUiThread($"已同步数据库快照到 Unity：目标 {snapshot.Targets.Count}，系统 {snapshot.TargetSystems.Count}，部件 {snapshot.TargetParts.Count}，毁伤树 {snapshot.DamageTrees.Count}，毁伤节点 {snapshot.DamageNodes.Count}");

            if (refreshCurrentDisplayAfterSync)
            {
                await RefreshUnityCurrentDisplayAsync(displayState, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || cts.IsCancellationRequested)
        {
            // 新的快照同步已经覆盖旧同步，取消属于正常流程。
        }
        catch (Exception ex)
        {
            SetStatusTextOnUiThread($"Unity 数据库快照同步失败：{ex.Message}");
            MySqlLog.LogWarning($"Unity 数据库快照同步失败：{ex}");
            Debug.WriteLine($"[TargetInfoViewModel] Unity 数据库快照同步失败：{ex}");
        }
        finally
        {
            if (ReferenceEquals(_databaseSnapshotSyncCts, cts))
            {
                _databaseSnapshotSyncCts = null;
            }

            cts.Dispose();
        }
    }

    /// <summary>
    /// 从 MySQL 仓储读取当前全量表数据，并组装为 Unity 端可直接反序列化的快照对象。
    /// </summary>
    private CombatDatabaseSnapshot CreateUnityDatabaseSnapshot()
    {
        CombatDatabaseSnapshot snapshot = new();
        _targetInfoRepository.AppendTargetTablesToSnapshot(snapshot);
        _damageTreeRepository.AppendDamageTreeTablesToSnapshot(snapshot);
        return snapshot;
    }

    /// <summary>
    /// 快照同步后恢复 Unity 当前显示状态。
    /// </summary>
    /// <remarks>
    /// server_ready 后 Unity 缓存刚被重建，需要重新发送当前目标、勾选部件和高亮部件。
    /// 数据库变更后也可复用该流程，避免新增目标或新增部件时早先的显示命令因缓存未就绪而丢失。
    /// </remarks>
    private async Task RefreshUnityCurrentDisplayAsync(UnityDisplayRefreshState displayState, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(displayState.TargetCode))
        {
            await _unityCommandService.ShowTargetAsync(displayState.TargetCode, cancellationToken).ConfigureAwait(false);
        }

        await _unityCommandService.ShowPartsAsync(displayState.CheckedPartCodes, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(displayState.HighlightPartCode))
        {
            await _unityCommandService.HighlightPartAsync(displayState.HighlightPartCode, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 捕获当前界面需要在 Unity 中恢复的显示状态。
    /// </summary>
    /// <remarks>
    /// 该方法会读取 WPF 绑定对象，因此如果调用来自 TCP 接收线程，需要切回 UI 线程执行。
    /// </remarks>
    private UnityDisplayRefreshState CaptureCurrentUnityDisplayState()
    {
        System.Windows.Threading.Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(CaptureCurrentUnityDisplayStateCore);
        }

        return CaptureCurrentUnityDisplayStateCore();
    }

    /// <summary>
    /// 在 UI 线程读取当前目标、勾选部件和高亮部件。
    /// </summary>
    private UnityDisplayRefreshState CaptureCurrentUnityDisplayStateCore()
    {
        return new UnityDisplayRefreshState(
            SelectedTarget?.Code ?? string.Empty,
            GetCheckedPartCodes(SelectedTarget),
            SelectedStructureNode?.Part?.PartCode ?? string.Empty);
    }

    /// <summary>
    /// 安排一次勾选部件同步。
    /// </summary>
    /// <remarks>
    /// 勾选父节点会引起大量子节点状态变化，因此这里采用取消前一次、延迟发送最新快照的策略。
    /// </remarks>
    private void ScheduleCheckedPartUnitySync()
    {
        _checkedPartSyncCts?.Cancel();

        // 在 UI 线程只做当前勾选部件快照，后续延迟、序列化和 TCP 发送都放到后台执行，
        // 避免切换目标或连续勾选时被 Unity 连接/发送流程阻塞。
        List<string> checkedPartCodes = GetCheckedPartCodes(SelectedTarget);
        CancellationTokenSource cts = new();
        _checkedPartSyncCts = cts;
        RunUnityCommandInBackground(token => SendCheckedPartsToUnityAsync(checkedPartCodes, cts, token), cts.Token);
    }

    /// <summary>
    /// 延迟发送“显示部件集合”命令。
    /// </summary>
    private async Task SendCheckedPartsToUnityAsync(IReadOnlyList<string> checkedPartCodes, CancellationTokenSource cts, CancellationToken cancellationToken)
    {
        try
        {
            // 短延迟用于合并父子节点级联勾选产生的一组连续事件。
            await Task.Delay(120, cancellationToken).ConfigureAwait(false);
            await _unityCommandService.ShowPartsAsync(checkedPartCodes, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || cts.IsCancellationRequested)
        {
            // 新的勾选同步已经覆盖旧同步，取消属于正常流程。
        }
        catch (Exception ex)
        {
            SetStatusTextOnUiThread($"Unity 显示部件命令发送失败：{ex.Message}");
            Debug.WriteLine($"[TargetInfoViewModel] Unity 显示勾选部件失败：{ex}");
        }
        finally
        {
            if (ReferenceEquals(_checkedPartSyncCts, cts))
            {
                _checkedPartSyncCts = null;
            }

            cts.Dispose();
        }
    }

    /// <summary>
    /// 在后台线程执行 Unity 命令，避免 TCP 连接、序列化或等待响应阻塞 WPF UI 线程。
    /// </summary>
    private void RunUnityCommandInBackground(Func<CancellationToken, Task> commandFactory, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await commandFactory(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 取消由上层控制，不需要冒泡到 UI。
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// 从后台线程安全地更新状态文本。
    /// </summary>
    private void SetStatusTextOnUiThread(string message)
    {
        System.Windows.Threading.Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            StatusText = message;
            return;
        }

        dispatcher.BeginInvoke(() => StatusText = message);
    }

    /// <summary>
    /// 结构树节点被用户勾选或取消勾选时触发 Unity 部件显示同步。
    /// </summary>
    private void OnStructureNodeCheckStateChanged(object? sender, EventArgs e)
    {
        ScheduleCheckedPartUnitySync();
    }

    /// <summary>
    /// 为目标结构树中所有节点订阅勾选状态变化事件。
    /// </summary>
    private void AttachCheckStateHandlers(TargetInfoItem? target)
    {
        if (target == null)
        {
            return;
        }

        foreach (TargetStructureTreeNode node in EnumerateNodes(target.StructureTreeNodes))
        {
            node.CheckStateChangedByUser += OnStructureNodeCheckStateChanged;
        }
    }

    /// <summary>
    /// 解除目标结构树中所有节点的勾选状态事件订阅。
    /// </summary>
    private void DetachCheckStateHandlers(TargetInfoItem? target)
    {
        if (target == null)
        {
            return;
        }

        foreach (TargetStructureTreeNode node in EnumerateNodes(target.StructureTreeNodes))
        {
            node.CheckStateChangedByUser -= OnStructureNodeCheckStateChanged;
        }
    }

    /// <summary>
    /// 获取当前目标中所有已勾选部件的唯一标识。
    /// </summary>
    private static List<string> GetCheckedPartCodes(TargetInfoItem? target)
    {
        if (target == null)
        {
            return new List<string>();
        }

        return EnumerateNodes(target.StructureTreeNodes)
            .Where(node => node.Part != null && node.IsChecked && !string.IsNullOrWhiteSpace(node.Part.PartCode))
            .Select(node => node.Part!.PartCode)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 深度优先遍历结构树节点。
    /// </summary>
    private static IEnumerable<TargetStructureTreeNode> EnumerateNodes(IEnumerable<TargetStructureTreeNode> nodes)
    {
        foreach (TargetStructureTreeNode node in nodes)
        {
            yield return node;

            foreach (TargetStructureTreeNode child in EnumerateNodes(node.Children))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// 为新增节点及其子节点订阅勾选事件。
    /// </summary>
    private void AttachCheckStateHandler(TargetStructureTreeNode node)
    {
        foreach (TargetStructureTreeNode child in EnumerateNodes(new[] { node }))
        {
            child.CheckStateChangedByUser += OnStructureNodeCheckStateChanged;
        }
    }

    /// <summary>
    /// 为待删除节点及其子节点解除勾选事件订阅。
    /// </summary>
    private void DetachCheckStateHandler(TargetStructureTreeNode node)
    {
        foreach (TargetStructureTreeNode child in EnumerateNodes(new[] { node }))
        {
            child.CheckStateChangedByUser -= OnStructureNodeCheckStateChanged;
        }
    }

    /// <summary>
    /// 需要在 Unity 中恢复的当前显示状态快照。
    /// </summary>
    private sealed record UnityDisplayRefreshState(string TargetCode, List<string> CheckedPartCodes, string HighlightPartCode);
}
