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

        return services;
    }
}
