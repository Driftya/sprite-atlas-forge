namespace Driftya.SpriteAtlasForge.Application;

public sealed record SpriteDetectionOptions
{
    public byte AlphaThreshold { get; init; } = 8;

    public int MinimumArea { get; init; } = 1;

    public int MergeDistance { get; init; }

    public int SourcePadding { get; init; }

    public int MaximumWidth { get; init; } = 16_384;

    public int MaximumHeight { get; init; } = 16_384;

    public long MaximumPixels { get; init; } = 67_108_864;

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MinimumArea);
        ArgumentOutOfRangeException.ThrowIfNegative(MergeDistance);
        ArgumentOutOfRangeException.ThrowIfNegative(SourcePadding);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumPixels);
    }
}
