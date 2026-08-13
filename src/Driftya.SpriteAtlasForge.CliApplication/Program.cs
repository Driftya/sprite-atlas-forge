using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Parsing;

var services = new ServiceCollection();
services.AddSpriteAtlasForge();

using var serviceProvider = services.BuildServiceProvider();
var applicationInfo = serviceProvider.GetRequiredService<AtlasForgeApplicationInfo>();

var rootCommand = new RootCommand(applicationInfo.Description);
rootCommand.SetAction((ParseResult _) => WriteApplicationInfo(applicationInfo));

var infoCommand = new Command("info", "Show application and native-format information.");
infoCommand.SetAction((ParseResult _) => WriteApplicationInfo(applicationInfo));
rootCommand.Subcommands.Add(infoCommand);

return rootCommand.Parse(args).Invoke();

static void WriteApplicationInfo(AtlasForgeApplicationInfo applicationInfo)
{
    Console.WriteLine(applicationInfo.Name);
    Console.WriteLine(applicationInfo.Description);
    Console.WriteLine($"Native project extension: {applicationInfo.NativeProjectExtension}");
    Console.WriteLine("Implementation status: Phase 0 foundation ready");
}
