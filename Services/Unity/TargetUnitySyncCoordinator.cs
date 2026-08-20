using CombatSimulation.Models.Unity;
using System.Diagnostics;

namespace CombatSimulation.Services.Unity;

/// <summary>
/// 统一协调目标模块的 Unity 命令、数据库快照、重连恢复、取消与防抖。
/// </summary>
internal sealed class TargetUnitySyncCoordinator : IDisposable
{
    private readonly IUnityCommandService _unityCommandService;
    private readonly Func<CancellationToken, Task<CombatDatabaseSnapshot>> _loadSnapshotAsync;
    private readonly Func<UnityDisplayState> _captureDisplayState;
    private readonly Action<string> _reportStatus;
    private CancellationTokenSource? _targetSelectionCts;
    private CancellationTokenSource? _partHighlightCts;
    private CancellationTokenSource? _checkedPartsCts;
    private CancellationTokenSource? _databaseSnapshotCts;
    private bool _disposed;

    public TargetUnitySyncCoordinator(
        IUnityCommandService unityCommandService,
        Func<CancellationToken, Task<CombatDatabaseSnapshot>> loadSnapshotAsync,
        Func<UnityDisplayState> captureDisplayState,
        Action<string> reportStatus)
    {
        _unityCommandService = unityCommandService ?? throw new ArgumentNullException(nameof(unityCommandService));
        _loadSnapshotAsync = loadSnapshotAsync ?? throw new ArgumentNullException(nameof(loadSnapshotAsync));
        _captureDisplayState = captureDisplayState ?? throw new ArgumentNullException(nameof(captureDisplayState));
        _reportStatus = reportStatus ?? throw new ArgumentNullException(nameof(reportStatus));
        _unityCommandService.EventReceived += OnUnityEventReceived;
    }

    public void ShowTarget(string? targetCode)
    {
        ReplaceCancellation(ref _targetSelectionCts, out CancellationToken token);
        if (!string.IsNullOrWhiteSpace(targetCode))
        {
            RunInBackground(ct => SendTargetAsync(targetCode, ct), token);
        }
    }

    public void HighlightPart(string? partCode)
    {
        ReplaceCancellation(ref _partHighlightCts, out CancellationToken token);
        if (!string.IsNullOrWhiteSpace(partCode))
        {
            RunInBackground(ct => SendHighlightedPartAsync(partCode, ct), token);
        }
    }

    public Task HighlightPartAsync(string? partCode, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(partCode)
            ? Task.CompletedTask
            : SendHighlightedPartAsync(partCode, cancellationToken);
    }

    public void ShowCheckedParts(IReadOnlyList<string> partCodes)
    {
        _checkedPartsCts?.Cancel();
        CancellationTokenSource cts = new();
        _checkedPartsCts = cts;
        string[] snapshot = partCodes.ToArray();
        RunInBackground(token => SendCheckedPartsAsync(snapshot, cts, token), cts.Token);
    }

    public void NotifyDatabaseChanged(bool refreshCurrentDisplayAfterSync = true)
    {
        ScheduleDatabaseSnapshot(refreshCurrentDisplayAfterSync, delayMilliseconds: 250);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _unityCommandService.EventReceived -= OnUnityEventReceived;
        CancelAndDispose(ref _targetSelectionCts);
        CancelAndDispose(ref _partHighlightCts);
        _checkedPartsCts?.Cancel();
        _databaseSnapshotCts?.Cancel();
        _checkedPartsCts = null;
        _databaseSnapshotCts = null;

    }

    private void OnUnityEventReceived(object? sender, UnityMessage message)
    {
        if (_disposed || message == null)
        {
            return;
        }

        if (string.Equals(message.Command, UnityCommandNames.ServerReady, StringComparison.OrdinalIgnoreCase)
            || string.Equals(message.Command, UnityCommandNames.RequestDatabaseSnapshot, StringComparison.OrdinalIgnoreCase))
        {
            ScheduleDatabaseSnapshot(refreshCurrentDisplayAfterSync: true, delayMilliseconds: 0);
        }
    }

    private async Task SendTargetAsync(string targetCode, CancellationToken cancellationToken)
    {
        try
        {
            await _unityCommandService.ShowTargetAsync(targetCode, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ReportFailure($"Unity 显示目标命令发送失败：{ex.Message}", "显示目标", ex);
        }
    }

    private async Task SendHighlightedPartAsync(string partCode, CancellationToken cancellationToken)
    {
        try
        {
            await _unityCommandService.HighlightPartAsync(partCode, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ReportFailure($"Unity 高亮部件命令发送失败：{ex.Message}", "高亮部件", ex);
        }
    }

    private void ScheduleDatabaseSnapshot(bool refreshCurrentDisplayAfterSync, int delayMilliseconds)
    {
        if (_disposed)
        {
            return;
        }

        _databaseSnapshotCts?.Cancel();
        UnityDisplayState displayState = _captureDisplayState();
        CancellationTokenSource cts = new();
        _databaseSnapshotCts = cts;
        RunInBackground(
            token => SendDatabaseSnapshotAsync(displayState, refreshCurrentDisplayAfterSync, delayMilliseconds, cts, token),
            cts.Token);
    }

    private async Task SendDatabaseSnapshotAsync(
        UnityDisplayState displayState,
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

            CombatDatabaseSnapshot snapshot = await _loadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            await _unityCommandService.LoadDatabaseSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
            _reportStatus($"已同步数据库快照到 Unity：目标 {snapshot.Targets.Count}，系统 {snapshot.TargetSystems.Count}，部件 {snapshot.TargetParts.Count}，毁伤树 {snapshot.DamageTrees.Count}，毁伤节点 {snapshot.DamageNodes.Count}");

            if (refreshCurrentDisplayAfterSync)
            {
                await RestoreDisplayAsync(displayState, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ReportFailure($"Unity 数据库快照同步失败：{ex.Message}", "同步数据库快照", ex);
        }
        finally
        {
            if (ReferenceEquals(_databaseSnapshotCts, cts))
            {
                _databaseSnapshotCts = null;
            }
            cts.Dispose();
        }
    }

    private async Task RestoreDisplayAsync(UnityDisplayState state, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(state.TargetCode))
        {
            await _unityCommandService.ShowTargetAsync(state.TargetCode, cancellationToken).ConfigureAwait(false);
        }

        await _unityCommandService.ShowPartsAsync(state.CheckedPartCodes, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(state.HighlightPartCode))
        {
            await _unityCommandService.HighlightPartAsync(state.HighlightPartCode, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendCheckedPartsAsync(
        IReadOnlyList<string> partCodes,
        CancellationTokenSource cts,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(120, cancellationToken).ConfigureAwait(false);
            await _unityCommandService.ShowPartsAsync(partCodes, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ReportFailure($"Unity 显示部件命令发送失败：{ex.Message}", "显示勾选部件", ex);
        }
        finally
        {
            if (ReferenceEquals(_checkedPartsCts, cts))
            {
                _checkedPartsCts = null;
            }
            cts.Dispose();
        }
    }

    private void RunInBackground(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                ReportFailure($"Unity 后台操作失败：{ex.Message}", "后台操作", ex);
            }
        }, CancellationToken.None);
    }

    private void ReportFailure(string userMessage, string operation, Exception exception)
    {
        _reportStatus(userMessage);
        Debug.WriteLine($"[TargetUnitySyncCoordinator] Unity {operation}失败：{exception}");
    }

    private static void ReplaceCancellation(ref CancellationTokenSource? field, out CancellationToken token)
    {
        CancelAndDispose(ref field);
        field = new CancellationTokenSource();
        token = field.Token;
    }

    private static void CancelAndDispose(ref CancellationTokenSource? field)
    {
        field?.Cancel();
        field?.Dispose();
        field = null;
    }
}

internal sealed record UnityDisplayState(
    string TargetCode,
    IReadOnlyList<string> CheckedPartCodes,
    string HighlightPartCode);