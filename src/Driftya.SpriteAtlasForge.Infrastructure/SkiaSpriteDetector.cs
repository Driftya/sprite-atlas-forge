using System.Security.Cryptography;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;
using SkiaSharp;

namespace Driftya.SpriteAtlasForge.Infrastructure;

public sealed class SkiaSpriteDetector : ISpriteDetector
{
    private const int AutomaticWandRadius = 8;
    private const int AutomaticGlowRadius = 32;
    private const double AutomaticGutterMaximumOccupancy = 0.4;
    private const double AutomaticGutterMinimumMarkerShare = 0.15;

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
        var automaticAlpha = options.BackgroundMode == SpriteBackgroundMode.Auto
            ? CalculateAutomaticAlphaThreshold(bitmap, size, options, cancellationToken)
            : new AutomaticAlphaAnalysis(
                options.AlphaThreshold,
                options.AlphaThreshold,
                RequiresSeededRefinement: false);
        var effectiveAlphaThreshold = Math.Max(options.AlphaThreshold, automaticAlpha.Threshold);
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
        var detachedDetailMask = options.RecoverDetachedDetails
            ? useBorderRemoval
                ? (byte[])mask.Clone()
                : BuildVisibleMask(bitmap, size, options.AlphaThreshold, cancellationToken)
            : null;
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

        IReadOnlyList<PixelRect> regions;
        if (options.BackgroundMode == SpriteBackgroundMode.Auto &&
            !useBorderRemoval &&
            automaticAlpha.RequiresSeededRefinement &&
            effectiveAlphaThreshold == automaticAlpha.Threshold)
        {
            var supportAlphaThreshold = (byte)Math.Max(
                options.AlphaThreshold,
                effectiveAlphaThreshold - 8);
            var supportMask = BuildAutomaticSupportMask(
                bitmap,
                size,
                supportAlphaThreshold,
                (byte)options.AlphaThreshold,
                cancellationToken);
            var markerMask = BuildVisibleMask(bitmap, size, automaticAlpha.MarkerThreshold, cancellationToken);
            if (options.NoiseReductionRadius > 0)
            {
                ApplyMorphologicalOpening(markerMask, size, options.NoiseReductionRadius, cancellationToken);
            }

            progress?.Report(new(
                "refine",
                0.15,
                $"Growing alpha {automaticAlpha.MarkerThreshold} sprite markers into connected detail through alpha {supportAlphaThreshold}."));
            regions = DetectSeededComponents(
                mask,
                markerMask,
                supportMask,
                size,
                options,
                progress,
                cancellationToken);
        }
        else
        {
            regions = DetectGroupedComponents(
                mask,
                size,
                options,
                progress,
                cancellationToken,
                attachmentDistance: options.BackgroundMode == SpriteBackgroundMode.Auto ? 8 : null);
        }

        if (detachedDetailMask is not null && options.BackgroundMode == SpriteBackgroundMode.Auto)
        {
            progress?.Report(new("recover", 0.85, "Recovering unambiguous detached sprite details."));
            regions = RecoverDetachedDetails(
                detachedDetailMask,
                size,
                regions,
                options.MinimumArea,
                SpriteDetectionOptions.DetachedDetailRecoveryDistance,
                cancellationToken);
        }

        var merged = regions
            .Select(region => region.Expand(options.SourcePadding, size))
            .Distinct()
            .OrderBy(region => region.Y)
            .ThenBy(region => region.X)
            .ThenBy(region => region.Width)
            .ThenBy(region => region.Height)
            .ToArray();

        return new DetectedSpriteSheet(size, sha256, merged);
    }

    private static IReadOnlyList<PixelRect> RecoverDetachedDetails(
        byte[] sourceMask,
        PixelSize size,
        IReadOnlyList<PixelRect> trustedRegions,
        int minimumArea,
        int attachmentDistance,
        CancellationToken cancellationToken)
    {
        if (trustedRegions.Count == 0)
        {
            return trustedRegions;
        }

        var protectedPixels = new bool[sourceMask.Length];
        foreach (var region in trustedRegions)
        {
            for (var y = region.Y; y < region.Bottom; y++)
            {
                Array.Fill(
                    protectedPixels,
                    true,
                    checked(y * size.Width + region.X),
                    region.Width);
            }
        }

        var owners = Enumerable.Repeat(-2, sourceMask.Length).ToArray();
        var bestDistanceSquared = Enumerable.Repeat(int.MaxValue, sourceMask.Length).ToArray();
        var maximumDistanceSquared = checked(attachmentDistance * attachmentDistance);
        for (var owner = 0; owner < trustedRegions.Count; owner++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var region = trustedRegions[owner];
            var searchBounds = region.Expand(attachmentDistance, size);
            for (var y = searchBounds.Y; y < searchBounds.Bottom; y++)
            {
                for (var x = searchBounds.X; x < searchBounds.Right; x++)
                {
                    var index = checked(y * size.Width + x);
                    if (sourceMask[index] == 0 || protectedPixels[index])
                    {
                        continue;
                    }

                    var deltaX = x < region.X
                        ? region.X - x
                        : x >= region.Right
                            ? x - region.Right + 1
                            : 0;
                    var deltaY = y < region.Y
                        ? region.Y - y
                        : y >= region.Bottom
                            ? y - region.Bottom + 1
                            : 0;
                    var distanceSquared = checked(deltaX * deltaX + deltaY * deltaY);
                    if (distanceSquared > maximumDistanceSquared)
                    {
                        continue;
                    }

                    if (distanceSquared < bestDistanceSquared[index])
                    {
                        bestDistanceSquared[index] = distanceSquared;
                        owners[index] = owner;
                    }
                    else if (distanceSquared == bestDistanceSquared[index] && owners[index] != owner)
                    {
                        owners[index] = -1;
                    }
                }
            }
        }

        var recovered = trustedRegions.ToArray();
        var queue = new Queue<int>();
        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < size.Width; x++)
            {
                var start = checked(y * size.Width + x);
                var owner = owners[start];
                if (owner < 0)
                {
                    continue;
                }

                var component = new ComponentAccumulator();
                owners[start] = -2;
                queue.Enqueue(start);
                while (queue.TryDequeue(out var index))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var currentX = index % size.Width;
                    var currentY = index / size.Width;
                    component.Include(currentX, currentY);
                    Enqueue(currentX - 1, currentY);
                    Enqueue(currentX + 1, currentY);
                    Enqueue(currentX, currentY - 1);
                    Enqueue(currentX, currentY + 1);
                }

                var maximumDetailPixels = Math.Max(
                    minimumArea,
                    trustedRegions[owner].Area / 3);
                if (component.PixelCount >= 2 && component.PixelCount <= maximumDetailPixels)
                {
                    recovered[owner] = recovered[owner].Union(component.Bounds);
                }

                void Enqueue(int candidateX, int candidateY)
                {
                    if (candidateX < 0 || candidateY < 0 || candidateX >= size.Width || candidateY >= size.Height)
                    {
                        return;
                    }

                    var candidate = checked(candidateY * size.Width + candidateX);
                    if (owners[candidate] != owner)
                    {
                        return;
                    }

                    owners[candidate] = -2;
                    queue.Enqueue(candidate);
                }
            }
        }

        return recovered;
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

    private static byte[] BuildAutomaticSupportMask(
        SKBitmap bitmap,
        PixelSize size,
        byte confidentAlphaThreshold,
        byte glowAlphaThreshold,
        CancellationToken cancellationToken)
    {
        var mask = new byte[checked(size.Width * size.Height)];
        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < size.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                var isChromaticGlow = color.Alpha > glowAlphaThreshold &&
                    color.Blue >= 96 &&
                    color.Blue >= color.Green &&
                    color.Blue - color.Red >= 48;
                if (color.Alpha > confidentAlphaThreshold)
                {
                    mask[checked(y * size.Width + x)] = 1;
                }
                else if (isChromaticGlow)
                {
                    mask[checked(y * size.Width + x)] = 2;
                }
            }
        }

        return mask;
    }

    private static AutomaticAlphaAnalysis CalculateAutomaticAlphaThreshold(
        SKBitmap bitmap,
        PixelSize size,
        SpriteDetectionOptions options,
        CancellationToken cancellationToken)
    {
        var histogram = new long[byte.MaxValue + 1];
        var alphaValues = new byte[checked(size.Width * size.Height)];
        long weightedSum = 0;
        var pixelCount = checked((long)size.Width * size.Height);
        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < size.Width; x++)
            {
                var alpha = bitmap.GetPixel(x, y).Alpha;
                alphaValues[checked(y * size.Width + x)] = alpha;
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

        var dominantForegroundAlpha = 0;
        long dominantForegroundCount = 0;
        for (var alpha = threshold + 1; alpha <= byte.MaxValue; alpha++)
        {
            if (histogram[alpha] <= dominantForegroundCount)
            {
                continue;
            }

            dominantForegroundAlpha = alpha;
            dominantForegroundCount = histogram[alpha];
        }

        var largestBoundsArea = CalculateLargestComponentBoundsArea(
            alphaValues,
            size,
            threshold,
            options,
            cancellationToken);
        if (largestBoundsArea * 4 < pixelCount)
        {
            return new(threshold, threshold, RequiresSeededRefinement: false);
        }

        // Otsu is intentionally the least destructive cutoff. Only refine it when that mask contains an
        // anomalously sheet-scale component. Generated shadow/watermark mattes break apart at a small
        // number of alpha levels below the dominant foreground mode; the last major collapse preserves
        // more semi-transparent sprite detail than always jumping directly to the mode.
        var selectedThreshold = threshold;
        var previousCandidateThreshold = threshold;
        var previousLargestBoundsArea = largestBoundsArea;
        ReadOnlySpan<int> offsetsFromForegroundMode = [16, 8, 4, 3, 2, 1];
        foreach (var offset in offsetsFromForegroundMode)
        {
            var candidateThreshold = (byte)Math.Max(threshold, dominantForegroundAlpha - offset);
            if (candidateThreshold <= previousCandidateThreshold)
            {
                continue;
            }

            previousCandidateThreshold = candidateThreshold;

            var candidateLargestBoundsArea = CalculateLargestComponentBoundsArea(
                alphaValues,
                size,
                candidateThreshold,
                options,
                cancellationToken);
            if (candidateLargestBoundsArea == 0)
            {
                break;
            }

            if (candidateLargestBoundsArea * 2 <= previousLargestBoundsArea)
            {
                selectedThreshold = candidateThreshold;
            }

            previousLargestBoundsArea = candidateLargestBoundsArea;
        }

        var markerThreshold = (byte)Math.Max(selectedThreshold, dominantForegroundAlpha - 1);
        return new(selectedThreshold, markerThreshold, selectedThreshold != threshold);
    }

    private static long CalculateLargestComponentBoundsArea(
        byte[] alphaValues,
        PixelSize size,
        byte alphaThreshold,
        SpriteDetectionOptions options,
        CancellationToken cancellationToken)
    {
        var mask = new byte[alphaValues.Length];
        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < size.Width; x++)
            {
                var index = checked(y * size.Width + x);
                mask[index] = alphaValues[index] > alphaThreshold ? (byte)1 : (byte)0;
            }
        }

        if (options.NoiseReductionRadius > 0)
        {
            ApplyMorphologicalOpening(mask, size, options.NoiseReductionRadius, cancellationToken);
        }

        long largestBoundsArea = 0;
        var regions = DetectComponents(mask, size, options, progress: null, cancellationToken);
        foreach (var region in MergeNearby(regions, options.MergeDistance))
        {
            largestBoundsArea = Math.Max(largestBoundsArea, checked((long)region.Width * region.Height));
        }

        return largestBoundsArea;
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

    private static IReadOnlyList<PixelRect> DetectGroupedComponents(
        byte[] mask,
        PixelSize size,
        SpriteDetectionOptions options,
        IProgress<AtlasProgress>? progress,
        CancellationToken cancellationToken,
        int? attachmentDistance = null)
    {
        if (options.MergeDistance > 8)
        {
            return MergeNearby(
                DetectComponents((byte[])mask.Clone(), size, options, progress, cancellationToken),
                options.MergeDistance);
        }

        var workingMask = (byte[])mask.Clone();
        var labels = new int[mask.Length];
        var queue = new Queue<int>();
        var components = new List<ComponentAccumulator> { new() };

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
                if (workingMask[startIndex] == 0)
                {
                    continue;
                }

                var label = components.Count;
                var component = new ComponentAccumulator();
                components.Add(component);
                workingMask[startIndex] = 0;
                labels[startIndex] = label;
                queue.Enqueue(startIndex);

                while (queue.TryDequeue(out var index))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var currentX = index % size.Width;
                    var currentY = index / size.Width;
                    component.Include(currentX, currentY);

                    TryEnqueue(currentX - 1, currentY);
                    TryEnqueue(currentX + 1, currentY);
                    TryEnqueue(currentX, currentY - 1);
                    TryEnqueue(currentX, currentY + 1);
                }

                void TryEnqueue(int candidateX, int candidateY)
                {
                    if (candidateX < 0 || candidateY < 0 ||
                        candidateX >= size.Width || candidateY >= size.Height)
                    {
                        return;
                    }

                    var candidateIndex = checked(candidateY * size.Width + candidateX);
                    if (workingMask[candidateIndex] == 0)
                    {
                        return;
                    }

                    workingMask[candidateIndex] = 0;
                    labels[candidateIndex] = label;
                    queue.Enqueue(candidateIndex);
                }
            }
        }

        var parents = Enumerable.Range(0, components.Count).ToArray();
        var qualifying = components
            .Select(component => component.PixelCount >= options.MinimumArea)
            .ToArray();
        var mergeSearchRadius = options.MergeDistance + 1;
        var attachmentSearchRadius = attachmentDistance ?? mergeSearchRadius;
        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < size.Width; x++)
            {
                var index = checked(y * size.Width + x);
                var label = labels[index];
                if (label <= 0 || !qualifying[label])
                {
                    continue;
                }

                for (var deltaY = 0; deltaY <= mergeSearchRadius; deltaY++)
                {
                    var minimumDeltaX = deltaY == 0 ? 1 : -mergeSearchRadius;
                    for (var deltaX = minimumDeltaX; deltaX <= mergeSearchRadius; deltaX++)
                    {
                        var candidateX = x + deltaX;
                        var candidateY = y + deltaY;
                        if (candidateX < 0 || candidateY < 0 ||
                            candidateX >= size.Width || candidateY >= size.Height)
                        {
                            continue;
                        }

                        var candidateLabel = labels[checked(candidateY * size.Width + candidateX)];
                        if (candidateLabel <= 0 || candidateLabel == label || !qualifying[candidateLabel])
                        {
                            continue;
                        }

                        Union(label, candidateLabel);
                    }
                }
            }
        }

        var attachmentRoots = Enumerable.Range(0, components.Count)
            .Select(_ => new HashSet<int>())
            .ToArray();
        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < size.Width; x++)
            {
                var label = labels[checked(y * size.Width + x)];
                if (label <= 0 || qualifying[label])
                {
                    continue;
                }

                for (var deltaY = -attachmentSearchRadius; deltaY <= attachmentSearchRadius; deltaY++)
                {
                    for (var deltaX = -attachmentSearchRadius; deltaX <= attachmentSearchRadius; deltaX++)
                    {
                        var candidateX = x + deltaX;
                        var candidateY = y + deltaY;
                        if (candidateX < 0 || candidateY < 0 ||
                            candidateX >= size.Width || candidateY >= size.Height)
                        {
                            continue;
                        }

                        var candidateLabel = labels[checked(candidateY * size.Width + candidateX)];
                        if (candidateLabel > 0 && qualifying[candidateLabel])
                        {
                            attachmentRoots[label].Add(Find(candidateLabel));
                        }
                    }
                }
            }
        }

        for (var label = 1; label < components.Count; label++)
        {
            if (!qualifying[label] && attachmentRoots[label].Count == 1)
            {
                Union(label, attachmentRoots[label].Single());
            }
        }

        var grouped = new Dictionary<int, ComponentAccumulator>();
        for (var label = 1; label < components.Count; label++)
        {
            if (!qualifying[label] && attachmentRoots[label].Count != 1)
            {
                continue;
            }

            var root = Find(label);
            if (!grouped.TryGetValue(root, out var aggregate))
            {
                aggregate = new ComponentAccumulator();
                grouped.Add(root, aggregate);
            }

            aggregate.Include(components[label]);
        }

        var regions = grouped.Values.Select(component => component.Bounds).ToList();
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var first = 0; first < regions.Count && !changed; first++)
            {
                for (var second = first + 1; second < regions.Count; second++)
                {
                    if (!Contains(regions[first], regions[second]) &&
                        !Contains(regions[second], regions[first]))
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

        int Find(int label)
        {
            while (parents[label] != label)
            {
                parents[label] = parents[parents[label]];
                label = parents[label];
            }

            return label;
        }

        void Union(int first, int second)
        {
            var firstRoot = Find(first);
            var secondRoot = Find(second);
            if (firstRoot != secondRoot)
            {
                parents[secondRoot] = firstRoot;
            }
        }
    }

    private static IReadOnlyList<PixelRect> DetectSeededComponents(
        byte[] seedMask,
        byte[] markerMask,
        byte[] supportMask,
        PixelSize size,
        SpriteDetectionOptions options,
        IProgress<AtlasProgress>? progress,
        CancellationToken cancellationToken)
    {
        var coarseMask = (byte[])seedMask.Clone();
        var labels = new int[seedMask.Length];
        var queue = new Queue<int>();
        var seeds = new List<LabeledRegion>();
        var qualifyingSeedCount = 0;
        var nextLabel = 0;

        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (y % Math.Max(1, size.Height / 20) == 0)
            {
                progress?.Report(new("detect", (double)y / size.Height * 0.7, "Finding confident sprite markers."));
            }

            for (var x = 0; x < size.Width; x++)
            {
                var startIndex = checked(y * size.Width + x);
                if (seedMask[startIndex] == 0)
                {
                    continue;
                }

                var label = ++nextLabel;
                var minX = x;
                var minY = y;
                var maxX = x;
                var maxY = y;
                var visiblePixelCount = 0;
                seedMask[startIndex] = 0;
                labels[startIndex] = label;
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

                seeds.Add(new(
                    label,
                    new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1)));
                if (visiblePixelCount >= options.MinimumArea)
                {
                    qualifyingSeedCount++;
                }

                void TryEnqueue(int candidateX, int candidateY)
                {
                    if (candidateX < 0 || candidateY < 0 ||
                        candidateX >= size.Width || candidateY >= size.Height)
                    {
                        return;
                    }

                    var candidateIndex = checked(candidateY * size.Width + candidateX);
                    if (seedMask[candidateIndex] == 0)
                    {
                        return;
                    }

                    seedMask[candidateIndex] = 0;
                    labels[candidateIndex] = label;
                    queue.Enqueue(candidateIndex);
                }
            }
        }

        if (qualifyingSeedCount == 0)
        {
            return [];
        }

        var groupedBounds = DetectGroupedComponents(
            coarseMask,
            size,
            options,
            progress: null,
            cancellationToken);
        var baseLabelByRawLabel = new int[nextLabel + 1];
        foreach (var seed in seeds)
        {
            for (var groupIndex = 0; groupIndex < groupedBounds.Count; groupIndex++)
            {
                if (!Contains(groupedBounds[groupIndex], seed.Bounds))
                {
                    continue;
                }

                baseLabelByRawLabel[seed.Label] = groupIndex + 1;
                break;
            }
        }

        var markerRegions = DetectGroupedComponents(
            markerMask,
            size,
            options,
            progress: null,
            cancellationToken);
        var markersByBase = Enumerable.Range(0, groupedBounds.Count)
            .Select(_ => new List<MarkerRegion>())
            .ToArray();
        foreach (var marker in markerRegions)
        {
            for (var groupIndex = 0; groupIndex < groupedBounds.Count; groupIndex++)
            {
                if (!Contains(groupedBounds[groupIndex], marker))
                {
                    continue;
                }

                markersByBase[groupIndex].Add(new(
                    marker,
                    CountVisiblePixels(markerMask, size, marker)));
                break;
            }
        }

        var partitionsByBase = new IReadOnlyList<PixelRect>[groupedBounds.Count];
        var finalLabelCount = 0;
        var firstLabelByBase = new int[groupedBounds.Count];
        for (var groupIndex = 0; groupIndex < groupedBounds.Count; groupIndex++)
        {
            var partitions = SplitAtValidatedVerticalGutters(
                coarseMask,
                size,
                groupedBounds[groupIndex],
                markersByBase[groupIndex],
                options.MinimumArea,
                cancellationToken);
            partitionsByBase[groupIndex] = partitions;
            firstLabelByBase[groupIndex] = finalLabelCount + 1;
            finalLabelCount += partitions.Count;
        }

        for (var index = 0; index < labels.Length; index++)
        {
            var rawLabel = labels[index];
            if (rawLabel <= 0)
            {
                continue;
            }

            var baseLabel = baseLabelByRawLabel[rawLabel];
            if (baseLabel <= 0)
            {
                labels[index] = 0;
                continue;
            }

            var baseIndex = baseLabel - 1;
            var x = index % size.Width;
            var partitions = partitionsByBase[baseIndex];
            for (var partitionIndex = 0; partitionIndex < partitions.Count; partitionIndex++)
            {
                var partition = partitions[partitionIndex];
                if (x >= partition.X && x < partition.Right)
                {
                    labels[index] = firstLabelByBase[baseIndex] + partitionIndex;
                    break;
                }
            }
        }

        var distances = new byte[labels.Length];
        queue.Clear();
        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < size.Width; x++)
            {
                var index = checked(y * size.Width + x);
                if (labels[index] > 0 && HasUnclaimedSupportNeighbor(labels, supportMask, size, x, y))
                {
                    queue.Enqueue(index);
                }
            }
        }

        while (queue.TryDequeue(out var index))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var distance = distances[index];
            if (distance >= AutomaticGlowRadius)
            {
                continue;
            }

            var x = index % size.Width;
            var y = index / size.Width;
            TryGrow(x - 1, y);
            TryGrow(x + 1, y);
            TryGrow(x, y - 1);
            TryGrow(x, y + 1);

            void TryGrow(int candidateX, int candidateY)
            {
                if (candidateX < 0 || candidateY < 0 ||
                    candidateX >= size.Width || candidateY >= size.Height)
                {
                    return;
                }

                var candidateIndex = checked(candidateY * size.Width + candidateX);
                var supportKind = supportMask[candidateIndex];
                var nextDistance = distance + 1;
                if (supportKind == 0 ||
                    supportKind == 1 && nextDistance > AutomaticWandRadius ||
                    labels[candidateIndex] != 0)
                {
                    return;
                }

                labels[candidateIndex] = labels[index];
                distances[candidateIndex] = (byte)nextDistance;
                queue.Enqueue(candidateIndex);
            }
        }

        var minimumX = Enumerable.Repeat(size.Width, finalLabelCount).ToArray();
        var minimumY = Enumerable.Repeat(size.Height, finalLabelCount).ToArray();
        var maximumX = Enumerable.Repeat(-1, finalLabelCount).ToArray();
        var maximumY = Enumerable.Repeat(-1, finalLabelCount).ToArray();
        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < size.Width; x++)
            {
                var label = labels[checked(y * size.Width + x)];
                if (label <= 0)
                {
                    continue;
                }

                var groupIndex = label - 1;
                minimumX[groupIndex] = Math.Min(minimumX[groupIndex], x);
                minimumY[groupIndex] = Math.Min(minimumY[groupIndex], y);
                maximumX[groupIndex] = Math.Max(maximumX[groupIndex], x);
                maximumY[groupIndex] = Math.Max(maximumY[groupIndex], y);
            }
        }

        var regions = new List<PixelRect>(finalLabelCount);
        for (var index = 0; index < finalLabelCount; index++)
        {
            if (maximumX[index] >= 0)
            {
                regions.Add(new PixelRect(
                    minimumX[index],
                    minimumY[index],
                    maximumX[index] - minimumX[index] + 1,
                    maximumY[index] - minimumY[index] + 1));
            }
        }

        return regions;
    }

    private static IReadOnlyList<PixelRect> SplitAtValidatedVerticalGutters(
        byte[] mask,
        PixelSize size,
        PixelRect bounds,
        IReadOnlyList<MarkerRegion> markers,
        int minimumArea,
        CancellationToken cancellationToken)
    {
        if (markers.Count < 2 || bounds.Width < 8)
        {
            return [bounds];
        }

        var columnCounts = new int[bounds.Width];
        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                columnCounts[x - bounds.X] += mask[checked(y * size.Width + x)];
            }
        }

        var maximumGutterPixels = Math.Max(
            1,
            (int)Math.Floor(bounds.Height * AutomaticGutterMaximumOccupancy));
        var totalMarkerPixels = markers.Sum(marker => marker.PixelCount);
        var minimumMarkerPixels = Math.Max(
            minimumArea,
            (int)Math.Ceiling(totalMarkerPixels * AutomaticGutterMinimumMarkerShare));
        GutterCandidate? best = null;
        var runStart = -1;
        for (var offset = 0; offset <= bounds.Width; offset++)
        {
            var isLowOccupancy = offset < bounds.Width && columnCounts[offset] <= maximumGutterPixels;
            if (isLowOccupancy && runStart < 0)
            {
                runStart = offset;
                continue;
            }

            if (isLowOccupancy || runStart < 0)
            {
                continue;
            }

            var runEnd = offset;
            if (runStart >= 2 && runEnd <= bounds.Width - 2 && runEnd - runStart >= 2)
            {
                var cutX = bounds.X + ((runStart + runEnd) / 2);
                var leftMarkers = markers.Where(marker => marker.CenterX < cutX).ToArray();
                var rightMarkers = markers.Where(marker => marker.CenterX >= cutX).ToArray();
                var leftPixels = leftMarkers.Sum(marker => marker.PixelCount);
                var rightPixels = rightMarkers.Sum(marker => marker.PixelCount);
                if (leftPixels >= minimumMarkerPixels && rightPixels >= minimumMarkerPixels)
                {
                    var meanOccupancy = 0d;
                    for (var runOffset = runStart; runOffset < runEnd; runOffset++)
                    {
                        meanOccupancy += (double)columnCounts[runOffset] / bounds.Height;
                    }

                    meanOccupancy /= runEnd - runStart;
                    var markerBalance = (double)Math.Min(leftPixels, rightPixels) /
                        Math.Max(leftPixels, rightPixels);
                    var score = (double)(runEnd - runStart) / bounds.Width -
                        meanOccupancy * 0.2 +
                        markerBalance * 0.02;
                    var candidate = new GutterCandidate(cutX, score, leftMarkers, rightMarkers);
                    if (best is null || candidate.Score > best.Value.Score)
                    {
                        best = candidate;
                    }
                }
            }

            runStart = -1;
        }

        if (best is null)
        {
            return [bounds];
        }

        var leftBounds = new PixelRect(bounds.X, bounds.Y, best.Value.CutX - bounds.X, bounds.Height);
        var rightBounds = new PixelRect(best.Value.CutX, bounds.Y, bounds.Right - best.Value.CutX, bounds.Height);
        return [
            .. SplitAtValidatedVerticalGutters(
                mask,
                size,
                leftBounds,
                best.Value.LeftMarkers,
                minimumArea,
                cancellationToken),
            .. SplitAtValidatedVerticalGutters(
                mask,
                size,
                rightBounds,
                best.Value.RightMarkers,
                minimumArea,
                cancellationToken),
        ];
    }

    private static int CountVisiblePixels(byte[] mask, PixelSize size, PixelRect bounds)
    {
        var count = 0;
        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                count += mask[checked(y * size.Width + x)];
            }
        }

        return count;
    }

    private static bool HasUnclaimedSupportNeighbor(
        int[] labels,
        byte[] supportMask,
        PixelSize size,
        int x,
        int y)
    {
        return IsUnclaimed(x - 1, y) ||
            IsUnclaimed(x + 1, y) ||
            IsUnclaimed(x, y - 1) ||
            IsUnclaimed(x, y + 1);

        bool IsUnclaimed(int candidateX, int candidateY)
        {
            if (candidateX < 0 || candidateY < 0 ||
                candidateX >= size.Width || candidateY >= size.Height)
            {
                return false;
            }

            var candidateIndex = checked(candidateY * size.Width + candidateX);
            return supportMask[candidateIndex] != 0 && labels[candidateIndex] == 0;
        }
    }

    private static bool Contains(PixelRect outer, PixelRect inner) =>
        outer.X <= inner.X &&
        outer.Y <= inner.Y &&
        outer.Right >= inner.Right &&
        outer.Bottom >= inner.Bottom;

    private readonly record struct LabeledRegion(int Label, PixelRect Bounds);

    private readonly record struct MarkerRegion(PixelRect Bounds, int PixelCount)
    {
        public int CenterX => Bounds.X + (Bounds.Width / 2);
    }

    private readonly record struct GutterCandidate(
        int CutX,
        double Score,
        IReadOnlyList<MarkerRegion> LeftMarkers,
        IReadOnlyList<MarkerRegion> RightMarkers);

    private sealed class ComponentAccumulator
    {
        private int _minimumX = int.MaxValue;
        private int _minimumY = int.MaxValue;
        private int _maximumX = -1;
        private int _maximumY = -1;

        public int PixelCount { get; private set; }

        public PixelRect Bounds => new(
            _minimumX,
            _minimumY,
            _maximumX - _minimumX + 1,
            _maximumY - _minimumY + 1);

        public void Include(int x, int y)
        {
            PixelCount++;
            _minimumX = Math.Min(_minimumX, x);
            _minimumY = Math.Min(_minimumY, y);
            _maximumX = Math.Max(_maximumX, x);
            _maximumY = Math.Max(_maximumY, y);
        }

        public void Include(ComponentAccumulator other)
        {
            PixelCount += other.PixelCount;
            _minimumX = Math.Min(_minimumX, other._minimumX);
            _minimumY = Math.Min(_minimumY, other._minimumY);
            _maximumX = Math.Max(_maximumX, other._maximumX);
            _maximumY = Math.Max(_maximumY, other._maximumY);
        }
    }

    private readonly record struct AutomaticAlphaAnalysis(
        byte Threshold,
        byte MarkerThreshold,
        bool RequiresSeededRefinement);

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
