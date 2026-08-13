namespace Driftya.SpriteAtlasForge.Domain;

public sealed record AtlasConnector
{
    public AtlasConnector(string name, int x, int y)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Connector name cannot be empty.", nameof(name));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);

        Name = name.Trim();
        X = x;
        Y = y;
    }

    public string Name { get; }

    public int X { get; }

    public int Y { get; }
}
