using System.Runtime.InteropServices;

namespace UnifiedMessenger.Services;

/// <summary>
/// Win10 fallback overlay when numeric badge APIs are unavailable.
/// </summary>
public static class TaskbarOverlayService
{
    private const int ITaskbarList3Clsid = 0x56FDF344;

    public static void TrySetOverlayCount(int count)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow!);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var taskbar = (ITaskbarList3?)Activator.CreateInstance(
                Type.GetTypeFromCLSID(new Guid("56FDF344-FD6D-11d0-958A-006097C9A090"))!);

            taskbar?.HrInit();
            taskbar?.SetOverlayIcon(hwnd, IntPtr.Zero, count > 0 ? count.ToString() : string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Taskbar overlay fallback failed: {ex.Message}");
        }
    }

    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(nint hwnd);
        void DeleteTab(nint hwnd);
        void ActivateTab(nint hwnd);
        void SetActiveAlt(nint hwnd);
        void MarkFullscreenWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        void SetProgressValue(nint hwnd, ulong completed, ulong total);
        void SetProgressState(nint hwnd, int tbpFlags);
        void RegisterTab(nint hwndTab, nint hwndMDI);
        void UnregisterTab(nint hwndTab);
        void SetTabOrder(nint hwndTab, nint hwndInsertBefore);
        void SetTabActive(nint hwndTab, nint hwndMDI, int dwFlags);
        void ThumbBarAddButtons(nint hwnd, uint cButtons, nint pButton);
        void ThumbBarUpdateButtons(nint hwnd, uint cButtons, nint pButton);
        void ThumbBarSetImageList(nint hwnd, nint himl);
        void SetOverlayIcon(nint hwnd, nint hIcon, [MarshalAs(UnmanagedType.LPWStr)] string pszDescription);
        void SetThumbnailTooltip(nint hwnd, nint pszTip);
        void SetThumbnailClip(nint hwnd, nint prcClip);
    }
}
