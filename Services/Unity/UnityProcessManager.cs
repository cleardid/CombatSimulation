using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace CombatSimulation.Services.Unity;

/// <summary>
/// Unity 进程管理器。
/// </summary>
/// <remarks>
/// 该类只负责 Unity 可执行程序的启动、进程引用维护和关闭。
/// TCP 通信由 <see cref="UnityTcpClient"/> 负责，窗口嵌入由 <see cref="UnityWindowHost"/> 负责。
/// </remarks>
internal sealed class UnityProcessManager
{
    /// <summary>
    /// 正常请求 Unity 主窗口关闭后的最大等待时间。
    /// </summary>
    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// 强制结束进程树后的补充等待时间。
    /// </summary>
    private static readonly TimeSpan ProcessKillWaitTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 保护 <see cref="_process"/> 的同步锁，避免启动和关闭流程并发修改进程引用。
    /// </summary>
    private readonly object _sync = new();

    /// <summary>
    /// 当前由 WPF 程序启动并托管的 Unity 进程。
    /// </summary>
    private Process? _process;

    /// <summary>
    /// 获取 Unity 进程是否仍在运行。
    /// </summary>
    public bool IsRunning => CurrentProcess is { HasExited: false };

    /// <summary>
    /// 获取当前缓存的 Unity 进程引用。
    /// </summary>
    /// <remarks>
    /// 返回值可能为 null，也可能已经退出；调用方如果需要可用进程，应使用 <see cref="GetRunningProcess"/>。
    /// </remarks>
    public Process? CurrentProcess
    {
        get
        {
            lock (_sync)
            {
                return _process;
            }
        }
    }

    /// <summary>
    /// 进程管理日志事件。
    /// </summary>
    public event EventHandler<string>? LogReceived;

    /// <summary>
    /// 在 Unity 尚未运行时启动 Unity；如果已经运行，则直接返回现有进程。
    /// </summary>
    /// <returns>当前可用的 Unity 进程。</returns>
    /// <exception cref="FileNotFoundException">未找到 Unity 可执行文件时抛出。</exception>
    /// <exception cref="InvalidOperationException">进程启动失败时抛出。</exception>
    public Process StartIfNeeded(nint parentHwnd = 0)
    {
        lock (_sync)
        {
            if (_process is { HasExited: false })
            {
                return _process;
            }

            string unityDirectory = Path.Combine(AppContext.BaseDirectory, "Unity");
            string unityExecutablePath = Path.Combine(unityDirectory, "CombatSimulationUnity.exe");

            if (!File.Exists(unityExecutablePath))
            {
                throw new FileNotFoundException(
                    "未找到 Unity 可执行文件，请确认 Unity 构建输出路径是否正确。",
                    unityExecutablePath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = unityExecutablePath,
                WorkingDirectory = unityDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add("-screen-fullscreen");
            startInfo.ArgumentList.Add("0");

            // Create the native host before launching Unity, following the UnityUIForWPF pattern.
            // Starting with -parentHWND avoids converting a popup after graphics initialization.
            // This reduces unnecessary swap-chain recreation during the first embed.
            if (parentHwnd != 0)
            {
                startInfo.ArgumentList.Add("-parentHWND");
                startInfo.ArgumentList.Add(parentHwnd.ToInt64().ToString(CultureInfo.InvariantCulture));
                startInfo.ArgumentList.Add("delayed");
            }

            startInfo.ArgumentList.Add("-logFile");
            startInfo.ArgumentList.Add("unity_player.log");


            _process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unity 进程启动失败。Process.Start 返回 null。");

            Log($"已启动 Unity 进程：{unityExecutablePath}");
            return _process;
        }
    }

    /// <summary>
    /// 获取当前正在运行的 Unity 进程。
    /// </summary>
    /// <returns>仍未退出的 Unity 进程。</returns>
    /// <exception cref="InvalidOperationException">Unity 尚未启动或已经退出时抛出。</exception>
    public Process GetRunningProcess()
    {
        lock (_sync)
        {
            if (_process == null || _process.HasExited)
            {
                throw new InvalidOperationException("Unity 进程尚未启动或已经退出。");
            }

            return _process;
        }
    }

    /// <summary>
    /// 关闭当前托管的 Unity 进程。
    /// </summary>
    /// <remarks>
    /// 优先通过 <see cref="Process.CloseMainWindow"/> 请求正常退出；如果 Unity 未在限定时间内退出，
    /// 则使用 <see cref="Process.Kill(bool)"/> 结束整个进程树，避免 WPF 退出后遗留 Unity 子进程。
    /// </remarks>
    public void StopIfNeeded()
    {
        Process? process;

        lock (_sync)
        {
            process = _process;
            _process = null;
        }

        if (process == null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();

                if (!process.WaitForExit((int)ProcessExitTimeout.TotalMilliseconds))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit((int)ProcessKillWaitTimeout.TotalMilliseconds);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UnityProcessManager] 关闭 Unity 进程失败：{ex.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    /// <summary>
    /// 分发进程管理日志，并同步写入调试输出窗口。
    /// </summary>
    private void Log(string message)
    {
        LogReceived?.Invoke(this, message);
        Debug.WriteLine($"[UnityProcessManager] {message}");
    }
}
