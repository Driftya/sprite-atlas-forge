using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Infrastructure;

public sealed class NativeAtlasExporter(IAtlasProjectStore projectStore) : IAtlasExporter
{
    public const string FormatIdentifier = "native";

    public string Format => FormatIdentifier;

    public async Task<AtlasExportResult> ExportAsync(
        AtlasProject project,
        string projectPath,
        string outputDirectory,
        IProgress<AtlasProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var outputFullPath = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputFullPath);
        var baseName = ExportFileSupport.GetProjectBaseName(projectPath);
        var descriptorPath = Path.Combine(outputFullPath, baseName + AtlasFormat.NativeExtension);
        var sourceImagePath = ExportFileSupport.ResolveProjectAsset(projectPath, project.Source.Image);
        var atlasImagePath = ExportFileSupport.ResolveProjectAsset(projectPath, project.Atlas.Image);
        var sourceFileName = Path.GetFileName(project.Source.Image);
        var atlasFileName = Path.GetFileName(project.Atlas.Image);
        var outputSourcePath = Path.Combine(outputFullPath, sourceFileName);
        var outputAtlasPath = Path.Combine(outputFullPath, atlasFileName);
        var exportedProject = new AtlasProject(
            project.Name,
            new AtlasSource(sourceFileName, project.Source.Size, project.Source.Sha256),
            new AtlasOutput(atlasFileName, project.Atlas.Size, project.Atlas.Repacked, project.Atlas.Packing),
            project.Sprites);

        progress?.Report(new("export", 0.2, "Writing native descriptor."));
        await projectStore.SaveAsync(exportedProject, descriptorPath, cancellationToken).ConfigureAwait(false);
        progress?.Report(new("export", 0.5, "Copying source image."));
        await ExportFileSupport.CopyAtomicallyAsync(sourceImagePath, outputSourcePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(sourceImagePath, atlasImagePath, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(outputSourcePath, outputAtlasPath, StringComparison.OrdinalIgnoreCase))
        {
            progress?.Report(new("export", 0.75, "Copying atlas image."));
            await ExportFileSupport.CopyAtomicallyAsync(atlasImagePath, outputAtlasPath, cancellationToken)
                .ConfigureAwait(false);
        }
        progress?.Report(new("complete", 1, "Native export complete."));

        var generatedFiles = new List<string> { descriptorPath, outputSourcePath };
        if (!string.Equals(outputSourcePath, outputAtlasPath, StringComparison.OrdinalIgnoreCase))
        {
            generatedFiles.Add(outputAtlasPath);
        }

        return new AtlasExportResult(
            Format,
            generatedFiles,
            Array.Empty<AtlasDiagnostic>());
    }
}
