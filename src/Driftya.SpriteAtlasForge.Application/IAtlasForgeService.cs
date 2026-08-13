using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Application;

public interface IAtlasForgeService
{
    Task<AtlasProject> DetectAsync(
        DetectAtlasRequest request,
        IProgress<AtlasProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<AtlasProject> LoadAsync(string projectPath, CancellationToken cancellationToken = default);

    Task SaveAsync(AtlasProject project, string projectPath, CancellationToken cancellationToken = default);

    Task SaveAsAsync(
        AtlasProject project,
        string sourceProjectPath,
        string destinationProjectPath,
        CancellationToken cancellationToken = default);

    Task<AtlasValidationResult> ValidateAsync(string projectPath, CancellationToken cancellationToken = default);

    Task<AtlasProject> AddConnectorAsync(AddConnectorRequest request, CancellationToken cancellationToken = default);

    Task<AtlasProject> RemoveConnectorAsync(RemoveConnectorRequest request, CancellationToken cancellationToken = default);

    Task<AtlasProject> UpdateConnectorAsync(UpdateConnectorRequest request, CancellationToken cancellationToken = default);

    Task<AtlasProject> RenameSpriteAsync(RenameSpriteRequest request, CancellationToken cancellationToken = default);

    Task<AtlasProject> UpdateSpriteRegionAsync(
        UpdateSpriteRegionRequest request,
        CancellationToken cancellationToken = default);

    Task<RepackAtlasResult> RepackAsync(
        RepackAtlasRequest request,
        IProgress<AtlasProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<AtlasExportResult> ExportAsync(
        ExportAtlasRequest request,
        IProgress<AtlasProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
