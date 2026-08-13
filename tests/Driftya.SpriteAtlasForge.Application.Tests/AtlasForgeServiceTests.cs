using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Application.Tests;

public sealed class AtlasForgeServiceTests
{
    [Test]
    public async Task Detect_creates_a_portable_project_and_reports_progress()
    {
        var root = Path.Combine(Path.GetTempPath(), "atlas-forge-application-tests", Guid.NewGuid().ToString("N"));
        var sourcePath = Path.Combine(root, "source", "modules.png");
        var projectPath = Path.Combine(root, "modules.saf.json");
        var store = new MemoryProjectStore();
        var detector = new StubDetector(new DetectedSpriteSheet(
            new PixelSize(32, 16),
            new string('a', 64),
            [new PixelRect(2, 3, 4, 5)]));
        var progress = new List<AtlasProgress>();
        var service = CreateService(store, detector);

        var project = await service.DetectAsync(
            new DetectAtlasRequest(sourcePath, projectPath),
            new InlineProgress<AtlasProgress>(progress.Add));

        await Assert.That(project.Name).IsEqualTo("modules");
        await Assert.That(project.Source.Image).IsEqualTo("source/modules.png");
        await Assert.That(project.Atlas.Repacked).IsFalse();
        await Assert.That(project.Sprites[0].Id).IsEqualTo("sprite_001");
        await Assert.That(store.SavedPath).IsEqualTo(Path.GetFullPath(projectPath));
        await Assert.That(progress.Select(item => item.Stage)).Contains("detect");
        await Assert.That(progress.Select(item => item.Stage)).Contains("complete");
    }

    [Test]
    public async Task Detect_rejects_a_source_outside_the_project_directory()
    {
        var store = new MemoryProjectStore();
        var service = CreateService(store, new StubDetector(CreateDetection()));
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.DetectAsync(
            new DetectAtlasRequest(
                Path.Combine(root, "outside.png"),
                Path.Combine(root, "project", "atlas.saf.json"))));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("inside the project directory");
    }

    [Test]
    public async Task Connector_and_sprite_operations_load_validate_and_save_through_the_port()
    {
        var store = new MemoryProjectStore { Project = CreateProject() };
        var service = CreateService(store, new StubDetector(CreateDetection()));

        var added = await service.AddConnectorAsync(new("project.saf.json", "module", "next", 8, 4));
        var updated = await service.UpdateConnectorAsync(new(
            "project.saf.json", "module", "next", "attachment", 6, 3));
        var renamed = await service.RenameSpriteAsync(new("project.saf.json", "module", "habitat"));
        var removed = await service.RemoveConnectorAsync(new("project.saf.json", "habitat", "attachment"));

        await Assert.That(added.GetSprite("module").Connectors).Count().IsEqualTo(1);
        await Assert.That(updated.GetSprite("module").Connectors[0])
            .IsEqualTo(new AtlasConnector("attachment", 6, 3));
        await Assert.That(renamed.GetSprite("habitat").Id).IsEqualTo("habitat");
        await Assert.That(removed.GetSprite("habitat").Connectors).IsEmpty();
        await Assert.That(store.SaveCount).IsEqualTo(4);
    }

    [Test]
    public async Task Validate_converts_format_failures_to_structured_diagnostics()
    {
        var store = new MemoryProjectStore
        {
            LoadException = new AtlasProjectFormatException("Unsupported version.", "formatVersion"),
        };
        var service = CreateService(store, new StubDetector(CreateDetection()));

        var result = await service.ValidateAsync("invalid.saf.json");

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Diagnostics[0].Code).IsEqualTo("SAF0001");
        await Assert.That(result.Diagnostics[0].Path).IsEqualTo("formatVersion");
    }

    [Test]
    public async Task Export_rejects_an_unknown_format_before_writing_output()
    {
        var store = new MemoryProjectStore { Project = CreateProject() };
        var service = CreateService(store, new StubDetector(CreateDetection()));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => service.ExportAsync(
            new ExportAtlasRequest("project.saf.json", "unknown", "output")));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("Available formats: test");
    }

    [Test]
    public async Task Repack_preserves_logical_connector_coordinates_and_records_options()
    {
        var root = Path.Combine(Path.GetTempPath(), "atlas-forge-application-tests", Guid.NewGuid().ToString("N"));
        var projectPath = Path.Combine(root, "project.saf.json");
        var store = new MemoryProjectStore
        {
            Project = CreateProject([new AtlasConnector("next", 8, 4)]),
        };
        var packer = new StubPacker(new AtlasPackingResult(
            new PixelSize(32, 32),
            [new PackedSprite("module", new PixelRect(2, 2, 10, 8))]));
        var fileSystem = new RecordingFileSystem();
        var composer = new RecordingComposer();
        var service = CreateService(
            store,
            new StubDetector(CreateDetection()),
            packer,
            composer,
            fileSystem);

        var result = await service.RepackAsync(new RepackAtlasRequest(
            projectPath,
            Path.Combine(root, "output"),
            new AtlasPackingOptions { Padding = 3, MaximumWidth = 64, MaximumHeight = 64 }));

        await Assert.That(result.Project.Atlas.Repacked).IsTrue();
        await Assert.That(result.Project.Atlas.Packing!.Padding).IsEqualTo(3);
        await Assert.That(result.Project.Sprites[0].Frame).IsEqualTo(new PixelRect(2, 2, 10, 8));
        await Assert.That(result.Project.Sprites[0].Connectors[0]).IsEqualTo(new AtlasConnector("next", 8, 4));
        await Assert.That(fileSystem.CopyCount).IsEqualTo(1);
        await Assert.That(composer.ComposeCount).IsEqualTo(1);
        await Assert.That(result.GeneratedFiles).Count().IsEqualTo(3);
    }

    private static AtlasForgeService CreateService(
        MemoryProjectStore store,
        ISpriteDetector detector,
        IAtlasPacker? packer = null,
        IAtlasImageComposer? composer = null,
        IAtlasFileSystem? fileSystem = null) =>
        new(
            store,
            detector,
            packer ?? new StubPacker(new AtlasPackingResult(new PixelSize(16, 16), [])),
            composer ?? new RecordingComposer(),
            fileSystem ?? new RecordingFileSystem(),
            [new StubExporter()]);

    private static DetectedSpriteSheet CreateDetection() => new(
        new PixelSize(16, 16),
        new string('a', 64),
        [new PixelRect(0, 0, 10, 8)]);

    private static AtlasProject CreateProject(IReadOnlyList<AtlasConnector>? connectors = null) => new(
        "project",
        new AtlasSource("source.png", new PixelSize(16, 16), new string('a', 64)),
        new AtlasOutput("source.png", new PixelSize(16, 16), false),
        [new AtlasSprite(
            "module",
            new PixelRect(0, 0, 10, 8),
            new PixelRect(0, 0, 10, 8),
            connectors)]);

    private sealed class MemoryProjectStore : IAtlasProjectStore
    {
        public AtlasProject? Project { get; set; }
        public Exception? LoadException { get; init; }
        public string? SavedPath { get; private set; }
        public int SaveCount { get; private set; }

        public Task<AtlasProject> LoadAsync(string projectPath, CancellationToken cancellationToken = default)
        {
            if (LoadException is not null)
            {
                return Task.FromException<AtlasProject>(LoadException);
            }

            return Task.FromResult(Project ?? throw new InvalidOperationException("No project was stored."));
        }

        public Task SaveAsync(AtlasProject project, string projectPath, CancellationToken cancellationToken = default)
        {
            Project = project;
            SavedPath = projectPath;
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubDetector(DetectedSpriteSheet detection) : ISpriteDetector
    {
        public Task<DetectedSpriteSheet> DetectAsync(
            string imagePath,
            SpriteDetectionOptions options,
            IProgress<AtlasProgress>? progress = null,
            CancellationToken cancellationToken = default) => Task.FromResult(detection);
    }

    private sealed class StubPacker(AtlasPackingResult result) : IAtlasPacker
    {
        public AtlasPackingResult Pack(AtlasProject project, AtlasPackingOptions options) => result;
    }

    private sealed class RecordingComposer : IAtlasImageComposer
    {
        public int ComposeCount { get; private set; }

        public Task ComposeAsync(
            string sourceImagePath,
            string outputImagePath,
            AtlasProject project,
            AtlasPackingResult packing,
            IProgress<AtlasProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ComposeCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingFileSystem : IAtlasFileSystem
    {
        public int CopyCount { get; private set; }

        public Task CopyFileAtomicallyAsync(
            string sourcePath,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            CopyCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubExporter : IAtlasExporter
    {
        public string Format => "test";

        public Task<AtlasExportResult> ExportAsync(
            AtlasProject project,
            string projectPath,
            string outputDirectory,
            IProgress<AtlasProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AtlasExportResult(Format, [], []));
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
