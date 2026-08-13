namespace Driftya.SpriteAtlasForge.Application;

public enum AtlasDiagnosticSeverity
{
    Information,
    Warning,
    Error,
}

public sealed record AtlasDiagnostic(
    string Code,
    string Message,
    string? Path = null,
    AtlasDiagnosticSeverity Severity = AtlasDiagnosticSeverity.Error);

public sealed record AtlasValidationResult(IReadOnlyList<AtlasDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.All(diagnostic => diagnostic.Severity != AtlasDiagnosticSeverity.Error);

    public static AtlasValidationResult Valid { get; } = new(Array.Empty<AtlasDiagnostic>());
}
