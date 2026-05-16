using CombatSimulation.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CombatSimulation.ViewModels;

/// <summary>
/// 目标系统添加/修改弹窗 ViewModel。
/// </summary>
public sealed partial class TargetSystemEditDialogViewModel : ObservableObject, IDialogRequestClose
{
    private readonly string _targetCode;
    private readonly string _parentSystemCode;
    private readonly bool _isTopSystem;

    /// <summary>
    /// 目标系统编辑界面 ViewModel
    /// </summary>
    /// <param name="system">新增或编辑的系统信息</param>
    /// <param name="isEditMode">是否为编辑模式，若为编辑，则目标已经存在，否则为新增 </param>
    /// <param name="parentSystemName">父系统名称</param>
    public TargetSystemEditDialogViewModel(TargetSystemInfoItem system, bool isEditMode)
    {
        EditedSystem = CloneSystem(system);
        if (string.IsNullOrWhiteSpace(EditedSystem.SystemCode))
        {
            EditedSystem.SystemCode = Guid.NewGuid().ToString("N");
        }

        _targetCode = EditedSystem.TargetCode;
        _parentSystemCode = EditedSystem.ParentSystemCode;
        _isTopSystem = EditedSystem.IsTopSystem;

        DialogTitle = isEditMode ? "修改目标系统信息" : "添加目标子系统";
        SystemName = EditedSystem.SystemName;
        SystemDescription = EditedSystem.SystemDescription;
        // 使得显示其父系统名称，而不是父系统标识
        ParentSystemDisplayText = GetParentSystemName(_parentSystemCode);
        IsTopSystemDisplayText = _isTopSystem ? "是" : "否";
    }

    public event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;

    public TargetSystemInfoItem EditedSystem { get; private set; }

    [ObservableProperty]
    private string _dialogTitle = "目标系统信息设置";

    [ObservableProperty]
    private string _systemName = string.Empty;

    [ObservableProperty]
    private string _systemDescription = string.Empty;

    [ObservableProperty]
    private string _parentSystemDisplayText = string.Empty;

    [ObservableProperty]
    private string _isTopSystemDisplayText = string.Empty;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    /// <summary>
    /// 保存系统输入内容，并请求窗口按确认结果关闭。
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        string systemName = SystemName.Trim();

        if (string.IsNullOrWhiteSpace(systemName))
        {
            ValidationMessage = "请输入系统名称。";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditedSystem.SystemCode))
        {
            EditedSystem.SystemCode = Guid.NewGuid().ToString("N");
        }

        EditedSystem.SystemName = systemName;
        EditedSystem.SystemDescription = SystemDescription.Trim();
        EditedSystem.ParentSystemCode = _parentSystemCode;
        EditedSystem.IsTopSystem = _isTopSystem;
        EditedSystem.TargetCode = _targetCode;
        ValidationMessage = string.Empty;

        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(dialogResult: true));
    }

    /// <summary>
    /// 取消编辑，并请求窗口按取消结果关闭。
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(dialogResult: false));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parentSystemCode"></param>
    /// <returns></returns>
    private static string GetParentSystemName(string parentSystemCode)
    {
        return string.IsNullOrWhiteSpace(parentSystemCode) || parentSystemCode == "-1"
            ? "无（顶系统）"
            : parentSystemCode;
    }

    private static TargetSystemInfoItem CloneSystem(TargetSystemInfoItem source)
    {
        return new TargetSystemInfoItem
        {
            SystemCode = source.SystemCode,
            SystemName = source.SystemName,
            SystemDescription = source.SystemDescription,
            IsTopSystem = source.IsTopSystem,
            ParentSystemCode = source.ParentSystemCode,
            TargetCode = source.TargetCode
        };
    }
}
