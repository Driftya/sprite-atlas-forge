using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Application;

public interface IAtlasExporter
{
    string Format { get; }

    Task<AtlasExportResult> ExportAsync(
        AtlasProject project,
        string projectPath,
        string outputDirectory,
        IProgress<AtlasProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
