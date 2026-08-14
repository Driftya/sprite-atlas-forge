namespace Driftya.SpriteAtlasForge.ClientApplication.Tests;

using PageModels;

public sealed class SpriteResizeSnapperTests
{
    [Test]
    public async Task Resize_snaps_a_dragged_edge_to_an_overlapping_sprite_edge()
    {
        SpriteCanvasOverlay[] overlays =
        [
            new("selected", 20, 20, 30, 30),
            new("neighbor", 60, 25, 20, 20),
        ];

        var result = SpriteResizeSnapper.Resize(
            new CanvasPixelBounds(20, 20, 30, 30),
            CanvasResizeHandle.Right,
            57,
            20,
            100,
            100,
            overlays,
            "selected",
            4);

        await Assert.That(result.Bounds).IsEqualTo(new CanvasPixelBounds(20, 20, 40, 30));
        await Assert.That(result.VerticalGuide).IsEqualTo(60);
    }

    [Test]
    public async Task Resize_ignores_nearby_edges_that_do_not_overlap_on_the_other_axis()
    {
        SpriteCanvasOverlay[] overlays =
        [
            new("selected", 20, 20, 30, 30),
            new("unrelated", 60, 70, 20, 20),
        ];

        var result = SpriteResizeSnapper.Resize(
            new CanvasPixelBounds(20, 20, 30, 30),
            CanvasResizeHandle.Right,
            57,
            20,
            100,
            100,
            overlays,
            "selected",
            4);

        await Assert.That(result.Bounds).IsEqualTo(new CanvasPixelBounds(20, 20, 37, 30));
        await Assert.That(result.VerticalGuide).IsNull();
    }

    [Test]
    public async Task Resize_snaps_corner_to_image_boundaries_and_keeps_a_nonzero_region()
    {
        var result = SpriteResizeSnapper.Resize(
            new CanvasPixelBounds(10, 10, 20, 20),
            CanvasResizeHandle.TopLeft,
            3,
            2,
            100,
            100,
            [],
            "selected",
            4);

        await Assert.That(result.Bounds).IsEqualTo(new CanvasPixelBounds(0, 0, 30, 30));
        await Assert.That(result.VerticalGuide).IsEqualTo(0);
        await Assert.That(result.HorizontalGuide).IsEqualTo(0);
    }

    [Test]
    public async Task Resize_can_snap_a_dragged_border_back_to_its_own_original_edge()
    {
        var result = SpriteResizeSnapper.Resize(
            new CanvasPixelBounds(20, 20, 30, 30),
            CanvasResizeHandle.Right,
            53,
            20,
            100,
            100,
            [new SpriteCanvasOverlay("selected", 20, 20, 30, 30)],
            "selected",
            4);

        await Assert.That(result.Bounds).IsEqualTo(new CanvasPixelBounds(20, 20, 30, 30));
        await Assert.That(result.VerticalGuide).IsEqualTo(50);
    }

    [Test]
    public async Task Resize_bypasses_snap_targets_when_snapping_is_disabled()
    {
        var result = SpriteResizeSnapper.Resize(
            new CanvasPixelBounds(20, 20, 30, 30),
            CanvasResizeHandle.Right,
            53,
            20,
            100,
            100,
            [
                new SpriteCanvasOverlay("selected", 20, 20, 30, 30),
                new SpriteCanvasOverlay("neighbor", 55, 20, 20, 30),
            ],
            "selected",
            8,
            snappingEnabled: false);

        await Assert.That(result.Bounds).IsEqualTo(new CanvasPixelBounds(20, 20, 33, 30));
        await Assert.That(result.VerticalGuide).IsNull();
        await Assert.That(result.HorizontalGuide).IsNull();
    }
}
