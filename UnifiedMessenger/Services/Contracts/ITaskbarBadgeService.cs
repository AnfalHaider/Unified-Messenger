namespace UnifiedMessenger.Services;

public interface ITaskbarBadgeService
{
    Task SyncBadgeAsync(int count);
}
