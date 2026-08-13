namespace Driftya.SpriteAtlasForge.Domain;

public readonly record struct PixelRect
{
    public PixelRect(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        _ = checked(x + width);
        _ = checked(y + height);

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public int Right => checked(X + Width);

    public int Bottom => checked(Y + Height);

    public int Area => checked(Width * Height);

    public bool FitsWithin(PixelSize size) => Right <= size.Width && Bottom <= size.Height;

    public bool ContainsLocalPoint(int x, int y) =>
        x >= 0 && y >= 0 && x <= Width && y <= Height;

    public PixelRect Expand(int padding, PixelSize bounds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(padding);

        var x = Math.Max(0, X - padding);
        var y = Math.Max(0, Y - padding);
        var right = Math.Min(bounds.Width, checked(Right + padding));
        var bottom = Math.Min(bounds.Height, checked(Bottom + padding));

        return new PixelRect(x, y, right - x, bottom - y);
    }

    public PixelRect Union(PixelRect other)
    {
        var x = Math.Min(X, other.X);
        var y = Math.Min(Y, other.Y);
        var right = Math.Max(Right, other.Right);
        var bottom = Math.Max(Bottom, other.Bottom);

        return new PixelRect(x, y, right - x, bottom - y);
    }
}
