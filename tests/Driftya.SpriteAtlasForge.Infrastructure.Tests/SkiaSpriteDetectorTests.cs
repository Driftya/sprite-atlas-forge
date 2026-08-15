using System;
using System.Collections.Generic;
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
    public async Task Detection_keeps_a_small_detail_ambiguous_between_two_sprites_separate()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("engine-detail.png");
        WritePng(path, 24, 12, bitmap =>
        {
            Fill(bitmap, 2, 2, 6, 7, SKColors.White);
            Fill(bitmap, 10, 4, 1, 3, SKColors.DeepSkyBlue);
            Fill(bitmap, 13, 2, 6, 7, SKColors.White);
        });
        var detector = new SkiaSpriteDetector();

        var result = await detector.DetectAsync(path, new SpriteDetectionOptions
        {
            MinimumArea = 20,
            MergeDistance = 2,
            NoiseReductionRadius = 0,
            BackgroundMode = SpriteBackgroundMode.AlphaOnly,
        });

        await Assert.That(result.Regions).IsEquivalentTo([
            new PixelRect(2, 2, 6, 7),
            new PixelRect(13, 2, 6, 7),
        ]);
    }

    [Test]
    public async Task Detection_attaches_a_small_engine_detail_to_its_only_nearby_sprite()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("attached-engine-detail.png");
        WritePng(path, 16, 12, bitmap =>
        {
            Fill(bitmap, 2, 2, 6, 7, SKColors.White);
            Fill(bitmap, 10, 4, 1, 3, SKColors.DeepSkyBlue);
        });
        var detector = new SkiaSpriteDetector();

        var result = await detector.DetectAsync(path, new SpriteDetectionOptions
        {
            MinimumArea = 20,
            MergeDistance = 2,
            NoiseReductionRadius = 0,
            BackgroundMode = SpriteBackgroundMode.AlphaOnly,
        });

        await Assert.That(result.Regions).IsEquivalentTo([new PixelRect(2, 2, 9, 7)]);
    }

    [Test]
    public async Task Auto_detection_attaches_a_small_detail_across_a_soft_gap()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("auto-attached-engine-detail.png");
        WritePng(path, 20, 12, bitmap =>
        {
            Fill(bitmap, 2, 2, 6, 7, new SKColor(255, 255, 255, 255));
            Fill(bitmap, 12, 4, 1, 3, new SKColor(40, 150, 255, 255));
        });
        var detector = new SkiaSpriteDetector();

        var result = await detector.DetectAsync(path, new SpriteDetectionOptions
        {
            MinimumArea = 20,
            MergeDistance = 2,
            NoiseReductionRadius = 0,
        });

        await Assert.That(result.Regions).IsEquivalentTo([new PixelRect(2, 2, 11, 7)]);
    }

    [Test]
    public async Task Auto_detached_detail_recovery_uses_the_unfiltered_silhouette_when_enabled()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("detached-detail-third-pass.png");
        WritePng(path, 30, 14, bitmap =>
        {
            Fill(bitmap, 2, 3, 6, 7, SKColors.White);
            Fill(bitmap, 20, 5, 1, 3, SKColors.DeepSkyBlue);
        });
        var detector = new SkiaSpriteDetector();
        var options = new SpriteDetectionOptions
        {
            MinimumArea = 20,
            MergeDistance = 2,
            NoiseReductionRadius = 1,
        };

        var normal = await detector.DetectAsync(path, options);
        var recovered = await detector.DetectAsync(path, options with { RecoverDetachedDetails = true });

        await Assert.That(normal.Regions).IsEquivalentTo([new PixelRect(2, 3, 6, 7)]);
        await Assert.That(recovered.Regions).IsEquivalentTo([new PixelRect(2, 3, 19, 7)]);
    }

    [Test]
    public async Task Auto_detached_detail_recovery_does_not_attach_an_ambiguous_fragment()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("ambiguous-detached-detail.png");
        WritePng(path, 40, 14, bitmap =>
        {
            Fill(bitmap, 2, 3, 6, 7, SKColors.White);
            Fill(bitmap, 18, 5, 1, 3, SKColors.DeepSkyBlue);
            Fill(bitmap, 29, 3, 6, 7, SKColors.White);
        });
        var detector = new SkiaSpriteDetector();

        var result = await detector.DetectAsync(path, new SpriteDetectionOptions
        {
            MinimumArea = 20,
            MergeDistance = 2,
            NoiseReductionRadius = 1,
            RecoverDetachedDetails = true,
        });

        await Assert.That(result.Regions).IsEquivalentTo([
            new PixelRect(2, 3, 6, 7),
            new PixelRect(29, 3, 6, 7),
        ]);
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
    public async Task Detection_does_not_merge_separated_pixels_only_because_their_bounds_overlap()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("overlapping-bounds.png");
        WritePng(path, 12, 12, bitmap =>
        {
            Fill(bitmap, 1, 1, 1, 8, SKColors.White);
            Fill(bitmap, 1, 1, 7, 1, SKColors.White);
            Fill(bitmap, 8, 4, 1, 7, SKColors.White);
            Fill(bitmap, 4, 10, 5, 1, SKColors.White);
        });
        var detector = new SkiaSpriteDetector();

        var result = await detector.DetectAsync(path, RawPixelOptions());

        await Assert.That(result.Regions).IsEquivalentTo([
            new PixelRect(1, 1, 7, 8),
            new PixelRect(4, 4, 5, 7),
        ]);
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
    public async Task Auto_background_mode_ignores_continuous_alpha_one_to_three_noise()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("alpha-one-to-three-noise.png");
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
                        (byte)(1 + ((x + y) % 3))));
                }
            }

            Fill(bitmap, 4, 5, 20, 16, new SKColor(180, 100, 30, 255));
            Fill(bitmap, 35, 8, 20, 17, new SKColor(30, 110, 190, 255));
        });
        var detector = new SkiaSpriteDetector();

        var options = new SpriteDetectionOptions
        {
            MinimumArea = 20,
            MergeDistance = SpriteDetectionOptions.DefaultMergeDistance,
            NoiseReductionRadius = 0,
        };

        var result = await detector.DetectAsync(path, options);
        var overMerged = await detector.DetectAsync(path, options with { MergeDistance = 44 });

        await Assert.That(result.Regions).IsEquivalentTo([
            new PixelRect(4, 5, 20, 16),
            new PixelRect(35, 8, 20, 17),
        ]);
        await Assert.That(overMerged.Regions).IsEquivalentTo([new PixelRect(4, 5, 51, 20)]);
    }

    [Test]
    public async Task Auto_background_mode_removes_semitransparent_shadow_bridges_below_the_foreground_mode()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("shadow-bridge.png");
        WritePng(path, 60, 32, bitmap =>
        {
            bitmap.Erase(new SKColor(70, 70, 75, 1));
            Fill(bitmap, 4, 5, 20, 16, new SKColor(180, 100, 30, 251));
            Fill(bitmap, 35, 8, 20, 17, new SKColor(30, 110, 190, 251));
            Fill(bitmap, 24, 12, 11, 3, new SKColor(50, 70, 90, 200));
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
    public async Task Auto_seeded_wand_recovers_soft_edges_without_merging_connected_markers()
    {
        using var directory = new TestDirectory();
        var path = directory.GetPath("connected-soft-edges.png");
        WritePng(path, 52, 30, bitmap =>
        {
            bitmap.Erase(new SKColor(70, 70, 75, 1));
            Fill(bitmap, 4, 6, 16, 14, new SKColor(120, 80, 45, 230));
            Fill(bitmap, 6, 8, 12, 10, new SKColor(180, 100, 30, 251));
            Fill(bitmap, 30, 7, 16, 14, new SKColor(45, 85, 120, 230));
            Fill(bitmap, 32, 9, 12, 10, new SKColor(30, 110, 190, 251));
            Fill(bitmap, 20, 12, 10, 3, new SKColor(70, 75, 80, 230));
        });
        var detector = new SkiaSpriteDetector();

        var result = await detector.DetectAsync(path, new SpriteDetectionOptions
        {
            MinimumArea = 20,
            MergeDistance = 0,
            NoiseReductionRadius = 0,
        });

        await Assert.That(result.Regions).IsEquivalentTo([
            new PixelRect(4, 6, 21, 14),
            new PixelRect(25, 7, 21, 14),
        ]);
    }

    [Test]
    public async Task Auto_background_mode_detects_the_generated_ship_modules_fixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ship-modules-02.png");
        var detector = new SkiaSpriteDetector();
        var progress = new List<AtlasProgress>();

        var result = await detector.DetectAsync(
            path,
            new SpriteDetectionOptions(),
            new InlineProgress<AtlasProgress>(progress.Add));

        await Assert.That(result.ImageSize).IsEqualTo(new PixelSize(1536, 1024));
        await Assert.That(progress[0].Message).IsEqualTo("Ignoring low-alpha background noise through alpha 248.");
        await Assert.That(result.Regions).Count().IsEqualTo(175);
        await Assert.That(result.Regions).Contains(new PixelRect(246, 494, 184, 106));
        await Assert.That(result.Regions).Contains(new PixelRect(1087, 521, 120, 167));
        await Assert.That(result.Regions).Contains(new PixelRect(1197, 604, 51, 84));
        await Assert.That(result.Regions).Contains(new PixelRect(1056, 652, 44, 35));
        await Assert.That(result.Regions).Contains(new PixelRect(1461, 762, 58, 78));
        await Assert.That(result.Regions).Contains(new PixelRect(1465, 793, 43, 89));
    }

    [Test]
    public async Task Auto_background_mode_preserves_the_semitransparent_ship_module_fixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ship-modules-01.png");
        var detector = new SkiaSpriteDetector();
        var progress = new List<AtlasProgress>();

        var result = await detector.DetectAsync(
            path,
            new SpriteDetectionOptions(),
            new InlineProgress<AtlasProgress>(progress.Add));

        await Assert.That(result.ImageSize).IsEqualTo(new PixelSize(1254, 1254));
        await Assert.That(progress[0].Message).IsEqualTo("Ignoring low-alpha background noise through alpha 243.");
        await Assert.That(result.Regions).Count().IsEqualTo(109);
        await Assert.That(result.Regions).Contains(new PixelRect(792, 16, 56, 70));
        await Assert.That(result.Regions).Contains(new PixelRect(22, 101, 213, 101));
        await Assert.That(result.Regions).Contains(new PixelRect(20, 1057, 162, 88));
        await Assert.That(result.Regions).Contains(new PixelRect(170, 1057, 120, 88));
        await Assert.That(result.Regions).Contains(new PixelRect(1156, 1067, 62, 89));
        await Assert.That(result.Regions).Contains(new PixelRect(1206, 1082, 13, 55));
    }

    [Test]
    public async Task Auto_detached_detail_recovery_restores_the_real_ship_module_ornaments()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ship-modules-01.png");
        var detector = new SkiaSpriteDetector();

        var result = await detector.DetectAsync(
            path,
            new SpriteDetectionOptions { RecoverDetachedDetails = true });

        await Assert.That(result.Regions).Count().IsEqualTo(109);
        await Assert.That(result.Regions).Contains(new PixelRect(273, 356, 222, 123));
        await Assert.That(result.Regions).DoesNotContain(new PixelRect(280, 369, 211, 98));
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

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
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
