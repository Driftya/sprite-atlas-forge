using System.Security.Cryptography;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;
using SkiaSharp;

namespace Driftya.SpriteAtlasForge.Infrastructure;

public sealed class SkiaSpriteDetector : ISpriteDetector
{
    public async Task<DetectedSpriteSheet> DetectAsync(
        string imagePath,
        SpriteDetectionOptions options,
        IProgress<AtlasProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (!string.Equals(Path.GetExtension(imagePath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Sprite detection currently supports PNG images only.");
        }

        var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken).ConfigureAwait(false);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(imageBytes));
        using var bitmap = SkiaPngDecoder.Decode(imageBytes, "The PNG image could not be decoded.");

        if (bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            throw new InvalidDataException("The PNG image has invalid dimensions.");
        }

        var pixelCount = checked((long)bitmap.Width * bitmap.Height);
        if (bitmap.Width > options.MaximumWidth ||
            bitmap.Height > options.MaximumHeight ||
            pixelCount > options.MaximumPixels)
        {
            throw new InvalidDataException(
                $"The PNG image is {bitmap.Width}x{bitmap.Height} ({pixelCount:N0} pixels), which exceeds " +
                $"the configured detection limit of {options.MaximumWidth}x{options.MaximumHeight} and " +
                $"{options.MaximumPixels:N0} pixels.");
        }

        var size = new PixelSize(bitmap.Width, bitmap.Height);
        var effectiveAlphaThreshold = options.BackgroundMode == SpriteBackgroundMode.Auto
            ? Math.Max(options.AlphaThreshold, CalculateAutomaticAlphaThreshold(bitmap, size, cancellationToken))
            : options.AlphaThreshold;
        var useBorderRemoval = options.BackgroundMode == SpriteBackgroundMode.BorderConnected ||
            options.BackgroundMode == SpriteBackgroundMode.Auto &&
            !HasTransparentBorder(bitmap, size, (byte)effectiveAlphaThreshold);
        var mask = useBorderRemoval
            ? BuildForegroundMaskFromOpaqueBackground(
                bitmap,
                size,
                (byte)effectiveAlphaThreshold,
                options.BackgroundColorTolerance,
                cancellationToken)
            : BuildVisibleMask(bitmap, size, (byte)effectiveAlphaThreshold, cancellationToken);
        if (useBorderRemoval)
        {
            progress?.Report(new("background", 0.05, "Removing opaque background connected to the image border."));
        }
        else if (options.BackgroundMode == SpriteBackgroundMode.Auto &&
                 effectiveAlphaThreshold > options.AlphaThreshold)
        {
            progress?.Report(new(
                "background",
                0.05,
                $"Ignoring low-alpha background noise through alpha {effectiveAlphaThreshold}."));
        }

        if (options.NoiseReductionRadius > 0)
        {
            progress?.Report(new("cleanup", 0.1, "Removing isolated pixels and thin artifact bridges."));
            ApplyMorphologicalOpening(mask, size, options.NoiseReductionRadius, cancellationToken);
        }

        var regions = DetectComponents(mask, size, options, progress, cancellationToken);
        var merged = MergeNearby(regions, options.MergeDistance)
            .Select(region => region.Expand(options.SourcePadding, size))
            .Distinct()
            .OrderBy(region => region.Y)
            .ThenBy(region => region.X)
            .ThenBy(region => region.Width)
            .ThenBy(region => region.Height)
            .ToArray();

        return new DetectedSpriteSheet(size, sha256, merged);
    }

    private static byte[] BuildVisibleMask(
        SKBitmap bitmap,
        PixelSize size,
        byte alphaThreshold,
        CancellationToken cancellationToken)
    {
        var mask = new byte[checked(size.Width * size.Height)];
        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < size.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha > alphaThreshold)
                {
                    mask[checked(y * size.Width + x)] = 1;
                }
            }
        }

        return mask;
    }

    private static byte CalculateAutomaticAlphaThreshold(
        SKBitmap bitmap,
        PixelSize size,
        CancellationToken cancellationToken)
    {
        var histogram = new long[byte.MaxValue + 1];
        long weightedSum = 0;
        var pixelCount = checked((long)size.Width * size.Height);
        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < size.Width; x++)
            {
                var alpha = bitmap.GetPixel(x, y).Alpha;
                histogram[alpha]++;
                weightedSum += alpha;
            }
        }

        long backgroundWeight = 0;
        long backgroundSum = 0;
        double maximumVariance = -1;
        byte threshold = 0;

        for (var candidate = 0; candidate < byte.MaxValue; candidate++)
        {
            backgroundWeight += histogram[candidate];
            if (backgroundWeight == 0)
            {
                continue;
            }

            var foregroundWeight = pixelCount - backgroundWeight;
            if (foregroundWeight == 0)
            {
                break;
            }

            backgroundSum += checked(candidate * histogram[candidate]);
            var backgroundMean = (double)backgroundSum / backgroundWeight;
            var foregroundMean = (double)(weightedSum - backgroundSum) / foregroundWeight;
            var meanDifference = backgroundMean - foregroundMean;
            var variance = backgroundWeight * (double)foregroundWeight * meanDifference * meanDifference;
            if (variance > maximumVariance)
            {
                maximumVariance = variance;
                threshold = (byte)candidate;
            }
        }

        return threshold;
    }

    private static bool HasTransparentBorder(SKBitmap bitmap, PixelSize size, byte alphaThreshold)
    {
        var transparent = 0;
        var samples = checked((size.Width * 2) + Math.Max(0, size.Height - 2) * 2);
        for (var x = 0; x < size.Width; x++)
        {
            transparent += bitmap.GetPixel(x, 0).Alpha <= alphaThreshold ? 1 : 0;
            transparent += bitmap.GetPixel(x, size.Height - 1).Alpha <= alphaThreshold ? 1 : 0;
        }

        for (var y = 1; y < size.Height - 1; y++)
        {
            transparent += bitmap.GetPixel(0, y).Alpha <= alphaThreshold ? 1 : 0;
            transparent += bitmap.GetPixel(size.Width - 1, y).Alpha <= alphaThreshold ? 1 : 0;
        }

        return transparent >= Math.Max(1, samples / 10);
    }

    private static byte[] BuildForegroundMaskFromOpaqueBackground(
        SKBitmap bitmap,
        PixelSize size,
        byte alphaThreshold,
        int tolerance,
        CancellationToken cancellationToken)
    {
        var background = new byte[checked(size.Width * size.Height)];
        var queue = new Queue<int>();

        for (var x = 0; x < size.Width; x++)
        {
            EnqueueSeed(x, 0);
            EnqueueSeed(x, size.Height - 1);
        }

        for (var y = 1; y < size.Height - 1; y++)
        {
            EnqueueSeed(0, y);
            EnqueueSeed(size.Width - 1, y);
        }

        while (queue.TryDequeue(out var index))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var x = index % size.Width;
            var y = index / size.Width;
            var color = bitmap.GetPixel(x, y);
            TryEnqueue(x - 1, y, color);
            TryEnqueue(x + 1, y, color);
            TryEnqueue(x, y - 1, color);
            TryEnqueue(x, y + 1, color);
        }

        var foreground = new byte[background.Length];
        for (var index = 0; index < foreground.Length; index++)
        {
            if (background[index] == 0 &&
                bitmap.GetPixel(index % size.Width, index / size.Width).Alpha > alphaThreshold)
            {
                foreground[index] = 1;
            }
        }

        return foreground;

        void EnqueueSeed(int x, int y)
        {
            var index = checked(y * size.Width + x);
            if (background[index] == 0)
            {
                background[index] = 1;
                queue.Enqueue(index);
            }
        }

        void TryEnqueue(int x, int y, SKColor previousColor)
        {
            if (x < 0 || y < 0 || x >= size.Width || y >= size.Height)
            {
                return;
            }

            var index = checked(y * size.Width + x);
            if (background[index] != 0)
            {
                return;
            }

            var candidate = bitmap.GetPixel(x, y);
            if (candidate.Alpha <= alphaThreshold || IsWithinColorStep(previousColor, candidate, tolerance))
            {
                background[index] = 1;
                queue.Enqueue(index);
            }
        }
    }

    private static bool IsWithinColorStep(SKColor first, SKColor second, int tolerance) =>
        Math.Abs(first.Red - second.Red) <= tolerance &&
        Math.Abs(first.Green - second.Green) <= tolerance &&
        Math.Abs(first.Blue - second.Blue) <= tolerance;

    private static void ApplyMorphologicalOpening(
        byte[] mask,
        PixelSize size,
        int radius,
        CancellationToken cancellationToken)
    {
        var scratch = new byte[mask.Length];
        ApplySquareMorphology(mask, scratch, size, radius, erode: true, cancellationToken);
        ApplySquareMorphology(mask, scratch, size, radius, erode: false, cancellationToken);
    }

    private static void ApplySquareMorphology(
        byte[] mask,
        byte[] scratch,
        PixelSize size,
        int radius,
        bool erode,
        CancellationToken cancellationToken)
    {
        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowStart = checked(y * size.Width);
            var visibleCount = 0;
            for (var candidateX = 0; candidateX <= Math.Min(size.Width - 1, radius); candidateX++)
            {
                visibleCount += mask[rowStart + candidateX];
            }

            for (var x = 0; x < size.Width; x++)
            {
                var minimumX = Math.Max(0, x - radius);
                var maximumX = Math.Min(size.Width - 1, x + radius);
                scratch[rowStart + x] = ShouldSet(visibleCount, maximumX - minimumX + 1, erode);

                var leavingX = x - radius;
                if (leavingX >= 0)
                {
                    visibleCount -= mask[rowStart + leavingX];
                }

                var enteringX = x + radius + 1;
                if (enteringX < size.Width)
                {
                    visibleCount += mask[rowStart + enteringX];
                }
            }
        }

        for (var x = 0; x < size.Width; x++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var visibleCount = 0;
            for (var candidateY = 0; candidateY <= Math.Min(size.Height - 1, radius); candidateY++)
            {
                visibleCount += scratch[checked(candidateY * size.Width + x)];
            }

            for (var y = 0; y < size.Height; y++)
            {
                var minimumY = Math.Max(0, y - radius);
                var maximumY = Math.Min(size.Height - 1, y + radius);
                mask[checked(y * size.Width + x)] = ShouldSet(
                    visibleCount,
                    maximumY - minimumY + 1,
                    erode);

                var leavingY = y - radius;
                if (leavingY >= 0)
                {
                    visibleCount -= scratch[checked(leavingY * size.Width + x)];
                }

                var enteringY = y + radius + 1;
                if (enteringY < size.Height)
                {
                    visibleCount += scratch[checked(enteringY * size.Width + x)];
                }
            }
        }

        static byte ShouldSet(int visibleCount, int windowLength, bool erode) =>
            (byte)(erode ? visibleCount == windowLength ? 1 : 0 : visibleCount > 0 ? 1 : 0);
    }

    private static IReadOnlyList<PixelRect> DetectComponents(
        byte[] mask,
        PixelSize size,
        SpriteDetectionOptions options,
        IProgress<AtlasProgress>? progress,
        CancellationToken cancellationToken)
    {
        var queue = new Queue<int>();
        var regions = new List<PixelRect>();

        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (y % Math.Max(1, size.Height / 20) == 0)
            {
                progress?.Report(new("detect", (double)y / size.Height * 0.8, "Scanning visible pixels."));
            }

            for (var x = 0; x < size.Width; x++)
            {
                var startIndex = checked(y * size.Width + x);
                if (mask[startIndex] == 0)
                {
                    continue;
                }

                var minX = x;
                var minY = y;
                var maxX = x;
                var maxY = y;
                var visiblePixelCount = 0;
                mask[startIndex] = 0;
                queue.Enqueue(startIndex);

                while (queue.TryDequeue(out var index))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var currentX = index % size.Width;
                    var currentY = index / size.Width;
                    visiblePixelCount++;
                    minX = Math.Min(minX, currentX);
                    minY = Math.Min(minY, currentY);
                    maxX = Math.Max(maxX, currentX);
                    maxY = Math.Max(maxY, currentY);

                    TryEnqueue(currentX - 1, currentY);
                    TryEnqueue(currentX + 1, currentY);
                    TryEnqueue(currentX, currentY - 1);
                    TryEnqueue(currentX, currentY + 1);
                }

                if (visiblePixelCount >= options.MinimumArea)
                {
                    regions.Add(new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1));
                }

                void TryEnqueue(int candidateX, int candidateY)
                {
                    if (candidateX < 0 || candidateY < 0 ||
                        candidateX >= size.Width || candidateY >= size.Height)
                    {
                        return;
                    }

                    var candidateIndex = checked(candidateY * size.Width + candidateX);
                    if (mask[candidateIndex] == 0)
                    {
                        return;
                    }

                    mask[candidateIndex] = 0;
                    queue.Enqueue(candidateIndex);
                }
            }
        }

        return regions;
    }

    private static IReadOnlyList<PixelRect> MergeNearby(IEnumerable<PixelRect> source, int mergeDistance)
    {
        var regions = source.OrderBy(region => region.Y).ThenBy(region => region.X).ToList();
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var first = 0; first < regions.Count && !changed; first++)
            {
                for (var second = first + 1; second < regions.Count; second++)
                {
                    if (!AreWithinDistance(regions[first], regions[second], mergeDistance))
                    {
                        continue;
                    }

                    regions[first] = regions[first].Union(regions[second]);
                    regions.RemoveAt(second);
                    changed = true;
                    break;
                }
            }
        }

        return regions;
    }

    private static bool AreWithinDistance(PixelRect first, PixelRect second, int distance)
    {
        var horizontalGap = Math.Max(0, Math.Max(first.X, second.X) - Math.Min(first.Right, second.Right));
        var verticalGap = Math.Max(0, Math.Max(first.Y, second.Y) - Math.Min(first.Bottom, second.Bottom));
        return horizontalGap <= distance && verticalGap <= distance;
    }
}
