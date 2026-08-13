using System;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Application.Tests;

public sealed class AtlasProjectEditorTests
{
    [Test]
    public async Task Add_and_remove_sprite_return_valid_immutable_projects()
    {
        var project = CreateProject();
        var added = new AtlasSprite(
            "sprite_002",
            new PixelRect(8, 8, 6, 6),
            new PixelRect(8, 8, 6, 6));

        var withSprite = AtlasProjectEditor.AddSprite(project, added);
        var removed = AtlasProjectEditor.RemoveSprite(withSprite, "sprite_001");

        await Assert.That(project.Sprites).Count().IsEqualTo(1);
        await Assert.That(withSprite.Sprites).Count().IsEqualTo(2);
        await Assert.That(removed.Sprites).IsEquivalentTo([added]);
    }

    [Test]
    public void Add_and_remove_sprite_reject_duplicate_and_missing_ids()
    {
        var project = CreateProject();
        var duplicate = new AtlasSprite(
            "SPRITE_001",
            new PixelRect(8, 8, 6, 6),
            new PixelRect(8, 8, 6, 6));

        Assert.Throws<ArgumentException>(() => AtlasProjectEditor.AddSprite(project, duplicate));
        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() =>
            AtlasProjectEditor.RemoveSprite(project, "missing"));
    }

    private static AtlasProject CreateProject() => new(
        "project",
        new AtlasSource("source.png", new PixelSize(16, 16), new string('a', 64)),
        new AtlasOutput("source.png", new PixelSize(16, 16), false),
        [new AtlasSprite(
            "sprite_001",
            new PixelRect(0, 0, 6, 6),
            new PixelRect(0, 0, 6, 6))]);
}
