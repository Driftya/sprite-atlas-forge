using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;
using Driftya.SpriteAtlasForge.Infrastructure;

namespace Driftya.SpriteAtlasForge.Infrastructure.Tests;

public sealed class AtlasPackerTests
{
    [Test]
    public async Task Packer_is_deterministic_bounded_and_non_overlapping()
    {
        var project = CreateProject();
        var packer = new DeterministicShelfAtlasPacker();
        var options = new AtlasPackingOptions
        {
            Padding = 2,
            MaximumWidth = 64,
            MaximumHeight = 64,
            PowerOfTwo = true,
        };

        var first = packer.Pack(project, options);
        var second = packer.Pack(project, options);

        await Assert.That(first.Size).IsEqualTo(second.Size);
        await Assert.That(first.Sprites).IsEquivalentTo(second.Sprites);
        await Assert.That(first.Size).IsEqualTo(new PixelSize(32, 64));
        await Assert.That(first.Sprites).IsEquivalentTo([
            new PackedSprite("tall", new PixelRect(2, 2, 8, 20)),
            new PackedSprite("square", new PixelRect(14, 2, 12, 12)),
            new PackedSprite("wide", new PixelRect(2, 26, 20, 8)),
        ]);
        await Assert.That(IsPowerOfTwo(first.Size.Width)).IsTrue();
        await Assert.That(IsPowerOfTwo(first.Size.Height)).IsTrue();
        foreach (var sprite in first.Sprites)
        {
            await Assert.That(sprite.Frame.FitsWithin(first.Size)).IsTrue();
        }

        for (var left = 0; left < first.Sprites.Count; left++)
        {
            for (var right = left + 1; right < first.Sprites.Count; right++)
            {
                await Assert.That(Overlaps(first.Sprites[left].Frame, first.Sprites[right].Frame)).IsFalse();
            }
        }
    }

    [Test]
    public async Task Packer_reports_when_a_sprite_exceeds_the_limits()
    {
        var project = CreateProject();
        var packer = new DeterministicShelfAtlasPacker();
        var options = new AtlasPackingOptions { Padding = 2, MaximumWidth = 8, MaximumHeight = 8 };

        var exception = Assert.Throws<InvalidOperationException>(() => packer.Pack(project, options));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("exceeds");
    }

    private static AtlasProject CreateProject()
    {
        var sprites = new[]
        {
            new AtlasSprite("wide", new PixelRect(0, 0, 20, 8), new PixelRect(0, 0, 20, 8)),
            new AtlasSprite("square", new PixelRect(24, 0, 12, 12), new PixelRect(24, 0, 12, 12)),
            new AtlasSprite("tall", new PixelRect(40, 0, 8, 20), new PixelRect(40, 0, 8, 20)),
        };
        return new AtlasProject(
            "packing",
            new AtlasSource("source.png", new PixelSize(64, 32), new string('c', 64)),
            new AtlasOutput("source.png", new PixelSize(64, 32), false),
            sprites);
    }

    private static bool IsPowerOfTwo(int value) => (value & (value - 1)) == 0;

    private static bool Overlaps(PixelRect left, PixelRect right) =>
        left.X < right.Right && left.Right > right.X &&
        left.Y < right.Bottom && left.Bottom > right.Y;
}
