using CombatSimulation.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace CombatSimulation.ViewModels;

/// <summary>
/// 目标添加/修改弹窗 ViewModel。
/// 只负责字段绑定、校验和生成编辑结果，不直接操作 TextBox 等 UI 元素。
/// </summary>
public sealed partial class TargetEditDialogViewModel : ObservableObject, IDialogRequestClose
{
    private readonly string _targetCode;

    public TargetEditDialogViewModel(TargetInfoItem target, bool isEditMode)
    {
        _targetCode = target.Code;
        EditedTarget = CloneTarget(target);

        DialogTitle = isEditMode ? "修改目标信息" : "添加目标信息";
        TargetName = target.Name;
        TargetCategory = target.Category;
        TargetDescription = target.Description;
    }

    public event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;

    public TargetInfoItem EditedTarget { get; private set; }

    [ObservableProperty]
    private string _dialogTitle = "目标信息设置";

    [ObservableProperty]
    private string _targetName = string.Empty;

    [ObservableProperty]
    private string _targetCategory = string.Empty;

    [ObservableProperty]
    private string _targetDescription = string.Empty;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    /// <summary>
    /// 保存输入内容，并请求窗口按确认结果关闭。
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        string targetName = TargetName.Trim();
        string targetCategory = TargetCategory.Trim();

        if (string.IsNullOrWhiteSpace(targetName))
        {
            ShowValidationMessage("请输入目标名称。");
            return;
        }

        if (string.IsNullOrWhiteSpace(targetCategory))
        {
            ShowValidationMessage("请输入目标种类。");
            return;
        }

        EditedTarget.Name = targetName;
        EditedTarget.Code = _targetCode;
        EditedTarget.Category = targetCategory;
        EditedTarget.Description = TargetDescription.Trim();
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

    private static TargetInfoItem CloneTarget(TargetInfoItem source)
    {
        TargetInfoItem target = new()
        {
            Name = source.Name,
            Code = source.Code,
            Category = source.Category,
            Description = source.Description
        };

        foreach (TargetSystemInfoItem system in source.Systems)
        {
            target.Systems.Add(system);
        }

        foreach (string damageTreeNode in source.DamageTreeNodes)
        {
            target.DamageTreeNodes.Add(damageTreeNode);
        }

        return target;
    }

    private void ShowValidationMessage(string message)
    {
        ValidationMessage = message;
        MessageBox.Show(message, DialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
