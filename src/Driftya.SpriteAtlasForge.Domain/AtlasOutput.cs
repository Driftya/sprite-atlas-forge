namespace Driftya.SpriteAtlasForge.Domain;

public sealed record AtlasOutput
{
    public AtlasOutput(
        string image,
        PixelSize size,
        bool repacked,
        AtlasPackingMetadata? packing = null)
    {
        if (!repacked && packing is not null)
        {
            throw new ArgumentException("Packing metadata is valid only for repacked atlases.", nameof(packing));
        }

        Image = AtlasAssetPath.Normalize(image, nameof(image));
        Size = size;
        Repacked = repacked;
        Packing = packing;
    }

    public string Image { get; }

    public PixelSize Size { get; }

    public bool Repacked { get; }

    public AtlasPackingMetadata? Packing { get; }
}
