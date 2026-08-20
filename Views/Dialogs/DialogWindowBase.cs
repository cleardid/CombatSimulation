using CombatSimulation.ViewModels;
using System;
using System.Windows;

namespace CombatSimulation.Views.Dialogs;

/// <summary>
/// 所有编辑弹窗的父类。
/// 负责绑定 ViewModel 发出的关闭请求，统一设置 DialogResult。
/// 样式统一放在 Resources/Styles/DialogWindowStyles.xaml，业务弹窗不再重复声明输入框、按钮、下拉框样式。
/// </summary>
public class DialogWindowBase : Window
{
    protected DialogWindowBase()
    {
        // WindowStartupLocation 不是依赖属性，不能写在 Style Setter 中，否则会触发 XamlParseException。
        // 所有继承该基类的弹窗默认居中显示，单个弹窗仍可在 XAML 中显式覆盖。
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
    }


    /// <summary>
    /// 弹窗尺寸在 XAML 中按 96DPI 设计值固定。
    /// 如果当前屏幕可用工作区放不下，则按工作区自动压缩一次，仍然禁止用户手动改变大小。
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        FitIntoCurrentWorkArea();
    }

    private void FitIntoCurrentWorkArea()
    {
        const double screenMargin = 32;
        const double minUsableWidth = 420;
        const double minUsableHeight = 320;

        double maxAvailableWidth = Math.Max(minUsableWidth, SystemParameters.WorkArea.Width - screenMargin);
        double maxAvailableHeight = Math.Max(minUsableHeight, SystemParameters.WorkArea.Height - screenMargin);

        double designWidth = double.IsNaN(Width) || Width <= 0 ? ActualWidth : Width;
        double designHeight = double.IsNaN(Height) || Height <= 0 ? ActualHeight : Height;

        double finalWidth = Math.Min(designWidth, maxAvailableWidth);
        double finalHeight = Math.Min(designHeight, maxAvailableHeight);

        MinWidth = finalWidth;
        MaxWidth = finalWidth;
        Width = finalWidth;

        MinHeight = finalHeight;
        MaxHeight = finalHeight;
        Height = finalHeight;

        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Manual;
    }

    /// <summary>
    /// 将弹窗 ViewModel 的关闭请求接到当前 Window。
    /// 子类构造函数中创建 ViewModel 后调用一次即可。
    /// </summary>
    protected void BindCloseRequest(IDialogRequestClose viewModel)
    {
        viewModel.CloseRequested += OnDialogCloseRequested;
        if (viewModel is IDialogRequestMessage messageSource)
        {
            messageSource.MessageRequested += OnDialogMessageRequested;
        }
    }

    private void OnDialogMessageRequested(object? sender, DialogMessageRequestedEventArgs e)
    {
        MessageBox.Show(
            this,
            e.Message,
            e.Title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OnDialogCloseRequested(object? sender, DialogCloseRequestedEventArgs e)
    {
        // ShowDialog 打开的窗口可以通过设置 DialogResult 自动关闭。
        // 若后续某处误用 Show 打开弹窗，直接设置 DialogResult 会抛出 InvalidOperationException，
        // 这里退化为普通 Close，避免所有弹窗共用基类时被单个调用方式拖垮。
        try
        {
            DialogResult = e.DialogResult;
        }
        catch (InvalidOperationException)
        {
            Close();
        }
    }
}
