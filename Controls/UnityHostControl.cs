using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CombatSimulation.Controls
{
    /// <summary>
    /// 用于承载 Unity 外部窗口的原生 HWND 宿主控件。
    /// </summary>
    /// <remarks>
    /// WPF 普通控件没有稳定的 Win32 子窗口句柄；如果直接把 Unity 窗口挂到 WPF 主窗口上，
    /// 需要反复计算相对于主窗口客户区的坐标，页面切换和布局更新时容易出现时序问题。
    ///
    /// 该控件通过 <see cref="HwndHost"/> 创建一个真正的 Win32 子窗口，Unity 只需要被挂到该
    /// 子窗口下面，并始终定位到 (0, 0)。WPF 负责移动和裁剪该宿主 HWND，UnityWindowHost 只负责
    /// 让 Unity 窗口填满这个原生宿主区域。
    /// </remarks>
    public sealed class UnityHostControl : HwndHost
    {
        private const int WsChild = 0x40000000;
        private const int WsVisible = 0x10000000;
        private const int WsClipChildren = 0x02000000;
        private const int WsClipSiblings = 0x04000000;

        private nint _hostHwnd;

        /// <summary>
        /// 原生宿主窗口句柄。句柄尚未创建或已经销毁时为 0。
        /// </summary>
        public nint HostHandle => _hostHwnd;

        /// <summary>
        /// 原生宿主 HWND 创建或销毁时触发。
        /// </summary>
        public event EventHandler? NativeHostChanged;

        /// <summary>
        /// 原生宿主 HWND 的位置或尺寸由 WPF 布局系统调整后触发。
        /// </summary>
        public event EventHandler? NativeHostBoundsChanged;

        /// <summary>
        /// 获取原生宿主窗口当前客户区尺寸，单位为 Win32 像素。
        /// </summary>
        public bool TryGetClientSize(out int width, out int height)
        {
            width = 0;
            height = 0;

            if (_hostHwnd == 0 || !IsWindow(_hostHwnd))
            {
                return false;
            }

            if (!GetClientRect(_hostHwnd, out WindowRect rect))
            {
                return false;
            }

            width = Math.Max(0, rect.Right - rect.Left);
            height = Math.Max(0, rect.Bottom - rect.Top);
            return width > 0 && height > 0;
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _hostHwnd = CreateWindowEx(
                0,
                "STATIC",
                string.Empty,
                WsChild | WsVisible | WsClipChildren | WsClipSiblings,
                0,
                0,
                1,
                1,
                hwndParent.Handle,
                0,
                0,
                nint.Zero);

            if (_hostHwnd == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "创建 Unity 原生宿主窗口失败。");
            }

            NativeHostChanged?.Invoke(this, EventArgs.Empty);
            return new HandleRef(this, _hostHwnd);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            nint handle = hwnd.Handle;
            _hostHwnd = 0;
            NativeHostChanged?.Invoke(this, EventArgs.Empty);

            if (handle != 0 && IsWindow(handle))
            {
                DestroyWindow(handle);
            }
        }

        protected override void OnWindowPositionChanged(Rect rcBoundingBox)
        {
            base.OnWindowPositionChanged(rcBoundingBox);
            NativeHostBoundsChanged?.Invoke(this, EventArgs.Empty);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern nint CreateWindowEx(
            int extendedStyle,
            string className,
            string windowName,
            int style,
            int x,
            int y,
            int width,
            int height,
            nint parentHwnd,
            nint menuHwnd,
            nint instanceHwnd,
            nint parameter);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(nint hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindow(nint hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(nint hwnd, out WindowRect rect);
    }
}
