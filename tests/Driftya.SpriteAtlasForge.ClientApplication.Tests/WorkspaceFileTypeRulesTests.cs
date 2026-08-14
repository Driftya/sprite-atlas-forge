using System;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.ClientApplication.Services;

namespace Driftya.SpriteAtlasForge.ClientApplication.Tests;

public sealed class WorkspaceFileTypeRulesTests
{
    [Test]
    public async Task Windows_picker_extensions_start_with_a_dot_and_never_use_platform_UTIs()
    {
        var extensions = WorkspaceFileTypeRules.PngExtensions
            .Concat(WorkspaceFileTypeRules.NativeProjectExtensions(".saf.json"))
            .ToArray();

        await Assert.That(extensions).IsEquivalentTo([".png", ".saf.json"]);
        await Assert.That(extensions.All(extension => extension.StartsWith('.'))).IsTrue();
        await Assert.That(extensions).DoesNotContain("public.png");
        await Assert.That(extensions).DoesNotContain("public.json");
    }

    [Test]
    [Arguments("public.png")]
    [Arguments("*.png")]
    [Arguments("")]
    public async Task Invalid_picker_extensions_fail_before_MAUI_receives_them(string extension)
    {
        var exception = Assert.Throws<ArgumentException>(() => WorkspaceFileTypeRules.Validate([extension]));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("must start with '.'");
    }

    [Test]
    public async Task Save_picker_suggests_the_complete_native_extension_once()
    {
        await Assert.That(WorkspaceFileTypeRules.EnsureExtension("modules", ".saf.json"))
            .IsEqualTo("modules.saf.json");
        await Assert.That(WorkspaceFileTypeRules.EnsureExtension("modules.saf.json", ".saf.json"))
            .IsEqualTo("modules.saf.json");
    }
}
