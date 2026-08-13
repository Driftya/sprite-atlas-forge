namespace Driftya.SpriteAtlasForge.ClientApplication.Services;

public interface IWorkspaceFilePicker
{
    Task<string?> PickPngAsync(CancellationToken cancellationToken = default);

    Task<string?> PickProjectAsync(string nativeProjectExtension, CancellationToken cancellationToken = default);

    Task<string?> PickProjectSavePathAsync(
        string suggestedName,
        string nativeProjectExtension,
        CancellationToken cancellationToken = default);

    Task<string?> PickPngSavePathAsync(
        string suggestedName,
        CancellationToken cancellationToken = default);
}
