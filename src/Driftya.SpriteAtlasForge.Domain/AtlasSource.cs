namespace Driftya.SpriteAtlasForge.Domain;

public sealed record AtlasSource
{
    public AtlasSource(string image, PixelSize size, string sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256) ||
            sha256.Length != 64 ||
            !sha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("SHA-256 must contain exactly 64 hexadecimal characters.", nameof(sha256));
        }

        Image = AtlasAssetPath.Normalize(image, nameof(image));
        Size = size;
        Sha256 = sha256.ToLowerInvariant();
    }

    public string Image { get; }

    public PixelSize Size { get; }

    public string Sha256 { get; }
}
