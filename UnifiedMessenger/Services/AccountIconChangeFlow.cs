using Microsoft.UI.Xaml;
using UnifiedMessenger.Dialogs;
using UnifiedMessenger.Models;

namespace UnifiedMessenger.Services;

/// <summary>
/// Shared orchestration for the Change-icon flow so the sidebar right-click menu (ShellController) and
/// Settings → Accounts run identical logic: show <see cref="ChangeIconDialog"/>, then apply the chosen
/// built-in glyph / imported profile photo / uploaded image / reset. Environment specifics — the
/// <see cref="XamlRoot"/>, the file picker, the WebView airspace toggle, and post-change UI refresh — are
/// supplied by the caller so neither call site duplicates the result-application logic.
/// </summary>
public static class AccountIconChangeFlow
{
    public static async Task RunAsync(
        ApplicationServices services,
        MessengerInstance instance,
        XamlRoot xamlRoot,
        Func<Task<byte[]?>> pickImageBytesAsync,
        Action? beforeShow = null,
        Action? afterShow = null,
        Action? onChanged = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(pickImageBytesAsync);

        var dialog = new ChangeIconDialog(instance) { XamlRoot = xamlRoot };
        beforeShow?.Invoke();
        try
        {
            await dialog.ShowAsync();
        }
        finally
        {
            afterShow?.Invoke();
        }

        // The Import button closes the dialog with Hide() (result None), so branch on the dialog's own
        // choice, not the ContentDialogResult.
        if (dialog.Result == AvatarChoiceKind.Cancel)
        {
            return;
        }

        var instanceId = instance.Id;
        try
        {
            if (dialog.Result == AvatarChoiceKind.UploadImage)
            {
                var bytes = await pickImageBytesAsync();
                if (bytes is null)
                {
                    return; // user cancelled the file picker
                }

                await ProfileAvatarService.SaveAvatarAsync(instanceId, bytes);
                await services.Registry.UpdateInstanceAvatarIconAsync(instanceId, null, null, null);
            }
            else if (dialog.Result == AvatarChoiceKind.ImportFromAccount)
            {
                var outcome = await AvatarImportService.TryImportProfilePhotoAsync(instanceId, instance.Platform);
                if (outcome != AvatarImportService.ImportResult.Imported)
                {
                    await services.Dialog.ShowErrorAsync(
                        "Couldn't import the profile photo",
                        "Open this account so its web session and profile photo are loaded, then try Import again.");
                    return;
                }

                // An imported image takes precedence in ProfileAvatarService — clear any built-in glyph.
                await services.Registry.UpdateInstanceAvatarIconAsync(instanceId, null, null, null);
            }
            else
            {
                // A built-in icon or reset supersedes any imported/uploaded image (which wins in
                // ProfileAvatarService.CreateAvatar) — so clear the cached PNG first.
                await ProfileAvatarService.RemoveAvatarAsync(instanceId);
                var built = dialog.Result == AvatarChoiceKind.BuiltInIcon;
                await services.Registry.UpdateInstanceAvatarIconAsync(
                    instanceId,
                    built ? dialog.ResultGlyph : null,
                    built ? dialog.ResultColor : null,
                    built ? dialog.ResultFontFamily : null);
            }

            onChanged?.Invoke();
        }
        catch (Exception ex)
        {
            await services.Dialog.ShowErrorAsync("Could not change icon", ex.Message);
        }
    }

    /// <summary>Opens a picture file picker and returns the chosen image bytes, or null if cancelled.</summary>
    public static async Task<byte[]?> PickImageBytesAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
        };
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".webp");

        // Unpackaged WinUI: the picker must be initialized with the window handle.
        if (App.CurrentWindow is null)
        {
            return null;
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        return file is null ? null : await File.ReadAllBytesAsync(file.Path);
    }
}
