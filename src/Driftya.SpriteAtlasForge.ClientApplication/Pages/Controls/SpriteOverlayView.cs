using System.Collections.Specialized;
using Driftya.SpriteAtlasForge.ClientApplication.PageModels;
using Microsoft.Maui.Graphics;

namespace Driftya.SpriteAtlasForge.ClientApplication.Pages.Controls;

public sealed class SpriteSelectedEventArgs(string spriteId) : EventArgs
{
    public string SpriteId { get; } = spriteId;
}

public sealed class CanvasTappedEventArgs(double x, double y) : EventArgs
{
    public double X { get; } = x;

    public double Y { get; } = y;
}

public sealed class SpriteRegionResizedEventArgs(
    string spriteId,
    int x,
    int y,
    int width,
    int height) : EventArgs
{
    public string SpriteId { get; } = spriteId;

    public int X { get; } = x;

    public int Y { get; } = y;

    public int Width { get; } = width;

    public int Height { get; } = height;
}

public sealed class SpriteOverlayView : GraphicsView, IDrawable
{
    private const float HandleSize = 7;
    private const float HitTolerance = 9;
    private SpriteCanvasOverlay? _pressedSprite;
    private SpriteCanvasOverlay? _resizedSprite;
    private ResizeHandle _resizeHandle;
    private PixelBounds? _previewBounds;
    private PointF _pressPoint;
    private bool _moved;

    public static readonly BindableProperty OverlaysProperty = BindableProperty.Create(
        nameof(Overlays),
        typeof(IReadOnlyList<SpriteCanvasOverlay>),
        typeof(SpriteOverlayView),
        defaultValue: null,
        propertyChanged: OnOverlaysChanged);

    public static readonly BindableProperty SelectedSpriteIdProperty = BindableProperty.Create(
        nameof(SelectedSpriteId),
        typeof(string),
        typeof(SpriteOverlayView),
        defaultValue: null,
        propertyChanged: InvalidatePropertyChanged);

    public static readonly BindableProperty ZoomScaleProperty = BindableProperty.Create(
        nameof(ZoomScale),
        typeof(double),
        typeof(SpriteOverlayView),
        1d,
        propertyChanged: InvalidatePropertyChanged);

    public static readonly BindableProperty SourceWidthProperty = BindableProperty.Create(
        nameof(SourceWidth),
        typeof(double),
        typeof(SpriteOverlayView),
        0d);

    public static readonly BindableProperty SourceHeightProperty = BindableProperty.Create(
        nameof(SourceHeight),
        typeof(double),
        typeof(SpriteOverlayView),
        0d);

    public static readonly BindableProperty CanResizeProperty = BindableProperty.Create(
        nameof(CanResize),
        typeof(bool),
        typeof(SpriteOverlayView),
        false,
        propertyChanged: InvalidatePropertyChanged);

    public SpriteOverlayView()
    {
        Drawable = this;
        StartInteraction += OnStartInteraction;
        DragInteraction += OnDragInteraction;
        EndInteraction += OnEndInteraction;
        CancelInteraction += OnCancelInteraction;
    }

    public event EventHandler<SpriteSelectedEventArgs>? SpriteSelected;

    public event EventHandler<CanvasTappedEventArgs>? CanvasTapped;

    public event EventHandler<SpriteRegionResizedEventArgs>? SpriteRegionResized;

    public IReadOnlyList<SpriteCanvasOverlay>? Overlays
    {
        get => (IReadOnlyList<SpriteCanvasOverlay>?)GetValue(OverlaysProperty);
        set => SetValue(OverlaysProperty, value);
    }

    public string? SelectedSpriteId
    {
        get => (string?)GetValue(SelectedSpriteIdProperty);
        set => SetValue(SelectedSpriteIdProperty, value);
    }

    public double ZoomScale
    {
        get => (double)GetValue(ZoomScaleProperty);
        set => SetValue(ZoomScaleProperty, value);
    }

    public double SourceWidth
    {
        get => (double)GetValue(SourceWidthProperty);
        set => SetValue(SourceWidthProperty, value);
    }

    public double SourceHeight
    {
        get => (double)GetValue(SourceHeightProperty);
        set => SetValue(SourceHeightProperty, value);
    }

    public bool CanResize
    {
        get => (bool)GetValue(CanResizeProperty);
        set => SetValue(CanResizeProperty, value);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var scale = EffectiveScale;
        foreach (var overlay in Overlays ?? [])
        {
            var bounds = _previewBounds is { } preview &&
                         string.Equals(overlay.SpriteId, _resizedSprite?.SpriteId, StringComparison.OrdinalIgnoreCase)
                ? preview
                : PixelBounds.From(overlay);
            var selected = string.Equals(overlay.SpriteId, SelectedSpriteId, StringComparison.OrdinalIgnoreCase);
            var displayed = bounds.Scale(scale);

            canvas.StrokeColor = selected ? Color.FromArgb("#FFD166") : Color.FromArgb("#77E09C");
            canvas.StrokeSize = selected ? 2 : 1;
            canvas.DrawRectangle(displayed);

            if (displayed.Width >= 44 && displayed.Height >= 16)
            {
                canvas.FontColor = Color.FromArgb("#FFD166");
                canvas.FontSize = 10;
                canvas.DrawString(
                    overlay.SpriteId,
                    displayed.X + 3,
                    displayed.Y,
                    Math.Max(1, displayed.Width - 6),
                    Math.Min(16, displayed.Height),
                    HorizontalAlignment.Left,
                    VerticalAlignment.Top,
                    TextFlow.ClipBounds);
            }

            if (selected && CanResize)
            {
                canvas.FillColor = Color.FromArgb("#FFD166");
                foreach (var point in HandlePoints(displayed))
                {
                    canvas.FillRectangle(
                        point.X - (HandleSize / 2),
                        point.Y - (HandleSize / 2),
                        HandleSize,
                        HandleSize);
                }
            }
        }
    }

    private double EffectiveScale => Math.Max(0.01, ZoomScale);

    private void OnStartInteraction(object? sender, TouchEventArgs args)
    {
        if (args.Touches.Length == 0)
        {
            return;
        }

        _pressPoint = args.Touches[0];
        _moved = false;
        _pressedSprite = null;
        _resizedSprite = null;
        _resizeHandle = ResizeHandle.None;
        _previewBounds = null;

        var selected = FindOverlay(SelectedSpriteId);
        if (CanResize && selected is not null)
        {
            var handle = HitTestHandle(PixelBounds.From(selected).Scale(EffectiveScale), _pressPoint);
            if (handle != ResizeHandle.None)
            {
                _resizedSprite = selected;
                _resizeHandle = handle;
                _previewBounds = PixelBounds.From(selected);
                Invalidate();
                return;
            }
        }

        var sourcePoint = ToSourcePoint(_pressPoint);
        _pressedSprite = HitTestSprite(sourcePoint);
        if (_pressedSprite is not null)
        {
            SpriteSelected?.Invoke(this, new SpriteSelectedEventArgs(_pressedSprite.SpriteId));
        }
    }

    private void OnDragInteraction(object? sender, TouchEventArgs args)
    {
        if (args.Touches.Length == 0)
        {
            return;
        }

        var point = args.Touches[0];
        _moved |= Math.Abs(point.X - _pressPoint.X) > 2 || Math.Abs(point.Y - _pressPoint.Y) > 2;
        if (_resizedSprite is null || _resizeHandle == ResizeHandle.None)
        {
            return;
        }

        _previewBounds = Resize(
            PixelBounds.From(_resizedSprite),
            _resizeHandle,
            ToSourcePoint(point),
            Math.Max(1, (int)Math.Round(SourceWidth)),
            Math.Max(1, (int)Math.Round(SourceHeight)));
        Invalidate();
    }

    private void OnEndInteraction(object? sender, TouchEventArgs args)
    {
        var releasePoint = args.Touches.Length > 0 ? args.Touches[0] : _pressPoint;
        if (_resizedSprite is not null && _previewBounds is { } resized)
        {
            var original = PixelBounds.From(_resizedSprite);
            if (_moved && resized != original)
            {
                SpriteRegionResized?.Invoke(this, new SpriteRegionResizedEventArgs(
                    _resizedSprite.SpriteId,
                    resized.X,
                    resized.Y,
                    resized.Width,
                    resized.Height));
            }
        }
        else if (!_moved)
        {
            var point = ToSourcePoint(releasePoint);
            CanvasTapped?.Invoke(this, new CanvasTappedEventArgs(point.X, point.Y));
        }

        ResetInteraction();
    }

    private void OnCancelInteraction(object? sender, EventArgs args) => ResetInteraction();

    private void ResetInteraction()
    {
        _pressedSprite = null;
        _resizedSprite = null;
        _resizeHandle = ResizeHandle.None;
        _previewBounds = null;
        _moved = false;
        Invalidate();
    }

    private SpriteCanvasOverlay? HitTestSprite(PointF point) => (Overlays ?? [])
        .Where(overlay => PixelBounds.From(overlay).Contains(point))
        .OrderBy(overlay => overlay.Width * overlay.Height)
        .FirstOrDefault();

    private SpriteCanvasOverlay? FindOverlay(string? spriteId) => (Overlays ?? [])
        .FirstOrDefault(overlay => string.Equals(
            overlay.SpriteId,
            spriteId,
            StringComparison.OrdinalIgnoreCase));

    private PointF ToSourcePoint(PointF point) => new(
        (float)(point.X / EffectiveScale),
        (float)(point.Y / EffectiveScale));

    private static ResizeHandle HitTestHandle(RectF bounds, PointF point)
    {
        var points = HandlePoints(bounds);
        var handles = new[]
        {
            ResizeHandle.TopLeft,
            ResizeHandle.Top,
            ResizeHandle.TopRight,
            ResizeHandle.Right,
            ResizeHandle.BottomRight,
            ResizeHandle.Bottom,
            ResizeHandle.BottomLeft,
            ResizeHandle.Left,
        };

        for (var index = 0; index < points.Length; index++)
        {
            if (Distance(points[index], point) <= HitTolerance)
            {
                return handles[index];
            }
        }

        return ResizeHandle.None;
    }

    private static PointF[] HandlePoints(RectF bounds) =>
    [
        new(bounds.Left, bounds.Top),
        new(bounds.Center.X, bounds.Top),
        new(bounds.Right, bounds.Top),
        new(bounds.Right, bounds.Center.Y),
        new(bounds.Right, bounds.Bottom),
        new(bounds.Center.X, bounds.Bottom),
        new(bounds.Left, bounds.Bottom),
        new(bounds.Left, bounds.Center.Y),
    ];

    private static float Distance(PointF first, PointF second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        return MathF.Sqrt((x * x) + (y * y));
    }

    private static PixelBounds Resize(
        PixelBounds original,
        ResizeHandle handle,
        PointF point,
        int sourceWidth,
        int sourceHeight)
    {
        var left = original.X;
        var top = original.Y;
        var right = original.Right;
        var bottom = original.Bottom;
        var x = (int)Math.Round(point.X);
        var y = (int)Math.Round(point.Y);

        if (handle.HasFlag(ResizeHandle.Left))
        {
            left = Math.Clamp(x, 0, right - 1);
        }

        if (handle.HasFlag(ResizeHandle.Right))
        {
            right = Math.Clamp(x, left + 1, sourceWidth);
        }

        if (handle.HasFlag(ResizeHandle.Top))
        {
            top = Math.Clamp(y, 0, bottom - 1);
        }

        if (handle.HasFlag(ResizeHandle.Bottom))
        {
            bottom = Math.Clamp(y, top + 1, sourceHeight);
        }

        return new PixelBounds(left, top, right - left, bottom - top);
    }

    private static void OnOverlaysChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var view = (SpriteOverlayView)bindable;
        if (oldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= view.OnOverlayCollectionChanged;
        }

        if (newValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += view.OnOverlayCollectionChanged;
        }

        view.Invalidate();
    }

    private static void InvalidatePropertyChanged(BindableObject bindable, object? oldValue, object? newValue) =>
        ((SpriteOverlayView)bindable).Invalidate();

    private void OnOverlayCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => Invalidate();

    [Flags]
    private enum ResizeHandle
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

    private readonly record struct PixelBounds(int X, int Y, int Width, int Height)
    {
        public int Right => X + Width;

        public int Bottom => Y + Height;

        public static PixelBounds From(SpriteCanvasOverlay overlay) => new(
            (int)Math.Round(overlay.X),
            (int)Math.Round(overlay.Y),
            Math.Max(1, (int)Math.Round(overlay.Width)),
            Math.Max(1, (int)Math.Round(overlay.Height)));

        public RectF Scale(double scale) => new(
            (float)(X * scale),
            (float)(Y * scale),
            (float)(Width * scale),
            (float)(Height * scale));

        public bool Contains(PointF point) =>
            point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;
    }
}
