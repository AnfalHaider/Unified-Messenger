namespace UnifiedMessenger.Services;

public interface ISystemTrayService : IDisposable
{
    void Attach(MainWindow window);

    void TrayMenu_Quit();

    void ShowMainWindow();
}
