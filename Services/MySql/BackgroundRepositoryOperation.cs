namespace CombatSimulation.Services.MySql;

/// <summary>
/// 将尚未提供原生异步 API 的 MySQL 操作集中调度到后台线程。
/// </summary>
/// <remarks>
/// 取消令牌可以阻止尚未开始的操作；底层同步命令一旦执行，无法在不中断连接的情况下强制取消。
/// </remarks>
internal static class BackgroundRepositoryOperation
{
    public static Task RunAsync(Action operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return Task.Run(operation, cancellationToken);
    }

    public static Task<TResult> RunAsync<TResult>(Func<TResult> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return Task.Run(operation, cancellationToken);
    }
}
