using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;
using SkiaSharp;

namespace Driftya.SpriteAtlasForge.Infrastructure;

public sealed class SkiaAtlasImageComposer : IAtlasImageComposer
{
    public async Task ComposeAsync(
        string sourceImagePath,
        string outputImagePath,
        AtlasProject project,
        AtlasPackingResult packing,
        IProgress<AtlasProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sourceBytes = await File.ReadAllBytesAsync(sourceImagePath, cancellationToken).ConfigureAwait(false);
        using var sourceBitmap = SKBitmap.Decode(sourceBytes)
            ?? throw new InvalidDataException("The source PNG could not be decoded for repacking.");
        using var atlasBitmap = new SKBitmap(
            packing.Size.Width,
            packing.Size.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        using var canvas = new SKCanvas(atlasBitmap);
        canvas.Clear(SKColors.Transparent);

        var packedById = packing.Sprites.ToDictionary(sprite => sprite.Id, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < project.Sprites.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sprite = project.Sprites[index];
            var frame = packedById[sprite.Id].Frame;
            var source = sprite.SourceRegion;
            canvas.DrawBitmap(
                sourceBitmap,
                new SKRect(source.X, source.Y, source.Right, source.Bottom),
                new SKRect(frame.X, frame.Y, frame.Right, frame.Bottom),
                SKSamplingOptions.Default);
            progress?.Report(new(
                "compose",
                0.2 + (double)(index + 1) / project.Sprites.Count * 0.65,
                $"Composed sprite {index + 1} of {project.Sprites.Count}."));
        }

        canvas.Flush();
        using var image = SKImage.FromBitmap(atlasBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("SkiaSharp could not encode the repacked atlas PNG.");

        var outputFullPath = Path.GetFullPath(outputImagePath);
        var directory = Path.GetDirectoryName(outputFullPath)
            ?? throw new ArgumentException("Output image path must have a parent directory.", nameof(outputImagePath));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(outputFullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                data.SaveTo(stream);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, outputFullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
