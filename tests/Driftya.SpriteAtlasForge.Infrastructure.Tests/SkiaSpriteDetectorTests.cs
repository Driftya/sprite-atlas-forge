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

        var result = await detector.DetectAsync(path, RawPixelOptions());

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
            NoiseReductionRadius = 0,
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

    [Test]
    public async Task Detection_cleanup_removes_specks_and_breaks_a_one_pixel_bridge()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("noisy.png");
        WritePng(path, 12, 10, bitmap =>
        {
            Fill(bitmap, 0, 2, 4, 5, SKColors.White);
            Fill(bitmap, 8, 2, 4, 5, SKColors.White);
            Fill(bitmap, 4, 4, 4, 1, SKColors.White);
            bitmap.SetPixel(5, 0, SKColors.White);
            bitmap.SetPixel(6, 9, SKColors.White);
        });
        var detector = new SkiaSpriteDetector();

        var result = await detector.DetectAsync(path, RawPixelOptions() with
        {
            NoiseReductionRadius = 1,
        });

        await Assert.That(result.Regions).IsEquivalentTo([
            new PixelRect(0, 2, 4, 5),
            new PixelRect(8, 2, 4, 5),
        ]);
    }

    [Test]
    public async Task Detection_groups_disconnected_opaque_content_inside_a_transparent_hole()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("transparent-hole.png");
        WritePng(path, 14, 12, bitmap =>
        {
            Fill(bitmap, 1, 1, 12, 10, SKColors.White);
            Fill(bitmap, 3, 3, 8, 6, SKColors.Transparent);
            Fill(bitmap, 6, 5, 2, 2, SKColors.White);
        });
        var detector = new SkiaSpriteDetector();

        var result = await detector.DetectAsync(path, RawPixelOptions());

        await Assert.That(result.Regions).IsEquivalentTo([new PixelRect(1, 1, 12, 10)]);
    }

    [Test]
    public async Task Generated_art_defaults_ignore_small_components_but_raw_mode_preserves_them()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("defaults.png");
        WritePng(path, 16, 12, bitmap =>
        {
            Fill(bitmap, 1, 2, 8, 8, SKColors.White);
            Fill(bitmap, 12, 3, 2, 2, SKColors.White);
        });
        var detector = new SkiaSpriteDetector();

        var cleaned = await detector.DetectAsync(path, new SpriteDetectionOptions());
        var raw = await detector.DetectAsync(path, RawPixelOptions());

        await Assert.That(cleaned.Regions).IsEquivalentTo([new PixelRect(1, 2, 8, 8)]);
        await Assert.That(raw.Regions).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Auto_background_mode_separates_sprites_on_an_opaque_gradient()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("opaque-gradient.png");
        WritePng(path, 48, 28, bitmap =>
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var shade = (byte)(45 + (x / 8) + (y / 7));
                    bitmap.SetPixel(x, y, new SKColor(shade, shade, shade, 255));
                }
            }

            Fill(bitmap, 4, 5, 14, 12, new SKColor(190, 110, 30));
            Fill(bitmap, 29, 7, 15, 14, new SKColor(30, 120, 190));
            Fill(bitmap, 8, 9, 5, 4, new SKColor(50, 50, 50));
        });
        var detector = new SkiaSpriteDetector();

        var result = await detector.DetectAsync(path, new SpriteDetectionOptions
        {
            MinimumArea = 20,
            MergeDistance = 0,
            NoiseReductionRadius = 0,
            BackgroundColorTolerance = 5,
        });

        await Assert.That(result.Regions).IsEquivalentTo([
            new PixelRect(4, 5, 14, 12),
            new PixelRect(29, 7, 15, 14),
        ]);
    }

    [Test]
    public async Task Auto_background_mode_ignores_multicolored_low_alpha_noise_between_sprites()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("low-alpha-noise.png");
        WritePng(path, 60, 32, bitmap =>
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    bitmap.SetPixel(x, y, new SKColor(
                        (byte)((x * 47 + y * 11) % 256),
                        (byte)((x * 13 + y * 61) % 256),
                        (byte)((x * 71 + y * 7) % 256),
                        (byte)(1 + ((x * 17 + y * 31) % 40))));
                }
            }

            Fill(bitmap, 4, 5, 20, 16, new SKColor(180, 100, 30, 255));
            Fill(bitmap, 35, 8, 20, 17, new SKColor(30, 110, 190, 255));
        });
        var detector = new SkiaSpriteDetector();

        var result = await detector.DetectAsync(path, new SpriteDetectionOptions
        {
            MinimumArea = 20,
            MergeDistance = 0,
            NoiseReductionRadius = 0,
        });

        await Assert.That(result.Regions).IsEquivalentTo([
            new PixelRect(4, 5, 20, 16),
            new PixelRect(35, 8, 20, 17),
        ]);
    }

    [Test]
    public async Task Alpha_only_mode_keeps_an_opaque_background_as_foreground_for_explicit_raw_control()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("opaque.png");
        WritePng(path, 20, 12, bitmap => bitmap.Erase(new SKColor(50, 50, 50, 255)));
        var detector = new SkiaSpriteDetector();

        var result = await detector.DetectAsync(path, RawPixelOptions() with
        {
            BackgroundMode = SpriteBackgroundMode.AlphaOnly,
        });

        await Assert.That(result.Regions).IsEquivalentTo([new PixelRect(0, 0, 20, 12)]);
    }

    private static void WritePng(string path, Action<SKBitmap> draw)
        => WritePng(path, 12, 10, draw);

    private static void WritePng(string path, int width, int height, Action<SKBitmap> draw)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(SKColors.Transparent);
        draw(bitmap);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private static SpriteDetectionOptions RawPixelOptions() => new()
    {
        MinimumArea = 1,
        MergeDistance = 0,
        NoiseReductionRadius = 0,
    };

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
