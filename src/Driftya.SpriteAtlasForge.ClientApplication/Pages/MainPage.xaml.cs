namespace Driftya.SpriteAtlasForge.ClientApplication.Pages;

using Controls;

public partial class MainPage : ContentPage
{
    private double _canvasPanStartX;
    private double _canvasPanStartY;
    private double _canvasPanTargetX;
    private double _canvasPanTargetY;
    private bool _canvasPanScrollPending;
    private bool _canvasPanTargetChanged;
    private bool _zoomScrollPending;
    private double _zoomScrollTargetX;
    private double _zoomScrollTargetY;
#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _platformView;
    private Microsoft.UI.Xaml.Input.KeyboardAccelerator? _deleteAccelerator;
#endif

    public MainPage(WorkspacePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
        HandlerChanged += OnPlatformHandlerChanged;
        HandlerChanging += OnPlatformHandlerChanging;
    }

    private async void OnOverlayCanvasTapped(object? sender, CanvasTappedEventArgs args)
    {
        if (BindingContext is not WorkspacePageModel model)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(model.NewConnectorName))
        {
            await model.AddConnectorAtCanvasCommand.ExecuteAsync(new CanvasPoint(
                args.X * model.CanvasZoomScale,
                args.Y * model.CanvasZoomScale));
        }
    }

    private void OnOverlaySpriteSelected(object? sender, SpriteSelectedEventArgs args)
    {
        if (BindingContext is not WorkspacePageModel model)
        {
            return;
        }

        model.SelectSpriteCommand.Execute(args.SpriteId);
    }

    private async void OnSpriteRegionResized(object? sender, SpriteRegionResizedEventArgs args)
    {
        if (BindingContext is not WorkspacePageModel model)
        {
            return;
        }

        await model.ResizeSpriteFromCanvasCommand.ExecuteAsync(new CanvasSpriteResize(
            args.SpriteId,
            args.X,
            args.Y,
            args.Width,
            args.Height));
    }

    private void OnOverlayCanvasPan(object? sender, CanvasPanEventArgs args)
    {
        if (args.Phase == CanvasPanPhase.Started)
        {
            _canvasPanStartX = AtlasScrollView.ScrollX;
            _canvasPanStartY = AtlasScrollView.ScrollY;
        }

        _canvasPanTargetX = Math.Max(0, _canvasPanStartX - args.TotalX);
        _canvasPanTargetY = Math.Max(0, _canvasPanStartY - args.TotalY);
        _canvasPanTargetChanged = true;
        QueueCanvasPanScroll();
    }

    private async void QueueCanvasPanScroll()
    {
        if (_canvasPanScrollPending)
        {
            return;
        }

        _canvasPanScrollPending = true;
        try
        {
            while (_canvasPanTargetChanged)
            {
                _canvasPanTargetChanged = false;
                var targetX = _canvasPanTargetX;
                var targetY = _canvasPanTargetY;
                await AtlasScrollView.ScrollToAsync(targetX, targetY, false);
            }
        }
        finally
        {
            _canvasPanScrollPending = false;
        }
    }

    private void OnOverlayCanvasZoomRequested(object? sender, CanvasZoomRequestedEventArgs args)
    {
        if (BindingContext is not WorkspacePageModel model || model.CurrentProject is null)
        {
            return;
        }

        var oldScale = model.CanvasZoomScale;
        var wheelSteps = args.WheelDelta / 120d;
        var newZoom = Math.Clamp(model.ZoomPercent * Math.Pow(1.1, wheelSteps), 25, 800);
        if (Math.Abs(newZoom - model.ZoomPercent) < 0.01)
        {
            return;
        }

        var viewportX = args.X - AtlasScrollView.ScrollX;
        var viewportY = args.Y - AtlasScrollView.ScrollY;
        var newScale = newZoom / 100d;
        _zoomScrollTargetX = Math.Max(0, (args.X / oldScale * newScale) - viewportX);
        _zoomScrollTargetY = Math.Max(0, (args.Y / oldScale * newScale) - viewportY);
        _zoomScrollPending = true;
        model.ZoomPercent = newZoom;
    }

    private async void OnAtlasCanvasSizeChanged(object? sender, EventArgs args)
    {
        if (!_zoomScrollPending)
        {
            return;
        }

        _zoomScrollPending = false;
        await AtlasScrollView.ScrollToAsync(_zoomScrollTargetX, _zoomScrollTargetY, false);
        _canvasPanStartX = AtlasScrollView.ScrollX;
        _canvasPanStartY = AtlasScrollView.ScrollY;
    }

    private void OnPlatformHandlerChanged(object? sender, EventArgs args)
    {
#if WINDOWS
        DetachPlatformKeyboardHandlers();
        if (Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement platformView)
        {
            return;
        }

        _platformView = platformView;
        _deleteAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.Delete,
        };
        _deleteAccelerator.Invoked += OnDeleteAcceleratorInvoked;
        platformView.KeyboardAccelerators.Add(_deleteAccelerator);
#endif
    }

    private void OnPlatformHandlerChanging(object? sender, HandlerChangingEventArgs args)
    {
#if WINDOWS
        DetachPlatformKeyboardHandlers();
#endif
    }

#if WINDOWS
    private async void OnDeleteAcceleratorInvoked(
        Microsoft.UI.Xaml.Input.KeyboardAccelerator sender,
        Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_platformView?.XamlRoot is null ||
            Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(_platformView.XamlRoot) is
                Microsoft.UI.Xaml.Controls.TextBox or
                Microsoft.UI.Xaml.Controls.PasswordBox or
                Microsoft.UI.Xaml.Controls.RichEditBox ||
            BindingContext is not WorkspacePageModel model ||
            !model.DeleteSelectedSpriteCommand.CanExecute(null))
        {
            return;
        }

        args.Handled = true;
        await model.DeleteSelectedSpriteCommand.ExecuteAsync(null);
    }

    private void DetachPlatformKeyboardHandlers()
    {
        if (_platformView is not null && _deleteAccelerator is not null)
        {
            _platformView.KeyboardAccelerators.Remove(_deleteAccelerator);
            _deleteAccelerator.Invoked -= OnDeleteAcceleratorInvoked;
        }

        _platformView = null;
        _deleteAccelerator = null;
    }
#endif

    private async void OnConnectorPanUpdated(object? sender, PanUpdatedEventArgs args)
    {
        if (args.StatusType != GestureStatus.Completed ||
            sender is not PanGestureRecognizer { Parent.BindingContext: ConnectorCanvasOverlay overlay } ||
            BindingContext is not WorkspacePageModel model)
        {
            return;
        }

        await model.MoveConnectorFromCanvasCommand.ExecuteAsync(new CanvasConnectorMove(
            overlay.Name,
            overlay.X + 6 + args.TotalX,
            overlay.Y + 6 + args.TotalY));
    }
}
