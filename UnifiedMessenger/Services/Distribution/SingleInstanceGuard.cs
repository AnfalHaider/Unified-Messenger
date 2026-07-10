namespace UnifiedMessenger.Services;

/// <summary>
/// Named mutex used with Inno Setup <c>AppMutex</c> so upgrades can close a running instance safely.
/// </summary>
public static class SingleInstanceGuard
{
    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(true, ApplicationPaths.ApplicationMutexName, out var createdNew);
            if (createdNew)
            {
                return true;
            }

            _mutex.Dispose();
            _mutex = null;
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Single-instance mutex failed: {ex.Message}");
            return true;
        }
    }

    public static void Release()
    {
        if (_mutex is null)
        {
            return;
        }

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Single-instance mutex release failed: {ex.Message}");
        }
        finally
        {
            _mutex.Dispose();
            _mutex = null;
        }
    }
}
