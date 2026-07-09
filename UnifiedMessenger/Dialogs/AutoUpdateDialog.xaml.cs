using Microsoft.UI.Xaml.Controls;

namespace UnifiedMessenger.Dialogs;

public sealed partial class AutoUpdateDialog : ContentDialog
{
    public AutoUpdateDialog(string currentVersion, string latestVersion)
    {
        InitializeComponent();
        DescriptionText.Text =
            $"A newer version ({latestVersion}) is available. You are running {currentVersion}.\n\n" +
            "Unified Messenger will download the installer, verify its signature, and restart to apply the update.";
    }
}
