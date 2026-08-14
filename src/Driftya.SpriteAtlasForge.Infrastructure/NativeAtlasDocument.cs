using System.Text.Json;
using System.Text.Json.Serialization;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Infrastructure;

internal sealed class NativeAtlasDocument
{
    [JsonPropertyOrder(0)]
    public int FormatVersion { get; set; }

    [JsonPropertyOrder(1)]
    public string? Name { get; set; }

    [JsonPropertyOrder(2)]
    public NativeSourceDocument? Source { get; set; }

    [JsonPropertyOrder(3)]
    public NativeAtlasOutputDocument? Atlas { get; set; }

    [JsonPropertyOrder(4)]
    public List<NativeSpriteDocument>? Sprites { get; set; }

    public static NativeAtlasDocument FromDomain(AtlasProject project) => new()
    {
        FormatVersion = project.FormatVersion,
        Name = project.Name,
        Source = new NativeSourceDocument
        {
            Image = project.Source.Image,
            Width = project.Source.Size.Width,
            Height = project.Source.Size.Height,
            Sha256 = project.Source.Sha256,
        },
        Atlas = new NativeAtlasOutputDocument
        {
            Image = project.Atlas.Image,
            Width = project.Atlas.Size.Width,
            Height = project.Atlas.Size.Height,
            Repacked = project.Atlas.Repacked,
            Packing = project.Atlas.Packing is null ? null : new NativePackingDocument
            {
                Algorithm = project.Atlas.Packing.Algorithm,
                Padding = project.Atlas.Packing.Padding,
                PowerOfTwo = project.Atlas.Packing.PowerOfTwo,
                MaximumWidth = project.Atlas.Packing.MaximumWidth,
                MaximumHeight = project.Atlas.Packing.MaximumHeight,
            },
        },
        Sprites = project.Sprites
            .OrderBy(sprite => sprite.SourceRegion.Y)
            .ThenBy(sprite => sprite.SourceRegion.X)
            .ThenBy(sprite => sprite.Id, StringComparer.Ordinal)
            .Select(NativeSpriteDocument.FromDomain)
            .ToList(),
    };

    public AtlasProject ToDomain()
    {
        if (FormatVersion != AtlasFormat.CurrentVersion)
        {
            throw new AtlasProjectFormatException(
                $"Unsupported formatVersion {FormatVersion}; this build supports version {AtlasFormat.CurrentVersion}.",
                "formatVersion");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new AtlasProjectFormatException("The project name is required.", "name");
        }

        if (Source is null)
        {
            throw new AtlasProjectFormatException("The source object is required.", "source");
        }

        if (Atlas is null)
        {
            throw new AtlasProjectFormatException("The atlas object is required.", "atlas");
        }

        if (Sprites is null)
        {
            throw new AtlasProjectFormatException("The sprites array is required.", "sprites");
        }

        try
        {
            return new AtlasProject(
                Name,
                Source.ToDomain(),
                Atlas.ToDomain(),
                Sprites.Select((sprite, index) => sprite.ToDomain(index)),
                FormatVersion);
        }
        catch (AtlasProjectFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new AtlasProjectFormatException(exception.Message, innerException: exception);
        }
    }
}

internal sealed class NativeSourceDocument
{
    public string? Image { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public string? Sha256 { get; set; }

    public AtlasSource ToDomain()
    {
        if (Image is null || Sha256 is null)
        {
            throw new AtlasProjectFormatException("Source image and sha256 are required.", "source");
        }

        return new AtlasSource(Image, new PixelSize(Width, Height), Sha256);
    }
}

internal sealed class NativeAtlasOutputDocument
{
    public string? Image { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool Repacked { get; set; }

    public NativePackingDocument? Packing { get; set; }

    public AtlasOutput ToDomain()
    {
        if (Image is null)
        {
            throw new AtlasProjectFormatException("Atlas image is required.", "atlas.image");
        }

        return new AtlasOutput(Image, new PixelSize(Width, Height), Repacked, Packing?.ToDomain());
    }
}

internal sealed class NativePackingDocument
{
    public string? Algorithm { get; set; }

    public int Padding { get; set; }

    public bool PowerOfTwo { get; set; }

    public int MaximumWidth { get; set; }

    public int MaximumHeight { get; set; }

    public AtlasPackingMetadata ToDomain()
    {
        if (Algorithm is null)
        {
            throw new AtlasProjectFormatException("Packing algorithm is required.", "atlas.packing.algorithm");
        }

        return new AtlasPackingMetadata(Algorithm, Padding, PowerOfTwo, MaximumWidth, MaximumHeight);
    }
}

internal sealed class NativeSpriteDocument
{
    [JsonPropertyOrder(0)]
    public string? Id { get; set; }

    [JsonPropertyOrder(1)]
    public NativeRectDocument? SourceRegion { get; set; }

    [JsonPropertyOrder(2)]
    public NativeRectDocument? Frame { get; set; }

    [JsonPropertyOrder(3)]
    public List<NativeConnectorDocument> Connectors { get; set; } = [];

    [JsonPropertyOrder(4)]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyOrder(5)]
    public SortedDictionary<string, JsonElement> Properties { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyOrder(6)]
    public bool IsApproved { get; set; }

    [JsonPropertyOrder(7)]
    public SortedDictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);

    public static NativeSpriteDocument FromDomain(AtlasSprite sprite) => new()
    {
        Id = sprite.Id,
        SourceRegion = NativeRectDocument.FromDomain(sprite.SourceRegion),
        Frame = NativeRectDocument.FromDomain(sprite.Frame),
        Connectors = sprite.Connectors.Select(connector => new NativeConnectorDocument
        {
            Name = connector.Name,
            X = connector.X,
            Y = connector.Y,
        }).ToList(),
        Tags = sprite.Tags.Order(StringComparer.Ordinal).ToList(),
        Properties = new SortedDictionary<string, JsonElement>(
            sprite.Properties.ToDictionary(
                property => property.Key,
                property => JsonDefaults.ToJsonElement(property.Value),
                StringComparer.Ordinal),
            StringComparer.Ordinal),
        IsApproved = sprite.IsApproved,
        Metadata = new SortedDictionary<string, string>(
            sprite.Metadata.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
            StringComparer.Ordinal),
    };

    public AtlasSprite ToDomain(int index)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new AtlasProjectFormatException("Sprite ID is required.", $"sprites[{index}].id");
        }

        if (SourceRegion is null || Frame is null)
        {
            throw new AtlasProjectFormatException(
                "Sprite sourceRegion and frame are required.",
                $"sprites[{index}]");
        }

        try
        {
            return new AtlasSprite(
                Id,
                SourceRegion.ToDomain(),
                Frame.ToDomain(),
                Connectors.Select(connector => connector.ToDomain()),
                Tags,
                Properties.ToDictionary(
                    property => property.Key,
                    property => JsonDefaults.FromJsonElement(property.Value),
                    StringComparer.Ordinal),
                IsApproved,
                Metadata);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new AtlasProjectFormatException(exception.Message, $"sprites[{index}]", exception);
        }
    }
}

internal sealed class NativeRectDocument
{
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public static NativeRectDocument FromDomain(PixelRect rectangle) => new()
    {
        X = rectangle.X,
        Y = rectangle.Y,
        Width = rectangle.Width,
        Height = rectangle.Height,
    };

    public PixelRect ToDomain() => new(X, Y, Width, Height);
}

internal sealed class NativeConnectorDocument
{
    public string? Name { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public AtlasConnector ToDomain() => new(
        Name ?? throw new AtlasProjectFormatException("Connector name is required."),
        X,
        Y);
}

internal static class JsonDefaults
{
    public static JsonSerializerOptions Create() => new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        IndentCharacter = ' ',
        IndentSize = 2,
        NumberHandling = JsonNumberHandling.Strict,
    };

    public static JsonElement ToJsonElement(AtlasPropertyValue value) => value.Kind switch
    {
        AtlasPropertyKind.Null => JsonSerializer.SerializeToElement<object?>(null),
        AtlasPropertyKind.String => JsonSerializer.SerializeToElement(value.Value),
        AtlasPropertyKind.Number => JsonSerializer.SerializeToElement(decimal.Parse(
            value.Value!,
            System.Globalization.CultureInfo.InvariantCulture)),
        AtlasPropertyKind.Boolean => JsonSerializer.SerializeToElement(bool.Parse(value.Value!)),
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static AtlasPropertyValue FromJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => AtlasPropertyValue.Null(),
        JsonValueKind.String => AtlasPropertyValue.FromString(element.GetString()!),
        JsonValueKind.Number => AtlasPropertyValue.FromNumber(element.GetDecimal()),
        JsonValueKind.True => AtlasPropertyValue.FromBoolean(true),
        JsonValueKind.False => AtlasPropertyValue.FromBoolean(false),
        _ => throw new AtlasProjectFormatException(
            "Custom properties support only null, string, number, and boolean values in format version 1."),
    };
}
