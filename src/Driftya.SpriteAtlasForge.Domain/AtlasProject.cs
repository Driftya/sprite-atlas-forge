namespace Driftya.SpriteAtlasForge.Domain;

public sealed record AtlasProject
{
    public AtlasProject(
        string name,
        AtlasSource source,
        AtlasOutput atlas,
        IEnumerable<AtlasSprite> sprites,
        int formatVersion = AtlasFormat.CurrentVersion)
    {
        if (formatVersion != AtlasFormat.CurrentVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(formatVersion),
                $"Unsupported atlas format version {formatVersion}.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name cannot be empty.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(sprites);

        var spriteArray = sprites.ToArray();
        var duplicateSprite = spriteArray
            .GroupBy(sprite => sprite.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateSprite is not null)
        {
            throw new ArgumentException($"Sprite ID '{duplicateSprite.Key}' is duplicated.", nameof(sprites));
        }

        var invalidSourceRegion = spriteArray.FirstOrDefault(sprite => !sprite.SourceRegion.FitsWithin(source.Size));
        if (invalidSourceRegion is not null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sprites),
                $"Sprite '{invalidSourceRegion.Id}' has a source region outside the source image.");
        }

        var invalidFrame = spriteArray.FirstOrDefault(sprite => !sprite.Frame.FitsWithin(atlas.Size));
        if (invalidFrame is not null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sprites),
                $"Sprite '{invalidFrame.Id}' has a frame outside the atlas image.");
        }

        FormatVersion = formatVersion;
        Name = name.Trim();
        Source = source;
        Atlas = atlas;
        Sprites = Array.AsReadOnly(spriteArray);
    }

    public int FormatVersion { get; }

    public string Name { get; }

    public AtlasSource Source { get; }

    public AtlasOutput Atlas { get; }

    public IReadOnlyList<AtlasSprite> Sprites { get; }

    public AtlasSprite GetSprite(string id) => Sprites.FirstOrDefault(sprite =>
        string.Equals(sprite.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Sprite '{id}' was not found.");

    public AtlasProject ReplaceSprite(AtlasSprite updatedSprite) =>
        new(
            Name,
            Source,
            Atlas,
            Sprites.Select(sprite =>
                string.Equals(sprite.Id, updatedSprite.Id, StringComparison.OrdinalIgnoreCase)
                    ? updatedSprite
                    : sprite),
            FormatVersion);
}
