using CombatSimulation.Services.Unity;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace CombatSimulation;

/// <summary>
/// 应用程序入口。
///
/// 当前启动流程：
/// 1. 显示主窗口。
/// 2. 后台启动 Unity 进程并建立 TCP 长连接。
/// 3. 程序退出时释放 Unity 通信服务，并关闭由 WPF 启动的 Unity 进程。
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 手动创建主窗口。App.xaml 中不再使用 StartupUri，避免窗口创建早于全局 UnityService 初始化。
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();

        // 先把 WPF 主窗口句柄交给 UnityService。Unity 进程启动后会作为主窗口的子窗口托管，默认保持隐藏。
        UnityService.Instance.SetDefaultParentWindow(new WindowInteropHelper(mainWindow).Handle);

        // 后台启动 Unity，不阻塞 WPF 主界面显示。
        // 业务模块调用 UnityService.Instance.SendRequestAsync 时，如果连接尚未完成，会自动等待/启动连接流程。
        _ = StartUnityRuntimeInBackgroundAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            // 退出阶段不能无限等待网络接收循环或 Unity 连接重试，否则主窗口关闭后 WPF 进程仍可能常驻。
            Task disposeTask = UnityService.Instance.DisposeAsync().AsTask();

            if (disposeTask.Wait(TimeSpan.FromSeconds(5)))
            {
                disposeTask.GetAwaiter().GetResult();
            }
            else
            {
                Debug.WriteLine("[App] 释放全局服务超时，继续退出 WPF 进程。");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] 释放全局服务时发生异常：{ex.Message}");
        }

        base.OnExit(e);
    }

    /// <summary>
    /// 后台启动 Unity 运行时。
    ///
    /// 这里捕获异常，避免 Unity 路径错误、端口占用或启动失败导致 WPF 主程序直接崩溃。
    /// </summary>
    private static async Task StartUnityRuntimeInBackgroundAsync()
    {
        try
        {
            await UnityService.Instance.StartAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] Unity 启动或连接失败：{ex.Message}");
        }
    }
}
