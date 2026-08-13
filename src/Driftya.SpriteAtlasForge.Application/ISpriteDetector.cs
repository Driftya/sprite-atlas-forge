namespace Driftya.SpriteAtlasForge.Application;

public interface ISpriteDetector
{
    Task<DetectedSpriteSheet> DetectAsync(
        string imagePath,
        SpriteDetectionOptions options,
        IProgress<AtlasProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
