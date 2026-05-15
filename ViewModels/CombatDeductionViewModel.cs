using CombatSimulation.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace CombatSimulation.ViewModels;

/// <summary>
/// 作战推演界面的状态与命令模型。
/// 当前实现提供前端演示级推演流程，用定时器模拟推演阶段、进度和事件日志变化。
/// </summary>
public sealed partial class CombatDeductionViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer;
    private int _elapsedSeconds;

    public CombatDeductionViewModel()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _timer.Tick += Timer_Tick;

        AddEvent("初始化", "作战推演模块初始化完成，等待启动推演。");
    }

    /// <summary>
    /// 推演过程事件日志，界面按最新事件优先显示。
    /// </summary>
    public ObservableCollection<CombatEventItem> EventLogs { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartDeductionCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseDeductionCommand))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyCanExecuteChangedFor(nameof(StartDeductionCommand))]
    private double _progressValue;

    [ObservableProperty]
    private string _simulationState = "未开始";

    [ObservableProperty]
    private string _currentPhase = "待机";

    [ObservableProperty]
    private string _elapsedTimeText = "00:00:00";

    [ObservableProperty]
    private string _blueForceStatus = "待命";

    [ObservableProperty]
    private string _redForceStatus = "未接触";

    [ObservableProperty]
    private string _battlefieldStatus = "推演环境未启动";

    [ObservableProperty]
    private string _moduleStatusText = "作战推演模块已加载";

    public string ProgressText => $"{ProgressValue:F0}%";

    [RelayCommand(CanExecute = nameof(CanStartDeduction))]
    private void StartDeduction()
    {
        IsRunning = true;
        SimulationState = "运行中";
        ModuleStatusText = "作战推演正在运行";
        BattlefieldStatus = "战场态势持续更新中";

        _timer.Start();
        AddEvent("控制", "作战推演启动。");
    }

    private bool CanStartDeduction()
    {
        return !IsRunning && ProgressValue < 100;
    }

    [RelayCommand(CanExecute = nameof(CanPauseDeduction))]
    private void PauseDeduction()
    {
        IsRunning = false;
        SimulationState = "已暂停";
        ModuleStatusText = "作战推演已暂停";
        BattlefieldStatus = "战场态势冻结在当前时刻";

        _timer.Stop();
        AddEvent("控制", "作战推演暂停。");
    }

    private bool CanPauseDeduction()
    {
        return IsRunning;
    }

    [RelayCommand]
    private void ResetDeduction()
    {
        _timer.Stop();

        IsRunning = false;
        _elapsedSeconds = 0;

        ProgressValue = 0;
        SimulationState = "未开始";
        CurrentPhase = "待机";
        ElapsedTimeText = "00:00:00";
        BlueForceStatus = "待命";
        RedForceStatus = "未接触";
        BattlefieldStatus = "推演环境未启动";
        ModuleStatusText = "作战推演已复位";

        EventLogs.Clear();
        AddEvent("复位", "作战推演状态已恢复到初始状态。");
    }

    /// <summary>
    /// 定时推进推演状态。
    /// </summary>
    private void Timer_Tick(object? sender, EventArgs e)
    {
        _elapsedSeconds++;
        ElapsedTimeText = TimeSpan.FromSeconds(_elapsedSeconds).ToString(@"hh\:mm\:ss");

        ProgressValue = Math.Min(100, ProgressValue + 2);

        UpdateSimulationPhase();

        if (_elapsedSeconds % 5 == 0)
        {
            AddEvent(CurrentPhase, GenerateEventText());
        }

        if (ProgressValue >= 100)
        {
            CompleteDeduction();
        }
    }

    private void UpdateSimulationPhase()
    {
        if (ProgressValue < 25)
        {
            CurrentPhase = "兵力展开";
            BlueForceStatus = "进入预定阵位";
            RedForceStatus = "目标活动迹象增强";
            BattlefieldStatus = "双方尚未直接接触，侦察单元持续搜索目标。";
        }
        else if (ProgressValue < 50)
        {
            CurrentPhase = "侦察接敌";
            BlueForceStatus = "完成目标初步定位";
            RedForceStatus = "发现蓝方侦察迹象";
            BattlefieldStatus = "蓝方侦察链路建立，目标区域态势逐步清晰。";
        }
        else if (ProgressValue < 75)
        {
            CurrentPhase = "火力打击";
            BlueForceStatus = "火力单元实施打击";
            RedForceStatus = "部分目标遭受压制";
            BattlefieldStatus = "火力单元进入主要作用阶段，目标毁伤效果持续评估。";
        }
        else
        {
            CurrentPhase = "效果评估";
            BlueForceStatus = "整理打击效果";
            RedForceStatus = "作战能力下降";
            BattlefieldStatus = "进入毁伤评估与态势复盘阶段。";
        }
    }

    private string GenerateEventText()
    {
        return CurrentPhase switch
        {
            "兵力展开" => "蓝方单元完成阶段性机动，部署状态更新。",
            "侦察接敌" => "侦察链路返回目标状态信息，目标位置可信度提升。",
            "火力打击" => "火力单元完成一次作用过程，毁伤评估模型已接收输入。",
            "效果评估" => "系统完成阶段性毁伤结果汇总，等待最终评估。",
            _ => "推演状态更新。"
        };
    }

    private void CompleteDeduction()
    {
        _timer.Stop();

        IsRunning = false;
        SimulationState = "已完成";
        CurrentPhase = "推演完成";
        ProgressValue = 100;
        ModuleStatusText = "作战推演已完成";
        BattlefieldStatus = "推演结束，结果可进入评估与复盘流程。";

        AddEvent("完成", "作战推演完成。");
    }

    private void AddEvent(string phase, string content)
    {
        EventLogs.Insert(0, new CombatEventItem
        {
            Time = DateTime.Now,
            Phase = phase,
            Content = content
        });
    }
}
