using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace UnifiedMessenger.Services;

public sealed class KeyboardShortcutService
{
    private readonly UIElement _host;

    public KeyboardShortcutService(UIElement host)
    {
        _host = host;
    }

    public void Register(VirtualKey key, VirtualKeyModifiers modifiers, Action handler)
    {
        var accelerator = new KeyboardAccelerator
        {
            Key = key,
            Modifiers = modifiers
        };

        accelerator.Invoked += (_, args) =>
        {
            handler();
            args.Handled = true;
        };

        _host.KeyboardAccelerators.Add(accelerator);
    }
}
