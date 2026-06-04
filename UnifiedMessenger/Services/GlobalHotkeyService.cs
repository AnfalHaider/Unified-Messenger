using System.Runtime.InteropServices;

namespace UnifiedMessenger.Services;

/// <summary>
/// Registers Ctrl+Space at the Windows level so the copilot hotkey works while the shell is hidden in the tray.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x554D;
    private const uint ModControl = 0x0002;
    private const uint VkSpace = 0x20;
    private const uint WmHotkey = 0x0312;

    private static readonly Lazy<GlobalHotkeyService> LazyInstance = new(() => new GlobalHotkeyService());

    private readonly object _gate = new();
    private IntPtr _messageHwnd;
    private WndProcDelegate? _wndProc;
    private bool _registered;
    private bool _disposed;

    private GlobalHotkeyService()
    {
    }

    public static GlobalHotkeyService Instance => LazyInstance.Value;

    public event EventHandler? CtrlSpacePressed;

    public void EnsureRegistered()
    {
        lock (_gate)
        {
            if (_disposed || _registered)
            {
                return;
            }

            _wndProc = WndProc;
            _messageHwnd = CreateMessageOnlyWindow(_wndProc);
            if (_messageHwnd == IntPtr.Zero)
            {
                return;
            }

            if (!RegisterHotKey(_messageHwnd, HotkeyId, ModControl, VkSpace))
            {
                DestroyWindow(_messageHwnd);
                _messageHwnd = IntPtr.Zero;
                return;
            }

            _registered = true;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_registered && _messageHwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_messageHwnd, HotkeyId);
            }

            if (_messageHwnd != IntPtr.Zero)
            {
                DestroyWindow(_messageHwnd);
                _messageHwnd = IntPtr.Zero;
            }

            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            CtrlSpacePressed?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private static IntPtr CreateMessageOnlyWindow(WndProcDelegate wndProc)
    {
        const string className = "UnifiedMessengerHotkeyHost";
        var hInstance = GetModuleHandle(null);

        var wc = new WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            lpfnWndProc = wndProc,
            hInstance = hInstance,
            lpszClassName = className
        };

        RegisterClassEx(ref wc);

        const uint wsPopup = 0x80000000;
        const int hwndMessage = -3;

        return CreateWindowEx(
            0,
            className,
            className,
            wsPopup,
            0,
            0,
            0,
            0,
            (IntPtr)hwndMessage,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);
    }

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint cbSize;
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }
}
