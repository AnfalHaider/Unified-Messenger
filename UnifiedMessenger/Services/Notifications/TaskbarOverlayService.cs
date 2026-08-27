using System.Runtime.InteropServices;

namespace UnifiedMessenger.Services;

/// <summary>
/// Win10 fallback overlay when numeric badge APIs are unavailable.
/// </summary>
public static class TaskbarOverlayService
{
    private static readonly Guid TaskbarListClsid = new("56FDF344-FD6D-11d0-958A-006097C9A090");
    private static readonly object OverlayGate = new();
    private static IntPtr _cachedOverlayIcon = IntPtr.Zero;

    public static bool TrySetOverlayCount(int count)
    {
        // No window yet is not a failure — the badge is cleared at startup, before the shell has anything
        // to hang an overlay on. Logging it as one put two warnings in app.log on every single launch.
        if (App.CurrentWindow is null)
        {
            return false;
        }

        object? taskbarComObject = null;
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            taskbarComObject = Activator.CreateInstance(Type.GetTypeFromCLSID(TaskbarListClsid)!);
            if (taskbarComObject is not ITaskbarList3 taskbar)
            {
                return false;
            }

            taskbar.HrInit();

            var normalized = NormalizeOverlayCount(count);
            var description = FormatOverlayLabel(normalized);
            IntPtr overlayIcon = IntPtr.Zero;

            lock (OverlayGate)
            {
                ReleaseCachedOverlayIcon();

                if (normalized > 0 &&
                    TaskbarOverlayIconRenderer.TryCreateCountIcon(normalized, out var createdIcon))
                {
                    overlayIcon = createdIcon;
                    _cachedOverlayIcon = createdIcon;
                }

                taskbar.SetOverlayIcon(hwnd, overlayIcon, description);
            }

            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarning("Notifications.Badge", $"Taskbar overlay failed: {ex.Message}");
            return false;
        }
        finally
        {
            // A fresh TaskbarList is created per call, and this runs on every unread-count change.
            // Releasing it here keeps that from accumulating RCWs until a GC happens to notice.
            if (taskbarComObject is not null && Marshal.IsComObject(taskbarComObject))
            {
                Marshal.FinalReleaseComObject(taskbarComObject);
            }
        }
    }

    public static void ClearOverlay()
    {
        TrySetOverlayCount(0);
    }

    internal static int NormalizeOverlayCount(int count) =>
        count <= 0 ? 0 : Math.Min(count, 99);

    internal static string FormatOverlayLabel(int count)
    {
        var normalized = NormalizeOverlayCount(count);
        return normalized <= 0 ? string.Empty : normalized.ToString();
    }

    private static void ReleaseCachedOverlayIcon()
    {
        if (_cachedOverlayIcon == IntPtr.Zero)
        {
            return;
        }

        TaskbarOverlayIconRenderer.DestroyIconHandle(_cachedOverlayIcon);
        _cachedOverlayIcon = IntPtr.Zero;
    }

    /// <summary>
    /// IID_ITaskbarList3 — the INTERFACE id, which is not the same as CLSID_TaskbarList above.
    /// </summary>
    /// <remarks>
    /// This attribute carried the CLSID, so every call created the taskbar object correctly and then asked
    /// it to QueryInterface for <c>{56FDF344-…}</c> — an id it implements as a class, not as an interface.
    /// The result was <c>E_NOINTERFACE</c> on every single badge update, which meant the overlay fallback
    /// had never worked; and because the Windows App SDK badge API does not work in this app's unpackaged
    /// self-contained configuration either, the taskbar badge as a whole had never worked, while Settings
    /// went on offering a toggle for it. The failure named its own cause in the exception message and was
    /// invisible for the life of the feature because it was reported with <c>Debug.WriteLine</c>.
    /// </remarks>
    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
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
