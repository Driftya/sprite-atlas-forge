namespace Driftya.SpriteAtlasForge.ClientApplication.Services;

public static class WorkspaceFileTypeRules
{
    public static IReadOnlyList<string> PngExtensions { get; } = Validate([".png"]);

    public static IReadOnlyList<string> NativeProjectExtensions(string nativeProjectExtension) =>
        Validate([nativeProjectExtension]);

    public static string EnsureExtension(string fileName, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var validatedExtension = Validate([extension])[0];
        return fileName.EndsWith(validatedExtension, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : fileName + validatedExtension;
    }

    public static IReadOnlyList<string> Validate(IEnumerable<string> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        var validated = extensions.Select(extension => extension?.Trim() ?? string.Empty).ToArray();
        if (validated.Length == 0 || validated.Any(extension =>
                string.IsNullOrWhiteSpace(extension) ||
                !extension.StartsWith(".", StringComparison.Ordinal) ||
                extension.Contains('*') ||
                extension.Contains('?')))
        {
            throw new ArgumentException(
                "File extensions must start with '.' and cannot contain wildcard characters.",
                nameof(extensions));
        }

        return Array.AsReadOnly(validated);
    }
}
