using Driftya.SpriteAtlasForge.Application;

namespace Driftya.SpriteAtlasForge.Infrastructure;

public sealed class LocalAtlasFileSystem : IAtlasFileSystem
{
    public Task CopyFileAtomicallyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default) =>
        ExportFileSupport.CopyAtomicallyAsync(sourcePath, destinationPath, cancellationToken);
}
