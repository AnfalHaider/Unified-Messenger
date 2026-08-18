using Microsoft.UI.Xaml;

namespace UnifiedMessenger.Services;

/// <summary>
/// The type, icon and spacing scales, for controls built in C# rather than XAML.
///
/// <para>
/// <b>Why this exists.</b> A large part of the app draws itself imperatively —
/// <c>CommandCenterPanel</c>, <c>WorkspaceSidebar</c>, the chart controls and most dialogs build
/// <c>Border</c>/<c>StackPanel</c> trees in code. Those files were writing literal numbers, and because
/// <c>DesignScaleTests</c> only ever read <c>.xaml</c>, nobody could see them drift. Measured at v4.99.34
/// the C# builders carried <b>eleven</b> distinct text sizes against the entire XAML surface's seven — in
/// fewer total uses. Every literal size in code is a value that will not follow the scale when it changes.
/// </para>
/// <para>
/// <b>Why constants and not a resource lookup.</b> Reading <c>Application.Current.Resources</c> is a
/// UI-thread-only WinRT call, and this app has already taken one process-terminating
/// <c>AccessViolationException</c> from giving a background code path a UI-thread dependency (see
/// <see cref="UmSemanticBrushes"/>). Constants are safe from any thread, work in the designer and in
/// headless tests, and cannot fail. The duplication with <c>Tokens.xaml</c> is deliberate and is pinned by
/// <c>DesignScaleTests.TheCodeScaleMatchesTheXamlTokens</c>, which parses the XAML and compares — the same
/// arrangement the status palette already uses.
/// </para>
/// </summary>
public static class UmScale
{
    /// <summary>
    /// The seven-step type ramp. See <c>docs/design-system/scales.md</c>.
    /// </summary>
    public static class Text
    {
        /// <summary>11 — timestamps, badges, axis labels, section eyebrows.</summary>
        public const double Caption = 11;

        /// <summary>12 — default body text.</summary>
        public const double Body = 12;

        /// <summary>14 — emphasised body, row titles, panel subtitles.</summary>
        public const double BodyStrong = 14;

        /// <summary>16 — section headings inside a page.</summary>
        public const double Subtitle = 16;

        /// <summary>20 — page and card titles.</summary>
        public const double Title = 20;

        /// <summary>24 — KPI values.</summary>
        public const double Metric = 24;

        /// <summary>32 — the one hero number.</summary>
        public const double Hero = 32;
    }

    /// <summary>
    /// Icon glyph sizes — a separate scale from <see cref="Text"/> on purpose. <c>FontSize</c> on a
    /// <c>FontIcon</c> sets a glyph size; it shares an attribute name with type and nothing else.
    /// </summary>
    public static class Icon
    {
        /// <summary>12 — inline within a chip or badge.</summary>
        public const double Sm = 12;

        /// <summary>16 — the default: row icons, buttons, status glyphs.</summary>
        public const double Md = 16;

        /// <summary>24 — section and feature icons.</summary>
        public const double Lg = 24;

        /// <summary>40 — empty-state hero icons.</summary>
        public const double Xl = 40;
    }

    /// <summary>The 4px spacing grid.</summary>
    public static class Space
    {
        public const double Xs = 4;
        public const double Sm = 8;
        public const double Md = 12;
        public const double Lg = 16;
        public const double Xl = 24;
        public const double Xxl = 32;
    }

    /// <summary>Uniform padding/margin on the grid.</summary>
    public static Thickness Pad(double all) => new(all);

    /// <summary>Horizontal/vertical padding. Both arguments must come from <see cref="Space"/>.</summary>
    public static Thickness Pad(double horizontal, double vertical) => new(horizontal, vertical, horizontal, vertical);

    /// <summary>Explicit four-sided padding, for the few genuinely asymmetric cases.</summary>
    public static Thickness Pad(double left, double top, double right, double bottom) =>
        new(left, top, right, bottom);
}
