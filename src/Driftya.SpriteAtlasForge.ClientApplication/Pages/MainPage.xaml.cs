namespace Driftya.SpriteAtlasForge.ClientApplication.Pages;

using Controls;

public partial class MainPage : ContentPage
{
    public MainPage(WorkspacePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
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
