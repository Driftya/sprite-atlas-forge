using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Application;

public interface IAtlasProjectStore
{
    Task<AtlasProject> LoadAsync(string projectPath, CancellationToken cancellationToken = default);

    Task SaveAsync(AtlasProject project, string projectPath, CancellationToken cancellationToken = default);
}
