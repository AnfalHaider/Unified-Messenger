namespace UnifiedMessenger.Services;

public interface IRichTriageStoreService
{
    bool IsLoaded { get; }

    RichTriageStoreLoadResult LastLoadResult { get; }

    Task<RichTriageStoreLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    Task FlushAsync(CancellationToken cancellationToken = default);

    void ScheduleDisplayOrderSave();

    Task ClearAsync(CancellationToken cancellationToken = default);
}
