using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Infrastructure;

public sealed class DeterministicShelfAtlasPacker : IAtlasPacker
{
    public AtlasPackingResult Pack(AtlasProject project, AtlasPackingOptions options)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (project.Sprites.Count == 0)
        {
            return new AtlasPackingResult(new PixelSize(1, 1), Array.Empty<PackedSprite>());
        }

        var items = project.Sprites
            .Select(sprite => new PackItem(
                sprite.Id,
                checked(sprite.SourceRegion.Width + options.Padding * 2),
                checked(sprite.SourceRegion.Height + options.Padding * 2),
                sprite.SourceRegion.Width,
                sprite.SourceRegion.Height))
            .OrderByDescending(item => item.OuterHeight)
            .ThenByDescending(item => item.OuterWidth)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();

        if (items.Any(item => item.OuterWidth > options.MaximumWidth || item.OuterHeight > options.MaximumHeight))
        {
            throw new InvalidOperationException("At least one sprite exceeds the configured maximum atlas dimensions.");
        }

        var totalArea = items.Aggregate(0L, (area, item) => checked(area + (long)item.OuterWidth * item.OuterHeight));
        var widest = items.Max(item => item.OuterWidth);
        var estimatedWidth = Math.Max(widest, checked((int)Math.Ceiling(Math.Sqrt(totalArea))));
        var initialWidth = options.PowerOfTwo ? NextPowerOfTwo(estimatedWidth) : options.MaximumWidth;

        for (var candidateWidth = initialWidth;
             candidateWidth <= options.MaximumWidth;
             candidateWidth = NextCandidateWidth(candidateWidth, options.MaximumWidth, options.PowerOfTwo))
        {
            if (TryPack(items, candidateWidth, options, out var packed, out var usedHeight))
            {
                var atlasHeight = options.PowerOfTwo ? NextPowerOfTwo(Math.Max(1, usedHeight)) : Math.Max(1, usedHeight);
                if (atlasHeight <= options.MaximumHeight)
                {
                    return new AtlasPackingResult(new PixelSize(candidateWidth, atlasHeight), packed);
                }
            }

            if (!options.PowerOfTwo || candidateWidth == options.MaximumWidth)
            {
                break;
            }
        }

        throw new InvalidOperationException(
            $"Sprites cannot fit within {options.MaximumWidth}x{options.MaximumHeight} using the configured padding.");
    }

    private static bool TryPack(
        IReadOnlyList<PackItem> items,
        int atlasWidth,
        AtlasPackingOptions options,
        out IReadOnlyList<PackedSprite> packed,
        out int usedHeight)
    {
        var result = new List<PackedSprite>(items.Count);
        var x = 0;
        var y = 0;
        var shelfHeight = 0;

        foreach (var item in items)
        {
            if (x > 0 && checked(x + item.OuterWidth) > atlasWidth)
            {
                y = checked(y + shelfHeight);
                x = 0;
                shelfHeight = 0;
            }

            if (checked(y + item.OuterHeight) > options.MaximumHeight)
            {
                packed = Array.Empty<PackedSprite>();
                usedHeight = 0;
                return false;
            }

            result.Add(new PackedSprite(
                item.Id,
                new PixelRect(
                    checked(x + options.Padding),
                    checked(y + options.Padding),
                    item.Width,
                    item.Height)));
            x = checked(x + item.OuterWidth);
            shelfHeight = Math.Max(shelfHeight, item.OuterHeight);
        }

        usedHeight = checked(y + shelfHeight);
        packed = result;
        return true;
    }

    private static int NextCandidateWidth(int current, int maximum, bool powerOfTwo)
    {
        if (!powerOfTwo || current >= maximum)
        {
            return maximum;
        }

        return current > maximum / 2 ? maximum : current * 2;
    }

    private static int NextPowerOfTwo(int value)
    {
        if (value <= 1)
        {
            return 1;
        }

        var result = 1;
        while (result < value)
        {
            result = checked(result * 2);
        }

        return result;
    }

    private sealed record PackItem(
        string Id,
        int OuterWidth,
        int OuterHeight,
        int Width,
        int Height);
}
