using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.ClientApplication.PageModels;
using Driftya.SpriteAtlasForge.ClientApplication.Services;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.ClientApplication.Tests;

public sealed class WorkspacePageModelTests
{
    [Test]
    public async Task Constructor_exposes_product_information_and_an_empty_workspace()
    {
        var model = new WorkspacePageModel(
            AtlasForgeApplicationInfo.Default,
            new StubService(CreateProject()),
            new StubFilePicker());

        await Assert.That(model.Title).IsEqualTo("Sprite Atlas Forge");
        await Assert.That(model.NativeProjectExtension).IsEqualTo(".saf.json");
        await Assert.That(model.HasProject).IsFalse();
        await Assert.That(model.SpriteCountText).IsEqualTo("0 sprites");
    }

    [Test]
    public async Task Zoom_uses_atlas_pixel_dimensions_so_scroll_panning_matches_the_rendered_image()
    {
        var project = CreateProject();
        var model = CreateLoadedModel(new StubService(project), project);

        model.ZoomPercent = 250;

        await Assert.That(model.CanvasImageWidth).IsEqualTo(40d);
        await Assert.That(model.CanvasImageHeight).IsEqualTo(40d);
    }

    [Test]
    public async Task Selecting_a_connector_populates_the_accessible_numeric_editor()
    {
        var connector = new AtlasConnector("next", 7, 4);
        var model = CreateLoadedModel(new StubService(CreateProject([connector])), CreateProject([connector]));

        model.SelectedConnector = connector;

        await Assert.That(model.NewConnectorName).IsEqualTo("next");
        await Assert.That(model.NewConnectorX).IsEqualTo(7);
        await Assert.That(model.NewConnectorY).IsEqualTo(4);
    }

    [Test]
    public async Task Update_connector_command_edits_in_memory_and_tracks_dirty_undo_state()
    {
        var original = new AtlasConnector("next", 7, 4);
        var service = new StubService(CreateProject([original]));
        var model = CreateLoadedModel(service, CreateProject([original]));
        model.SelectedConnector = original;
        model.NewConnectorName = "attachment";
        model.NewConnectorX = 9;
        model.NewConnectorY = 2;

        await model.UpdateConnectorCommand.ExecuteAsync(null);

        await Assert.That(model.SelectedConnector).IsEqualTo(new AtlasConnector("attachment", 9, 2));
        await Assert.That(model.IsDirty).IsTrue();
        await Assert.That(model.CanUndo).IsTrue();
        await Assert.That(model.Status).IsEqualTo("Connector updated.");
        await Assert.That(model.IsBusy).IsFalse();
    }

    [Test]
    public async Task Cancel_command_cancels_a_running_detection()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var service = new StubService(CreateProject()) { WaitForDetectionCancellation = true };
        var model = new WorkspacePageModel(
            AtlasForgeApplicationInfo.Default,
            service,
            new StubFilePicker { PngPath = Path.Combine(root, "source.png") });

        var operation = model.OpenImageCommand.ExecuteAsync(null);
        await service.DetectStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        model.CancelCommand.Execute(null);
        await operation;

        await Assert.That(model.Status).IsEqualTo("Operation cancelled.");
        await Assert.That(model.IsBusy).IsFalse();
    }

    [Test]
    public async Task Open_image_command_detects_and_loads_a_workspace_through_the_picker_port()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var imagePath = Path.Combine(root, "modules.png");
        var project = CreateProject();
        var service = new StubService(project);
        var picker = new StubFilePicker { PngPath = imagePath };
        var model = new WorkspacePageModel(AtlasForgeApplicationInfo.Default, service, picker);

        await model.OpenImageCommand.ExecuteAsync(null);

        await Assert.That(service.LastDetectRequest).IsNotNull();
        await Assert.That(service.LastDetectRequest!.SourceImagePath).IsEqualTo(imagePath);
        await Assert.That(service.LastDetectRequest.ProjectPath).IsEqualTo(Path.Combine(root, "modules.saf.json"));
        await Assert.That(model.HasProject).IsTrue();
        await Assert.That(model.Sprites).Count().IsEqualTo(1);
        await Assert.That(model.Status).IsEqualTo("Detected 1 sprites.");
    }

    [Test]
    public async Task Open_project_save_and_validate_commands_refresh_user_visible_state()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var projectPath = Path.Combine(root, "project.saf.json");
        var service = new StubService(CreateProject());
        var picker = new StubFilePicker { ProjectPath = projectPath };
        var model = new WorkspacePageModel(AtlasForgeApplicationInfo.Default, service, picker);

        await model.OpenProjectCommand.ExecuteAsync(null);
        await model.SaveCommand.ExecuteAsync(null);
        await model.ValidateCommand.ExecuteAsync(null);

        await Assert.That(service.LoadCount).IsEqualTo(1);
        await Assert.That(service.SaveCount).IsEqualTo(1);
        await Assert.That(service.ValidateCount).IsEqualTo(0);
        await Assert.That(model.ProjectPath).IsEqualTo(Path.GetFullPath(projectPath));
        await Assert.That(model.Status).IsEqualTo("Project is valid.");
    }

    [Test]
    public async Task Rename_add_and_remove_commands_refresh_selected_sprite_metadata()
    {
        var initial = CreateProject();
        var service = new StubService(initial);
        var model = CreateLoadedModel(service, initial);
        model.SpriteIdDraft = "habitat";
        await model.RenameSpriteCommand.ExecuteAsync(null);

        var connector = new AtlasConnector("next", 5, 3);
        model.NewConnectorName = connector.Name;
        model.NewConnectorX = connector.X;
        model.NewConnectorY = connector.Y;
        await model.AddConnectorCommand.ExecuteAsync(null);

        await model.RemoveConnectorCommand.ExecuteAsync(connector);

        await Assert.That(model.SelectedSprite!.Id).IsEqualTo("habitat");
        await Assert.That(model.SelectedSprite.Connectors).IsEmpty();
        await Assert.That(model.Status).IsEqualTo("Connector removed.");
    }

    [Test]
    public async Task Repack_and_export_commands_use_predictable_sibling_output_directories()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var project = CreateProject();
        var service = new StubService(project);
        var model = CreateLoadedModel(service, project);
        model.ProjectPath = Path.Combine(root, "project.saf.json");
        model.PackingPadding = 4;
        model.PackingMaximumWidth = 128;
        model.PackingMaximumHeight = 64;
        model.PackingPowerOfTwo = false;

        await model.RepackCommand.ExecuteAsync(null);
        await model.ExportCommand.ExecuteAsync(null);

        await Assert.That(service.LastRepackRequest!.OutputDirectory).IsEqualTo(Path.Combine(root, "repacked"));
        await Assert.That(service.LastRepackRequest.Options!.Padding).IsEqualTo(4);
        await Assert.That(service.LastRepackRequest.Options.MaximumWidth).IsEqualTo(128);
        await Assert.That(service.LastRepackRequest.Options.MaximumHeight).IsEqualTo(64);
        await Assert.That(service.LastRepackRequest.Options.PowerOfTwo).IsFalse();
        await Assert.That(service.LastExportRequest!.Format).IsEqualTo("phaser-json-hash");
        await Assert.That(service.LastExportRequest.OutputDirectory)
            .IsEqualTo(Path.Combine(root, "repacked", "export-phaser-json-hash"));
        await Assert.That(model.Status).IsEqualTo("Exported 1 phaser-json-hash files.");
    }

    [Test]
    public async Task Region_edit_refreshes_canvas_and_supports_undo_and_redo()
    {
        var project = CreateProject();
        var model = CreateLoadedModel(new StubService(project), project);
        model.SourceRegionX = 2;
        model.SourceRegionY = 3;
        model.SourceRegionWidth = 6;
        model.SourceRegionHeight = 5;

        await model.UpdateSpriteRegionCommand.ExecuteAsync(null);

        await Assert.That(model.SelectedSprite!.SourceRegion).IsEqualTo(new PixelRect(2, 3, 6, 5));
        await Assert.That(model.SpriteOverlays[0].X).IsEqualTo(2d);
        model.UndoCommand.Execute(null);
        await Assert.That(model.SelectedSprite!.SourceRegion).IsEqualTo(new PixelRect(0, 0, 10, 8));
        model.RedoCommand.Execute(null);
        await Assert.That(model.SelectedSprite!.SourceRegion).IsEqualTo(new PixelRect(2, 3, 6, 5));
    }

    [Test]
    public async Task Canvas_connector_coordinates_round_trip_through_zoom_transform()
    {
        var project = CreateProject();
        var model = CreateLoadedModel(new StubService(project), project);
        model.ZoomPercent = 200;
        model.NewConnectorName = "next";

        await model.AddConnectorAtCanvasCommand.ExecuteAsync(new CanvasPoint(12, 8));

        await Assert.That(model.SelectedConnector).IsEqualTo(new AtlasConnector("next", 6, 4));
        await Assert.That(model.ConnectorOverlays[0].X).IsEqualTo(6d);
        await Assert.That(model.ConnectorOverlays[0].Y).IsEqualTo(2d);
    }

    [Test]
    public async Task Save_as_uses_picker_destination_and_clears_dirty_state()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source.saf.json");
        var destination = Path.Combine(root, "copy", "copy.saf.json");
        var project = CreateProject();
        var service = new StubService(project);
        var model = CreateLoadedModel(service, project);
        model.ProjectPath = source;
        model.SpriteIdDraft = "renamed";
        await model.RenameSpriteCommand.ExecuteAsync(null);
        service.ResultProject = model.CurrentProject!;

        var picker = new StubFilePicker { SavePath = destination };
        var saveAsModel = new WorkspacePageModel(AtlasForgeApplicationInfo.Default, service, picker)
        {
            CurrentProject = model.CurrentProject,
            ProjectPath = source,
            IsDirty = true,
        };
        foreach (var sprite in model.Sprites)
        {
            saveAsModel.Sprites.Add(sprite);
        }

        await saveAsModel.SaveAsCommand.ExecuteAsync(null);

        await Assert.That(service.LastSaveAsDestination).IsEqualTo(destination);
        await Assert.That(saveAsModel.ProjectPath).IsEqualTo(Path.GetFullPath(destination));
        await Assert.That(saveAsModel.IsDirty).IsFalse();
    }

    [Test]
    public async Task Open_project_keeps_dirty_workspace_when_discard_is_declined()
    {
        var project = CreateProject();
        var service = new StubService(project);
        var model = new WorkspacePageModel(
            AtlasForgeApplicationInfo.Default,
            service,
            new StubFilePicker { ProjectPath = "replacement.saf.json" },
            new StubInteraction(false))
        {
            CurrentProject = project,
            ProjectPath = "current.saf.json",
            IsDirty = true,
        };

        await model.OpenProjectCommand.ExecuteAsync(null);

        await Assert.That(service.LoadCount).IsEqualTo(0);
        await Assert.That(model.ProjectPath).IsEqualTo("current.saf.json");
        await Assert.That(model.IsDirty).IsTrue();
    }

    private static WorkspacePageModel CreateLoadedModel(StubService service, AtlasProject project)
    {
        var model = new WorkspacePageModel(AtlasForgeApplicationInfo.Default, service, new StubFilePicker())
        {
            CurrentProject = project,
            ProjectPath = "project.saf.json",
            SelectedSprite = project.Sprites[0],
        };
        foreach (var sprite in project.Sprites)
        {
            model.Sprites.Add(sprite);
        }

        return model;
    }

    private static AtlasProject CreateProject(
        IReadOnlyList<AtlasConnector>? connectors = null,
        string spriteId = "module") => new(
        "project",
        new AtlasSource("source.png", new PixelSize(16, 16), new string('a', 64)),
        new AtlasOutput("source.png", new PixelSize(16, 16), false),
        [new AtlasSprite(
            spriteId,
            new PixelRect(0, 0, 10, 8),
            new PixelRect(0, 0, 10, 8),
            connectors)]);

    private sealed class StubService(AtlasProject project) : IAtlasForgeService
    {
        public AtlasProject ResultProject { get; set; } = project;
        public DetectAtlasRequest? LastDetectRequest { get; private set; }
        public RenameSpriteRequest? LastRenameRequest { get; private set; }
        public AddConnectorRequest? LastAddRequest { get; private set; }
        public RemoveConnectorRequest? LastRemoveRequest { get; private set; }
        public UpdateConnectorRequest? LastUpdateRequest { get; private set; }
        public RepackAtlasRequest? LastRepackRequest { get; private set; }
        public ExportAtlasRequest? LastExportRequest { get; private set; }
        public bool CancelUpdate { get; init; }
        public bool WaitForDetectionCancellation { get; init; }
        public TaskCompletionSource DetectStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? LastSaveAsDestination { get; private set; }
        public int LoadCount { get; private set; }
        public int SaveCount { get; private set; }
        public int ValidateCount { get; private set; }

        public Task<AtlasProject> DetectAsync(
            DetectAtlasRequest request,
            IProgress<AtlasProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastDetectRequest = request;
            DetectStarted.TrySetResult();
            if (WaitForDetectionCancellation)
            {
                return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                    .ContinueWith<AtlasProject>(
                        _ => throw new InvalidOperationException("Detection should have been cancelled."),
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnRanToCompletion,
                        TaskScheduler.Default);
            }

            return Task.FromResult(ResultProject);
        }

        public Task<AtlasProject> LoadAsync(string projectPath, CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return Task.FromResult(ResultProject);
        }

        public Task SaveAsync(
            AtlasProject atlasProject,
            string projectPath,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task SaveAsAsync(
            AtlasProject atlasProject,
            string sourceProjectPath,
            string destinationProjectPath,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            LastSaveAsDestination = destinationProjectPath;
            return Task.CompletedTask;
        }

        public Task<AtlasValidationResult> ValidateAsync(
            string projectPath,
            CancellationToken cancellationToken = default)
        {
            ValidateCount++;
            return Task.FromResult(AtlasValidationResult.Valid);
        }

        public Task<AtlasProject> AddConnectorAsync(
            AddConnectorRequest request,
            CancellationToken cancellationToken = default)
        {
            LastAddRequest = request;
            return Task.FromResult(ResultProject);
        }

        public Task<AtlasProject> RemoveConnectorAsync(
            RemoveConnectorRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRemoveRequest = request;
            return Task.FromResult(ResultProject);
        }

        public Task<AtlasProject> UpdateConnectorAsync(
            UpdateConnectorRequest request,
            CancellationToken cancellationToken = default)
        {
            LastUpdateRequest = request;
            return CancelUpdate
                ? Task.FromCanceled<AtlasProject>(new CancellationToken(canceled: true))
                : Task.FromResult(ResultProject);
        }

        public Task<AtlasProject> RenameSpriteAsync(
            RenameSpriteRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRenameRequest = request;
            return Task.FromResult(ResultProject);
        }

        public Task<AtlasProject> UpdateSpriteRegionAsync(
            UpdateSpriteRegionRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(ResultProject);

        public Task<RepackAtlasResult> RepackAsync(
            RepackAtlasRequest request,
            IProgress<AtlasProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastRepackRequest = request;
            return Task.FromResult(new RepackAtlasResult(
                ResultProject,
                [Path.Combine(request.OutputDirectory, "project.saf.json")]));
        }

        public Task<AtlasExportResult> ExportAsync(
            ExportAtlasRequest request,
            IProgress<AtlasProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastExportRequest = request;
            return Task.FromResult(new AtlasExportResult(request.Format, ["atlas.json"], []));
        }
    }

    private sealed class StubFilePicker : IWorkspaceFilePicker
    {
        public string? PngPath { get; init; }
        public string? ProjectPath { get; init; }
        public string? SavePath { get; init; }

        public Task<string?> PickPngAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PngPath);

        public Task<string?> PickProjectAsync(
            string nativeProjectExtension,
            CancellationToken cancellationToken = default) => Task.FromResult(ProjectPath);

        public Task<string?> PickProjectSavePathAsync(
            string suggestedName,
            string nativeProjectExtension,
            CancellationToken cancellationToken = default) => Task.FromResult(SavePath);
    }

    private sealed class StubInteraction(bool discard) : IWorkspaceInteraction
    {
        public Task<bool> ConfirmDiscardChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(discard);
    }
}
