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

public enum CanvasPanPhase
{
    Started,
    Changed,
    Completed,
}

public sealed class CanvasPanEventArgs(CanvasPanPhase phase, double totalX, double totalY) : EventArgs
{
    public CanvasPanPhase Phase { get; } = phase;

    public double TotalX { get; } = totalX;

    public double TotalY { get; } = totalY;
}

public sealed class CanvasZoomRequestedEventArgs(double x, double y, int wheelDelta) : EventArgs
{
    public double X { get; } = x;

    public double Y { get; } = y;

    public int WheelDelta { get; } = wheelDelta;
}

public sealed class SpriteOverlayView : GraphicsView, IDrawable
{
    private const float HandleSize = 7;
    private const float HitTolerance = 9;
    private const double SnapDistance = 8;
    private SpriteCanvasOverlay? _pressedSprite;
    private SpriteCanvasOverlay? _resizedSprite;
    private CanvasResizeHandle _resizeHandle;
    private CanvasResizeHandle _selectedBorder;
    private CanvasPixelBounds? _previewBounds;
    private int? _verticalSnapGuide;
    private int? _horizontalSnapGuide;
    private PointF _pressPoint;
    private bool _moved;
    private bool _suppressGraphicsInteraction;
#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _platformView;
    private uint? _rightPanPointerId;
    private Windows.Foundation.Point _rightPanOrigin;
#endif

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
        propertyChanged: OnSelectedSpriteChanged);

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
        HandlerChanged += OnPlatformHandlerChanged;
        HandlerChanging += OnPlatformHandlerChanging;
    }

    public event EventHandler<SpriteSelectedEventArgs>? SpriteSelected;

    public event EventHandler<CanvasTappedEventArgs>? CanvasTapped;

    public event EventHandler<SpriteRegionResizedEventArgs>? SpriteRegionResized;

    public event EventHandler<CanvasPanEventArgs>? CanvasPan;

    public event EventHandler<CanvasZoomRequestedEventArgs>? CanvasZoomRequested;

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
                : CanvasPixelBounds.From(overlay);
            var selected = string.Equals(overlay.SpriteId, SelectedSpriteId, StringComparison.OrdinalIgnoreCase);
            var displayed = ScaleBounds(bounds, scale);

            canvas.StrokeColor = selected
                ? Color.FromArgb("#FFD166")
                : overlay.IsApproved
                    ? Color.FromArgb("#77E09C")
                    : Color.FromArgb("#6E8DFF");
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

                if (_selectedBorder != CanvasResizeHandle.None)
                {
                    canvas.StrokeColor = Color.FromArgb("#FF4D5A");
                    canvas.StrokeSize = 3;
                    DrawSelectedBorder(canvas, displayed, _selectedBorder);
                }
            }
        }

        canvas.StrokeColor = Color.FromArgb("#42D9FF");
        canvas.StrokeSize = 1;
        if (_verticalSnapGuide is { } verticalGuide)
        {
            var x = (float)(verticalGuide * scale);
            canvas.DrawLine(x, 0, x, (float)(SourceHeight * scale));
        }

        if (_horizontalSnapGuide is { } horizontalGuide)
        {
            var y = (float)(horizontalGuide * scale);
            canvas.DrawLine(0, y, (float)(SourceWidth * scale), y);
        }
    }

    private double EffectiveScale => Math.Max(0.01, ZoomScale);

    private void OnStartInteraction(object? sender, TouchEventArgs args)
    {
        if (_suppressGraphicsInteraction || args.Touches.Length == 0)
        {
            return;
        }

        _pressPoint = args.Touches[0];
        _moved = false;
        _pressedSprite = null;
        _resizedSprite = null;
        _resizeHandle = CanvasResizeHandle.None;
        _previewBounds = null;
        _verticalSnapGuide = null;
        _horizontalSnapGuide = null;

        var selected = FindOverlay(SelectedSpriteId);
        if (CanResize && selected is not null)
        {
            var handle = HitTestHandle(ScaleBounds(CanvasPixelBounds.From(selected), EffectiveScale), _pressPoint);
            if (handle != CanvasResizeHandle.None)
            {
                _selectedBorder = IsSingleBorder(handle)
                    ? _selectedBorder == handle ? CanvasResizeHandle.None : handle
                    : CanvasResizeHandle.None;
                _resizedSprite = selected;
                _resizeHandle = handle;
                _previewBounds = CanvasPixelBounds.From(selected);
                Invalidate();
                return;
            }
        }

        var sourcePoint = ToSourcePoint(_pressPoint);
        _pressedSprite = HitTestSprite(sourcePoint);
    }

    private void OnDragInteraction(object? sender, TouchEventArgs args)
    {
        if (args.Touches.Length == 0)
        {
            return;
        }

        var point = args.Touches[0];
        _moved |= Math.Abs(point.X - _pressPoint.X) > 2 || Math.Abs(point.Y - _pressPoint.Y) > 2;
        if (_resizedSprite is null || _resizeHandle == CanvasResizeHandle.None)
        {
            return;
        }

        var preview = SpriteResizeSnapper.Resize(
            CanvasPixelBounds.From(_resizedSprite),
            _resizeHandle,
            ToSourcePoint(point).X,
            ToSourcePoint(point).Y,
            Math.Max(1, (int)Math.Round(SourceWidth)),
            Math.Max(1, (int)Math.Round(SourceHeight)),
            Overlays ?? [],
            _resizedSprite.SpriteId,
            SnapDistance / EffectiveScale,
            snappingEnabled: !IsShiftPressed());
        _previewBounds = preview.Bounds;
        _verticalSnapGuide = preview.VerticalGuide;
        _horizontalSnapGuide = preview.HorizontalGuide;
        Invalidate();
    }

    private void OnEndInteraction(object? sender, TouchEventArgs args)
    {
        if (_suppressGraphicsInteraction)
        {
            _suppressGraphicsInteraction = false;
            ResetInteraction();
            return;
        }

        var releasePoint = args.Touches.Length > 0 ? args.Touches[0] : _pressPoint;
        if (_resizedSprite is not null && _previewBounds is { } resized)
        {
            var original = CanvasPixelBounds.From(_resizedSprite);
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
            if (_pressedSprite is not null)
            {
                SpriteSelected?.Invoke(this, new SpriteSelectedEventArgs(_pressedSprite.SpriteId));
            }
            else
            {
                var point = ToSourcePoint(releasePoint);
                CanvasTapped?.Invoke(this, new CanvasTappedEventArgs(point.X, point.Y));
            }
        }

        ResetInteraction();
    }

    private void OnCancelInteraction(object? sender, EventArgs args) => ResetInteraction();

    private void ResetInteraction()
    {
        _pressedSprite = null;
        _resizedSprite = null;
        _resizeHandle = CanvasResizeHandle.None;
        _previewBounds = null;
        _verticalSnapGuide = null;
        _horizontalSnapGuide = null;
        _moved = false;
        Invalidate();
    }

    public bool TryNudgeSelectedBorder(Windows.System.VirtualKey key) => NudgeSelectedBorder(key);

    public bool HasSelectedBorder => _selectedBorder != CanvasResizeHandle.None;

    private bool NudgeSelectedBorder(Windows.System.VirtualKey key)
    {
        if (!CanResize || _selectedBorder == CanvasResizeHandle.None ||
            FindOverlay(SelectedSpriteId) is not { } selected)
        {
            return false;
        }

        if ((_selectedBorder is CanvasResizeHandle.Left or CanvasResizeHandle.Right) &&
            key is not (Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Right) ||
            (_selectedBorder is CanvasResizeHandle.Top or CanvasResizeHandle.Bottom) &&
            key is not (Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down))
        {
            return false;
        }

        var bounds = CanvasPixelBounds.From(selected);
        var delta = key is Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Up ? -1 : 1;
        var (pointX, pointY) = _selectedBorder switch
        {
            CanvasResizeHandle.Left => ((double)bounds.X + delta, (double)bounds.Y),
            CanvasResizeHandle.Right => ((double)bounds.Right + delta, (double)bounds.Y),
            CanvasResizeHandle.Top => ((double)bounds.X, (double)bounds.Y + delta),
            CanvasResizeHandle.Bottom => ((double)bounds.X, (double)bounds.Bottom + delta),
            _ => (double.NaN, double.NaN),
        };

        if (double.IsNaN(pointX) || double.IsNaN(pointY))
        {
            return false;
        }

        var preview = SpriteResizeSnapper.Resize(
            bounds,
            _selectedBorder,
            pointX,
            pointY,
            Math.Max(1, (int)Math.Round(SourceWidth)),
            Math.Max(1, (int)Math.Round(SourceHeight)),
            Overlays ?? [],
            selected.SpriteId,
            0,
            snappingEnabled: false);

        if (preview.Bounds == bounds)
        {
            return false;
        }

        SpriteRegionResized?.Invoke(this, new SpriteRegionResizedEventArgs(
            selected.SpriteId,
            preview.Bounds.X,
            preview.Bounds.Y,
            preview.Bounds.Width,
            preview.Bounds.Height));
        return true;
    }

    private SpriteCanvasOverlay? HitTestSprite(PointF point) => (Overlays ?? [])
        .Where(overlay => Contains(CanvasPixelBounds.From(overlay), point))
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

    private static CanvasResizeHandle HitTestHandle(RectF bounds, PointF point)
    {
        var points = HandlePoints(bounds);
        var handles = new[]
        {
            CanvasResizeHandle.TopLeft,
            CanvasResizeHandle.Top,
            CanvasResizeHandle.TopRight,
            CanvasResizeHandle.Right,
            CanvasResizeHandle.BottomRight,
            CanvasResizeHandle.Bottom,
            CanvasResizeHandle.BottomLeft,
            CanvasResizeHandle.Left,
        };

        for (var index = 0; index < points.Length; index++)
        {
            if (Distance(points[index], point) <= HitTolerance)
            {
                return handles[index];
            }
        }

        return CanvasResizeHandle.None;
    }

    private static bool IsSingleBorder(CanvasResizeHandle handle) => handle is
        CanvasResizeHandle.Left or
        CanvasResizeHandle.Top or
        CanvasResizeHandle.Right or
        CanvasResizeHandle.Bottom;

    private static void DrawSelectedBorder(ICanvas canvas, RectF bounds, CanvasResizeHandle border)
    {
        switch (border)
        {
            case CanvasResizeHandle.Left:
                canvas.DrawLine(bounds.Left, bounds.Top, bounds.Left, bounds.Bottom);
                break;
            case CanvasResizeHandle.Top:
                canvas.DrawLine(bounds.Left, bounds.Top, bounds.Right, bounds.Top);
                break;
            case CanvasResizeHandle.Right:
                canvas.DrawLine(bounds.Right, bounds.Top, bounds.Right, bounds.Bottom);
                break;
            case CanvasResizeHandle.Bottom:
                canvas.DrawLine(bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom);
                break;
        }
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

    private static RectF ScaleBounds(CanvasPixelBounds bounds, double scale) => new(
        (float)(bounds.X * scale),
        (float)(bounds.Y * scale),
        (float)(bounds.Width * scale),
        (float)(bounds.Height * scale));

    private static bool Contains(CanvasPixelBounds bounds, PointF point) =>
        point.X >= bounds.X && point.X <= bounds.Right && point.Y >= bounds.Y && point.Y <= bounds.Bottom;

    private static bool IsShiftPressed()
    {
#if WINDOWS
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(
            Windows.System.VirtualKey.Shift);
        return state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
#else
        return false;
#endif
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

    private static void OnSelectedSpriteChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var view = (SpriteOverlayView)bindable;
        // Selection bindings can briefly pass through null while an edit replaces
        // the immutable sprite instance. Keep the active border in that case so
        // repeated keyboard nudges continue operating on the same border.
        if (oldValue is string oldId && newValue is string newId &&
            !string.Equals(oldId, newId, StringComparison.OrdinalIgnoreCase))
        {
            view._selectedBorder = CanvasResizeHandle.None;
        }

        view.Invalidate();
    }

    private void OnOverlayCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => Invalidate();

    private void OnPlatformHandlerChanged(object? sender, EventArgs args)
    {
#if WINDOWS
        AttachPlatformPointerHandlers();
#endif
    }

    private void OnPlatformHandlerChanging(object? sender, HandlerChangingEventArgs args)
    {
#if WINDOWS
        DetachPlatformPointerHandlers();
#endif
    }

#if WINDOWS
    private void AttachPlatformPointerHandlers()
    {
        DetachPlatformPointerHandlers();
        if (Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement platformView)
        {
            return;
        }

        _platformView = platformView;
        platformView.PointerPressed += OnPlatformPointerPressed;
        platformView.PointerMoved += OnPlatformPointerMoved;
        platformView.PointerReleased += OnPlatformPointerReleased;
        platformView.PointerCanceled += OnPlatformPointerCanceled;
        platformView.PointerCaptureLost += OnPlatformPointerCaptureLost;
        platformView.PointerWheelChanged += OnPlatformPointerWheelChanged;
    }

    private void DetachPlatformPointerHandlers()
    {
        if (_platformView is null)
        {
            return;
        }

        _platformView.PointerPressed -= OnPlatformPointerPressed;
        _platformView.PointerMoved -= OnPlatformPointerMoved;
        _platformView.PointerReleased -= OnPlatformPointerReleased;
        _platformView.PointerCanceled -= OnPlatformPointerCanceled;
        _platformView.PointerCaptureLost -= OnPlatformPointerCaptureLost;
        _platformView.PointerWheelChanged -= OnPlatformPointerWheelChanged;
        _platformView = null;
        _rightPanPointerId = null;
    }

    private void OnPlatformPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (_platformView is null)
        {
            return;
        }

        _platformView.Focus(Microsoft.UI.Xaml.FocusState.Pointer);
        if (!args.GetCurrentPoint(_platformView).Properties.IsRightButtonPressed)
        {
            return;
        }

        _rightPanPointerId = args.Pointer.PointerId;
        _rightPanOrigin = args.GetCurrentPoint(null).Position;
        _suppressGraphicsInteraction = true;
        _platformView.CapturePointer(args.Pointer);
        CanvasPan?.Invoke(this, new CanvasPanEventArgs(CanvasPanPhase.Started, 0, 0));
        args.Handled = true;
    }

    private void OnPlatformPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (_platformView is null || _rightPanPointerId != args.Pointer.PointerId)
        {
            return;
        }

        var position = args.GetCurrentPoint(null).Position;
        CanvasPan?.Invoke(this, new CanvasPanEventArgs(
            CanvasPanPhase.Changed,
            position.X - _rightPanOrigin.X,
            position.Y - _rightPanOrigin.Y));
        args.Handled = true;
    }

    private void OnPlatformPointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (_platformView is null)
        {
            return;
        }

        var point = args.GetCurrentPoint(_platformView);
        if (!point.Properties.IsRightButtonPressed || point.Properties.MouseWheelDelta == 0)
        {
            return;
        }

        _rightPanOrigin = args.GetCurrentPoint(null).Position;
        CanvasPan?.Invoke(this, new CanvasPanEventArgs(CanvasPanPhase.Started, 0, 0));
        CanvasZoomRequested?.Invoke(this, new CanvasZoomRequestedEventArgs(
            point.Position.X,
            point.Position.Y,
            point.Properties.MouseWheelDelta));
        args.Handled = true;
    }

    private void OnPlatformPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args) =>
        CompletePlatformPan(args, true);

    private void OnPlatformPointerCanceled(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args) =>
        CompletePlatformPan(args, false);

    private void OnPlatformPointerCaptureLost(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args) =>
        CompletePlatformPan(args, false);

    private void CompletePlatformPan(Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args, bool includeFinalPosition)
    {
        if (_platformView is null || _rightPanPointerId != args.Pointer.PointerId)
        {
            return;
        }

        var position = includeFinalPosition
            ? args.GetCurrentPoint(null).Position
            : _rightPanOrigin;
        _rightPanPointerId = null;
        _platformView.ReleasePointerCapture(args.Pointer);
        CanvasPan?.Invoke(this, new CanvasPanEventArgs(
            CanvasPanPhase.Completed,
            position.X - _rightPanOrigin.X,
            position.Y - _rightPanOrigin.Y));
        Dispatcher.Dispatch(() => _suppressGraphicsInteraction = false);
        args.Handled = true;
    }
#endif
}
