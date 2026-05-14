using CombatSimulation.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CombatSimulation.Models.Unity;
using CombatSimulation.Services.Unity;
using System.Threading.Tasks;

namespace CombatSimulation.ViewModels
{
    public sealed partial class TargetInfoViewModel : ObservableObject
    {
        private const string StructureInfoPanel = "目标结构信息";
        private const string DamageTreeInfoPanel = "毁伤树信息";
        private const string AllTargetCategory = "全部类型";

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public TargetInfoViewModel()
        {
            // 使用全局唯一 UnityService，避免 ViewModel 自行创建 TCP 客户端。
            UnityService.Instance.StateChanged += OnUnityStateChanged;
            UnityService.Instance.EventReceived += OnUnityEventReceived;
            UnityService.Instance.LogReceived += OnUnityLogReceived;

            UnityStatus = ToUnityStatusText(UnityService.Instance.State);

            // 初始化目标类型筛选项
            TargetCategories.Add(AllTargetCategory);
            // 获取所有目标类型
            foreach (string category in Targets.Select(target => target.Category).Distinct())
            {
                TargetCategories.Add(category);
            }

            // 使用 WPF 集合视图筛选目标，不重建原始目标集合
            FilteredTargets = CollectionViewSource.GetDefaultView(Targets);
            // 设置过滤器
            FilteredTargets.Filter = FilterTargetByCategory;

            // 默认选择第一个目标
            SelectedTarget = FilteredTargets.Cast<TargetInfoItem>().FirstOrDefault();
            // 默认进入目标结构信息模块
            SelectedInfoPanel = StructureInfoPanel;
        }

        /// <summary>
        /// 目标种类列表
        /// </summary>
        public ObservableCollection<string> TargetCategories { get; } = new();

        /// <summary>
        /// 符合选中类型的目标信息列表
        /// </summary>
        public ICollectionView FilteredTargets { get; }

        /// <summary>
        /// 默认目标数据列表
        /// </summary>
        public ObservableCollection<TargetInfoItem> Targets { get; } = new()
        {
            new TargetInfoItem
            {
                Name = "主战坦克 Alpha",
                Code = "MBT_001",
                Category = "地面装甲",
                Description = "示例目标。用于验证目标结构与毁伤树编辑页面。",
                StructureNodes =
                {
                    "车体 · Structure",
                    "炮塔 · WeaponPlatform",
                    "火控系统 · FireControlSystem",
                    "动力系统 · PowerSystem"
                },
                DamageTreeNodes =
                {
                    "命中车体",
                    "穿透装甲",
                    "损伤动力系统",
                    "目标机动能力下降"
                }
            },
            new TargetInfoItem
            {
                Name = "雷达站 Beta",
                Code = "RADAR_001",
                Category = "固定设施",
                Description = "固定雷达设施目标。用于验证固定设施类目标的信息展示。",
                StructureNodes =
                {
                    "天线阵面 · AntennaArray",
                    "信号处理舱 · SignalCabin",
                    "电源系统 · PowerUnit",
                    "通信链路 · CommunicationLink"
                },
                DamageTreeNodes =
                {
                    "毁伤天线阵面",
                    "信号处理能力下降",
                    "通信链路中断",
                    "目标探测能力丧失"
                }
            },
        };

        /// <summary>
        /// 选中的目标类型
        /// </summary>
        [ObservableProperty]
        private string _selectedTargetCategory = AllTargetCategory;

        /// <summary>
        /// 选中的目标
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LaunchPreviewCommand))]
        [NotifyCanExecuteChangedFor(nameof(SyncCurrentTargetCommand))]
        private TargetInfoItem? _selectedTarget;

        /// <summary>
        /// Unity 状态信息
        /// </summary>
        [ObservableProperty]
        private string _unityStatus = "未加载 Unity 预览";

        /// <summary>
        /// 当前模块状态信息
        /// </summary>
        [ObservableProperty]
        private string _statusText = "目标信息模块已加载";

        /// <summary>
        /// 当前选中的分模块：目标结构 或 目标毁伤树
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsStructureInfoSelected))]
        [NotifyPropertyChangedFor(nameof(IsDamageTreeInfoSelected))]
        [NotifyPropertyChangedFor(nameof(StructureInfoVisibility))]
        [NotifyPropertyChangedFor(nameof(DamageTreeInfoVisibility))]
        [NotifyCanExecuteChangedFor(nameof(LaunchPreviewCommand))]
        [NotifyCanExecuteChangedFor(nameof(SyncCurrentTargetCommand))]
        [NotifyCanExecuteChangedFor(nameof(ImportModelCommand))]
        // NotifyPropertyChangedFor - 当这个字段生成的属性发生变化时，通知另一个依赖属性也发生了变化
        // NotifyCanExecuteChangedFor - 当这个属性变化时，通知某个命令重新判断自己是否可以执行
        private string _selectedInfoPanel = string.Empty;

        /// <summary>
        /// 是否选择了目标结构树模块，用于控制高亮按钮
        /// </summary>
        public bool IsStructureInfoSelected => SelectedInfoPanel == StructureInfoPanel;

        /// <summary>
        /// 是否选择了目标毁伤树模块，用于控制高亮按钮
        /// </summary>
        public bool IsDamageTreeInfoSelected => SelectedInfoPanel == DamageTreeInfoPanel;

        /// <summary>
        /// 是否选择了目标结构树模块，用于显示结构树模块
        /// </summary>
        public Visibility StructureInfoVisibility =>
            IsStructureInfoSelected ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 是否选择了目标毁伤树模块，用于显示毁伤树模块
        /// </summary>
        public Visibility DamageTreeInfoVisibility =>
            IsDamageTreeInfoSelected ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 用于 _selectedTargetCategory 代表的属性自动调用
        /// </summary>
        /// <param name="value">无意义</param>
        partial void OnSelectedTargetCategoryChanged(string value)
        {
            // 刷新当前选中的目标列表
            FilteredTargets.Refresh();

            if (!IsSelectedTargetInCurrentFilter())
            {
                SelectedTarget = FilteredTargets.Cast<TargetInfoItem>().FirstOrDefault();
            }
        }

        /// <summary>
        /// 当前选中的目标是否在过滤后的列表中
        /// </summary>
        /// <returns>空或不在列表，则返回 false</returns>
        private bool IsSelectedTargetInCurrentFilter()
        {
            // 如果为空，则为 false
            if (SelectedTarget == null)
            {
                return false;
            }

            return FilterTargetByCategory(SelectedTarget);
        }

        /// <summary>
        ///  检查当前选中的目标是否属于当前列表
        /// </summary>
        /// <param name="item">当前选中的目标</param>
        /// <returns></returns>
        private bool FilterTargetByCategory(object item)
        {
            // 如果不在列表中，则直接返回 false
            if (item is not TargetInfoItem target)
            {
                return false;
            }

            // 再做一次安全性检查：当前选中的类型是否为全部类型 或 选中的目标的类型
            return SelectedTargetCategory == AllTargetCategory ||
                   SelectedTargetCategory == target.Category;
        }

        /// <summary>
        /// 用于 _selectedTarget 代表的属性自动调用
        /// </summary>
        /// <param name="value">无意义</param>
        partial void OnSelectedTargetChanged(TargetInfoItem? value)
        {
            StatusText = value == null
                ? "未选择目标"
                : $"当前目标：{value.Name}";
        }

        /// <summary>
        /// 用于 _selectedInfoPanel 代表的属性自动调用
        /// </summary>
        /// <param name="value">无意义</param>
        partial void OnSelectedInfoPanelChanged(string value)
        {
            StatusText = IsStructureInfoSelected
                ? "当前位于目标结构信息模块，可使用 Unity 预览接口"
                : "当前位于毁伤树信息模块，Unity 预览接口未启用";
        }

        /// <summary>
        /// 显示目标结构信息
        /// 响应目标结构信息按钮点击操作
        /// </summary>
        [RelayCommand]
        private void ShowStructureInfo()
        {
            SelectedInfoPanel = StructureInfoPanel;

            // await UnityService.Instance.ShowUnityWindowAsync();
        }

        /// <summary>
        /// 显示毁伤树信息
        /// 响应目标毁伤树信息按钮点击操作
        /// </summary>
        [RelayCommand]
        private void ShowDamageTreeInfo()
        {
            SelectedInfoPanel = DamageTreeInfoPanel;
        }

        /// <summary>
        /// 请求 Unity 加载当前目标模型。
        /// Unity 进程和 TCP 长连接由全局 UnityService 维护，这里只负责发送业务命令。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanUseUnity))]
        private async Task LaunchPreviewAsync()
        {
            if (SelectedTarget == null)
            {
                StatusText = "未选择目标，无法启动 Unity 预览";
                return;
            }

            if (!UnityService.Instance.IsConnected)
            {
                UnityStatus = ToUnityStatusText(UnityService.Instance.State);
                StatusText = "Unity 全局连接尚未建立，无法发送 load_model 命令";
                return;
            }

            try
            {
                UnityStatus = $"正在请求 Unity 加载模型：{SelectedTarget.Name}";

                UnityMessage response = await UnityService.Instance.SendRequestAsync(
                    UnityCommandNames.LoadModel,
                    new
                    {
                        modelId = SelectedTarget.Code,
                        position = new[] { 0, 0, 0 }
                    },
                    TimeSpan.FromSeconds(30));

                if (response.Success == true)
                {
                    UnityStatus = $"Unity 已接收模型加载请求：{SelectedTarget.Name}";
                    StatusText = $"已发送 load_model：{SelectedTarget.Code}";
                }
                else
                {
                    UnityStatus = "Unity 加载模型失败";
                    StatusText = response.Error?.Message ?? "Unity 返回未知错误";
                }
            }
            catch (Exception ex)
            {
                UnityStatus = "Unity 连接或加载失败";
                StatusText = ex.Message;
            }
        }

        /// <summary>
        /// 同步当前目标信息到 Unity。
        /// Unity 进程和 TCP 长连接由全局 UnityService 维护，这里只负责发送业务命令。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSyncCurrentTarget))]
        private async Task SyncCurrentTargetAsync()
        {
            if (SelectedTarget == null)
            {
                StatusText = "未选择目标，无法同步到 Unity";
                return;
            }

            if (!UnityService.Instance.IsConnected)
            {
                UnityStatus = ToUnityStatusText(UnityService.Instance.State);
                StatusText = "Unity 全局连接尚未建立，无法发送 sync_target 命令";
                return;
            }

            try
            {
                UnityMessage response = await UnityService.Instance.SendRequestAsync(
                    UnityCommandNames.SyncTarget,
                    new
                    {
                        targetCode = SelectedTarget.Code,
                        name = SelectedTarget.Name,
                        category = SelectedTarget.Category,
                        description = SelectedTarget.Description
                    },
                    TimeSpan.FromSeconds(5));

                if (response.Success == true)
                {
                    UnityStatus = $"已同步目标：{SelectedTarget.Name}";
                    StatusText = $"目标 {SelectedTarget.Code} 已同步到 Unity";
                }
                else
                {
                    UnityStatus = "同步目标失败";
                    StatusText = response.Error?.Message ?? "Unity 返回未知错误";
                }
            }
            catch (Exception ex)
            {
                UnityStatus = "同步目标失败";
                StatusText = ex.Message;
            }
        }

        /// <summary>
        /// 导入模型命令。
        /// 当前只保留 UI 入口，后续可接入文件选择器或资源管理模块。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanUseUnity))]
        private void ImportModel()
        {
            UnityStatus = "等待导入模型文件";
            StatusText = "模型导入接口已触发，后续可接入文件选择器";
        }

        /// <summary>
        /// Unity 预览功能是否可用。
        /// 这里不再负责连接 Unity；只有全局长连接已建立、当前位于结构信息模块、且已选择目标时才允许发送命令。
        /// </summary>
        private bool CanUseUnity()
        {
            return IsStructureInfoSelected && SelectedTarget != null && UnityService.Instance.IsConnected;
        }

        /// <summary>
        /// 是否可以同步目标信息到 Unity。
        /// </summary>
        private bool CanSyncCurrentTarget()
        {
            return IsStructureInfoSelected && SelectedTarget != null && UnityService.Instance.IsConnected;
        }

        private void OnUnityStateChanged(object? sender, UnityConnectionState state)
        {
            RunOnUiThread(() =>
            {
                UnityStatus = ToUnityStatusText(state);

                LaunchPreviewCommand.NotifyCanExecuteChanged();
                SyncCurrentTargetCommand.NotifyCanExecuteChanged();
                ImportModelCommand.NotifyCanExecuteChanged();
            });
        }

        /// <summary>
        /// 将 Unity 连接状态转换为界面状态文本。
        /// </summary>
        private static string ToUnityStatusText(UnityConnectionState state)
        {
            return state switch
            {
                UnityConnectionState.Disconnected => "Unity 未连接",
                UnityConnectionState.Connecting => "正在连接 Unity...",
                UnityConnectionState.Connected => "Unity 已连接",
                UnityConnectionState.Reconnecting => "Unity 连接中断，正在重连...",
                UnityConnectionState.Disconnecting => "正在断开 Unity...",
                UnityConnectionState.Faulted => "Unity 通信异常",
                _ => $"Unity 状态：{state}"
            };
        }

        private void OnUnityLogReceived(object? sender, string log)
        {
            RunOnUiThread(() =>
            {
                StatusText = log;
            });
        }

        private void OnUnityEventReceived(object? sender, UnityMessage message)
        {
            RunOnUiThread(() =>
            {
                switch (message.Command)
                {
                    case UnityCommandNames.ModelLoaded:
                        UnityStatus = "Unity 模型加载完成";
                        StatusText = $"收到 Unity 事件：{UnityCommandNames.ModelLoaded}";
                        break;

                    case UnityCommandNames.PropertyChanged:
                        StatusText = "Unity 对象属性已变化";
                        break;

                    case UnityCommandNames.Error:
                        UnityStatus = "Unity 推送错误事件";
                        StatusText = message.Data?.ToString() ?? "Unity 发生错误";
                        break;

                    default:
                        StatusText = $"收到 Unity 事件：{message.Command}";
                        break;
                }
            });
        }

        private static void RunOnUiThread(Action action)
        {
            Application? application = Application.Current;

            if (application == null || application.Dispatcher.HasShutdownStarted || application.Dispatcher.HasShutdownFinished)
            {
                return;
            }

            if (application.Dispatcher.CheckAccess())
            {
                action();
                return;
            }

            application.Dispatcher.BeginInvoke(action);
        }
    }
}