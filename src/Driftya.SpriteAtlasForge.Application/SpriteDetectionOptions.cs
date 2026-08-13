namespace Driftya.SpriteAtlasForge.Application;

public sealed record SpriteDetectionOptions
{
    public byte AlphaThreshold { get; init; } = 8;

    public int MinimumArea { get; init; } = 1;

    public int MergeDistance { get; init; }

    public int SourcePadding { get; init; }

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MinimumArea);
        ArgumentOutOfRangeException.ThrowIfNegative(MergeDistance);
        ArgumentOutOfRangeException.ThrowIfNegative(SourcePadding);
    }
}
