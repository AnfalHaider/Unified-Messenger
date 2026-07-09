using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UnifiedMessenger.Models;
using UnifiedMessenger.Services;
using Windows.UI;

namespace UnifiedMessenger.Dialogs;

/// <summary>What the user chose in <see cref="ChangeIconDialog"/>.</summary>
public enum AvatarChoiceKind
{
    Cancel,
    BuiltInIcon,
    ResetToInitials,
    ImportFromAccount,
    UploadImage
}

/// <summary>
/// Lets the user set an account's avatar to a social-media brand logo (Font Awesome Brands) or a general
/// built-in icon (Segoe Fluent), each on a flat color circle, or reset to initials. Code-only ContentDialog
/// (no XAML registration). Upload/import live in a separate flow.
/// </summary>
public sealed class ChangeIconDialog : ContentDialog
{
    /// <summary>Bundled Font Awesome 6 Brands font (Assets/Fonts/fa-brands-400.ttf).</summary>
    public const string BrandFontFamily = "ms-appx:///Assets/Fonts/fa-brands-400.ttf#Font Awesome 6 Brands";

    // Social-media brand logos: Font Awesome 6 Brands glyph (PUA codepoint) + the platform's brand color
    // (dark enough that a white glyph reads clearly). Escapes used so glyphs never depend on invisible chars.
    private static readonly (string Glyph, string Color)[] BrandIcons =
    [
        ("", "#25D366"), // whatsapp
        ("", "#229ED9"), // telegram
        ("", "#E4405F"), // instagram
        ("", "#1877F2"), // facebook
        ("", "#0084FF"), // messenger
        ("", "#000000"), // x (twitter)
        ("", "#010101"), // tiktok
        ("", "#FF0000"), // youtube
        ("", "#0A66C2"), // linkedin
        ("", "#5865F2"), // discord
        ("", "#BD081C"), // pinterest
        ("", "#FF4500"), // reddit
        ("", "#07C160"), // wechat (weixin)
        ("", "#4285F4"), // google
    ];

    // General-purpose icons (Segoe Fluent), each on its own flat color.
    private static readonly (string Glyph, string Color)[] GeneralIcons =
    [
        ("", "#1D9E75"), // message
        ("", "#378ADD"), // contact
        ("", "#534AB7"), // people
        ("", "#D85A30"), // home
        ("", "#BA7517"), // mail
        ("", "#993556"), // star
        ("", "#E24B4A"), // cart
        ("", "#639922"), // map pin
        ("", "#5F5E5A"), // gear
    ];

    private readonly Grid _preview;
    private readonly string _displayName;
    private string? _selectedGlyph;
    private string? _selectedColor;
    private string? _selectedFontFamily;

    public AvatarChoiceKind Result { get; private set; } = AvatarChoiceKind.Cancel;

    public string? ResultGlyph => _selectedGlyph;

    public string? ResultColor => _selectedColor;

    public string? ResultFontFamily => _selectedFontFamily;

    public ChangeIconDialog(MessengerInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _displayName = instance.DisplayName;
        _selectedGlyph = instance.CustomIconGlyph;
        _selectedColor = instance.CustomIconColor;
        _selectedFontFamily = instance.CustomIconFontFamily;

        Title = $"Change icon — {instance.DisplayName}";
        PrimaryButtonText = "Save";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        _preview = new Grid { Width = 56, Height = 56 };

        var root = new StackPanel { Spacing = 14, MinWidth = 380 };

        var previewRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        previewRow.Children.Add(_preview);
        previewRow.Children.Add(new TextBlock
        {
            Text = "Pick a social platform or a general icon, or reset to colored initials.",
            TextWrapping = TextWrapping.WrapWholeWords,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush("TextFillColorSecondaryBrush"),
            FontSize = 12,
            MaxWidth = 280
        });
        root.Children.Add(previewRow);

        root.Children.Add(SectionLabel("Social media"));
        root.Children.Add(BuildIconWrap(BrandIcons, BrandFontFamily));

        root.Children.Add(SectionLabel("General"));
        root.Children.Add(BuildIconWrap(GeneralIcons, fontFamily: null));

        root.Children.Add(SectionLabel("From the account"));
        var importButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        var importContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        importContent.Children.Add(new FontIcon { Glyph = "", FontSize = 14 }); // Download
        importContent.Children.Add(new TextBlock { Text = "Import this account's profile photo" });
        importButton.Content = importContent;
        ToolTipService.SetToolTip(importButton, "Pulls the profile photo from the signed-in session. The account must be loaded.");
        importButton.Click += (_, _) =>
        {
            Result = AvatarChoiceKind.ImportFromAccount;
            Hide();
        };
        root.Children.Add(importButton);

        var uploadButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        var uploadContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        uploadContent.Children.Add(new FontIcon { Glyph = "", FontSize = 14 }); // Upload
        uploadContent.Children.Add(new TextBlock { Text = "Upload an image from this PC" });
        uploadButton.Content = uploadContent;
        uploadButton.Click += (_, _) =>
        {
            Result = AvatarChoiceKind.UploadImage;
            Hide();
        };
        root.Children.Add(uploadButton);

        var resetButton = new Button { Content = "Reset to initials", HorizontalAlignment = HorizontalAlignment.Left };
        resetButton.Click += (_, _) =>
        {
            _selectedGlyph = null;
            _selectedColor = null;
            _selectedFontFamily = null;
            RenderPreview();
        };
        root.Children.Add(resetButton);

        // Keep the dialog from growing taller than the window on small displays.
        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 480,
            Content = root
        };
        RenderPreview();

        PrimaryButtonClick += (_, _) =>
            Result = _selectedGlyph is null ? AvatarChoiceKind.ResetToInitials : AvatarChoiceKind.BuiltInIcon;
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Opacity = 0.7
    };

    private VariableSizedWrapGrid BuildIconWrap((string Glyph, string Color)[] icons, string? fontFamily)
    {
        var wrap = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, MaximumRowsOrColumns = 7 };
        foreach (var (glyph, color) in icons)
        {
            wrap.Children.Add(BuildIconButton(glyph, color, fontFamily));
        }

        return wrap;
    }

    private Button BuildIconButton(string glyph, string color, string? fontFamily)
    {
        var host = new Grid { Width = 36, Height = 36 };
        host.Children.Add(new Ellipse { Width = 36, Height = 36, Fill = Brush(color) });
        var icon = new FontIcon
        {
            Glyph = glyph,
            FontSize = 18,
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!string.IsNullOrWhiteSpace(fontFamily))
        {
            icon.FontFamily = new FontFamily(fontFamily);
        }
        host.Children.Add(icon);

        var button = new Button
        {
            Content = host,
            Padding = new Thickness(4),
            Margin = new Thickness(0, 0, 6, 6),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8)
        };
        button.Click += (_, _) =>
        {
            _selectedGlyph = glyph;
            _selectedColor = color;
            _selectedFontFamily = fontFamily;
            RenderPreview();
        };
        return button;
    }

    private void RenderPreview()
    {
        _preview.Children.Clear();
        if (_selectedGlyph is not null)
        {
            _preview.Children.Add(new Ellipse { Width = 56, Height = 56, Fill = Brush(_selectedColor ?? "#6B7280") });
            var icon = new FontIcon
            {
                Glyph = _selectedGlyph,
                FontSize = 28,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (!string.IsNullOrWhiteSpace(_selectedFontFamily))
            {
                icon.FontFamily = new FontFamily(_selectedFontFamily);
            }
            _preview.Children.Add(icon);
        }
        else
        {
            _preview.Children.Add(new Ellipse
            {
                Width = 56,
                Height = 56,
                Fill = new SolidColorBrush(Color.FromArgb(255, 107, 114, 128))
            });
            _preview.Children.Add(new TextBlock
            {
                Text = PlatformBrandingHelper.GetInitials(_displayName),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
    }

    private static SolidColorBrush Brush(string keyOrHex)
    {
        if (keyOrHex.StartsWith('#'))
        {
            return PlatformBrandingHelper.GetAccentBrush(keyOrHex);
        }

        return Application.Current.Resources.TryGetValue(keyOrHex, out var value) && value is SolidColorBrush brush
            ? brush
            : new SolidColorBrush(Colors.Gray);
    }
}
