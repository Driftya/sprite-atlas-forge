namespace Driftya.SpriteAtlasForge.Application;

public sealed class AtlasProjectFormatException : Exception
{
    public AtlasProjectFormatException(string message, string? path = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Path = path;
    }

    public string? Path { get; }
}
