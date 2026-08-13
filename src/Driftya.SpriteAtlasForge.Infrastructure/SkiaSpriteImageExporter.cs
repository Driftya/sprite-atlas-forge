using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;
using SkiaSharp;

namespace Driftya.SpriteAtlasForge.Infrastructure;

public sealed class SkiaSpriteImageExporter : ISpriteImageExporter
{
    public async Task ExportAsync(
        string sourceImagePath,
        string destinationPath,
        PixelRect region,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var sourceBytes = await File.ReadAllBytesAsync(sourceImagePath, cancellationToken).ConfigureAwait(false);
        using var source = SkiaPngDecoder.Decode(
            sourceBytes,
            "The atlas PNG could not be decoded for sprite export.");
        if (!region.FitsWithin(new PixelSize(source.Width, source.Height)))
        {
            throw new InvalidDataException("The selected sprite region is outside the displayed atlas image.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var crop = new SKBitmap(region.Width, region.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(crop))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(
                source,
                new SKRect(region.X, region.Y, region.Right, region.Bottom),
                new SKRect(0, 0, region.Width, region.Height),
                SKSamplingOptions.Default);
            canvas.Flush();
        }

        using var image = SKImage.FromBitmap(crop);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("SkiaSharp could not encode the selected sprite PNG.");

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The destination must have a parent directory.", nameof(destinationPath));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
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

            File.Move(temporaryPath, fullPath, overwrite: true);
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
