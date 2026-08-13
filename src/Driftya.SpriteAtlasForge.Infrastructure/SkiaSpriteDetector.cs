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
        using var bitmap = SKBitmap.Decode(imageBytes)
            ?? throw new InvalidDataException("The PNG image could not be decoded.");

        if (bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            throw new InvalidDataException("The PNG image has invalid dimensions.");
        }

        var size = new PixelSize(bitmap.Width, bitmap.Height);
        var regions = DetectComponents(bitmap, size, options, progress, cancellationToken);
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

    private static IReadOnlyList<PixelRect> DetectComponents(
        SKBitmap bitmap,
        PixelSize size,
        SpriteDetectionOptions options,
        IProgress<AtlasProgress>? progress,
        CancellationToken cancellationToken)
    {
        var visited = new bool[checked(size.Width * size.Height)];
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
                if (visited[startIndex] || bitmap.GetPixel(x, y).Alpha <= options.AlphaThreshold)
                {
                    visited[startIndex] = true;
                    continue;
                }

                var minX = x;
                var minY = y;
                var maxX = x;
                var maxY = y;
                var visiblePixelCount = 0;
                visited[startIndex] = true;
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
                    if (visited[candidateIndex])
                    {
                        return;
                    }

                    visited[candidateIndex] = true;
                    if (bitmap.GetPixel(candidateX, candidateY).Alpha > options.AlphaThreshold)
                    {
                        queue.Enqueue(candidateIndex);
                    }
                }
            }
        }

        return regions;
    }

    private static IReadOnlyList<PixelRect> MergeNearby(IEnumerable<PixelRect> source, int mergeDistance)
    {
        var regions = source.OrderBy(region => region.Y).ThenBy(region => region.X).ToList();
        if (mergeDistance == 0)
        {
            return regions;
        }

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
