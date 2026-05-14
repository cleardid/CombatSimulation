using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace CombatSimulation.ViewModels
{
    public sealed partial class MainWindowViewModel : ObservableObject
    {
        /// <summary>
        /// 静态模块名称
        /// </summary>
        private static readonly string[] Modules = ["目标信息", "推演设定", "作战推演"];

        /// <summary>
        /// 当前模块名称
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentModuleText))]
        [NotifyPropertyChangedFor(nameof(IsTargetInfoSelected))]
        [NotifyPropertyChangedFor(nameof(IsDeductionSettingSelected))]
        [NotifyPropertyChangedFor(nameof(IsCombatDeductionSelected))]
        [NotifyPropertyChangedFor(nameof(TargetInfoVisibility))]
        [NotifyPropertyChangedFor(nameof(DeductionSettingVisibility))]
        [NotifyPropertyChangedFor(nameof(CombatDeductionVisibility))]
        private string _currentModuleName = Modules[0];

        /// <summary>
        /// 用于显示当前模块信息，位于 UI 界面左下角
        /// </summary>
        public string CurrentModuleText => $"当前模块\n{CurrentModuleName}";

        /// <summary>
        /// 是否选中了目标信息模块，用于高亮按钮
        /// </summary>
        public bool IsTargetInfoSelected => CurrentModuleName == Modules[0];

        /// <summary>
        /// 是否选中了推演设定模块，用于高亮按钮
        /// </summary>
        public bool IsDeductionSettingSelected => CurrentModuleName == Modules[1];

        /// <summary>
        /// 是否选中了作战推演模块，用于高亮按钮
        /// </summary>
        public bool IsCombatDeductionSelected => CurrentModuleName == Modules[2];

        /// <summary>
        /// 控制目标信息页面显示状态
        /// </summary>
        public Visibility TargetInfoVisibility =>
            IsTargetInfoSelected ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 控制推演设定页面显示状态
        /// </summary>
        public Visibility DeductionSettingVisibility =>
            IsDeductionSettingSelected ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 控制作战推演页面显示状态
        /// </summary>
        public Visibility CombatDeductionVisibility =>
            IsCombatDeductionSelected ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 进入目标信息模块
        /// </summary>
        [RelayCommand]
        private void ShowTargetInfo()
        {
            CurrentModuleName = Modules[0];
        }

        /// <summary>
        /// 进入推演设定模块
        /// </summary>
        [RelayCommand]
        private void ShowDeductionSetting()
        {
            CurrentModuleName = Modules[1];
        }

        /// <summary>
        /// 进入作战推演模块
        /// </summary>
        [RelayCommand]
        private void ShowCombatDeduction()
        {
            CurrentModuleName = Modules[2];
        }

        /// <summary>
        /// 关闭程序。
        /// 这里直接调用 Application.Shutdown，而不是只关闭 MainWindow，避免无边框自定义关闭按钮只关闭窗口、
        /// 但后台 Unity 通信释放流程没有进入完整应用退出路径。
        /// </summary>
        [RelayCommand]
        private static void CloseWindow()
        {
            Application.Current.Shutdown();
        }
    }
}