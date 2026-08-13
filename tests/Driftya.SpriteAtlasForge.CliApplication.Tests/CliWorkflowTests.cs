using System.IO;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.CliApplication;
using Driftya.SpriteAtlasForge.Infrastructure;
using SkiaSharp;
using System.CommandLine;

namespace Driftya.SpriteAtlasForge.CliApplication.Tests;

public sealed class CliWorkflowTests
{
    [Test]
    public async Task Detect_command_creates_deterministic_native_projects()
    {
        using var directory = new TestDirectory();
        var imagePath = directory.GetPath("source.png");
        var firstPath = directory.GetPath("first.saf.json");
        var secondPath = directory.GetPath("second.saf.json");
        WritePng(imagePath);
        var store = new NativeAtlasProjectStore();
        var service = new AtlasForgeService(
            store,
            new SkiaSpriteDetector(),
            new DeterministicShelfAtlasPacker(),
            new SkiaAtlasImageComposer(),
            new LocalAtlasFileSystem(),
            [new NativeAtlasExporter(store), new PhaserJsonHashExporter()]);
        var cli = new AtlasForgeCli(service, AtlasForgeApplicationInfo.Default);

        var firstExitCode = await cli.CreateRootCommand().Parse([
            "detect", imagePath, "--output", firstPath, "--name", "cli-smoke", "--json",
        ]).InvokeAsync();
        var secondExitCode = await cli.CreateRootCommand().Parse([
            "detect", imagePath, "--output", secondPath, "--name", "cli-smoke", "--json",
        ]).InvokeAsync();

        await Assert.That(firstExitCode).IsEqualTo(0);
        await Assert.That(secondExitCode).IsEqualTo(0);
        await Assert.That(File.Exists(firstPath)).IsTrue();
        await Assert.That(await File.ReadAllTextAsync(firstPath))
            .IsEqualTo(await File.ReadAllTextAsync(secondPath));
    }

    [Test]
    public async Task Connector_commands_add_move_rename_and_remove_metadata()
    {
        using var directory = new TestDirectory();
        var imagePath = directory.GetPath("source.png");
        var projectPath = directory.GetPath("project.saf.json");
        WritePng(imagePath);
        var store = new NativeAtlasProjectStore();
        var service = new AtlasForgeService(
            store,
            new SkiaSpriteDetector(),
            new DeterministicShelfAtlasPacker(),
            new SkiaAtlasImageComposer(),
            new LocalAtlasFileSystem(),
            [new NativeAtlasExporter(store), new PhaserJsonHashExporter()]);
        var root = new AtlasForgeCli(service, AtlasForgeApplicationInfo.Default).CreateRootCommand();
        await root.Parse(["detect", imagePath, "--output", projectPath]).InvokeAsync();

        var addExitCode = await root.Parse([
            "connector", "add", projectPath, "--sprite", "sprite_001", "--name", "next", "--x", "4", "--y", "3",
        ]).InvokeAsync();
        var updateExitCode = await root.Parse([
            "connector", "update", projectPath, "--sprite", "sprite_001", "--current-name", "next",
            "--name", "attachment", "--x", "2", "--y", "1",
        ]).InvokeAsync();
        var updated = await store.LoadAsync(projectPath);
        var removeExitCode = await root.Parse([
            "connector", "remove", projectPath, "--sprite", "sprite_001", "--name", "attachment",
        ]).InvokeAsync();
        var removed = await store.LoadAsync(projectPath);

        await Assert.That(addExitCode).IsEqualTo(0);
        await Assert.That(updateExitCode).IsEqualTo(0);
        await Assert.That(updated.Sprites[0].Connectors[0].Name).IsEqualTo("attachment");
        await Assert.That(updated.Sprites[0].Connectors[0].X).IsEqualTo(2);
        await Assert.That(removeExitCode).IsEqualTo(0);
        await Assert.That(removed.Sprites[0].Connectors).IsEmpty();
    }

    private static void WritePng(string path)
    {
        using var bitmap = new SKBitmap(10, 8, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(SKColors.Transparent);
        for (var y = 2; y < 6; y++)
        {
            for (var x = 3; x < 8; x++)
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
