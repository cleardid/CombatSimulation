using System.Windows;

namespace CombatSimulation.Util
{
    /// <summary>
    /// 让 ContextMenu、DataTemplate 等不在视觉树中的元素也能稳定绑定到父级 DataContext。
    /// </summary>
    public sealed class BindingProxy : Freezable
    {
        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
            nameof(Data),
            typeof(object),
            typeof(BindingProxy),
            new UIPropertyMetadata(null));

        public object? Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }
    }
}
