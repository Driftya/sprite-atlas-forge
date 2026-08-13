using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Application;

public sealed record DetectedSpriteSheet(
    PixelSize ImageSize,
    string Sha256,
    IReadOnlyList<PixelRect> Regions);
