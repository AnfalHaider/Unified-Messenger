namespace UnifiedMessenger.Services;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? CtrlSpacePressed;

    void EnsureRegistered();
}
