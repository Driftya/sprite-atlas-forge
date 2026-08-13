using System.Text.Json;
using System.Text.Json.Serialization;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Infrastructure;

public sealed class PhaserJsonHashExporter : IAtlasExporter
{
    public const string FormatIdentifier = "phaser-json-hash";

    private static readonly JsonSerializerOptions SerializerOptions = JsonDefaults.Create();

    public string Format => FormatIdentifier;

    public async Task<AtlasExportResult> ExportAsync(
        AtlasProject project,
        string projectPath,
        string outputDirectory,
        IProgress<AtlasProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var outputFullPath = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputFullPath);
        var baseName = ExportFileSupport.GetProjectBaseName(projectPath);
        var jsonPath = Path.Combine(outputFullPath, baseName + ".phaser.json");
        var sourceImagePath = ExportFileSupport.ResolveProjectAsset(projectPath, project.Atlas.Image);
        var imageFileName = Path.GetFileName(project.Atlas.Image);
        var imagePath = Path.Combine(outputFullPath, imageFileName);
        var temporaryJsonPath = Path.Combine(outputFullPath, $".{Path.GetFileName(jsonPath)}.{Guid.NewGuid():N}.tmp");

        progress?.Report(new("export", 0.1, "Building Phaser JSON Hash metadata."));
        var document = PhaserDocument.FromDomain(project, imageFileName);

        try
        {
            await using (var stream = new FileStream(
                temporaryJsonPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryJsonPath, jsonPath, overwrite: true);
            progress?.Report(new("export", 0.7, "Copying atlas image."));
            await ExportFileSupport.CopyAtomicallyAsync(sourceImagePath, imagePath, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(temporaryJsonPath))
            {
                File.Delete(temporaryJsonPath);
            }
        }

        progress?.Report(new("complete", 1, "Phaser export complete."));
        return new AtlasExportResult(
            Format,
            [jsonPath, imagePath],
            [new AtlasDiagnostic(
                "SAF2001",
                "Connectors, tags, and custom properties are emitted as additional per-frame fields.",
                Severity: AtlasDiagnosticSeverity.Information)]);
    }

    private sealed class PhaserDocument
    {
        [JsonPropertyOrder(0)]
        public SortedDictionary<string, PhaserFrame> Frames { get; init; } = new(StringComparer.Ordinal);

        [JsonPropertyOrder(1)]
        public required PhaserMeta Meta { get; init; }

        public static PhaserDocument FromDomain(AtlasProject project, string imageFileName)
        {
            var frames = new SortedDictionary<string, PhaserFrame>(StringComparer.Ordinal);
            foreach (var sprite in project.Sprites.OrderBy(sprite => sprite.Id, StringComparer.Ordinal))
            {
                frames.Add(sprite.Id, PhaserFrame.FromDomain(sprite));
            }

            return new PhaserDocument
            {
                Frames = frames,
                Meta = new PhaserMeta
                {
                    App = "Sprite Atlas Forge",
                    Version = AtlasFormat.CurrentVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Image = imageFileName,
                    Format = "RGBA8888",
                    Size = new PhaserSize { Width = project.Atlas.Size.Width, Height = project.Atlas.Size.Height },
                    Scale = "1",
                },
            };
        }
    }

    private sealed class PhaserFrame
    {
        [JsonPropertyOrder(0)]
        public required PhaserRect Frame { get; init; }

        [JsonPropertyOrder(1)]
        public bool Rotated { get; init; }

        [JsonPropertyOrder(2)]
        public bool Trimmed { get; init; }

        [JsonPropertyOrder(3)]
        public required PhaserRect SpriteSourceSize { get; init; }

        [JsonPropertyOrder(4)]
        public required PhaserSize SourceSize { get; init; }

        [JsonPropertyOrder(5)]
        public required IReadOnlyList<PhaserConnector> Connectors { get; init; }

        [JsonPropertyOrder(6)]
        public required IReadOnlyList<string> Tags { get; init; }

        [JsonPropertyOrder(7)]
        public required SortedDictionary<string, JsonElement> Properties { get; init; }

        public static PhaserFrame FromDomain(AtlasSprite sprite) => new()
        {
            Frame = PhaserRect.FromDomain(sprite.Frame),
            Rotated = false,
            Trimmed = false,
            SpriteSourceSize = new PhaserRect
            {
                X = 0,
                Y = 0,
                Width = sprite.SourceRegion.Width,
                Height = sprite.SourceRegion.Height,
            },
            SourceSize = new PhaserSize
            {
                Width = sprite.SourceRegion.Width,
                Height = sprite.SourceRegion.Height,
            },
            Connectors = sprite.Connectors
                .Select(connector => new PhaserConnector
                {
                    Name = connector.Name,
                    X = connector.X,
                    Y = connector.Y,
                })
                .ToArray(),
            Tags = sprite.Tags.Order(StringComparer.Ordinal).ToArray(),
            Properties = new SortedDictionary<string, JsonElement>(
                sprite.Properties.ToDictionary(
                    property => property.Key,
                    property => JsonDefaults.ToJsonElement(property.Value),
                    StringComparer.Ordinal),
                StringComparer.Ordinal),
        };
    }

    private sealed class PhaserRect
    {
        public int X { get; init; }

        public int Y { get; init; }

        [JsonPropertyName("w")]
        public int Width { get; init; }

        [JsonPropertyName("h")]
        public int Height { get; init; }

        public static PhaserRect FromDomain(PixelRect rectangle) => new()
        {
            X = rectangle.X,
            Y = rectangle.Y,
            Width = rectangle.Width,
            Height = rectangle.Height,
        };
    }

    private sealed class PhaserSize
    {
        [JsonPropertyName("w")]
        public int Width { get; init; }

        [JsonPropertyName("h")]
        public int Height { get; init; }
    }

    private sealed class PhaserConnector
    {
        public required string Name { get; init; }

        public int X { get; init; }

        public int Y { get; init; }
    }

    private sealed class PhaserMeta
    {
        public required string App { get; init; }

        public required string Version { get; init; }

        public required string Image { get; init; }

        public required string Format { get; init; }

        public required PhaserSize Size { get; init; }

        public required string Scale { get; init; }
    }
}
