using System.Linq;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Driftya.SpriteAtlasForge.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
    [Test]
    public async Task AddSpriteAtlasForge_registers_shared_application_information()
    {
        var services = new ServiceCollection();

        services.AddSpriteAtlasForge();

        using var serviceProvider = services.BuildServiceProvider();
        var applicationInfo = serviceProvider.GetRequiredService<AtlasForgeApplicationInfo>();
        var atlasForgeService = serviceProvider.GetRequiredService<IAtlasForgeService>();

        await Assert.That(applicationInfo).IsEqualTo(AtlasForgeApplicationInfo.Default);
        await Assert.That(applicationInfo.NativeProjectExtension).IsEqualTo(".saf.json");
        await Assert.That(atlasForgeService).IsNotNull();
    }

    [Test]
    public async Task AddSpriteAtlasForge_is_idempotent_for_shared_singletons()
    {
        var services = new ServiceCollection();

        services.AddSpriteAtlasForge();
        services.AddSpriteAtlasForge();

        var registrations = services.Count(descriptor =>
            descriptor.ServiceType == typeof(AtlasForgeApplicationInfo));

        await Assert.That(registrations).IsEqualTo(1);
    }
}
