namespace Driftya.SpriteAtlasForge.ClientApplication;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        _shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(_shell);
}
