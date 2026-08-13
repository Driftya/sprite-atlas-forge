namespace Driftya.SpriteAtlasForge.Application;

public sealed record AtlasExportResult(
    string Format,
    IReadOnlyList<string> GeneratedFiles,
    IReadOnlyList<AtlasDiagnostic> Diagnostics);
