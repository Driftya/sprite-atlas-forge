using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.ClientApplication.Services;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.ClientApplication.PageModels;

public sealed record CanvasPoint(double X, double Y);

public sealed record CanvasConnectorMove(string Name, double X, double Y);

public sealed record CanvasSpriteResize(string SpriteId, int X, int Y, int Width, int Height);

public sealed record SpriteCanvasOverlay(
    string SpriteId,
    double X,
    double Y,
    double Width,
    double Height,
    bool IsApproved = false);

public sealed record SpriteMetadataEntry(string Key, string Value);

public sealed record ConnectorCanvasOverlay(
    string Name,
    double X,
    double Y,
    bool IsSelected);

public partial class WorkspacePageModel : ObservableObject
{
    private readonly IAtlasForgeService _atlasForgeService;
    private readonly ISpriteImageExporter _spriteImageExporter;
    private readonly IWorkspaceFilePicker _filePicker;
    private readonly IWorkspaceInteraction _interaction;
    private readonly Stack<EditorSnapshot> _undoHistory = [];
    private readonly Stack<EditorSnapshot> _redoHistory = [];
    private CancellationTokenSource? _activeOperation;
    private bool _refreshingSelection;

    public WorkspacePageModel(
        AtlasForgeApplicationInfo applicationInfo,
        IAtlasForgeService atlasForgeService,
        ISpriteImageExporter spriteImageExporter,
        IWorkspaceFilePicker filePicker,
        IWorkspaceInteraction? interaction = null)
    {
        _atlasForgeService = atlasForgeService;
        _spriteImageExporter = spriteImageExporter;
        _filePicker = filePicker;
        _interaction = interaction ?? AlwaysDiscardWorkspaceInteraction.Instance;
        Title = applicationInfo.Name;
        Description = applicationInfo.Description;
        NativeProjectExtension = applicationInfo.NativeProjectExtension;
    }

    public string Title { get; }

    public string Description { get; }

    public string NativeProjectExtension { get; }

    public ObservableCollection<AtlasSprite> Sprites { get; } = [];

    public ObservableCollection<SpriteCanvasOverlay> SpriteOverlays { get; } = [];

    public ObservableCollection<ConnectorCanvasOverlay> ConnectorOverlays { get; } = [];

    public ObservableCollection<SpriteMetadataEntry> SelectedSpriteMetadata { get; } = [];

    public IReadOnlyList<string> ExportFormats { get; } =
        ["native", "phaser-json-hash", "unity-6-spritesheet", "godot-4-atlas-textures"];

    public IReadOnlyList<string> DetectionBackgroundModes { get; } =
        ["Auto", "Alpha only", "Border connected"];

    public bool HasProject => CurrentProject is not null;

    public bool CanUndo => _undoHistory.Count > 0;

    public bool CanRedo => _redoHistory.Count > 0;

    public bool CanCancel => _activeOperation is not null;

    public bool HasProgress => IsBusy && ProgressFraction > 0;

    public string SpriteCountText => Sprites.Count == 1 ? "1 sprite" : $"{Sprites.Count} sprites";

    public double CanvasImageWidth => (CurrentProject?.Atlas.Size.Width ?? 0) * ZoomPercent / 100d;

    public double CanvasImageHeight => (CurrentProject?.Atlas.Size.Height ?? 0) * ZoomPercent / 100d;

    public double CanvasZoomScale => ZoomPercent / 100d;

    public double SourceImageWidth => CurrentProject?.Atlas.Size.Width ?? 0;

    public double SourceImageHeight => CurrentProject?.Atlas.Size.Height ?? 0;

    public bool CanResizeSelectedSprite => CurrentProject is { Atlas.Repacked: false } && SelectedSprite is not null;

    public bool HasSelectedSprite => SelectedSprite is not null;

    [ObservableProperty]
    public partial string Status { get; set; } = "Open a PNG or native atlas project to begin.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    [NotifyPropertyChangedFor(nameof(CanvasImageWidth))]
    [NotifyPropertyChangedFor(nameof(CanvasImageHeight))]
    [NotifyPropertyChangedFor(nameof(SourceImageWidth))]
    [NotifyPropertyChangedFor(nameof(SourceImageHeight))]
    [NotifyPropertyChangedFor(nameof(CanResizeSelectedSprite))]
    public partial AtlasProject? CurrentProject { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanResizeSelectedSprite))]
    [NotifyPropertyChangedFor(nameof(HasSelectedSprite))]
    public partial AtlasSprite? SelectedSprite { get; set; }

    [ObservableProperty]
    public partial string? ProjectPath { get; set; }

    [ObservableProperty]
    public partial string? CurrentImagePath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProgress))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanvasImageWidth))]
    [NotifyPropertyChangedFor(nameof(CanvasImageHeight))]
    [NotifyPropertyChangedFor(nameof(CanvasZoomScale))]
    public partial double ZoomPercent { get; set; } = 100;

    [ObservableProperty]
    public partial double ProgressFraction { get; set; }

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial int DetectionAlphaThreshold { get; set; } = SpriteDetectionOptions.DefaultAlphaThreshold;

    [ObservableProperty]
    public partial string DetectionBackgroundMode { get; set; } = "Auto";

    [ObservableProperty]
    public partial int DetectionBackgroundTolerance { get; set; } =
        SpriteDetectionOptions.DefaultBackgroundColorTolerance;

    [ObservableProperty]
    public partial int DetectionMinimumArea { get; set; } = SpriteDetectionOptions.DefaultMinimumArea;

    [ObservableProperty]
    public partial int DetectionMergeDistance { get; set; } = SpriteDetectionOptions.DefaultMergeDistance;

    [ObservableProperty]
    public partial int DetectionNoiseReductionRadius { get; set; } =
        SpriteDetectionOptions.DefaultNoiseReductionRadius;

    [ObservableProperty]
    public partial int DetectionSourcePadding { get; set; }

    [ObservableProperty]
    public partial string SelectedExportFormat { get; set; } = "phaser-json-hash";

    [ObservableProperty]
    public partial int PackingPadding { get; set; } = 2;

    [ObservableProperty]
    public partial int PackingMaximumWidth { get; set; } = 4096;

    [ObservableProperty]
    public partial int PackingMaximumHeight { get; set; } = 4096;

    [ObservableProperty]
    public partial bool PackingPowerOfTwo { get; set; } = true;

    [ObservableProperty]
    public partial string SpriteIdDraft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSelectedSpriteApproved { get; set; }

    [ObservableProperty]
    public partial string NewMetadataKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewMetadataValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int SourceRegionX { get; set; }

    [ObservableProperty]
    public partial int SourceRegionY { get; set; }

    [ObservableProperty]
    public partial int SourceRegionWidth { get; set; }

    [ObservableProperty]
    public partial int SourceRegionHeight { get; set; }

    [ObservableProperty]
    public partial string NewConnectorName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int NewConnectorX { get; set; }

    [ObservableProperty]
    public partial int NewConnectorY { get; set; }

    [ObservableProperty]
    public partial AtlasConnector? SelectedConnector { get; set; }

    partial void OnCurrentProjectChanged(AtlasProject? value) => RefreshCanvasOverlays();

    partial void OnZoomPercentChanged(double value) => RefreshConnectorOverlays();

    partial void OnSelectedSpriteChanged(AtlasSprite? value)
    {
        _refreshingSelection = true;
        SpriteIdDraft = value?.Id ?? string.Empty;
        IsSelectedSpriteApproved = value?.IsApproved ?? false;
        SourceRegionX = value?.SourceRegion.X ?? 0;
        SourceRegionY = value?.SourceRegion.Y ?? 0;
        SourceRegionWidth = value?.SourceRegion.Width ?? 0;
        SourceRegionHeight = value?.SourceRegion.Height ?? 0;
        SelectedConnector = null;
        SelectedSpriteMetadata.Clear();
        if (value is not null)
        {
            foreach (var entry in value.Metadata)
            {
                SelectedSpriteMetadata.Add(new SpriteMetadataEntry(entry.Key, entry.Value));
            }
        }
        _refreshingSelection = false;
        RefreshCanvasOverlays();
    }

    public Task SetSelectedSpriteApprovedAsync(bool approved, CancellationToken cancellationToken = default)
    {
        if (_refreshingSelection || CurrentProject is null || SelectedSprite is null ||
            SelectedSprite.IsApproved == approved)
        {
            return Task.CompletedTask;
        }

        var selectedId = SelectedSprite.Id;
        return ApplyEditAsync(
            () => AtlasProjectEditor.SetSpriteApproved(CurrentProject, selectedId, approved),
            selectedId,
            null,
            approved ? "Sprite approved for export." : "Sprite approval removed.",
            cancellationToken);
    }

    [RelayCommand]
    private Task AddMetadataAsync(CancellationToken cancellationToken)
    {
        if (CurrentProject is null || SelectedSprite is null || string.IsNullOrWhiteSpace(NewMetadataKey))
        {
            Status = "Enter a metadata key before adding it.";
            return Task.CompletedTask;
        }

        var selectedId = SelectedSprite.Id;
        return ApplyEditAsync(
            () => AtlasProjectEditor.AddSpriteMetadata(CurrentProject, selectedId, NewMetadataKey, NewMetadataValue),
            selectedId,
            null,
            "Metadata added.",
            cancellationToken,
            () => { NewMetadataKey = string.Empty; NewMetadataValue = string.Empty; });
    }

    [RelayCommand]
    private Task RemoveMetadataAsync(SpriteMetadataEntry? entry, CancellationToken cancellationToken)
    {
        if (CurrentProject is null || SelectedSprite is null || entry is null)
        {
            return Task.CompletedTask;
        }

        var selectedId = SelectedSprite.Id;
        return ApplyEditAsync(
            () => AtlasProjectEditor.RemoveSpriteMetadata(CurrentProject, selectedId, entry.Key),
            selectedId,
            null,
            "Metadata removed.",
            cancellationToken);
    }

    partial void OnSelectedConnectorChanged(AtlasConnector? value)
    {
        if (value is not null)
        {
            NewConnectorName = value.Name;
            NewConnectorX = value.X;
            NewConnectorY = value.Y;
        }

        RefreshCanvasOverlays();
    }

    [RelayCommand]
    private async Task OpenImageAsync(CancellationToken cancellationToken)
    {
        if (!await CanReplaceWorkspaceAsync(cancellationToken))
        {
            return;
        }

        await RunBusyAsync(async token =>
        {
            var filePath = await _filePicker.PickPngAsync(token);
            if (filePath is null)
            {
                return;
            }

            var projectPath = Path.Combine(
                Path.GetDirectoryName(filePath)!,
                Path.GetFileNameWithoutExtension(filePath) + NativeProjectExtension);
            var project = await _atlasForgeService.DetectAsync(
                new DetectAtlasRequest(
                    filePath,
                    projectPath,
                    Options: CreateDetectionOptions()),
                CreateProgress(),
                token);
            LoadWorkspace(project, projectPath);
            Status = $"Detected {project.Sprites.Count} sprites.";
        }, cancellationToken);
    }

    [RelayCommand]
    private async Task OpenProjectAsync(CancellationToken cancellationToken)
    {
        if (!await CanReplaceWorkspaceAsync(cancellationToken))
        {
            return;
        }

        await RunBusyAsync(async token =>
        {
            var filePath = await _filePicker.PickProjectAsync(NativeProjectExtension, token);
            if (filePath is null)
            {
                return;
            }

            var project = await _atlasForgeService.LoadAsync(filePath, token);
            LoadWorkspace(project, filePath);
            Status = $"Opened {project.Name}.";
        }, cancellationToken);
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (CurrentProject is null || ProjectPath is null)
        {
            return;
        }

        await RunBusyAsync(async token =>
        {
            await _atlasForgeService.SaveAsync(CurrentProject, ProjectPath, token);
            IsDirty = false;
            Status = "Project saved.";
        }, cancellationToken);
    }

    [RelayCommand]
    private async Task SaveAsAsync(CancellationToken cancellationToken)
    {
        if (CurrentProject is null || ProjectPath is null)
        {
            return;
        }

        await RunBusyAsync(async token =>
        {
            var destination = await _filePicker.PickProjectSavePathAsync(
                CurrentProject.Name,
                NativeProjectExtension,
                token);
            if (destination is null)
            {
                return;
            }

            await _atlasForgeService.SaveAsAsync(CurrentProject, ProjectPath, destination, token);
            ProjectPath = Path.GetFullPath(destination);
            UpdateCurrentImagePath();
            IsDirty = false;
            Status = $"Project saved as {Path.GetFileName(destination)}.";
        }, cancellationToken);
    }

    [RelayCommand]
    private Task ValidateAsync(CancellationToken cancellationToken)
    {
        if (CurrentProject is null)
        {
            return Task.CompletedTask;
        }

        return RunBusyAsync(_ =>
        {
            var validation = AtlasProjectValidator.Validate(CurrentProject);
            Status = validation.IsValid
                ? "Project is valid."
                : string.Join(" ", validation.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"));
            return Task.CompletedTask;
        }, cancellationToken);
    }

    [RelayCommand]
    private Task AddSpriteAsync(CancellationToken cancellationToken)
    {
        if (CurrentProject is null)
        {
            return Task.CompletedTask;
        }

        if (CurrentProject.Atlas.Repacked)
        {
            Status = "Add sprites before repacking the atlas.";
            return Task.CompletedTask;
        }

        var spriteId = CreateNextSpriteId(CurrentProject);
        var width = Math.Min(64, CurrentProject.Source.Size.Width);
        var height = Math.Min(64, CurrentProject.Source.Size.Height);
        var x = (CurrentProject.Source.Size.Width - width) / 2;
        var y = (CurrentProject.Source.Size.Height - height) / 2;
        var region = new PixelRect(x, y, width, height);
        return ApplyEditAsync(
            () => AtlasProjectEditor.AddSprite(
                CurrentProject,
                new AtlasSprite(spriteId, region, region)),
            spriteId,
            null,
            "Sprite added. Adjust its source region, then choose Update region.",
            cancellationToken);
    }

    [RelayCommand]
    private Task DeleteSelectedSpriteAsync(CancellationToken cancellationToken)
    {
        if (CurrentProject is null || SelectedSprite is null)
        {
            return Task.CompletedTask;
        }

        var selectedId = SelectedSprite.Id;
        var selectedIndex = CurrentProject.Sprites
            .Select((sprite, index) => (sprite, index))
            .First(item => string.Equals(item.sprite.Id, selectedId, StringComparison.OrdinalIgnoreCase))
            .index;
        var remaining = CurrentProject.Sprites
            .Where(sprite => !string.Equals(sprite.Id, selectedId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var nextSelection = remaining.Length == 0
            ? null
            : remaining[Math.Min(selectedIndex, remaining.Length - 1)].Id;
        return ApplyEditAsync(
            () => AtlasProjectEditor.RemoveSprite(CurrentProject, selectedId),
            nextSelection,
            null,
            $"Sprite '{selectedId}' deleted.",
            cancellationToken);
    }

    [RelayCommand]
    private Task RenameSpriteAsync(CancellationToken cancellationToken)
    {
        if (CurrentProject is null || SelectedSprite is null || string.IsNullOrWhiteSpace(SpriteIdDraft))
        {
            return Task.CompletedTask;
        }

        var currentId = SelectedSprite.Id;
        var newId = SpriteIdDraft;
        return ApplyEditAsync(
            () => AtlasProjectEditor.RenameSprite(CurrentProject, currentId, newId),
            newId,
            null,
            $"Renamed sprite to '{newId}'.",
            cancellationToken);
    }

    [RelayCommand]
    private Task UpdateSpriteRegionAsync(CancellationToken cancellationToken)
    {
        if (CurrentProject is null || SelectedSprite is null)
        {
            return Task.CompletedTask;
        }

        var selectedId = SelectedSprite.Id;
        return ApplyEditAsync(
            () => AtlasProjectEditor.UpdateSpriteRegion(
                CurrentProject,
                selectedId,
                new PixelRect(SourceRegionX, SourceRegionY, SourceRegionWidth, SourceRegionHeight)),
            selectedId,
            null,
            "Sprite region updated.",
            cancellationToken);
    }

    [RelayCommand]
    private Task ResizeSpriteFromCanvasAsync(CanvasSpriteResize resize, CancellationToken cancellationToken)
    {
        if (CurrentProject is null || CurrentProject.Atlas.Repacked)
        {
            return Task.CompletedTask;
        }

        var sprite = CurrentProject.Sprites.FirstOrDefault(candidate => string.Equals(
            candidate.Id,
            resize.SpriteId,
            StringComparison.OrdinalIgnoreCase));
        if (sprite is null)
        {
            return Task.CompletedTask;
        }

        var region = new PixelRect(resize.X, resize.Y, resize.Width, resize.Height);
        return ApplyEditAsync(
            () => AtlasProjectEditor.UpdateSpriteRegion(CurrentProject, sprite.Id, region),
            sprite.Id,
            null,
            "Sprite region resized.",
            cancellationToken);
    }

    [RelayCommand]
    private async Task SaveSelectedSpriteImageAsync(CancellationToken cancellationToken)
    {
        if (CurrentProject is null || SelectedSprite is null || string.IsNullOrWhiteSpace(CurrentImagePath))
        {
            return;
        }

        var destination = await _filePicker.PickPngSavePathAsync(
            SanitizeFileName(SelectedSprite.Id),
            cancellationToken);
        if (destination is null)
        {
            return;
        }

        await RunBusyAsync(async token =>
        {
            await _spriteImageExporter.ExportAsync(
                CurrentImagePath,
                destination,
                SelectedSprite.Frame,
                token);
            Status = $"Saved sprite '{SelectedSprite.Id}' to {Path.GetFileName(destination)}.";
        }, cancellationToken);
    }

    [RelayCommand]
    private Task AddConnectorAsync(CancellationToken cancellationToken) => AddConnectorAtAsync(
        NewConnectorX,
        NewConnectorY,
        cancellationToken);

    [RelayCommand]
    private Task AddConnectorAtCanvasAsync(CanvasPoint point, CancellationToken cancellationToken)
    {
        if (SelectedSprite is null)
        {
            return Task.CompletedTask;
        }

        var scale = ZoomPercent / 100d;
        var x = (int)Math.Round((point.X / scale) - SelectedSprite.Frame.X);
        var y = (int)Math.Round((point.Y / scale) - SelectedSprite.Frame.Y);
        NewConnectorX = x;
        NewConnectorY = y;
        return AddConnectorAtAsync(x, y, cancellationToken);
    }

    private Task AddConnectorAtAsync(int x, int y, CancellationToken cancellationToken)
    {
        if (CurrentProject is null || SelectedSprite is null || string.IsNullOrWhiteSpace(NewConnectorName))
        {
            Status = "Enter a connector name before placing it.";
            return Task.CompletedTask;
        }

        var selectedId = SelectedSprite.Id;
        var connectorName = NewConnectorName;
        return ApplyEditAsync(
            () => AtlasProjectEditor.AddConnector(
                CurrentProject,
                selectedId,
                new AtlasConnector(connectorName, x, y)),
            selectedId,
            connectorName,
            "Connector added.",
            cancellationToken,
            () => NewConnectorName = string.Empty);
    }

    [RelayCommand]
    private Task RemoveConnectorAsync(AtlasConnector? connector, CancellationToken cancellationToken)
    {
        if (CurrentProject is null || SelectedSprite is null || connector is null)
        {
            return Task.CompletedTask;
        }

        var selectedId = SelectedSprite.Id;
        return ApplyEditAsync(
            () => AtlasProjectEditor.RemoveConnector(CurrentProject, selectedId, connector.Name),
            selectedId,
            null,
            "Connector removed.",
            cancellationToken);
    }

    [RelayCommand]
    private Task UpdateConnectorAsync(CancellationToken cancellationToken)
    {
        if (CurrentProject is null || SelectedSprite is null || SelectedConnector is null ||
            string.IsNullOrWhiteSpace(NewConnectorName))
        {
            return Task.CompletedTask;
        }

        var selectedId = SelectedSprite.Id;
        var currentName = SelectedConnector.Name;
        var newName = NewConnectorName;
        return ApplyEditAsync(
            () => AtlasProjectEditor.UpdateConnector(
                CurrentProject,
                selectedId,
                currentName,
                new AtlasConnector(newName, NewConnectorX, NewConnectorY)),
            selectedId,
            newName,
            "Connector updated.",
            cancellationToken);
    }

    [RelayCommand]
    private Task MoveConnectorFromCanvasAsync(CanvasConnectorMove move, CancellationToken cancellationToken)
    {
        if (CurrentProject is null || SelectedSprite is null)
        {
            return Task.CompletedTask;
        }

        var connector = SelectedSprite.Connectors.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, move.Name, StringComparison.OrdinalIgnoreCase));
        if (connector is null)
        {
            return Task.CompletedTask;
        }

        var scale = ZoomPercent / 100d;
        var x = (int)Math.Round((move.X / scale) - SelectedSprite.Frame.X);
        var y = (int)Math.Round((move.Y / scale) - SelectedSprite.Frame.Y);
        var selectedId = SelectedSprite.Id;
        return ApplyEditAsync(
            () => AtlasProjectEditor.UpdateConnector(
                CurrentProject,
                selectedId,
                connector.Name,
                new AtlasConnector(connector.Name, x, y)),
            selectedId,
            connector.Name,
            "Connector moved.",
            cancellationToken);
    }

    [RelayCommand]
    private void SelectSprite(string spriteId)
    {
        SelectedSprite = Sprites.FirstOrDefault(sprite =>
            string.Equals(sprite.Id, spriteId, StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand]
    private void Undo()
    {
        if (CurrentProject is null || _undoHistory.Count == 0)
        {
            return;
        }

        _redoHistory.Push(CreateSnapshot());
        RestoreSnapshot(_undoHistory.Pop());
        IsDirty = true;
        Status = "Undid the last edit.";
        NotifyHistoryChanged();
    }

    [RelayCommand]
    private void Redo()
    {
        if (CurrentProject is null || _redoHistory.Count == 0)
        {
            return;
        }

        _undoHistory.Push(CreateSnapshot());
        RestoreSnapshot(_redoHistory.Pop());
        IsDirty = true;
        Status = "Redid the last edit.";
        NotifyHistoryChanged();
    }

    [RelayCommand]
    private void Cancel() => _activeOperation?.Cancel();

    [RelayCommand]
    private async Task RepackAsync(CancellationToken cancellationToken)
    {
        if (CurrentProject is null || ProjectPath is null)
        {
            return;
        }

        await RunBusyAsync(async token =>
        {
            await SaveIfDirtyAsync(token);
            var outputDirectory = Path.Combine(Path.GetDirectoryName(ProjectPath)!, "repacked");
            var result = await _atlasForgeService.RepackAsync(
                new RepackAtlasRequest(
                    ProjectPath,
                    outputDirectory,
                    new AtlasPackingOptions
                    {
                        Padding = PackingPadding,
                        MaximumWidth = PackingMaximumWidth,
                        MaximumHeight = PackingMaximumHeight,
                        PowerOfTwo = PackingPowerOfTwo,
                    }),
                CreateProgress(),
                token);
            var outputProjectPath = result.GeneratedFiles.First(path =>
                path.EndsWith(NativeProjectExtension, StringComparison.OrdinalIgnoreCase));
            LoadWorkspace(result.Project, outputProjectPath);
            Status = $"Repacked to {result.Project.Atlas.Size.Width}x{result.Project.Atlas.Size.Height}.";
        }, cancellationToken);
    }

    [RelayCommand]
    private async Task ExportAsync(CancellationToken cancellationToken)
    {
        if (CurrentProject is null || ProjectPath is null)
        {
            return;
        }

        await RunBusyAsync(async token =>
        {
            await SaveIfDirtyAsync(token);
            var outputDirectory = Path.Combine(
                Path.GetDirectoryName(ProjectPath)!,
                $"export-{SelectedExportFormat}");
            var result = await _atlasForgeService.ExportAsync(
                new ExportAtlasRequest(ProjectPath, SelectedExportFormat, outputDirectory),
                CreateProgress(),
                token);
            Status = $"Exported {result.GeneratedFiles.Count} {result.Format} files.";
        }, cancellationToken);
    }

    private Task ApplyEditAsync(
        Func<AtlasProject> edit,
        string? selectedSpriteId,
        string? selectedConnectorName,
        string successStatus,
        CancellationToken cancellationToken,
        Action? afterEdit = null) => RunBusyAsync(_ =>
    {
        _undoHistory.Push(CreateSnapshot());
        try
        {
            var updated = edit();
            _redoHistory.Clear();
            SetProject(updated, selectedSpriteId, selectedConnectorName);
            IsDirty = true;
            Status = successStatus;
            afterEdit?.Invoke();
            NotifyHistoryChanged();
        }
        catch
        {
            _undoHistory.Pop();
            throw;
        }

        return Task.CompletedTask;
    }, cancellationToken);

    private void LoadWorkspace(AtlasProject project, string projectPath)
    {
        ProjectPath = Path.GetFullPath(projectPath);
        SetProject(project, project.Sprites.FirstOrDefault()?.Id, null);
        UpdateCurrentImagePath();
        _undoHistory.Clear();
        _redoHistory.Clear();
        IsDirty = false;
        NotifyHistoryChanged();
    }

    private void SetProject(AtlasProject project, string? selectedSpriteId, string? selectedConnectorName)
    {
        CurrentProject = project;
        Sprites.Clear();
        foreach (var sprite in project.Sprites)
        {
            Sprites.Add(sprite);
        }

        SelectedSprite = selectedSpriteId is null
            ? Sprites.FirstOrDefault()
            : Sprites.FirstOrDefault(sprite =>
                string.Equals(sprite.Id, selectedSpriteId, StringComparison.OrdinalIgnoreCase));
        SelectedConnector = selectedConnectorName is null
            ? null
            : SelectedSprite?.Connectors.FirstOrDefault(connector =>
                string.Equals(connector.Name, selectedConnectorName, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(SpriteCountText));
        RefreshCanvasOverlays();
    }

    private void UpdateCurrentImagePath()
    {
        if (CurrentProject is null || ProjectPath is null)
        {
            CurrentImagePath = null;
            return;
        }

        CurrentImagePath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(ProjectPath)!,
            CurrentProject.Atlas.Image.Replace('/', Path.DirectorySeparatorChar)));
    }

    private void RefreshCanvasOverlays()
    {
        SpriteOverlays.Clear();
        if (CurrentProject is null)
        {
            ConnectorOverlays.Clear();
            return;
        }

        foreach (var sprite in CurrentProject.Sprites)
        {
            SpriteOverlays.Add(new SpriteCanvasOverlay(
                sprite.Id,
                sprite.Frame.X,
                sprite.Frame.Y,
                sprite.Frame.Width,
                sprite.Frame.Height,
                sprite.IsApproved));
        }

        RefreshConnectorOverlays();
    }

    private void RefreshConnectorOverlays()
    {
        ConnectorOverlays.Clear();

        if (SelectedSprite is null)
        {
            return;
        }

        var scale = ZoomPercent / 100d;
        foreach (var connector in SelectedSprite.Connectors)
        {
            ConnectorOverlays.Add(new ConnectorCanvasOverlay(
                connector.Name,
                ((SelectedSprite.Frame.X + connector.X) * scale) - 6,
                ((SelectedSprite.Frame.Y + connector.Y) * scale) - 6,
                string.Equals(connector.Name, SelectedConnector?.Name, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private EditorSnapshot CreateSnapshot() => new(
        CurrentProject ?? throw new InvalidOperationException("No project is loaded."),
        SelectedSprite?.Id,
        SelectedConnector?.Name);

    private void RestoreSnapshot(EditorSnapshot snapshot) =>
        SetProject(snapshot.Project, snapshot.SelectedSpriteId, snapshot.SelectedConnectorName);

    private void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private IProgress<AtlasProgress> CreateProgress() => new Progress<AtlasProgress>(progress =>
    {
        ProgressFraction = Math.Clamp(progress.Fraction, 0, 1);
        Status = progress.Message;
        OnPropertyChanged(nameof(HasProgress));
    });

    private static string SanitizeFileName(string spriteId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(spriteId
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "sprite" : sanitized;
    }

    private SpriteDetectionOptions CreateDetectionOptions()
    {
        if (DetectionAlphaThreshold is < byte.MinValue or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DetectionAlphaThreshold),
                "Alpha threshold must be between 0 and 255.");
        }

        var options = new SpriteDetectionOptions
        {
            AlphaThreshold = (byte)DetectionAlphaThreshold,
            BackgroundMode = DetectionBackgroundMode switch
            {
                "Auto" => SpriteBackgroundMode.Auto,
                "Alpha only" => SpriteBackgroundMode.AlphaOnly,
                "Border connected" => SpriteBackgroundMode.BorderConnected,
                _ => throw new ArgumentException($"Unsupported background mode '{DetectionBackgroundMode}'."),
            },
            BackgroundColorTolerance = DetectionBackgroundTolerance,
            MinimumArea = DetectionMinimumArea,
            MergeDistance = DetectionMergeDistance,
            NoiseReductionRadius = DetectionNoiseReductionRadius,
            SourcePadding = DetectionSourcePadding,
        };
        options.Validate();
        return options;
    }

    private static string CreateNextSpriteId(AtlasProject project)
    {
        for (var number = 1; ; number++)
        {
            var candidate = $"sprite_{number:000}";
            if (!project.Sprites.Any(sprite =>
                    string.Equals(sprite.Id, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }
    }

    private async Task SaveIfDirtyAsync(CancellationToken cancellationToken)
    {
        if (IsDirty && CurrentProject is not null && ProjectPath is not null)
        {
            await _atlasForgeService.SaveAsync(CurrentProject, ProjectPath, cancellationToken);
            IsDirty = false;
        }
    }

    private Task<bool> CanReplaceWorkspaceAsync(CancellationToken cancellationToken) =>
        IsDirty
            ? _interaction.ConfirmDiscardChangesAsync(cancellationToken)
            : Task.FromResult(true);

    private async Task RunBusyAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken externalCancellationToken)
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken);
        _activeOperation = source;
        IsBusy = true;
        ProgressFraction = 0;
        OnPropertyChanged(nameof(CanCancel));
        try
        {
            await operation(source.Token);
        }
        catch (OperationCanceledException)
        {
            Status = "Operation cancelled.";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
        finally
        {
            _activeOperation = null;
            IsBusy = false;
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(HasProgress));
        }
    }

    private sealed record EditorSnapshot(
        AtlasProject Project,
        string? SelectedSpriteId,
        string? SelectedConnectorName);

    private sealed class AlwaysDiscardWorkspaceInteraction : IWorkspaceInteraction
    {
        public static AlwaysDiscardWorkspaceInteraction Instance { get; } = new();

        public Task<bool> ConfirmDiscardChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
