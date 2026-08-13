namespace Driftya.SpriteAtlasForge.Domain;

public sealed record AtlasPackingMetadata
{
    public AtlasPackingMetadata(
        string algorithm,
        int padding,
        bool powerOfTwo,
        int maximumWidth,
        int maximumHeight)
    {
        if (string.IsNullOrWhiteSpace(algorithm))
        {
            throw new ArgumentException("Packing algorithm cannot be empty.", nameof(algorithm));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(padding);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumHeight);

        Algorithm = algorithm.Trim();
        Padding = padding;
        PowerOfTwo = powerOfTwo;
        MaximumWidth = maximumWidth;
        MaximumHeight = maximumHeight;
    }

    public string Algorithm { get; }

    public int Padding { get; }

    public bool PowerOfTwo { get; }

    public int MaximumWidth { get; }

    public int MaximumHeight { get; }
}
