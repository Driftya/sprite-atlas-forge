using Driftya.SpriteAtlasForge.Infrastructure;
using Driftya.SpriteAtlasForge.ClientApplication.Services;
using Microsoft.Extensions.Logging;

namespace Driftya.SpriteAtlasForge.ClientApplication;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
            });

        builder.Services.AddSpriteAtlasForge();
        builder.Services.AddSingleton<IWorkspaceFilePicker, MauiWorkspaceFilePicker>();
        builder.Services.AddSingleton<IWorkspaceInteraction, MauiWorkspaceInteraction>();
        builder.Services.AddSingleton<WorkspacePageModel>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
