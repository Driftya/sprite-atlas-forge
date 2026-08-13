using System.IO;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Domain;
using Driftya.SpriteAtlasForge.Infrastructure;
using SkiaSharp;

namespace Driftya.SpriteAtlasForge.Infrastructure.Tests;

public sealed class SkiaSpriteImageExporterTests
{
    [Test]
    public async Task Export_crops_the_selected_region_to_a_png()
    {
        using var directory = new TestDirectory();
        var sourcePath = directory.GetPath("atlas.png");
        var destinationPath = directory.GetPath("sprite.png");
        using (var bitmap = new SKBitmap(6, 5, SKColorType.Rgba8888, SKAlphaType.Premul))
        {
            bitmap.Erase(SKColors.Transparent);
            using var canvas = new SKCanvas(bitmap);
            using var paint = new SKPaint { Color = SKColors.DeepSkyBlue };
            canvas.DrawRect(new SKRect(2, 1, 5, 4), paint);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            await using var stream = File.Create(sourcePath);
            data.SaveTo(stream);
        }

        await new SkiaSpriteImageExporter().ExportAsync(
            sourcePath,
            destinationPath,
            new PixelRect(2, 1, 3, 3));

        using var exported = SKBitmap.Decode(destinationPath);
        await Assert.That(exported.Width).IsEqualTo(3);
        await Assert.That(exported.Height).IsEqualTo(3);
        await Assert.That(exported.GetPixel(1, 1)).IsEqualTo(SKColors.DeepSkyBlue);
    }
}
