using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.ClientApplication.Services;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.ClientApplication.PageModels;

public partial class WorkspacePageModel : ObservableObject
{
    private readonly IAtlasForgeService _atlasForgeService;
    private readonly IWorkspaceFilePicker _filePicker;

    public WorkspacePageModel(
        AtlasForgeApplicationInfo applicationInfo,
        IAtlasForgeService atlasForgeService,
        IWorkspaceFilePicker filePicker)
    {
        _atlasForgeService = atlasForgeService;
        _filePicker = filePicker;
        Title = applicationInfo.Name;
        Description = applicationInfo.Description;
        NativeProjectExtension = applicationInfo.NativeProjectExtension;
    }

    public string Title { get; }

    public string Description { get; }

    public string NativeProjectExtension { get; }

    public ObservableCollection<AtlasSprite> Sprites { get; } = [];

    public bool HasProject => CurrentProject is not null;

    public string SpriteCountText => Sprites.Count == 1 ? "1 sprite" : $"{Sprites.Count} sprites";

    public double CanvasImageWidth => (CurrentProject?.Atlas.Size.Width ?? 0) * ZoomPercent / 100d;

    public double CanvasImageHeight => (CurrentProject?.Atlas.Size.Height ?? 0) * ZoomPercent / 100d;

    [ObservableProperty]
    public partial string Status { get; set; } = "Open a PNG or native atlas project to begin.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    [NotifyPropertyChangedFor(nameof(CanvasImageWidth))]
    [NotifyPropertyChangedFor(nameof(CanvasImageHeight))]
    public partial AtlasProject? CurrentProject { get; set; }

    [ObservableProperty]
    public partial AtlasSprite? SelectedSprite { get; set; }

    [ObservableProperty]
    public partial string? ProjectPath { get; set; }

    [ObservableProperty]
    public partial string? CurrentImagePath { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanvasImageWidth))]
    [NotifyPropertyChangedFor(nameof(CanvasImageHeight))]
    public partial double ZoomPercent { get; set; } = 100;

    [ObservableProperty]
    public partial string SpriteIdDraft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewConnectorName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int NewConnectorX { get; set; }

    [ObservableProperty]
    public partial int NewConnectorY { get; set; }

    [ObservableProperty]
    public partial AtlasConnector? SelectedConnector { get; set; }

    partial void OnSelectedSpriteChanged(AtlasSprite? value)
    {
        SpriteIdDraft = value?.Id ?? string.Empty;
        SelectedConnector = null;
    }

    partial void OnSelectedConnectorChanged(AtlasConnector? value)
    {
        if (value is null)
        {
            return;
        }

        NewConnectorName = value.Name;
        NewConnectorX = value.X;
        NewConnectorY = value.Y;
    }

    [RelayCommand]
    private async Task OpenImageAsync(CancellationToken cancellationToken)
    {
        var filePath = await _filePicker.PickPngAsync(cancellationToken);
        if (filePath is null)
        {
            return;
        }

        var projectPath = Path.Combine(
            Path.GetDirectoryName(filePath)!,
            Path.GetFileNameWithoutExtension(filePath) + NativeProjectExtension);
        await RunBusyAsync(async () =>
        {
            var project = await _atlasForgeService.DetectAsync(
                new DetectAtlasRequest(filePath, projectPath),
                cancellationToken: cancellationToken);
            LoadWorkspace(project, projectPath);
            Status = $"Detected {project.Sprites.Count} sprites.";
        });
    }

    [RelayCommand]
    private async Task OpenProjectAsync(CancellationToken cancellationToken)
    {
        var filePath = await _filePicker.PickProjectAsync(NativeProjectExtension, cancellationToken);
        if (filePath is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var project = await _atlasForgeService.LoadAsync(filePath, cancellationToken);
            LoadWorkspace(project, filePath);
            Status = $"Opened {project.Name}.";
        });
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (CurrentProject is null || ProjectPath is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _atlasForgeService.SaveAsync(CurrentProject, ProjectPath, cancellationToken);
            Status = "Project saved.";
        });
    }

    [RelayCommand]
    private async Task ValidateAsync(CancellationToken cancellationToken)
    {
        if (ProjectPath is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var validation = await _atlasForgeService.ValidateAsync(ProjectPath, cancellationToken);
            Status = validation.IsValid
                ? "Project is valid."
                : string.Join(" ", validation.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        });
    }

    [RelayCommand]
    private async Task RenameSpriteAsync(CancellationToken cancellationToken)
    {
        if (ProjectPath is null || SelectedSprite is null || string.IsNullOrWhiteSpace(SpriteIdDraft))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var project = await _atlasForgeService.RenameSpriteAsync(
                new RenameSpriteRequest(ProjectPath, SelectedSprite.Id, SpriteIdDraft),
                cancellationToken);
            LoadWorkspace(project, ProjectPath);
            SelectedSprite = project.GetSprite(SpriteIdDraft);
            Status = $"Renamed sprite to '{SpriteIdDraft}'.";
        });
    }

    [RelayCommand]
    private async Task AddConnectorAsync(CancellationToken cancellationToken)
    {
        if (ProjectPath is null || SelectedSprite is null || string.IsNullOrWhiteSpace(NewConnectorName))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var project = await _atlasForgeService.AddConnectorAsync(
                new AddConnectorRequest(
                    ProjectPath,
                    SelectedSprite.Id,
                    NewConnectorName,
                    NewConnectorX,
                    NewConnectorY),
                cancellationToken);
            var selectedId = SelectedSprite.Id;
            LoadWorkspace(project, ProjectPath);
            SelectedSprite = project.GetSprite(selectedId);
            NewConnectorName = string.Empty;
            Status = "Connector added.";
        });
    }

    [RelayCommand]
    private async Task RemoveConnectorAsync(AtlasConnector connector, CancellationToken cancellationToken)
    {
        if (ProjectPath is null || SelectedSprite is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var project = await _atlasForgeService.RemoveConnectorAsync(
                new RemoveConnectorRequest(ProjectPath, SelectedSprite.Id, connector.Name),
                cancellationToken);
            var selectedId = SelectedSprite.Id;
            LoadWorkspace(project, ProjectPath);
            SelectedSprite = project.GetSprite(selectedId);
            Status = "Connector removed.";
        });
    }

    [RelayCommand]
    private async Task UpdateConnectorAsync(CancellationToken cancellationToken)
    {
        if (ProjectPath is null || SelectedSprite is null || SelectedConnector is null ||
            string.IsNullOrWhiteSpace(NewConnectorName))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var selectedId = SelectedSprite.Id;
            var connectorName = NewConnectorName;
            var project = await _atlasForgeService.UpdateConnectorAsync(
                new UpdateConnectorRequest(
                    ProjectPath,
                    selectedId,
                    SelectedConnector.Name,
                    connectorName,
                    NewConnectorX,
                    NewConnectorY),
                cancellationToken);
            LoadWorkspace(project, ProjectPath);
            SelectedSprite = project.GetSprite(selectedId);
            SelectedConnector = SelectedSprite.Connectors.Single(connector =>
                string.Equals(connector.Name, connectorName, StringComparison.OrdinalIgnoreCase));
            Status = "Connector updated.";
        });
    }

    [RelayCommand]
    private async Task RepackAsync(CancellationToken cancellationToken)
    {
        if (ProjectPath is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var outputDirectory = Path.Combine(Path.GetDirectoryName(ProjectPath)!, "repacked");
            var result = await _atlasForgeService.RepackAsync(
                new RepackAtlasRequest(ProjectPath, outputDirectory),
                cancellationToken: cancellationToken);
            Status = $"Repacked to {result.Project.Atlas.Size.Width}x{result.Project.Atlas.Size.Height}.";
        });
    }

    [RelayCommand]
    private async Task ExportPhaserAsync(CancellationToken cancellationToken)
    {
        if (ProjectPath is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var outputDirectory = Path.Combine(Path.GetDirectoryName(ProjectPath)!, "export-phaser");
            var result = await _atlasForgeService.ExportAsync(
                new ExportAtlasRequest(ProjectPath, "phaser-json-hash", outputDirectory),
                cancellationToken: cancellationToken);
            Status = $"Exported {result.GeneratedFiles.Count} Phaser files.";
        });
    }

    private void LoadWorkspace(AtlasProject project, string projectPath)
    {
        CurrentProject = project;
        ProjectPath = Path.GetFullPath(projectPath);
        CurrentImagePath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(ProjectPath)!,
            project.Atlas.Image.Replace('/', Path.DirectorySeparatorChar)));
        Sprites.Clear();
        foreach (var sprite in project.Sprites)
        {
            Sprites.Add(sprite);
        }

        SelectedSprite = Sprites.FirstOrDefault();
        OnPropertyChanged(nameof(SpriteCountText));
    }

    private async Task RunBusyAsync(Func<Task> operation)
    {
        IsBusy = true;
        try
        {
            await operation();
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
            IsBusy = false;
        }
    }
}
