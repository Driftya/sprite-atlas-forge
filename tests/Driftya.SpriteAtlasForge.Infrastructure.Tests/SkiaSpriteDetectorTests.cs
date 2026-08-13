using System;
using System.IO;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;
using Driftya.SpriteAtlasForge.Infrastructure;
using SkiaSharp;

namespace Driftya.SpriteAtlasForge.Infrastructure.Tests;

public sealed class SkiaSpriteDetectorTests
{
    [Test]
    public async Task Detection_finds_components_and_sorts_top_to_bottom_then_left_to_right()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("sprites.png");
        WritePng(path, bitmap =>
        {
            Fill(bitmap, 8, 1, 2, 2, SKColors.White);
            Fill(bitmap, 1, 1, 3, 2, SKColors.White);
            Fill(bitmap, 2, 6, 2, 2, SKColors.White);
        });
        var detector = new SkiaSpriteDetector();

        var result = await detector.DetectAsync(path, new SpriteDetectionOptions());

        await Assert.That(result.ImageSize).IsEqualTo(new PixelSize(12, 10));
        await Assert.That(result.Regions).IsEquivalentTo([
            new PixelRect(1, 1, 3, 2),
            new PixelRect(8, 1, 2, 2),
            new PixelRect(2, 6, 2, 2),
        ]);
        await Assert.That(result.Sha256).Length().IsEqualTo(64);
    }

    [Test]
    public async Task Detection_applies_alpha_threshold_minimum_area_merge_and_clamped_padding()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("sprites.png");
        WritePng(path, bitmap =>
        {
            Fill(bitmap, 0, 0, 2, 2, new SKColor(255, 255, 255, 255));
            Fill(bitmap, 3, 0, 2, 2, new SKColor(255, 255, 255, 255));
            bitmap.SetPixel(9, 7, new SKColor(255, 255, 255, 4));
        });
        var detector = new SkiaSpriteDetector();
        var options = new SpriteDetectionOptions
        {
            AlphaThreshold = 8,
            MinimumArea = 2,
            MergeDistance = 1,
            SourcePadding = 2,
        };

        var result = await detector.DetectAsync(path, options);

        await Assert.That(result.Regions).Count().IsEqualTo(1);
        await Assert.That(result.Regions[0]).IsEqualTo(new PixelRect(0, 0, 7, 4));
    }

    [Test]
    public async Task Detection_rejects_non_PNG_input()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("sprites.jpg");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        var detector = new SkiaSpriteDetector();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            detector.DetectAsync(path, new SpriteDetectionOptions()));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("PNG");
    }

    [Test]
    public async Task Detection_rejects_corrupt_and_over_limit_PNGs_before_allocating_component_buffers()
    {
        using var directory = new TestDirectory();
        var corruptPath = directory.GetPath("corrupt.png");
        await File.WriteAllBytesAsync(corruptPath, [1, 2, 3]);
        var validPath = directory.GetPath("valid.png");
        WritePng(validPath, _ => { });
        var detector = new SkiaSpriteDetector();

        var corrupt = await Assert.ThrowsAsync<InvalidDataException>(() =>
            detector.DetectAsync(corruptPath, new SpriteDetectionOptions()));
        var overLimit = await Assert.ThrowsAsync<InvalidDataException>(() =>
            detector.DetectAsync(validPath, new SpriteDetectionOptions { MaximumPixels = 100 }));

        await Assert.That(corrupt!.Message).Contains("could not be decoded");
        await Assert.That(overLimit!.Message).Contains("exceeds the configured detection limit");
    }

    private static void WritePng(string path, Action<SKBitmap> draw)
    {
        using var bitmap = new SKBitmap(12, 10, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(SKColors.Transparent);
        draw(bitmap);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private static void Fill(SKBitmap bitmap, int x, int y, int width, int height, SKColor color)
    {
        for (var currentY = y; currentY < y + height; currentY++)
        {
            for (var currentX = x; currentX < x + width; currentX++)
            {
                bitmap.SetPixel(currentX, currentY, color);
            }
        }
    }
}
