namespace Driftya.SpriteAtlasForge.ClientApplication.Pages;

public partial class MainPage : ContentPage
{
    public MainPage(WorkspacePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    private async void OnCanvasTapped(object? sender, TappedEventArgs args)
    {
        if (BindingContext is not WorkspacePageModel model || args.GetPosition(AtlasCanvas) is not { } point)
        {
            return;
        }

        await model.AddConnectorAtCanvasCommand.ExecuteAsync(new CanvasPoint(point.X, point.Y));
    }

    private async void OnSpriteTapped(object? sender, TappedEventArgs args)
    {
        if (BindingContext is not WorkspacePageModel model ||
            sender is not TapGestureRecognizer { Parent.BindingContext: SpriteCanvasOverlay overlay } ||
            args.GetPosition(AtlasCanvas) is not { } point)
        {
            return;
        }

        model.SelectSpriteCommand.Execute(overlay.SpriteId);
        if (!string.IsNullOrWhiteSpace(model.NewConnectorName))
        {
            await model.AddConnectorAtCanvasCommand.ExecuteAsync(new CanvasPoint(point.X, point.Y));
        }
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
