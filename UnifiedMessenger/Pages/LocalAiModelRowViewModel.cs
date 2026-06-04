using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UnifiedMessenger.Pages;

public sealed class LocalAiModelRowViewModel : INotifyPropertyChanged
{
    private bool _isDownloading;
    private bool _isInstalled;
    private double _progress;
    private string _statusText = "Not downloaded";
    private bool _canDownload = true;

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

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                OnPropertyChanged(nameof(DownloadButtonText));
            }
        }
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            if (SetProperty(ref _isInstalled, value))
            {
                OnPropertyChanged(nameof(DownloadButtonText));
            }
        }
    }

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool CanDownload
    {
        get => _canDownload;
        set
        {
            if (SetProperty(ref _canDownload, value))
            {
                OnPropertyChanged(nameof(DownloadButtonText));
            }
        }
    }

    public string DownloadButtonText =>
        IsInstalled ? "Installed" : IsDownloading ? "Downloading…" : "Download";

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
