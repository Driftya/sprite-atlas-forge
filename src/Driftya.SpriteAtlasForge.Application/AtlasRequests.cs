namespace Driftya.SpriteAtlasForge.Application;

public sealed record DetectAtlasRequest(
    string SourceImagePath,
    string ProjectPath,
    string? Name = null,
    SpriteDetectionOptions? Options = null);

public sealed record AddConnectorRequest(
    string ProjectPath,
    string SpriteId,
    string Name,
    int X,
    int Y);

public sealed record RemoveConnectorRequest(
    string ProjectPath,
    string SpriteId,
    string Name);

public sealed record UpdateConnectorRequest(
    string ProjectPath,
    string SpriteId,
    string CurrentName,
    string Name,
    int X,
    int Y);

public sealed record RenameSpriteRequest(
    string ProjectPath,
    string SpriteId,
    string NewId);

public sealed record ExportAtlasRequest(
    string ProjectPath,
    string Format,
    string OutputDirectory);

public sealed record RepackAtlasRequest(
    string ProjectPath,
    string OutputDirectory,
    AtlasPackingOptions? Options = null);

public sealed record AtlasProgress(string Stage, double Fraction, string Message);
