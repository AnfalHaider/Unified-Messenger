using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace UnifiedMessenger.Services;

public sealed class KeyboardShortcutService : IDisposable
{
    /// <summary>US keyboard comma key (Ctrl+, opens settings).</summary>
    public const VirtualKey SettingsShortcutKey = (VirtualKey)188;
    private readonly UIElement _host;
    private readonly List<KeyboardAccelerator> _accelerators = [];
    private bool _disposed;

    public KeyboardShortcutService(UIElement host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    public void Register(
        VirtualKey key,
        VirtualKeyModifiers modifiers,
        Action handler,
        Func<bool>? canExecute = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(handler);

        var accelerator = new KeyboardAccelerator
        {
            Key = key,
            Modifiers = modifiers
        };

        accelerator.Invoked += (_, args) =>
        {
            if (!ShouldHandleShortcut(canExecute))
            {
                return;
            }

            handler();
            args.Handled = true;
        };

        _host.KeyboardAccelerators.Add(accelerator);
        _accelerators.Add(accelerator);
    }

    public void RegisterIndexedShortcuts(
        VirtualKey firstKey,
        int count,
        VirtualKeyModifiers modifiers,
        Action<int> handler,
        Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        for (var index = 0; index < count; index++)
        {
            var capturedIndex = index;
            Register(
                ResolveIndexedShortcutKey(firstKey, index),
                modifiers,
                () => handler(capturedIndex),
                canExecute);
        }
    }

    internal static bool ShouldHandleShortcut(Func<bool>? canExecute) =>
        canExecute is null || canExecute();

    internal static VirtualKey ResolveIndexedShortcutKey(VirtualKey firstKey, int index)
    {
        if (index < 0 || index > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Shortcut index must be between 0 and 8.");
        }

        return (VirtualKey)((int)firstKey + index);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var accelerator in _accelerators)
        {
            _host.KeyboardAccelerators.Remove(accelerator);
        }

        _accelerators.Clear();
        _disposed = true;
    }
}
