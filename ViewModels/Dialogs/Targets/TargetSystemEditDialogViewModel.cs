using CombatSimulation.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

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
    /// 目标系统编辑界面 ViewModel。
    /// </summary>
    /// <param name="system">新增或编辑的系统信息。</param>
    /// <param name="isEditMode">是否为编辑模式，若为编辑，则目标已经存在，否则为新增。</param>
    /// <param name="allTargets">当前目标模块已经加载的全部目标数据，用于把父系统唯一标识解析为父系统名称。</param>
    public TargetSystemEditDialogViewModel(TargetSystemInfoItem system, bool isEditMode, IEnumerable<TargetInfoItem>? allTargets = null)
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
        // 父系统在数据库中保存的是 ParentSystemCode；界面展示时再从已加载的目标数据中反查系统名称。
        ParentSystemDisplayText = GetParentSystemName(_parentSystemCode, allTargets);
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
            ShowValidationMessage("请输入系统名称。");
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
    /// 根据父系统唯一标识，从目标模块已经加载的全部目标数据中查找父系统名称。
    /// </summary>
    /// <remarks>
    /// 数据库字段 ParentSystemCode 仍然保存唯一标识；这里只影响弹窗展示文本。
    /// 找不到父系统时返回原始标识，便于发现数据库中可能存在的父子关系异常。
    /// </remarks>
    private static string GetParentSystemName(string parentSystemCode, IEnumerable<TargetInfoItem>? allTargets)
    {
        if (string.IsNullOrWhiteSpace(parentSystemCode) || parentSystemCode == "-1")
        {
            return "无（顶系统）";
        }

        TargetSystemInfoItem? parentSystem = allTargets?
            .SelectMany(GetAllSystems)
            .FirstOrDefault(system => string.Equals(system.SystemCode, parentSystemCode, StringComparison.Ordinal));

        return string.IsNullOrWhiteSpace(parentSystem?.SystemName)
            ? parentSystemCode
            : parentSystem.SystemName;
    }

    /// <summary>
    /// 展开一个目标下的全部系统，包括顶层系统和所有子系统。
    /// </summary>
    private static IEnumerable<TargetSystemInfoItem> GetAllSystems(TargetInfoItem target)
    {
        foreach (TargetSystemInfoItem system in target.Systems)
        {
            foreach (TargetSystemInfoItem nestedSystem in GetSelfAndChildSystems(system))
            {
                yield return nestedSystem;
            }
        }
    }

    /// <summary>
    /// 展开一个系统节点自身及其全部子系统。
    /// </summary>
    private static IEnumerable<TargetSystemInfoItem> GetSelfAndChildSystems(TargetSystemInfoItem system)
    {
        yield return system;

        foreach (TargetSystemInfoItem childSystem in system.ChildSystems)
        {
            foreach (TargetSystemInfoItem nestedSystem in GetSelfAndChildSystems(childSystem))
            {
                yield return nestedSystem;
            }
        }
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

    private void ShowValidationMessage(string message)
    {
        ValidationMessage = message;
        MessageBox.Show(message, DialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
