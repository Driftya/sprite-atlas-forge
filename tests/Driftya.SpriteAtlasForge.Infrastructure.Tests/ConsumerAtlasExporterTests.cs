using System.IO;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Domain;
using Driftya.SpriteAtlasForge.Infrastructure;

namespace Driftya.SpriteAtlasForge.Infrastructure.Tests;

public sealed class ConsumerAtlasExporterTests
{
    [Test]
    public async Task Unity_6_export_writes_approved_multi_sprite_import_metadata()
    {
        using var directory = new TestDirectory();
        var projectPath = directory.GetPath("project/modules.saf.json");
        var imagePath = directory.GetPath("project/modules.png");
        var outputDirectory = directory.GetPath("unity");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);

        var result = await new UnitySpriteSheetExporter().ExportAsync(
            CreateProject(), projectPath, outputDirectory);

        await Assert.That(result.GeneratedFiles).Count().IsEqualTo(2);
        var metadata = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "modules.png.meta"));
        await Assert.That(metadata).Contains("spriteMode: 2");
        await Assert.That(metadata).Contains("name: \"engine\"");
        await Assert.That(metadata).Contains("y: 22");
        await Assert.That(metadata).DoesNotContain("unapproved");
        await Assert.That(File.Exists(Path.Combine(outputDirectory, "modules.png"))).IsTrue();
    }

    [Test]
    public async Task Godot_4_export_writes_one_atlas_texture_per_approved_sprite()
    {
        using var directory = new TestDirectory();
        var projectPath = directory.GetPath("project/modules.saf.json");
        var imagePath = directory.GetPath("project/modules.png");
        var outputDirectory = directory.GetPath("godot");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3]);

        var result = await new GodotAtlasTextureExporter().ExportAsync(
            CreateProject(), projectPath, outputDirectory);

        await Assert.That(result.GeneratedFiles).Count().IsEqualTo(2);
        var resourcePath = Path.Combine(outputDirectory, "modules.engine.tres");
        var resource = await File.ReadAllTextAsync(resourcePath);
        await Assert.That(resource).Contains("type=\"AtlasTexture\"");
        await Assert.That(resource).Contains("path=\"modules.png\"");
        await Assert.That(resource).Contains("region = Rect2(3, 4, 8, 6)");
        await Assert.That(resource).Contains("filter_clip = true");
        await Assert.That(Directory.GetFiles(outputDirectory, "*.tres")).Count().IsEqualTo(1);
    }

    private static AtlasProject CreateProject() => new(
        "modules",
        new AtlasSource("modules.png", new PixelSize(32, 32), new string('a', 64)),
        new AtlasOutput("modules.png", new PixelSize(32, 32), false),
        [
            new AtlasSprite(
                "engine",
                new PixelRect(3, 4, 8, 6),
                new PixelRect(3, 4, 8, 6),
                isApproved: true),
            new AtlasSprite(
                "unapproved",
                new PixelRect(12, 4, 8, 6),
                new PixelRect(12, 4, 8, 6)),
        ]);
}
