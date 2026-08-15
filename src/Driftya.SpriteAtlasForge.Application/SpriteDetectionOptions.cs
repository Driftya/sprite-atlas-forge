namespace Driftya.SpriteAtlasForge.Application;

public enum SpriteBackgroundMode
{
    Auto,
    AlphaOnly,
    BorderConnected,
}

public sealed record SpriteDetectionOptions
{
    public const byte DefaultAlphaThreshold = 8;

    public const int DefaultMinimumArea = 64;

    public const int DefaultMergeDistance = 2;

    public const int DefaultNoiseReductionRadius = 1;

    public const int DefaultBackgroundColorTolerance = 12;

    public const int DetachedDetailRecoveryDistance = 16;

    public SpriteBackgroundMode BackgroundMode { get; init; } = SpriteBackgroundMode.Auto;

    /// <summary>
    /// Maximum per-channel color step followed while removing opaque background from the image border.
    /// </summary>
    public int BackgroundColorTolerance { get; init; } = DefaultBackgroundColorTolerance;

    public byte AlphaThreshold { get; init; } = DefaultAlphaThreshold;

    public int MinimumArea { get; init; } = DefaultMinimumArea;

    public int MergeDistance { get; init; } = DefaultMergeDistance;

    /// <summary>
    /// Radius of the square morphological opening applied to the alpha mask.
    /// Zero preserves raw pixel-art connectivity; one removes isolated pixels and one-pixel bridges.
    /// </summary>
    public int NoiseReductionRadius { get; init; } = DefaultNoiseReductionRadius;

    public int SourcePadding { get; init; }

    public bool RecoverDetachedDetails { get; init; }

    public int MaximumWidth { get; init; } = 16_384;

    public int MaximumHeight { get; init; } = 16_384;

    public long MaximumPixels { get; init; } = 67_108_864;

    public void Validate()
    {
        if (!Enum.IsDefined(BackgroundMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BackgroundMode),
                "Background mode is not supported.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MinimumArea);
        ArgumentOutOfRangeException.ThrowIfNegative(MergeDistance);
        ArgumentOutOfRangeException.ThrowIfNegative(NoiseReductionRadius);
        if (NoiseReductionRadius > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(NoiseReductionRadius),
                "Noise-reduction radius cannot exceed 4 pixels.");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(SourcePadding);
        ArgumentOutOfRangeException.ThrowIfNegative(BackgroundColorTolerance);
        if (BackgroundColorTolerance > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BackgroundColorTolerance),
                "Background color tolerance cannot exceed 255.");
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumPixels);
    }
}
