using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Application;

/// <summary>
/// Applies deterministic, in-memory metadata edits shared by interactive and command-line hosts.
/// </summary>
public static class AtlasProjectEditor
{
    public static AtlasProject AddSprite(AtlasProject project, AtlasSprite sprite)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sprite);
        return new(
            project.Name,
            project.Source,
            project.Atlas,
            project.Sprites.Append(sprite),
            project.FormatVersion);
    }

    public static AtlasProject RemoveSprite(AtlasProject project, string spriteId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var sprite = project.GetSprite(spriteId);
        return new(
            project.Name,
            project.Source,
            project.Atlas,
            project.Sprites.Where(candidate =>
                !string.Equals(candidate.Id, sprite.Id, StringComparison.OrdinalIgnoreCase)),
            project.FormatVersion);
    }

    public static AtlasProject RenameSprite(AtlasProject project, string spriteId, string newId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var sprite = project.GetSprite(spriteId);
        var renamed = new AtlasSprite(
            newId,
            sprite.SourceRegion,
            sprite.Frame,
            sprite.Connectors,
            sprite.Tags,
            sprite.Properties);

        return ReplaceSprite(project, sprite.Id, renamed);
    }

    public static AtlasProject AddConnector(
        AtlasProject project,
        string spriteId,
        AtlasConnector connector)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.ReplaceSprite(project.GetSprite(spriteId).AddConnector(connector));
    }

    public static AtlasProject UpdateConnector(
        AtlasProject project,
        string spriteId,
        string currentName,
        AtlasConnector connector)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.ReplaceSprite(project.GetSprite(spriteId).UpdateConnector(currentName, connector));
    }

    public static AtlasProject RemoveConnector(AtlasProject project, string spriteId, string name)
    {
        ArgumentNullException.ThrowIfNull(project);
        var sprite = project.GetSprite(spriteId);
        var updatedSprite = sprite.RemoveConnector(name);
        if (updatedSprite.Connectors.Count == sprite.Connectors.Count)
        {
            throw new KeyNotFoundException($"Connector '{name}' was not found on sprite '{sprite.Id}'.");
        }

        return project.ReplaceSprite(updatedSprite);
    }

    public static AtlasProject UpdateSpriteRegion(
        AtlasProject project,
        string spriteId,
        PixelRect sourceRegion)
    {
        ArgumentNullException.ThrowIfNull(project);
        var sprite = project.GetSprite(spriteId);
        var frame = project.Atlas.Repacked
            ? new PixelRect(sprite.Frame.X, sprite.Frame.Y, sourceRegion.Width, sourceRegion.Height)
            : sourceRegion;
        return project.ReplaceSprite(sprite.UpdateRegion(sourceRegion, frame));
    }

    private static AtlasProject ReplaceSprite(
        AtlasProject project,
        string currentId,
        AtlasSprite replacement) =>
        new(
            project.Name,
            project.Source,
            project.Atlas,
            project.Sprites.Select(candidate =>
                string.Equals(candidate.Id, currentId, StringComparison.OrdinalIgnoreCase)
                    ? replacement
                    : candidate),
            project.FormatVersion);
}
