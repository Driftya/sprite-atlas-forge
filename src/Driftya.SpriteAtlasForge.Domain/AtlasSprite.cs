using System.Collections.ObjectModel;

namespace Driftya.SpriteAtlasForge.Domain;

public sealed record AtlasSprite
{
    public AtlasSprite(
        string id,
        PixelRect sourceRegion,
        PixelRect frame,
        IEnumerable<AtlasConnector>? connectors = null,
        IEnumerable<string>? tags = null,
        IReadOnlyDictionary<string, AtlasPropertyValue>? properties = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Sprite ID cannot be empty.", nameof(id));
        }

        Id = id.Trim();
        SourceRegion = sourceRegion;
        Frame = frame;

        var connectorArray = connectors?.ToArray() ?? [];
        var duplicateConnector = connectorArray
            .GroupBy(connector => connector.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateConnector is not null)
        {
            throw new ArgumentException($"Connector name '{duplicateConnector.Key}' is duplicated.", nameof(connectors));
        }

        var outsideConnector = connectorArray.FirstOrDefault(connector =>
            !sourceRegion.ContainsLocalPoint(connector.X, connector.Y));

        if (outsideConnector is not null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(connectors),
                $"Connector '{outsideConnector.Name}' is outside the sprite's logical bounds.");
        }

        Connectors = Array.AsReadOnly(connectorArray);

        var tagArray = (tags ?? [])
            .Select(tag => tag?.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Tags = Array.AsReadOnly(tagArray);

        var propertyMap = new SortedDictionary<string, AtlasPropertyValue>(StringComparer.Ordinal);
        foreach (var property in properties ?? new Dictionary<string, AtlasPropertyValue>())
        {
            if (string.IsNullOrWhiteSpace(property.Key))
            {
                throw new ArgumentException("Property names cannot be empty.", nameof(properties));
            }

            propertyMap.Add(
                property.Key.Trim(),
                property.Value ?? throw new ArgumentException("Property values cannot be null.", nameof(properties)));
        }

        Properties = new ReadOnlyDictionary<string, AtlasPropertyValue>(propertyMap);
    }

    public string Id { get; }

    public PixelRect SourceRegion { get; }

    public PixelRect Frame { get; }

    public IReadOnlyList<AtlasConnector> Connectors { get; }

    public IReadOnlyList<string> Tags { get; }

    public IReadOnlyDictionary<string, AtlasPropertyValue> Properties { get; }

    public AtlasSprite AddConnector(AtlasConnector connector) =>
        new(Id, SourceRegion, Frame, Connectors.Append(connector), Tags, Properties);

    public AtlasSprite UpdateConnector(string currentName, AtlasConnector connector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentName);
        ArgumentNullException.ThrowIfNull(connector);

        if (!Connectors.Any(candidate =>
                string.Equals(candidate.Name, currentName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new KeyNotFoundException($"Connector '{currentName}' was not found on sprite '{Id}'.");
        }

        return new(
            Id,
            SourceRegion,
            Frame,
            Connectors.Select(candidate =>
                string.Equals(candidate.Name, currentName, StringComparison.OrdinalIgnoreCase)
                    ? connector
                    : candidate),
            Tags,
            Properties);
    }

    public AtlasSprite UpdateRegion(PixelRect sourceRegion, PixelRect frame) =>
        new(Id, sourceRegion, frame, Connectors, Tags, Properties);

    public AtlasSprite RemoveConnector(string name) =>
        new(
            Id,
            SourceRegion,
            Frame,
            Connectors.Where(connector => !string.Equals(connector.Name, name, StringComparison.OrdinalIgnoreCase)),
            Tags,
            Properties);
}
