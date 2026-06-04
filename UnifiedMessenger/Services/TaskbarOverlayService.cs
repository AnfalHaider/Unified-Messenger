using System.Runtime.InteropServices;

namespace UnifiedMessenger.Services;

/// <summary>
/// Win10 fallback overlay when numeric badge APIs are unavailable.
/// </summary>
public static class TaskbarOverlayService
{
    private static readonly Guid TaskbarListClsid = new("56FDF344-FD6D-11d0-958A-006097C9A090");

    public static bool TrySetOverlayCount(int count)
    {
        if (App.CurrentWindow is null)
        {
            return false;
        }

        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            var taskbar = (ITaskbarList3?)Activator.CreateInstance(Type.GetTypeFromCLSID(TaskbarListClsid)!);
            if (taskbar is null)
            {
                return false;
            }

            taskbar.HrInit();

            var description = count > 0 ? NormalizeOverlayCount(count).ToString() : string.Empty;
            taskbar.SetOverlayIcon(hwnd, IntPtr.Zero, description);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Taskbar overlay fallback failed: {ex.Message}");
            return false;
        }
    }

    public static void ClearOverlay()
    {
        TrySetOverlayCount(0);
    }

    internal static int NormalizeOverlayCount(int count) =>
        count <= 0 ? 0 : Math.Min(count, 99);

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
