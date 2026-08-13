using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Application;

/// <summary>
/// Describes stable product-level information shared by every application host.
/// </summary>
public sealed record AtlasForgeApplicationInfo(
    string Name,
    string Description,
    string NativeProjectExtension)
{
    public static AtlasForgeApplicationInfo Default { get; } = new(
        "Sprite Atlas Forge",
        "Turn existing spritesheets into structured sprite atlases.",
        AtlasFormat.NativeExtension);
}
