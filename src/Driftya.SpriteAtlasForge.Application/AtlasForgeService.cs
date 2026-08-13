using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Application;

public sealed class AtlasForgeService : IAtlasForgeService
{
    private readonly IAtlasProjectStore _projectStore;
    private readonly ISpriteDetector _spriteDetector;
    private readonly IAtlasPacker _atlasPacker;
    private readonly IAtlasImageComposer _imageComposer;
    private readonly IAtlasFileSystem _fileSystem;
    private readonly IReadOnlyDictionary<string, IAtlasExporter> _exporters;

    public AtlasForgeService(
        IAtlasProjectStore projectStore,
        ISpriteDetector spriteDetector,
        IAtlasPacker atlasPacker,
        IAtlasImageComposer imageComposer,
        IAtlasFileSystem fileSystem,
        IEnumerable<IAtlasExporter> exporters)
    {
        _projectStore = projectStore;
        _spriteDetector = spriteDetector;
        _atlasPacker = atlasPacker;
        _imageComposer = imageComposer;
        _fileSystem = fileSystem;
        _exporters = exporters.ToDictionary(exporter => exporter.Format, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<AtlasProject> DetectAsync(
        DetectAtlasRequest request,
        IProgress<AtlasProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var options = request.Options ?? new SpriteDetectionOptions();
        options.Validate();

        progress?.Report(new("detect", 0, "Reading source image."));
        var detection = await _spriteDetector
            .DetectAsync(request.SourceImagePath, options, progress, cancellationToken)
            .ConfigureAwait(false);

        var projectFullPath = Path.GetFullPath(request.ProjectPath);
        var projectDirectory = Path.GetDirectoryName(projectFullPath)
            ?? throw new ArgumentException("Project path must have a parent directory.", nameof(request));
        var sourceFullPath = Path.GetFullPath(request.SourceImagePath);
        var sourceReference = Path.GetRelativePath(projectDirectory, sourceFullPath).Replace('\\', '/');

        if (sourceReference.StartsWith("../", StringComparison.Ordinal) || sourceReference == "..")
        {
            throw new ArgumentException(
                "The source image must be inside the project directory so the native project remains portable.",
                nameof(request));
        }

        var projectName = string.IsNullOrWhiteSpace(request.Name)
            ? Path.GetFileNameWithoutExtension(sourceFullPath)
            : request.Name.Trim();
        var sprites = detection.Regions.Select((region, index) =>
            new AtlasSprite($"sprite_{index + 1:D3}", region, region));
        var project = new AtlasProject(
            projectName,
            new AtlasSource(sourceReference, detection.ImageSize, detection.Sha256),
            new AtlasOutput(sourceReference, detection.ImageSize, repacked: false),
            sprites);

        progress?.Report(new("save", 0.9, "Saving native atlas project."));
        await _projectStore.SaveAsync(project, projectFullPath, cancellationToken).ConfigureAwait(false);
        progress?.Report(new("complete", 1, $"Detected {project.Sprites.Count} sprites."));

        return project;
    }

    public Task<AtlasProject> LoadAsync(string projectPath, CancellationToken cancellationToken = default) =>
        _projectStore.LoadAsync(projectPath, cancellationToken);

    public Task SaveAsync(
        AtlasProject project,
        string projectPath,
        CancellationToken cancellationToken = default) =>
        _projectStore.SaveAsync(project, projectPath, cancellationToken);

    public async Task<AtlasValidationResult> ValidateAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var project = await _projectStore.LoadAsync(projectPath, cancellationToken).ConfigureAwait(false);
            return AtlasProjectValidator.Validate(project);
        }
        catch (AtlasProjectFormatException exception)
        {
            return new AtlasValidationResult([
                new AtlasDiagnostic("SAF0001", exception.Message, exception.Path),
            ]);
        }
    }

    public async Task<AtlasProject> AddConnectorAsync(
        AddConnectorRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectStore.LoadAsync(request.ProjectPath, cancellationToken).ConfigureAwait(false);
        var sprite = project.GetSprite(request.SpriteId);
        var updated = project.ReplaceSprite(sprite.AddConnector(new AtlasConnector(request.Name, request.X, request.Y)));
        await _projectStore.SaveAsync(updated, request.ProjectPath, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<AtlasProject> RemoveConnectorAsync(
        RemoveConnectorRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectStore.LoadAsync(request.ProjectPath, cancellationToken).ConfigureAwait(false);
        var sprite = project.GetSprite(request.SpriteId);
        var connectorCount = sprite.Connectors.Count;
        var updatedSprite = sprite.RemoveConnector(request.Name);

        if (updatedSprite.Connectors.Count == connectorCount)
        {
            throw new KeyNotFoundException($"Connector '{request.Name}' was not found on sprite '{sprite.Id}'.");
        }

        var updated = project.ReplaceSprite(updatedSprite);
        await _projectStore.SaveAsync(updated, request.ProjectPath, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<AtlasProject> UpdateConnectorAsync(
        UpdateConnectorRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectStore.LoadAsync(request.ProjectPath, cancellationToken).ConfigureAwait(false);
        var sprite = project.GetSprite(request.SpriteId);
        var updatedSprite = sprite.UpdateConnector(
            request.CurrentName,
            new AtlasConnector(request.Name, request.X, request.Y));
        var updated = project.ReplaceSprite(updatedSprite);
        await _projectStore.SaveAsync(updated, request.ProjectPath, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<AtlasProject> RenameSpriteAsync(
        RenameSpriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectStore.LoadAsync(request.ProjectPath, cancellationToken).ConfigureAwait(false);
        var sprite = project.GetSprite(request.SpriteId);
        var renamed = new AtlasSprite(
            request.NewId,
            sprite.SourceRegion,
            sprite.Frame,
            sprite.Connectors,
            sprite.Tags,
            sprite.Properties);
        var updated = new AtlasProject(
            project.Name,
            project.Source,
            project.Atlas,
            project.Sprites.Select(candidate =>
                string.Equals(candidate.Id, sprite.Id, StringComparison.OrdinalIgnoreCase) ? renamed : candidate));
        await _projectStore.SaveAsync(updated, request.ProjectPath, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<RepackAtlasResult> RepackAsync(
        RepackAtlasRequest request,
        IProgress<AtlasProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectStore.LoadAsync(request.ProjectPath, cancellationToken).ConfigureAwait(false);
        var options = request.Options ?? new AtlasPackingOptions();
        options.Validate();
        progress?.Report(new("pack", 0.05, "Calculating deterministic sprite placement."));
        var packing = _atlasPacker.Pack(project, options);

        var outputDirectory = Path.GetFullPath(request.OutputDirectory);
        var baseName = Path.GetFileName(request.ProjectPath);
        if (baseName.EndsWith(AtlasFormat.NativeExtension, StringComparison.OrdinalIgnoreCase))
        {
            baseName = baseName[..^AtlasFormat.NativeExtension.Length];
        }
        else
        {
            baseName = Path.GetFileNameWithoutExtension(baseName);
        }

        var sourceExtension = Path.GetExtension(project.Source.Image);
        var sourceFileName = baseName + ".source" + sourceExtension;
        var atlasFileName = baseName + ".atlas.png";
        var outputProjectPath = Path.Combine(outputDirectory, baseName + AtlasFormat.NativeExtension);
        var outputSourcePath = Path.Combine(outputDirectory, sourceFileName);
        var outputAtlasPath = Path.Combine(outputDirectory, atlasFileName);
        var sourceProjectDirectory = Path.GetDirectoryName(Path.GetFullPath(request.ProjectPath))!;
        var sourceImagePath = Path.GetFullPath(Path.Combine(
            sourceProjectDirectory,
            project.Source.Image.Replace('/', Path.DirectorySeparatorChar)));

        await _fileSystem
            .CopyFileAtomicallyAsync(sourceImagePath, outputSourcePath, cancellationToken)
            .ConfigureAwait(false);
        await _imageComposer
            .ComposeAsync(sourceImagePath, outputAtlasPath, project, packing, progress, cancellationToken)
            .ConfigureAwait(false);

        var frames = packing.Sprites.ToDictionary(sprite => sprite.Id, StringComparer.OrdinalIgnoreCase);
        var repackedProject = new AtlasProject(
            project.Name,
            new AtlasSource(sourceFileName, project.Source.Size, project.Source.Sha256),
            new AtlasOutput(
                atlasFileName,
                packing.Size,
                repacked: true,
                new AtlasPackingMetadata(
                    "deterministic-shelf-v1",
                    options.Padding,
                    options.PowerOfTwo,
                    options.MaximumWidth,
                    options.MaximumHeight)),
            project.Sprites.Select(sprite => new AtlasSprite(
                sprite.Id,
                sprite.SourceRegion,
                frames[sprite.Id].Frame,
                sprite.Connectors,
                sprite.Tags,
                sprite.Properties)));
        await _projectStore.SaveAsync(repackedProject, outputProjectPath, cancellationToken).ConfigureAwait(false);
        progress?.Report(new("complete", 1, "Repacked atlas complete."));

        return new RepackAtlasResult(
            repackedProject,
            [outputProjectPath, outputSourcePath, outputAtlasPath]);
    }

    public async Task<AtlasExportResult> ExportAsync(
        ExportAtlasRequest request,
        IProgress<AtlasProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectStore.LoadAsync(request.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!_exporters.TryGetValue(request.Format, out var exporter))
        {
            throw new NotSupportedException(
                $"Exporter '{request.Format}' is not available. Available formats: {string.Join(", ", _exporters.Keys.Order())}.");
        }

        return await exporter
            .ExportAsync(project, request.ProjectPath, request.OutputDirectory, progress, cancellationToken)
            .ConfigureAwait(false);
    }

}
