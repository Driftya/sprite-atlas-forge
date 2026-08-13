using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Domain.Tests;

public sealed class ValueObjectTests
{
    [Test]
    public async Task Pixel_rectangle_reports_geometry_and_clamps_expansion()
    {
        var rectangle = new PixelRect(2, 3, 4, 5);

        await Assert.That(rectangle.Right).IsEqualTo(6);
        await Assert.That(rectangle.Bottom).IsEqualTo(8);
        await Assert.That(rectangle.Area).IsEqualTo(20);
        await Assert.That(rectangle.FitsWithin(new PixelSize(6, 8))).IsTrue();
        await Assert.That(rectangle.ContainsLocalPoint(4, 5)).IsTrue();
        await Assert.That(rectangle.ContainsLocalPoint(5, 5)).IsFalse();
        await Assert.That(rectangle.Expand(4, new PixelSize(8, 9)))
            .IsEqualTo(new PixelRect(0, 0, 8, 9));
        await Assert.That(rectangle.Union(new PixelRect(0, 7, 3, 2)))
            .IsEqualTo(new PixelRect(0, 3, 6, 6));
    }

    [Test]
    public async Task Pixel_rectangle_rejects_negative_values_and_arithmetic_overflow()
    {
        var negative = Assert.Throws<ArgumentOutOfRangeException>(() => new PixelRect(-1, 0, 1, 1));
        var overflow = Assert.Throws<OverflowException>(() => new PixelRect(int.MaxValue, 0, 1, 1));
        var padding = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PixelRect(0, 0, 1, 1).Expand(-1, new PixelSize(1, 1)));

        await Assert.That(negative).IsNotNull();
        await Assert.That(overflow).IsNotNull();
        await Assert.That(padding).IsNotNull();
    }

    [Test]
    public async Task Property_values_preserve_explicit_JSON_kinds_and_invariant_numbers()
    {
        var values = new[]
        {
            AtlasPropertyValue.Null(),
            AtlasPropertyValue.FromString("Middle"),
            AtlasPropertyValue.FromNumber(1.25m),
            AtlasPropertyValue.FromBoolean(true),
            AtlasPropertyValue.FromBoolean(false),
        };

        await Assert.That(values[0].Kind).IsEqualTo(AtlasPropertyKind.Null);
        await Assert.That(values[0].Value).IsNull();
        await Assert.That(values[1].Value).IsEqualTo("Middle");
        await Assert.That(values[2].Value).IsEqualTo("1.25");
        await Assert.That(values[3].Value).IsEqualTo("true");
        await Assert.That(values[4].Value).IsEqualTo("false");
        await Assert.That(Assert.Throws<ArgumentNullException>(() => AtlasPropertyValue.FromString(null!)))
            .IsNotNull();
    }

    [Test]
    public async Task Packing_metadata_validates_and_normalizes_reproducibility_options()
    {
        var metadata = new AtlasPackingMetadata(" shelf-v1 ", 2, true, 1024, 512);

        await Assert.That(metadata.Algorithm).IsEqualTo("shelf-v1");
        await Assert.That(metadata.Padding).IsEqualTo(2);
        await Assert.That(metadata.PowerOfTwo).IsTrue();
        await Assert.That(metadata.MaximumWidth).IsEqualTo(1024);
        await Assert.That(metadata.MaximumHeight).IsEqualTo(512);
        await Assert.That(Assert.Throws<ArgumentException>(() =>
            new AtlasPackingMetadata(" ", 0, false, 1, 1))).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AtlasPackingMetadata("shelf", -1, false, 1, 1))).IsNotNull();
    }

    [Test]
    public async Task Project_lookup_and_replacement_are_case_insensitive_and_preserve_other_sprites()
    {
        var first = new AtlasSprite("First", new PixelRect(0, 0, 4, 4), new PixelRect(0, 0, 4, 4));
        var second = new AtlasSprite("second", new PixelRect(4, 0, 4, 4), new PixelRect(4, 0, 4, 4));
        var project = new AtlasProject(
            "atlas",
            new AtlasSource("source.png", new PixelSize(8, 4), new string('A', 64)),
            new AtlasOutput("source.png", new PixelSize(8, 4), false),
            [first, second]);
        var replacement = first.AddConnector(new AtlasConnector("next", 4, 2));

        var updated = project.ReplaceSprite(replacement);

        await Assert.That(project.GetSprite("FIRST")).IsEqualTo(first);
        await Assert.That(updated.GetSprite("first").Connectors).Count().IsEqualTo(1);
        await Assert.That(updated.GetSprite("SECOND")).IsEqualTo(second);
        await Assert.That(Assert.Throws<KeyNotFoundException>(() => project.GetSprite("missing"))).IsNotNull();
    }

    [Test]
    public async Task Assets_and_outputs_enforce_portable_consistent_metadata()
    {
        var source = new AtlasSource(" source\\modules.png ", new PixelSize(8, 8), new string('A', 64));
        var packing = new AtlasPackingMetadata("shelf", 1, false, 16, 16);
        var output = new AtlasOutput("atlas.png", new PixelSize(8, 8), true, packing);

        await Assert.That(source.Image).IsEqualTo("source/modules.png");
        await Assert.That(source.Sha256).IsEqualTo(new string('a', 64));
        await Assert.That(output.Packing).IsEqualTo(packing);
        await Assert.That(Assert.Throws<ArgumentException>(() =>
            new AtlasSource("C:/source.png", new PixelSize(1, 1), new string('a', 64)))).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentException>(() =>
            new AtlasSource("source.png", new PixelSize(1, 1), "not-a-hash"))).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentException>(() =>
            new AtlasOutput("atlas.png", new PixelSize(1, 1), false, packing))).IsNotNull();
    }
}
