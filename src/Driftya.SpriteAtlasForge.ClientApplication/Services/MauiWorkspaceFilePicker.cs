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
                [DevicePlatform.WinUI] = [".png"],
                [DevicePlatform.MacCatalyst] = ["public.png"],
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
                [DevicePlatform.WinUI] = [nativeProjectExtension],
                [DevicePlatform.MacCatalyst] = ["public.json"],
            }),
        });
        cancellationToken.ThrowIfCancellationRequested();
        return file?.FullPath;
    }
}
