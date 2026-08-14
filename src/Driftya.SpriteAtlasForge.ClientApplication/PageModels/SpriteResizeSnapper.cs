namespace Driftya.SpriteAtlasForge.ClientApplication.PageModels;

[Flags]
public enum CanvasResizeHandle
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8,
    TopLeft = Top | Left,
    TopRight = Top | Right,
    BottomRight = Bottom | Right,
    BottomLeft = Bottom | Left,
}

public readonly record struct CanvasPixelBounds(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public static CanvasPixelBounds From(SpriteCanvasOverlay overlay) => new(
        (int)Math.Round(overlay.X),
        (int)Math.Round(overlay.Y),
        Math.Max(1, (int)Math.Round(overlay.Width)),
        Math.Max(1, (int)Math.Round(overlay.Height)));
}

public readonly record struct CanvasResizePreview(
    CanvasPixelBounds Bounds,
    int? VerticalGuide,
    int? HorizontalGuide);

public static class SpriteResizeSnapper
{
    public static CanvasResizePreview Resize(
        CanvasPixelBounds original,
        CanvasResizeHandle handle,
        double pointX,
        double pointY,
        int sourceWidth,
        int sourceHeight,
        IReadOnlyList<SpriteCanvasOverlay> overlays,
        string resizedSpriteId,
        double snapDistance,
        bool snappingEnabled = true)
    {
        var left = original.X;
        var top = original.Y;
        var right = original.Right;
        var bottom = original.Bottom;
        var x = (int)Math.Round(pointX);
        var y = (int)Math.Round(pointY);

        if (handle.HasFlag(CanvasResizeHandle.Left))
        {
            left = Math.Clamp(x, 0, right - 1);
        }

        if (handle.HasFlag(CanvasResizeHandle.Right))
        {
            right = Math.Clamp(x, left + 1, sourceWidth);
        }

        if (handle.HasFlag(CanvasResizeHandle.Top))
        {
            top = Math.Clamp(y, 0, bottom - 1);
        }

        if (handle.HasFlag(CanvasResizeHandle.Bottom))
        {
            bottom = Math.Clamp(y, top + 1, sourceHeight);
        }

        int? verticalGuide = null;
        int? horizontalGuide = null;
        if (!snappingEnabled)
        {
            return new CanvasResizePreview(
                new CanvasPixelBounds(left, top, right - left, bottom - top),
                verticalGuide,
                horizontalGuide);
        }

        var tolerance = Math.Max(0, (int)Math.Ceiling(snapDistance));
        var others = overlays
            .Where(overlay => !string.Equals(overlay.SpriteId, resizedSpriteId, StringComparison.OrdinalIgnoreCase))
            .Select(CanvasPixelBounds.From)
            .ToArray();

        if (handle.HasFlag(CanvasResizeHandle.Left))
        {
            verticalGuide = FindNearest(
                left,
                VerticalTargets(others, top, bottom, sourceWidth).Append(original.X),
                tolerance,
                0,
                right - 1);
            left = verticalGuide ?? left;
        }
        else if (handle.HasFlag(CanvasResizeHandle.Right))
        {
            verticalGuide = FindNearest(
                right,
                VerticalTargets(others, top, bottom, sourceWidth).Append(original.Right),
                tolerance,
                left + 1,
                sourceWidth);
            right = verticalGuide ?? right;
        }

        if (handle.HasFlag(CanvasResizeHandle.Top))
        {
            horizontalGuide = FindNearest(
                top,
                HorizontalTargets(others, left, right, sourceHeight).Append(original.Y),
                tolerance,
                0,
                bottom - 1);
            top = horizontalGuide ?? top;
        }
        else if (handle.HasFlag(CanvasResizeHandle.Bottom))
        {
            horizontalGuide = FindNearest(
                bottom,
                HorizontalTargets(others, left, right, sourceHeight).Append(original.Bottom),
                tolerance,
                top + 1,
                sourceHeight);
            bottom = horizontalGuide ?? bottom;
        }

        return new CanvasResizePreview(
            new CanvasPixelBounds(left, top, right - left, bottom - top),
            verticalGuide,
            horizontalGuide);
    }

    private static IEnumerable<int> VerticalTargets(
        IEnumerable<CanvasPixelBounds> others,
        int top,
        int bottom,
        int sourceWidth)
    {
        yield return 0;
        yield return sourceWidth;

        foreach (var bounds in others.Where(bounds => RangesOverlap(top, bottom, bounds.Y, bounds.Bottom)))
        {
            yield return bounds.X;
            yield return bounds.Right;
        }
    }

    private static IEnumerable<int> HorizontalTargets(
        IEnumerable<CanvasPixelBounds> others,
        int left,
        int right,
        int sourceHeight)
    {
        yield return 0;
        yield return sourceHeight;

        foreach (var bounds in others.Where(bounds => RangesOverlap(left, right, bounds.X, bounds.Right)))
        {
            yield return bounds.Y;
            yield return bounds.Bottom;
        }
    }

    private static int? FindNearest(
        int value,
        IEnumerable<int> targets,
        int tolerance,
        int minimum,
        int maximum) => targets
        .Where(target => target >= minimum && target <= maximum)
        .Select(target => new { Target = target, Distance = Math.Abs(target - value) })
        .Where(candidate => candidate.Distance <= tolerance)
        .OrderBy(candidate => candidate.Distance)
        .ThenBy(candidate => candidate.Target)
        .Select(candidate => (int?)candidate.Target)
        .FirstOrDefault();

    private static bool RangesOverlap(int firstStart, int firstEnd, int secondStart, int secondEnd) =>
        firstStart < secondEnd && secondStart < firstEnd;
}
