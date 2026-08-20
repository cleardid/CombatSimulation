namespace CombatSimulation.ViewModels;

/// <summary>
/// 弹窗 ViewModel 的消息请求协议。ViewModel 只提供文本，Window 决定具体展示方式。
/// </summary>
public interface IDialogRequestMessage
{
    event EventHandler<DialogMessageRequestedEventArgs>? MessageRequested;
}
