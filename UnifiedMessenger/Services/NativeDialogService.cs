using System.Runtime.InteropServices;

namespace UnifiedMessenger.Services;

internal static class NativeDialogService
{
    private const uint MbIconError = 0x00000010;

    public static void ShowError(string title, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "An unexpected error occurred.";
        }

        _ = MessageBoxW(IntPtr.Zero, message, title, MbIconError);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
