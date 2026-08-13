using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Application;

public sealed record AtlasPackingOptions
{
    public int Padding { get; init; } = 2;

    public int MaximumWidth { get; init; } = 4096;

    public int MaximumHeight { get; init; } = 4096;

    public bool PowerOfTwo { get; init; } = true;

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(Padding);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumHeight);
    }
}

public sealed record PackedSprite(string Id, PixelRect Frame);

public sealed record AtlasPackingResult(PixelSize Size, IReadOnlyList<PackedSprite> Sprites);

public sealed record RepackAtlasResult(AtlasProject Project, IReadOnlyList<string> GeneratedFiles);

public interface IAtlasPacker
{
    AtlasPackingResult Pack(AtlasProject project, AtlasPackingOptions options);
}

public interface IAtlasImageComposer
{
    Task ComposeAsync(
        string sourceImagePath,
        string outputImagePath,
        AtlasProject project,
        AtlasPackingResult packing,
        IProgress<AtlasProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
