namespace CombatSimulation.ViewModels;

/// <summary>
/// 弹窗消息请求参数。
/// </summary>
public sealed class DialogMessageRequestedEventArgs : EventArgs
{
    public DialogMessageRequestedEventArgs(string message, string title)
    {
        Message = string.IsNullOrWhiteSpace(message) ? "操作失败。" : message;
        Title = string.IsNullOrWhiteSpace(title) ? "提示" : title;
    }

    public string Message { get; }
    public string Title { get; }
}
