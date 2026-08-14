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
        var extensions = WorkspaceFileTypeRules.NativeProjectExtensions(nativeProjectExtension);
        var picker = CreateSavePicker();
        picker.SuggestedFileName = WorkspaceFileTypeRules.EnsureExtension(suggestedName, extensions[0]);
        picker.CommitButtonText = "Save project";
        picker.DefaultFileExtension = extensions[0];
        picker.FileTypeChoices.Add("Sprite Atlas Forge project", extensions.ToList());

        var result = await picker.PickSaveFileAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return result?.Path;
    }

    public async Task<string?> PickPngSavePathAsync(
        string suggestedName,
        CancellationToken cancellationToken = default)
    {
        var picker = CreateSavePicker();
        picker.SuggestedFileName = WorkspaceFileTypeRules.EnsureExtension(suggestedName, ".png");
        picker.CommitButtonText = "Save sprite PNG";
        picker.DefaultFileExtension = ".png";
        picker.FileTypeChoices.Add("PNG image", [".png"]);

        var result = await picker.PickSaveFileAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return result?.Path;
    }

    private static Windows.Storage.Pickers.FileSavePicker CreateSavePicker()
    {
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()
            ?? throw new InvalidOperationException("The application window is not available.");
        var nativeWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window
            ?? throw new InvalidOperationException("The Windows application window is not initialized.");
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        return picker;
    }
}
