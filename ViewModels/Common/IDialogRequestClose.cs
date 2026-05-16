namespace CombatSimulation.ViewModels;

/// <summary>
/// 弹窗 ViewModel 的统一关闭协议。
/// ViewModel 只发出“需要关闭”的意图，Window 负责真正设置 DialogResult。
/// </summary>
public interface IDialogRequestClose
{
    event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;
}
