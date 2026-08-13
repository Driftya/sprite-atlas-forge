using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Domain.Tests;

public sealed class DomainInvariantTests
{
    [Test]
    public async Task PixelRect_rejects_non_positive_dimensions()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new PixelRect(0, 0, 0, 10));

        await Assert.That(exception.ParamName).IsEqualTo("width");
    }

    [Test]
    public async Task Sprite_allows_connectors_on_its_boundary()
    {
        var sprite = new AtlasSprite(
            "habitat",
            new PixelRect(10, 20, 100, 40),
            new PixelRect(10, 20, 100, 40),
            [new AtlasConnector("next", 100, 40)]);

        await Assert.That(sprite.Connectors).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Sprite_rejects_connectors_outside_logical_bounds()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new AtlasSprite(
            "habitat",
            new PixelRect(10, 20, 100, 40),
            new PixelRect(10, 20, 100, 40),
            [new AtlasConnector("next", 101, 40)]));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("outside");
    }

    [Test]
    public async Task Sprite_rejects_case_insensitive_duplicate_connector_names()
    {
        var exception = Assert.Throws<ArgumentException>(() => new AtlasSprite(
            "habitat",
            new PixelRect(0, 0, 100, 40),
            new PixelRect(0, 0, 100, 40),
            [new AtlasConnector("Anchor", 0, 20), new AtlasConnector("anchor", 1, 20)]));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("duplicated");
    }

    [Test]
    public async Task Project_rejects_case_insensitive_duplicate_sprite_ids()
    {
        var source = new AtlasSource("sprites.png", new PixelSize(100, 100), new string('a', 64));
        var atlas = new AtlasOutput("sprites.png", new PixelSize(100, 100), false);
        var first = new AtlasSprite("Engine", new PixelRect(0, 0, 10, 10), new PixelRect(0, 0, 10, 10));
        var second = new AtlasSprite("engine", new PixelRect(20, 0, 10, 10), new PixelRect(20, 0, 10, 10));

        var exception = Assert.Throws<ArgumentException>(() => new AtlasProject("test", source, atlas, [first, second]));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("duplicated");
    }

    [Test]
    public async Task Project_rejects_regions_outside_the_source_image()
    {
        var source = new AtlasSource("sprites.png", new PixelSize(20, 20), new string('a', 64));
        var atlas = new AtlasOutput("sprites.png", new PixelSize(20, 20), false);
        var sprite = new AtlasSprite(
            "outside",
            new PixelRect(15, 15, 10, 10),
            new PixelRect(0, 0, 10, 10));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AtlasProject("test", source, atlas, [sprite]));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("outside the source image");
    }

    [Test]
    public async Task Source_rejects_non_portable_asset_paths()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AtlasSource("../outside.png", new PixelSize(10, 10), new string('a', 64)));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("relative");
    }

    [Test]
    public async Task Sprite_updates_a_connector_in_place_while_preserving_order()
    {
        var sprite = new AtlasSprite(
            "habitat",
            new PixelRect(0, 0, 100, 40),
            new PixelRect(0, 0, 100, 40),
            [new AtlasConnector("anchor", 0, 20), new AtlasConnector("next", 99, 20)]);

        var updated = sprite.UpdateConnector("NEXT", new AtlasConnector("attachment", 80, 10));

        await Assert.That(updated.Connectors).IsEquivalentTo([
            new AtlasConnector("anchor", 0, 20),
            new AtlasConnector("attachment", 80, 10),
        ]);
    }

    [Test]
    public async Task Sprite_update_rejects_missing_and_duplicate_connector_names()
    {
        var sprite = new AtlasSprite(
            "habitat",
            new PixelRect(0, 0, 100, 40),
            new PixelRect(0, 0, 100, 40),
            [new AtlasConnector("anchor", 0, 20), new AtlasConnector("next", 99, 20)]);

        var missing = Assert.Throws<KeyNotFoundException>(() =>
            sprite.UpdateConnector("missing", new AtlasConnector("replacement", 10, 10)));
        var duplicate = Assert.Throws<ArgumentException>(() =>
            sprite.UpdateConnector("next", new AtlasConnector("anchor", 10, 10)));

        await Assert.That(missing).IsNotNull();
        await Assert.That(duplicate).IsNotNull();
    }
}
