namespace Driftya.SpriteAtlasForge.Domain;

internal static class AtlasAssetPath
{
    public static string Normalize(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Asset path cannot be empty.", parameterName);
        }

        var normalized = value.Trim().Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (normalized.StartsWith('/') ||
            normalized.Contains(':', StringComparison.Ordinal) ||
            segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Asset paths must be relative and cannot traverse parent directories.", parameterName);
        }

        return string.Join('/', segments);
    }
}
