namespace Driftya.SpriteAtlasForge.ClientApplication.Services;

public interface IWorkspaceInteraction
{
    Task<bool> ConfirmDiscardChangesAsync(CancellationToken cancellationToken = default);
}
