using CommunityToolkit.Mvvm.ComponentModel;

namespace UnifiedMessenger.ViewModels;

public partial class LocalAiModelRowViewModel : ViewModelBase
{
    public LocalAiModelRowViewModel(
        string modelId,
        string displayName,
        string sizeLabel,
        string description)
    {
        ModelId = modelId;
        DisplayName = displayName;
        SizeLabel = sizeLabel;
        Description = description;
    }

    public string ModelId { get; }

    public string DisplayName { get; }

    public string SizeLabel { get; }

    public string Description { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadButtonText))]
    private bool _isDownloading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadButtonText))]
    private bool _isInstalled;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusText = "Not downloaded";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadButtonText))]
    private bool _canDownload = true;

    public string DownloadButtonText =>
        IsInstalled ? "Installed" : IsDownloading ? "Downloading…" : "Download";
}
