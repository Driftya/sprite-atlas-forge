using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Application;

public interface ISpriteImageExporter
{
    Task ExportAsync(
        string sourceImagePath,
        string destinationPath,
        PixelRect region,
        CancellationToken cancellationToken = default);
}
