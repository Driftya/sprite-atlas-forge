using System.Globalization;

namespace Driftya.SpriteAtlasForge.Domain;

public enum AtlasPropertyKind
{
    Null,
    String,
    Number,
    Boolean,
}

public sealed record AtlasPropertyValue
{
    private AtlasPropertyValue(AtlasPropertyKind kind, string? value)
    {
        Kind = kind;
        Value = value;
    }

    public AtlasPropertyKind Kind { get; }

    public string? Value { get; }

    public static AtlasPropertyValue Null() => new(AtlasPropertyKind.Null, null);

    public static AtlasPropertyValue FromString(string value) =>
        new(AtlasPropertyKind.String, value ?? throw new ArgumentNullException(nameof(value)));

    public static AtlasPropertyValue FromNumber(decimal value) =>
        new(AtlasPropertyKind.Number, value.ToString(CultureInfo.InvariantCulture));

    public static AtlasPropertyValue FromBoolean(bool value) =>
        new(AtlasPropertyKind.Boolean, value ? "true" : "false");
}
