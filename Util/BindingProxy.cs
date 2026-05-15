using System.Windows;

namespace CombatSimulation.Util;

/// <summary>
/// 让 ContextMenu、DataTemplate 等不在视觉树中的元素也能稳定绑定到父级 DataContext。
/// </summary>
public sealed class BindingProxy : Freezable
{
    /// <summary>
    /// 代理保存的 DataContext 依赖属性。
    /// </summary>
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data),
        typeof(object),
        typeof(BindingProxy),
        new UIPropertyMetadata(null));

    /// <summary>
    /// 需要暴露给非视觉树元素的绑定源。
    /// </summary>
    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>
    /// WPF Freezable 必需的实例创建方法。
    /// </summary>
    protected override Freezable CreateInstanceCore()
    {
        return new BindingProxy();
    }
}
