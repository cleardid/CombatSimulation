using CombatSimulation.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CombatSimulation.ViewModels
{
    public sealed partial class DeductionSettingViewModel : ObservableObject
    {
        public ObservableCollection<string> DeductionModes { get; } = new()
        {
            "人工推演",
            "半自动推演",
            "自动推演"
        };

        public ObservableCollection<string> BattlefieldEnvironments { get; } = new()
        {
            "昼间 / 晴 / 能见度良好",
            "夜间 / 多云 / 能见度一般",
            "雨天 / 低云 / 能见度较差",
            "复杂电磁环境"
        };

        public ObservableCollection<string> DamageModels { get; } = new()
        {
            "标准毁伤模型",
            "概率毁伤模型",
            "组件级毁伤模型",
            "自定义毁伤模型"
        };

        public ObservableCollection<DeductionParameterItem> Parameters { get; } = new()
        {
            new DeductionParameterItem
            {
                Name = "推演步长",
                Value = "1",
                Unit = "s",
                Description = "每次推演状态更新的时间间隔。"
            },
            new DeductionParameterItem
            {
                Name = "最大推演时间",
                Value = "3600",
                Unit = "s",
                Description = "单次作战推演允许运行的最大时长。"
            },
            new DeductionParameterItem
            {
                Name = "战场区域",
                Value = "Area-001",
                Unit = "-",
                Description = "当前推演绑定的战场空间范围。"
            },
            new DeductionParameterItem
            {
                Name = "命中判定规则",
                Value = "HitRule-Default",
                Unit = "-",
                Description = "用于判断武器作用结果的命中规则。"
            },
            new DeductionParameterItem
            {
                Name = "毁伤评估模型",
                Value = "DamageModel-Std",
                Unit = "-",
                Description = "用于评估目标毁伤等级的模型。"
            }
        };

        [ObservableProperty]
        private string _selectedDeductionMode = "半自动推演";

        [ObservableProperty]
        private string _selectedBattlefieldEnvironment = "昼间 / 晴 / 能见度良好";

        [ObservableProperty]
        private string _selectedDamageModel = "标准毁伤模型";

        [ObservableProperty]
        private bool _enableRandomFactor = true;

        [ObservableProperty]
        private bool _enableRealtimeLog = true;

        [ObservableProperty]
        private string _moduleStatusText = "推演设定模块已加载";

        partial void OnSelectedDeductionModeChanged(string value)
        {
            ModuleStatusText = $"推演模式已切换为：{value}";
        }

        partial void OnSelectedBattlefieldEnvironmentChanged(string value)
        {
            ModuleStatusText = $"战场环境已切换为：{value}";
        }

        partial void OnSelectedDamageModelChanged(string value)
        {
            ModuleStatusText = $"毁伤模型已切换为：{value}";
        }

        partial void OnEnableRandomFactorChanged(bool value)
        {
            ModuleStatusText = value ? "已启用随机因素" : "已关闭随机因素";
        }

        partial void OnEnableRealtimeLogChanged(bool value)
        {
            ModuleStatusText = value ? "已启用实时日志" : "已关闭实时日志";
        }

        [RelayCommand]
        private void LoadDefault()
        {
            SelectedDeductionMode = "半自动推演";
            SelectedBattlefieldEnvironment = "昼间 / 晴 / 能见度良好";
            SelectedDamageModel = "标准毁伤模型";
            EnableRandomFactor = true;
            EnableRealtimeLog = true;

            ModuleStatusText = "已加载默认推演设定";
        }

        [RelayCommand]
        private void SaveSetting()
        {
            ModuleStatusText = $"推演设定已保存：{SelectedDeductionMode}，{SelectedBattlefieldEnvironment}，{SelectedDamageModel}";
        }

        [RelayCommand]
        private void ResetSetting()
        {
            SelectedDeductionMode = string.Empty;
            SelectedBattlefieldEnvironment = string.Empty;
            SelectedDamageModel = string.Empty;
            EnableRandomFactor = false;
            EnableRealtimeLog = false;

            ModuleStatusText = "推演设定已重置";
        }
    }
}