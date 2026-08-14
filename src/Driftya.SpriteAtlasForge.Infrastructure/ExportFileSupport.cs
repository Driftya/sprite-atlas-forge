namespace Driftya.SpriteAtlasForge.Infrastructure;

internal static class ExportFileSupport
{
    public static string ResolveProjectAsset(string projectPath, string relativeAssetPath)
    {
        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))
            ?? throw new ArgumentException("Project path must have a parent directory.", nameof(projectPath));
        return Path.GetFullPath(
            Path.Combine(projectDirectory, relativeAssetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    public static async Task CopyAtomicallyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var sourceFullPath = Path.GetFullPath(sourcePath);
        var destinationFullPath = Path.GetFullPath(destinationPath);
        if (string.Equals(sourceFullPath, destinationFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var directory = Path.GetDirectoryName(destinationFullPath)
            ?? throw new ArgumentException("Destination path must have a parent directory.", nameof(destinationPath));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var source = new FileStream(
                sourceFullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, destinationFullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static async Task WriteTextAtomicallyAsync(
        string destinationPath,
        string content,
        CancellationToken cancellationToken)
    {
        var destinationFullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(destinationFullPath)
            ?? throw new ArgumentException("Destination path must have a parent directory.", nameof(destinationPath));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                content,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, destinationFullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static string GetProjectBaseName(string projectPath)
    {
        var fileName = Path.GetFileName(projectPath);
        return fileName.EndsWith(Domain.AtlasFormat.NativeExtension, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^Domain.AtlasFormat.NativeExtension.Length]
            : Path.GetFileNameWithoutExtension(fileName);
    }
}
