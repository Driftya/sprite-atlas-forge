using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.CliApplication;
using Driftya.SpriteAtlasForge.Domain;
using System.CommandLine;

namespace Driftya.SpriteAtlasForge.CliApplication.Tests;

public sealed class AtlasForgeCliTests
{
    [Test]
    public async Task Root_info_and_valid_validation_commands_return_success()
    {
        var service = new StubService();
        var root = new AtlasForgeCli(service, AtlasForgeApplicationInfo.Default).CreateRootCommand();

        var rootExitCode = await root.Parse([]).InvokeAsync();
        var infoExitCode = await root.Parse(["info"]).InvokeAsync();
        var validateExitCode = await root.Parse(["validate", "project.saf.json", "--json"]).InvokeAsync();

        await Assert.That(rootExitCode).IsEqualTo(0);
        await Assert.That(infoExitCode).IsEqualTo(0);
        await Assert.That(validateExitCode).IsEqualTo(0);
        await Assert.That(service.ValidateCount).IsEqualTo(1);
    }

    [Test]
    public async Task Detect_forwards_all_explicit_options()
    {
        var service = new StubService();
        var root = new AtlasForgeCli(service, AtlasForgeApplicationInfo.Default).CreateRootCommand();

        var exitCode = await root.Parse([
            "detect", "source.png", "--output", "project.saf.json", "--name", "modules",
            "--alpha-threshold", "12", "--minimum-area", "3", "--merge-distance", "2",
            "--source-padding", "1", "--max-width", "2048", "--max-height", "1024",
            "--max-pixels", "1000000", "--noise-reduction-radius", "2",
            "--background-mode", "border-connected", "--background-tolerance", "15", "--json",
            "--recover-detached-details",
        ]).InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(service.LastDetectRequest).IsNotNull();
        await Assert.That(service.LastDetectRequest!.Name).IsEqualTo("modules");
        await Assert.That(service.LastDetectRequest.Options!.AlphaThreshold).IsEqualTo((byte)12);
        await Assert.That(service.LastDetectRequest.Options.MinimumArea).IsEqualTo(3);
        await Assert.That(service.LastDetectRequest.Options.MergeDistance).IsEqualTo(2);
        await Assert.That(service.LastDetectRequest.Options.SourcePadding).IsEqualTo(1);
        await Assert.That(service.LastDetectRequest.Options.NoiseReductionRadius).IsEqualTo(2);
        await Assert.That(service.LastDetectRequest.Options.BackgroundMode)
            .IsEqualTo(SpriteBackgroundMode.BorderConnected);
        await Assert.That(service.LastDetectRequest.Options.BackgroundColorTolerance).IsEqualTo(15);
        await Assert.That(service.LastDetectRequest.Options.RecoverDetachedDetails).IsTrue();
        await Assert.That(service.LastDetectRequest.Options.MaximumWidth).IsEqualTo(2048);
        await Assert.That(service.LastDetectRequest.Options.MaximumHeight).IsEqualTo(1024);
        await Assert.That(service.LastDetectRequest.Options.MaximumPixels).IsEqualTo(1_000_000);
    }

    [Test]
    public async Task Metadata_repack_and_export_commands_forward_requests()
    {
        var service = new StubService();
        var root = new AtlasForgeCli(service, AtlasForgeApplicationInfo.Default).CreateRootCommand();

        var rename = await root.Parse([
            "sprite", "rename", "project.saf.json", "--sprite", "module", "--new-id", "habitat",
        ]).InvokeAsync();
        var region = await root.Parse([
            "sprite", "region", "project.saf.json", "--sprite", "module",
            "--x", "1", "--y", "2", "--width", "6", "--height", "5", "--json",
        ]).InvokeAsync();
        var repack = await root.Parse([
            "repack", "project.saf.json", "--output", "repacked", "--padding", "4",
            "--max-width", "128", "--max-height", "64", "--no-power-of-two", "--json",
        ]).InvokeAsync();
        var export = await root.Parse([
            "export", "project.saf.json", "--format", "native", "--output", "export", "--json",
        ]).InvokeAsync();

        await Assert.That(rename).IsEqualTo(0);
        await Assert.That(service.LastRenameRequest!.NewId).IsEqualTo("habitat");
        await Assert.That(region).IsEqualTo(0);
        await Assert.That(service.LastRegionRequest!.Width).IsEqualTo(6);
        await Assert.That(service.LastRegionRequest.Height).IsEqualTo(5);
        await Assert.That(repack).IsEqualTo(0);
        await Assert.That(service.LastRepackRequest!.Options!.Padding).IsEqualTo(4);
        await Assert.That(service.LastRepackRequest.Options.PowerOfTwo).IsFalse();
        await Assert.That(export).IsEqualTo(0);
        await Assert.That(service.LastExportRequest!.Format).IsEqualTo("native");
    }

    [Test]
    [Arguments("unity-6-spritesheet")]
    [Arguments("godot-4-atlas-textures")]
    public async Task Consumer_export_formats_are_accepted(string format)
    {
        var service = new StubService();
        var root = new AtlasForgeCli(service, AtlasForgeApplicationInfo.Default).CreateRootCommand();

        var exitCode = await root.Parse([
            "export", "project.saf.json", "--format", format, "--output", "export",
        ]).InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(service.LastExportRequest!.Format).IsEqualTo(format);
    }

    [Test]
    [Arguments(typeof(AtlasProjectFormatException), 3)]
    [Arguments(typeof(IOException), 4)]
    [Arguments(typeof(UnauthorizedAccessException), 4)]
    [Arguments(typeof(OperationCanceledException), 5)]
    [Arguments(typeof(InvalidOperationException), 6)]
    public async Task Processing_exceptions_map_to_stable_exit_codes(Type exceptionType, int expectedExitCode)
    {
        var service = new StubService { DetectException = CreateException(exceptionType) };
        var root = new AtlasForgeCli(service, AtlasForgeApplicationInfo.Default).CreateRootCommand();

        var exitCode = await root.Parse([
            "detect", "source.png", "--output", "project.saf.json",
        ]).InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(expectedExitCode);
    }

    [Test]
    public async Task Invalid_validation_and_out_of_range_detection_options_return_invalid_project()
    {
        var service = new StubService
        {
            ValidationResult = new AtlasValidationResult([
                new AtlasDiagnostic("SAF1001", "Invalid region.", "sprites[0].sourceRegion"),
            ]),
        };
        var root = new AtlasForgeCli(service, AtlasForgeApplicationInfo.Default).CreateRootCommand();

        var validate = await root.Parse(["validate", "project.saf.json"]).InvokeAsync();
        var detect = await root.Parse([
            "detect", "source.png", "--output", "project.saf.json", "--alpha-threshold", "256",
        ]).InvokeAsync();
        var invalidLimit = await root.Parse([
            "detect", "source.png", "--output", "project.saf.json", "--max-pixels", "0",
        ]).InvokeAsync();

        await Assert.That(validate).IsEqualTo(3);
        await Assert.That(detect).IsEqualTo(3);
        await Assert.That(invalidLimit).IsEqualTo(3);
    }

    private static Exception CreateException(Type exceptionType)
    {
        if (exceptionType == typeof(AtlasProjectFormatException))
        {
            return new AtlasProjectFormatException("Invalid project.");
        }

        if (exceptionType == typeof(IOException))
        {
            return new IOException("I/O failed.");
        }

        if (exceptionType == typeof(UnauthorizedAccessException))
        {
            return new UnauthorizedAccessException("Access denied.");
        }

        if (exceptionType == typeof(OperationCanceledException))
        {
            return new OperationCanceledException();
        }

        return new InvalidOperationException("Processing failed.");
    }

    private sealed class StubService : IAtlasForgeService
    {
        private static readonly AtlasProject Project = new(
            "project",
            new AtlasSource("source.png", new PixelSize(16, 16), new string('a', 64)),
            new AtlasOutput("source.png", new PixelSize(16, 16), false),
            [new AtlasSprite("module", new PixelRect(0, 0, 8, 8), new PixelRect(0, 0, 8, 8))]);

        public Exception? DetectException { get; init; }
        public AtlasValidationResult ValidationResult { get; init; } = AtlasValidationResult.Valid;
        public DetectAtlasRequest? LastDetectRequest { get; private set; }
        public RenameSpriteRequest? LastRenameRequest { get; private set; }
        public UpdateSpriteRegionRequest? LastRegionRequest { get; private set; }
        public RepackAtlasRequest? LastRepackRequest { get; private set; }
        public ExportAtlasRequest? LastExportRequest { get; private set; }
        public int ValidateCount { get; private set; }

        public Task<AtlasProject> DetectAsync(
            DetectAtlasRequest request,
            IProgress<AtlasProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastDetectRequest = request;
            return DetectException is null
                ? Task.FromResult(Project)
                : Task.FromException<AtlasProject>(DetectException);
        }

        public Task<AtlasProject> LoadAsync(string projectPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(Project);

        public Task SaveAsync(
            AtlasProject project,
            string projectPath,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveAsAsync(
            AtlasProject project,
            string sourceProjectPath,
            string destinationProjectPath,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<AtlasValidationResult> ValidateAsync(
            string projectPath,
            CancellationToken cancellationToken = default)
        {
            ValidateCount++;
            return Task.FromResult(ValidationResult);
        }

        public Task<AtlasProject> AddConnectorAsync(
            AddConnectorRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(Project);

        public Task<AtlasProject> RemoveConnectorAsync(
            RemoveConnectorRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(Project);

        public Task<AtlasProject> UpdateConnectorAsync(
            UpdateConnectorRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(Project);

        public Task<AtlasProject> RenameSpriteAsync(
            RenameSpriteRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRenameRequest = request;
            return Task.FromResult(Project);
        }

        public Task<AtlasProject> UpdateSpriteRegionAsync(
            UpdateSpriteRegionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRegionRequest = request;
            return Task.FromResult(Project);
        }

        public Task<RepackAtlasResult> RepackAsync(
            RepackAtlasRequest request,
            IProgress<AtlasProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastRepackRequest = request;
            return Task.FromResult(new RepackAtlasResult(Project, ["atlas.saf.json"]));
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
}
