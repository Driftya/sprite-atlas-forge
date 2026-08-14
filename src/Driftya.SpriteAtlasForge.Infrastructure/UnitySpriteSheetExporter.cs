using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Infrastructure;

public sealed class UnitySpriteSheetExporter : IAtlasExporter
{
    public const string FormatIdentifier = "unity-6-spritesheet";

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
        var metadataPath = imagePath + ".meta";

        progress?.Report(new("export", 0.1, "Building Unity 6 sprite import metadata."));
        var metadata = BuildMetadata(project, imageFileName);
        await ExportFileSupport.WriteTextAtomicallyAsync(metadataPath, metadata, cancellationToken)
            .ConfigureAwait(false);
        progress?.Report(new("export", 0.7, "Copying atlas image."));
        await ExportFileSupport.CopyAtomicallyAsync(sourceImagePath, imagePath, cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(new("complete", 1, "Unity sprite-sheet export complete."));
        return new AtlasExportResult(
            Format,
            [metadataPath, imagePath],
            [new AtlasDiagnostic(
                "SAF2101",
                "Unity imports approved frames as multiple sprites; connectors, tags, properties, and metadata remain in the native project.",
                Severity: AtlasDiagnosticSeverity.Information)]);
    }

    internal static string BuildMetadata(AtlasProject project, string imageFileName)
    {
        var sprites = project.Sprites
            .Where(sprite => sprite.IsApproved)
            .OrderBy(sprite => sprite.Id, StringComparer.Ordinal)
            .ToArray();
        var guid = HexHash($"Sprite Atlas Forge\n{project.Name}\n{imageFileName}", 32);
        var builder = new StringBuilder();
        builder.AppendLine("fileFormatVersion: 2");
        builder.Append("guid: ").AppendLine(guid);
        builder.AppendLine("TextureImporter:");
        builder.AppendLine(sprites.Length == 0 ? "  internalIDToNameTable: []" : "  internalIDToNameTable:");
        for (var index = 0; index < sprites.Length; index++)
        {
            builder.AppendLine("  - first:");
            builder.Append("      213: ").AppendLine(InternalId(index).ToString(CultureInfo.InvariantCulture));
            builder.Append("    second: ").AppendLine(Quote(sprites[index].Id));
        }

        builder.AppendLine("  externalObjects: {}");
        builder.AppendLine("  serializedVersion: 13");
        builder.AppendLine("  mipmaps:");
        builder.AppendLine("    mipMapMode: 0");
        builder.AppendLine("    enableMipMap: 0");
        builder.AppendLine("    sRGBTexture: 1");
        builder.AppendLine("  isReadable: 0");
        builder.AppendLine("  streamingMipmaps: 0");
        builder.AppendLine("  textureType: 8");
        builder.AppendLine("  textureShape: 1");
        builder.AppendLine("  singleChannelComponent: 0");
        builder.AppendLine("  flipbookRows: 1");
        builder.AppendLine("  flipbookColumns: 1");
        builder.Append("  maxTextureSize: ").AppendLine(
            GetMaxTextureSize(project.Atlas.Size).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("  textureSettings:");
        builder.AppendLine("    serializedVersion: 2");
        builder.AppendLine("    filterMode: 1");
        builder.AppendLine("    aniso: 1");
        builder.AppendLine("    mipBias: 0");
        builder.AppendLine("    wrapU: 1");
        builder.AppendLine("    wrapV: 1");
        builder.AppendLine("    wrapW: 1");
        builder.AppendLine("  nPOTScale: 0");
        builder.AppendLine("  lightmap: 0");
        builder.AppendLine("  compressionQuality: 50");
        builder.AppendLine("  spriteMode: 2");
        builder.AppendLine("  spriteExtrude: 1");
        builder.AppendLine("  spriteMeshType: 1");
        builder.AppendLine("  alignment: 0");
        builder.AppendLine("  spritePivot: {x: 0.5, y: 0.5}");
        builder.AppendLine("  spritePixelsToUnits: 100");
        builder.AppendLine("  spriteBorder: {x: 0, y: 0, z: 0, w: 0}");
        builder.AppendLine("  spriteGenerateFallbackPhysicsShape: 1");
        builder.AppendLine("  alphaUsage: 1");
        builder.AppendLine("  alphaIsTransparency: 1");
        builder.AppendLine("  spriteTessellationDetail: -1");
        builder.AppendLine("  textureFormat: 1");
        builder.AppendLine("  platformSettings: []");
        builder.AppendLine("  spriteSheet:");
        builder.AppendLine("    serializedVersion: 2");
        builder.AppendLine(sprites.Length == 0 ? "    sprites: []" : "    sprites:");
        for (var index = 0; index < sprites.Length; index++)
        {
            AppendSprite(builder, project, sprites[index], index, guid);
        }

        builder.AppendLine("    outline: []");
        builder.AppendLine("    customData:");
        builder.AppendLine("    secondaryTextures: []");
        builder.AppendLine("    nameFileIdTable:");
        for (var index = 0; index < sprites.Length; index++)
        {
            builder.Append("      ").Append(Quote(sprites[index].Id)).Append(": ")
                .AppendLine(InternalId(index).ToString(CultureInfo.InvariantCulture));
        }

        builder.AppendLine("  userData:");
        builder.AppendLine("  assetBundleName:");
        builder.AppendLine("  assetBundleVariant:");
        return builder.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void AppendSprite(
        StringBuilder builder,
        AtlasProject project,
        AtlasSprite sprite,
        int index,
        string guid)
    {
        var frame = sprite.Frame;
        var unityY = project.Atlas.Size.Height - frame.Y - frame.Height;
        builder.AppendLine("    - serializedVersion: 2");
        builder.Append("      name: ").AppendLine(Quote(sprite.Id));
        builder.AppendLine("      rect:");
        builder.AppendLine("        serializedVersion: 2");
        builder.Append("        x: ").AppendLine(frame.X.ToString(CultureInfo.InvariantCulture));
        builder.Append("        y: ").AppendLine(unityY.ToString(CultureInfo.InvariantCulture));
        builder.Append("        width: ").AppendLine(frame.Width.ToString(CultureInfo.InvariantCulture));
        builder.Append("        height: ").AppendLine(frame.Height.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("      alignment: 0");
        builder.AppendLine("      pivot: {x: 0.5, y: 0.5}");
        builder.AppendLine("      border: {x: 0, y: 0, z: 0, w: 0}");
        builder.AppendLine("      customData:");
        builder.AppendLine("      outline: []");
        builder.AppendLine("      physicsShape: []");
        builder.AppendLine("      tessellationDetail: 0");
        builder.AppendLine("      bones: []");
        builder.Append("      spriteID: ").AppendLine(HexHash($"{guid}\n{sprite.Id}", 32));
        builder.Append("      internalID: ").AppendLine(InternalId(index).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("      vertices: []");
        builder.AppendLine("      indices:");
        builder.AppendLine("      edges: []");
        builder.AppendLine("      weights: []");
    }

    private static long InternalId(int index) => 21_300_000L + (index * 2L);

    private static int GetMaxTextureSize(PixelSize size)
    {
        var required = Math.Max(size.Width, size.Height);
        var result = 32;
        while (result < required && result < 16_384)
        {
            result *= 2;
        }

        return Math.Min(result, 16_384);
    }

    private static string Quote(string value) => JsonSerializer.Serialize(value);

    private static string HexHash(string value, int length) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..length];
}
