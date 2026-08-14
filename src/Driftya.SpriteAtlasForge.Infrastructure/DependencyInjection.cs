using Driftya.SpriteAtlasForge.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Driftya.SpriteAtlasForge.Infrastructure;

/// <summary>
/// Registers the shared Sprite Atlas Forge application and infrastructure services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSpriteAtlasForge(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(AtlasForgeApplicationInfo.Default);
        services.TryAddSingleton<IAtlasProjectStore, NativeAtlasProjectStore>();
        services.TryAddSingleton<ISpriteDetector, SkiaSpriteDetector>();
        services.TryAddSingleton<IAtlasPacker, DeterministicShelfAtlasPacker>();
        services.TryAddSingleton<IAtlasImageComposer, SkiaAtlasImageComposer>();
        services.TryAddSingleton<ISpriteImageExporter, SkiaSpriteImageExporter>();
        services.TryAddSingleton<IAtlasFileSystem, LocalAtlasFileSystem>();
        services.TryAddSingleton<IAtlasForgeService, AtlasForgeService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAtlasExporter, NativeAtlasExporter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAtlasExporter, PhaserJsonHashExporter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAtlasExporter, UnitySpriteSheetExporter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAtlasExporter, GodotAtlasTextureExporter>());

        return services;
    }
}
