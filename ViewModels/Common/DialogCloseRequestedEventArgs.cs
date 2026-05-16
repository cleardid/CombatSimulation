namespace CombatSimulation.ViewModels;

/// <summary>
/// 弹窗关闭请求参数。ViewModel 只表达关闭意图，具体 DialogResult 由 Window 代码后置设置。
/// </summary>
public sealed class DialogCloseRequestedEventArgs : EventArgs
{
    public DialogCloseRequestedEventArgs(bool dialogResult)
    {
        DialogResult = dialogResult;
    }

    public bool DialogResult { get; }
}
