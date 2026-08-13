using SkiaSharp;

namespace Driftya.SpriteAtlasForge.Infrastructure;

internal static class SkiaPngDecoder
{
    public static SKBitmap Decode(byte[] bytes, string errorMessage)
    {
        try
        {
            return SKBitmap.Decode(bytes) ?? throw new InvalidDataException(errorMessage);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(errorMessage, exception);
        }
    }
}
