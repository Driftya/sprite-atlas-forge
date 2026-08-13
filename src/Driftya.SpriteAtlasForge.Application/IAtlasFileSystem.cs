namespace Driftya.SpriteAtlasForge.Application;

public interface IAtlasFileSystem
{
    Task CopyFileAtomicallyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);
}
