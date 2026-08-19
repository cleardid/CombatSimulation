using CombatSimulation.ViewModels;
using System.Windows;

namespace CombatSimulation;

/// <summary>
/// 主窗口。
/// 这里只负责初始化窗口和绑定 MainWindowViewModel。
/// 页面切换、窗口关闭等逻辑交给命令处理。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 设置主窗口的数据上下文，使 XAML 可以绑定 MainWindowViewModel 中的属性和命令
        this.DataContext = new MainWindowViewModel();
    }

    /// <summary>
    /// 主窗口关闭时释放目标模块持有的 Unity 事件订阅和后台同步任务。
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        TargetInfoModule.Dispose();
        base.OnClosed(e);
    }
}
