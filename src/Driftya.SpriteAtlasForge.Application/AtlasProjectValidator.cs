using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Application;

public static class AtlasProjectValidator
{
    public static AtlasValidationResult Validate(AtlasProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var diagnostics = new List<AtlasDiagnostic>();

        for (var spriteIndex = 0; spriteIndex < project.Sprites.Count; spriteIndex++)
        {
            var sprite = project.Sprites[spriteIndex];
            var spritePath = $"sprites[{spriteIndex}]";

            if (!sprite.SourceRegion.FitsWithin(project.Source.Size))
            {
                diagnostics.Add(new(
                    "SAF1001",
                    $"Sprite '{sprite.Id}' extends beyond the source image.",
                    $"{spritePath}.sourceRegion"));
            }

            if (!sprite.Frame.FitsWithin(project.Atlas.Size))
            {
                diagnostics.Add(new(
                    "SAF1002",
                    $"Sprite '{sprite.Id}' extends beyond the atlas image.",
                    $"{spritePath}.frame"));
            }

            for (var connectorIndex = 0; connectorIndex < sprite.Connectors.Count; connectorIndex++)
            {
                var connector = sprite.Connectors[connectorIndex];
                if (!sprite.SourceRegion.ContainsLocalPoint(connector.X, connector.Y))
                {
                    diagnostics.Add(new(
                        "SAF1003",
                        $"Connector '{connector.Name}' is outside sprite '{sprite.Id}'.",
                        $"{spritePath}.connectors[{connectorIndex}]"));
                }
            }
        }

        return new AtlasValidationResult(diagnostics);
    }
}
