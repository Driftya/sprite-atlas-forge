using System.Text.Json;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;

namespace Driftya.SpriteAtlasForge.Infrastructure;

public sealed class NativeAtlasProjectStore : IAtlasProjectStore
{
    private static readonly JsonSerializerOptions SerializerOptions = JsonDefaults.Create();

    public async Task<AtlasProject> LoadAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var fullPath = Path.GetFullPath(projectPath);
        try
        {
            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var document = await JsonSerializer
                .DeserializeAsync<NativeAtlasDocument>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            return document?.ToDomain()
                ?? throw new AtlasProjectFormatException("The native atlas document is empty.");
        }
        catch (AtlasProjectFormatException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new AtlasProjectFormatException(
                $"Invalid native atlas JSON: {exception.Message}",
                exception.Path,
                exception);
        }
    }

    public async Task SaveAsync(
        AtlasProject project,
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var fullPath = Path.GetFullPath(projectPath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Project path must have a parent directory.", nameof(projectPath));
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer
                    .SerializeAsync(stream, NativeAtlasDocument.FromDomain(project), SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
