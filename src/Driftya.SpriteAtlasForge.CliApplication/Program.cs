using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.CliApplication;
using Driftya.SpriteAtlasForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddSpriteAtlasForge();

await using var serviceProvider = services.BuildServiceProvider();
var cli = new AtlasForgeCli(
    serviceProvider.GetRequiredService<IAtlasForgeService>(),
    serviceProvider.GetRequiredService<AtlasForgeApplicationInfo>());

return await cli.CreateRootCommand().Parse(args).InvokeAsync();
