using System;

namespace Driftya.SpriteAtlasForge.Application.Tests;

public sealed class SpriteDetectionOptionsTests
{
    [Test]
    public void Validate_accepts_supported_background_boundaries()
    {
        new SpriteDetectionOptions
        {
            BackgroundMode = SpriteBackgroundMode.BorderConnected,
            BackgroundColorTolerance = byte.MaxValue,
        }.Validate();
    }

    [Test]
    public void Validate_rejects_an_unknown_background_mode()
    {
        var options = new SpriteDetectionOptions
        {
            BackgroundMode = (SpriteBackgroundMode)int.MaxValue,
        };

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    [Test]
    public void Validate_rejects_background_tolerance_outside_byte_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpriteDetectionOptions
        {
            BackgroundColorTolerance = -1,
        }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpriteDetectionOptions
        {
            BackgroundColorTolerance = byte.MaxValue + 1,
        }.Validate());
    }
}
