namespace Driftya.SpriteAtlasForge.ClientApplication.Services;

public sealed class MauiWorkspaceInteraction : IWorkspaceInteraction
{
    public async Task<bool> ConfirmDiscardChangesAsync(CancellationToken cancellationToken = default)
    {
        var page = Shell.Current?.CurrentPage
            ?? throw new InvalidOperationException("The application page is not available.");
        var discard = await page.DisplayAlertAsync(
            "Unsaved changes",
            "Discard the unsaved atlas edits and open another file?",
            "Discard",
            "Keep editing");
        cancellationToken.ThrowIfCancellationRequested();
        return discard;
    }
}
