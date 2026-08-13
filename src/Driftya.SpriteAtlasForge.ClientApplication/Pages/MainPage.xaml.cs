namespace Driftya.SpriteAtlasForge.ClientApplication.Pages;

public partial class MainPage : ContentPage
{
    public MainPage(WorkspacePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
