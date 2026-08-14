using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Infrastructure;

public sealed class GodotAtlasTextureExporter : IAtlasExporter
{
    public const string FormatIdentifier = "godot-4-atlas-textures";

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
        var sourceImagePath = ExportFileSupport.ResolveProjectAsset(projectPath, project.Atlas.Image);
        var imageFileName = Path.GetFileName(project.Atlas.Image);
        var imagePath = Path.Combine(outputFullPath, imageFileName);
        var baseName = ExportFileSupport.GetProjectBaseName(projectPath);
        var generatedFiles = new List<string>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var approvedSprites = project.Sprites
            .Where(sprite => sprite.IsApproved)
            .OrderBy(sprite => sprite.Id, StringComparer.Ordinal)
            .ToArray();

        progress?.Report(new("export", 0.1, "Building Godot 4 AtlasTexture resources."));
        for (var index = 0; index < approvedSprites.Length; index++)
        {
            var sprite = approvedSprites[index];
            var fileName = GetResourceFileName(baseName, sprite.Id, usedNames);
            var resourcePath = Path.Combine(outputFullPath, fileName);
            await ExportFileSupport.WriteTextAtomicallyAsync(
                resourcePath,
                BuildResource(imageFileName, sprite.Frame),
                cancellationToken).ConfigureAwait(false);
            generatedFiles.Add(resourcePath);
            progress?.Report(new(
                "export",
                0.1 + (0.6 * (index + 1) / Math.Max(1, approvedSprites.Length)),
                $"Wrote Godot resource for '{sprite.Id}'."));
        }

        await ExportFileSupport.CopyAtomicallyAsync(sourceImagePath, imagePath, cancellationToken)
            .ConfigureAwait(false);
        generatedFiles.Add(imagePath);
        progress?.Report(new("complete", 1, "Godot AtlasTexture export complete."));

        return new AtlasExportResult(
            Format,
            generatedFiles,
            [new AtlasDiagnostic(
                "SAF2201",
                "Godot AtlasTexture resources contain approved frame regions; connectors, tags, properties, and metadata remain in the native project.",
                Severity: AtlasDiagnosticSeverity.Information)]);
    }

    internal static string BuildResource(string imageFileName, PixelRect frame) =>
        "[gd_resource type=\"AtlasTexture\" load_steps=2 format=3]\n\n" +
        $"[ext_resource type=\"Texture2D\" path=\"{Escape(imageFileName)}\" id=\"1_atlas\"]\n\n" +
        "[resource]\n" +
        "atlas = ExtResource(\"1_atlas\")\n" +
        $"region = Rect2({FormatInteger(frame.X)}, {FormatInteger(frame.Y)}, {FormatInteger(frame.Width)}, {FormatInteger(frame.Height)})\n" +
        "filter_clip = true\n";

    private static string GetResourceFileName(string baseName, string spriteId, ISet<string> usedNames)
    {
        var safeId = new string(spriteId.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_').ToArray());
        var candidate = $"{baseName}.{safeId}.tres";
        if (usedNames.Add(candidate))
        {
            return candidate;
        }

        var suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(spriteId)))
            .ToLowerInvariant()[..8];
        candidate = $"{baseName}.{safeId}.{suffix}.tres";
        usedNames.Add(candidate);
        return candidate;
    }

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string FormatInteger(int value) => value.ToString(CultureInfo.InvariantCulture);
}
