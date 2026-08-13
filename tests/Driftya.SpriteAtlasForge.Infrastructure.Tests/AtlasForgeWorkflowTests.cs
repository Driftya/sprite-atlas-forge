using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Infrastructure;
using SkiaSharp;

namespace Driftya.SpriteAtlasForge.Infrastructure.Tests;

public sealed class AtlasForgeWorkflowTests
{
    [Test]
    public async Task Detect_connector_validate_and_Phaser_export_form_one_vertical_slice()
    {
        using var directory = new TestDirectory();
        var imagePath = directory.GetPath("source/modules.png");
        var projectPath = directory.GetPath("modules.saf.json");
        var outputDirectory = directory.GetPath("export");
        WritePng(imagePath);
        var store = new NativeAtlasProjectStore();
        var service = new AtlasForgeService(
            store,
            new SkiaSpriteDetector(),
            new DeterministicShelfAtlasPacker(),
            new SkiaAtlasImageComposer(),
            new LocalAtlasFileSystem(),
            [new NativeAtlasExporter(store), new PhaserJsonHashExporter()]);

        var detected = await service.DetectAsync(new DetectAtlasRequest(
            imagePath,
            projectPath,
            "modules",
            new SpriteDetectionOptions
            {
                MinimumArea = 1,
                MergeDistance = 0,
                NoiseReductionRadius = 0,
            }));
        var connected = await service.AddConnectorAsync(
            new AddConnectorRequest(projectPath, "sprite_001", "next", 3, 2));
        var validation = await service.ValidateAsync(projectPath);
        var repack = await service.RepackAsync(new RepackAtlasRequest(
            projectPath,
            directory.GetPath("repacked"),
            new AtlasPackingOptions { Padding = 1, MaximumWidth = 32, MaximumHeight = 32 }));
        var export = await service.ExportAsync(
            new ExportAtlasRequest(projectPath, PhaserJsonHashExporter.FormatIdentifier, outputDirectory));
        var secondExport = await service.ExportAsync(
            new ExportAtlasRequest(
                projectPath,
                PhaserJsonHashExporter.FormatIdentifier,
                directory.GetPath("export-second")));
        var nativeExport = await service.ExportAsync(
            new ExportAtlasRequest(projectPath, NativeAtlasExporter.FormatIdentifier, directory.GetPath("export-native")));

        await Assert.That(detected.Sprites).Count().IsEqualTo(1);
        await Assert.That(connected.Sprites[0].Connectors[0].Name).IsEqualTo("next");
        await Assert.That(validation.IsValid).IsTrue();
        await Assert.That(repack.Project.Atlas.Repacked).IsTrue();
        await Assert.That(repack.Project.Atlas.Packing).IsNotNull();
        await Assert.That(repack.Project.Atlas.Packing!.Algorithm).IsEqualTo("deterministic-shelf-v1");
        await Assert.That(repack.Project.Sprites[0].Connectors[0].X).IsEqualTo(3);
        foreach (var generatedFile in repack.GeneratedFiles)
        {
            await Assert.That(File.Exists(generatedFile)).IsTrue();
        }
        await Assert.That(export.GeneratedFiles).Count().IsEqualTo(2);
        foreach (var generatedFile in export.GeneratedFiles)
        {
            await Assert.That(File.Exists(generatedFile)).IsTrue();
        }
        await Assert.That(nativeExport.GeneratedFiles).Count().IsEqualTo(2);
        var exportedNativeProject = await store.LoadAsync(nativeExport.GeneratedFiles[0]);
        await Assert.That(exportedNativeProject.Source.Image).IsEqualTo("modules.png");

        var phaserJson = await File.ReadAllTextAsync(export.GeneratedFiles[0]);
        await Assert.That(phaserJson).IsEqualTo(await File.ReadAllTextAsync(secondExport.GeneratedFiles[0]));
        using var document = JsonDocument.Parse(phaserJson);
        var frame = document.RootElement.GetProperty("frames").GetProperty("sprite_001");
        await Assert.That(frame.GetProperty("connectors")[0].GetProperty("name").GetString())
            .IsEqualTo("next");
        await Assert.That(document.RootElement.GetProperty("meta").GetProperty("image").GetString())
            .IsEqualTo("modules.png");
    }

    private static void WritePng(string path)
    {
        using var bitmap = new SKBitmap(8, 6, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(SKColors.Transparent);
        for (var y = 2; y < 4; y++)
        {
            for (var x = 2; x < 6; x++)
            {
                bitmap.SetPixel(x, y, SKColors.White);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}
