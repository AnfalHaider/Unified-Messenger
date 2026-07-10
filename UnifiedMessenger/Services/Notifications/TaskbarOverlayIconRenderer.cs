using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace UnifiedMessenger.Services;

/// <summary>
/// Renders small numeric badge icons for Win10 taskbar overlay fallback.
/// </summary>
internal static class TaskbarOverlayIconRenderer
{
    private const int IconSize = 16;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    internal static bool TryCreateCountIcon(int count, out IntPtr iconHandle)
    {
        iconHandle = IntPtr.Zero;
        var label = TaskbarOverlayService.FormatOverlayLabel(count);
        if (string.IsNullOrEmpty(label))
        {
            return false;
        }

        try
        {
            using var bitmap = new Bitmap(IconSize, IconSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);

            using var fillBrush = new SolidBrush(Color.FromArgb(230, 209, 52, 56));
            graphics.FillEllipse(fillBrush, 0, 0, IconSize - 1, IconSize - 1);

            using var font = new Font("Segoe UI", 7f, FontStyle.Bold, GraphicsUnit.Point);
            using var textBrush = new SolidBrush(Color.White);
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            graphics.DrawString(label, font, textBrush, new RectangleF(0, 0, IconSize, IconSize), format);

            iconHandle = bitmap.GetHicon();
            return iconHandle != IntPtr.Zero;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Taskbar overlay icon render failed: {ex.Message}");
            return false;
        }
    }

    internal static void DestroyIconHandle(IntPtr iconHandle)
    {
        if (iconHandle == IntPtr.Zero)
        {
            return;
        }

        DestroyIcon(iconHandle);
    }
}
