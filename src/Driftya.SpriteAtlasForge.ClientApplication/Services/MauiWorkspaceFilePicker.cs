namespace Driftya.SpriteAtlasForge.ClientApplication.Services;

public sealed class MauiWorkspaceFilePicker : IWorkspaceFilePicker
{
    public async Task<string?> PickPngAsync(CancellationToken cancellationToken = default)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Open PNG spritesheet",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = WorkspaceFileTypeRules.PngExtensions,
            }),
        });
        cancellationToken.ThrowIfCancellationRequested();
        return file?.FullPath;
    }

    public async Task<string?> PickProjectAsync(
        string nativeProjectExtension,
        CancellationToken cancellationToken = default)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Open Sprite Atlas Forge project",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = WorkspaceFileTypeRules.NativeProjectExtensions(nativeProjectExtension),
            }),
        });
        cancellationToken.ThrowIfCancellationRequested();
        return file?.FullPath;
    }

    public async Task<string?> PickProjectSavePathAsync(
        string suggestedName,
        string nativeProjectExtension,
        CancellationToken cancellationToken = default)
    {
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()
            ?? throw new InvalidOperationException("The application window is not available.");
        var nativeWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window
            ?? throw new InvalidOperationException("The Windows application window is not initialized.");
        var extensions = WorkspaceFileTypeRules.NativeProjectExtensions(nativeProjectExtension);
        var picker = new Microsoft.Windows.Storage.Pickers.FileSavePicker(nativeWindow.AppWindow.Id)
        {
            SuggestedFileName = suggestedName,
            CommitButtonText = "Save project",
            DefaultFileExtension = extensions[0],
        };
        picker.FileTypeChoices.Add("Sprite Atlas Forge project", extensions.ToList());

        var result = await picker.PickSaveFileAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return result?.Path;
    }
}
