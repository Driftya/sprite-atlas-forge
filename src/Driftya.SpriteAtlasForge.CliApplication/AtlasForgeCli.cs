using System.Text.Json;
using Driftya.SpriteAtlasForge.Application;
using Driftya.SpriteAtlasForge.Domain;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Driftya.SpriteAtlasForge.CliApplication;

public sealed class AtlasForgeCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        IndentCharacter = ' ',
        IndentSize = 2,
    };

    private readonly IAtlasForgeService _service;
    private readonly AtlasForgeApplicationInfo _applicationInfo;

    public AtlasForgeCli(IAtlasForgeService service, AtlasForgeApplicationInfo applicationInfo)
    {
        _service = service;
        _applicationInfo = applicationInfo;
    }

    public RootCommand CreateRootCommand()
    {
        var root = new RootCommand(_applicationInfo.Description);
        root.SetAction((ParseResult _) => WriteApplicationInfo());
        root.Subcommands.Add(CreateInfoCommand());
        root.Subcommands.Add(CreateDetectCommand());
        root.Subcommands.Add(CreateValidateCommand());
        root.Subcommands.Add(CreateConnectorCommand());
        root.Subcommands.Add(CreateSpriteCommand());
        root.Subcommands.Add(CreateRepackCommand());
        root.Subcommands.Add(CreateExportCommand());
        return root;
    }

    private Command CreateInfoCommand()
    {
        var command = new Command("info", "Show application and native-format information.");
        command.SetAction((ParseResult _) => WriteApplicationInfo());
        return command;
    }

    private Command CreateDetectCommand()
    {
        var image = new Argument<string>("image") { Description = "PNG spritesheet to inspect." };
        var output = RequiredOption("--output", "Destination .saf.json project path.");
        var name = new Option<string?>("--name") { Description = "Project name. Defaults to the PNG filename." };
        var alphaThreshold = new Option<int?>("--alpha-threshold") { Description = "Visible-pixel alpha threshold (default: 8)." };
        var minimumArea = new Option<int?>("--minimum-area") { Description = "Minimum visible pixels in a component (default: 1)." };
        var mergeDistance = new Option<int?>("--merge-distance") { Description = "Maximum gap for merging disconnected pieces (default: 0)." };
        var sourcePadding = new Option<int?>("--source-padding") { Description = "Padding added around detected regions (default: 0)." };
        var maximumWidth = new Option<int?>("--max-width") { Description = "Maximum source width (default: 16384)." };
        var maximumHeight = new Option<int?>("--max-height") { Description = "Maximum source height (default: 16384)." };
        var maximumPixels = new Option<long?>("--max-pixels") { Description = "Maximum source pixel count (default: 67108864)." };
        var json = JsonOption();
        var command = new Command("detect", "Detect sprites in a PNG and create a native atlas project.");
        command.Arguments.Add(image);
        command.Options.Add(output);
        command.Options.Add(name);
        command.Options.Add(alphaThreshold);
        command.Options.Add(minimumArea);
        command.Options.Add(mergeDistance);
        command.Options.Add(sourcePadding);
        command.Options.Add(maximumWidth);
        command.Options.Add(maximumHeight);
        command.Options.Add(maximumPixels);
        command.Options.Add(json);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(async () =>
        {
            var threshold = parseResult.GetValue(alphaThreshold) ?? 8;
            if (threshold is < byte.MinValue or > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(alphaThreshold), "Alpha threshold must be between 0 and 255.");
            }

            var options = new SpriteDetectionOptions
            {
                AlphaThreshold = (byte)threshold,
                MinimumArea = parseResult.GetValue(minimumArea) ?? 1,
                MergeDistance = parseResult.GetValue(mergeDistance) ?? 0,
                SourcePadding = parseResult.GetValue(sourcePadding) ?? 0,
                MaximumWidth = parseResult.GetValue(maximumWidth) ?? 16_384,
                MaximumHeight = parseResult.GetValue(maximumHeight) ?? 16_384,
                MaximumPixels = parseResult.GetValue(maximumPixels) ?? 67_108_864,
            };
            options.Validate();
            var request = new DetectAtlasRequest(
                parseResult.GetRequiredValue(image),
                parseResult.GetRequiredValue(output),
                parseResult.GetValue(name),
                options);
            var project = await _service.DetectAsync(request, cancellationToken: cancellationToken);
            WriteResult(
                new
                {
                    project = Path.GetFullPath(request.ProjectPath),
                    source = project.Source.Image,
                    width = project.Source.Size.Width,
                    height = project.Source.Size.Height,
                    sprites = project.Sprites.Count,
                },
                parseResult.GetValue(json),
                $"Detected {project.Sprites.Count} sprites and wrote {Path.GetFullPath(request.ProjectPath)}");
            return CliExitCode.Success;
        }));
        return command;
    }

    private Command CreateValidateCommand()
    {
        var project = ProjectArgument();
        var json = JsonOption();
        var command = new Command("validate", "Validate a native atlas project.");
        command.Arguments.Add(project);
        command.Options.Add(json);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(async () =>
        {
            var projectPath = parseResult.GetRequiredValue(project);
            var result = await _service.ValidateAsync(projectPath, cancellationToken);
            WriteResult(
                new { valid = result.IsValid, diagnostics = result.Diagnostics },
                parseResult.GetValue(json),
                result.IsValid ? $"Valid: {Path.GetFullPath(projectPath)}" : "Project validation failed.");

            if (!result.IsValid && !parseResult.GetValue(json))
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    Console.Error.WriteLine($"{diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}");
                }
            }

            return result.IsValid ? CliExitCode.Success : CliExitCode.InvalidProject;
        }));
        return command;
    }

    private Command CreateConnectorCommand()
    {
        var connector = new Command("connector", "Add, update, or remove named sprite connectors.");
        connector.Subcommands.Add(CreateConnectorAddCommand());
        connector.Subcommands.Add(CreateConnectorUpdateCommand());
        connector.Subcommands.Add(CreateConnectorRemoveCommand());
        return connector;
    }

    private Command CreateConnectorAddCommand()
    {
        var project = ProjectArgument();
        var sprite = RequiredOption("--sprite", "Sprite ID.");
        var name = RequiredOption("--name", "Unique connector name.");
        var x = new Option<int>("--x") { Description = "Sprite-local X coordinate.", Required = true };
        var y = new Option<int>("--y") { Description = "Sprite-local Y coordinate.", Required = true };
        var json = JsonOption();
        var command = new Command("add", "Add a named connector to a sprite.");
        command.Arguments.Add(project);
        command.Options.Add(sprite);
        command.Options.Add(name);
        command.Options.Add(x);
        command.Options.Add(y);
        command.Options.Add(json);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(async () =>
        {
            var request = new AddConnectorRequest(
                parseResult.GetRequiredValue(project),
                parseResult.GetRequiredValue(sprite),
                parseResult.GetRequiredValue(name),
                parseResult.GetValue(x),
                parseResult.GetValue(y));
            var updated = await _service.AddConnectorAsync(request, cancellationToken);
            var connectorCount = updated.GetSprite(request.SpriteId).Connectors.Count;
            WriteResult(
                new { sprite = request.SpriteId, connector = request.Name, connectors = connectorCount },
                parseResult.GetValue(json),
                $"Added connector '{request.Name}' to '{request.SpriteId}'.");
            return CliExitCode.Success;
        }));
        return command;
    }

    private Command CreateConnectorUpdateCommand()
    {
        var project = ProjectArgument();
        var sprite = RequiredOption("--sprite", "Sprite ID.");
        var currentName = RequiredOption("--current-name", "Current connector name.");
        var name = RequiredOption("--name", "New unique connector name.");
        var x = new Option<int>("--x") { Description = "New sprite-local X coordinate.", Required = true };
        var y = new Option<int>("--y") { Description = "New sprite-local Y coordinate.", Required = true };
        var json = JsonOption();
        var command = new Command("update", "Rename or move a named connector.");
        command.Arguments.Add(project);
        command.Options.Add(sprite);
        command.Options.Add(currentName);
        command.Options.Add(name);
        command.Options.Add(x);
        command.Options.Add(y);
        command.Options.Add(json);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(async () =>
        {
            var request = new UpdateConnectorRequest(
                parseResult.GetRequiredValue(project),
                parseResult.GetRequiredValue(sprite),
                parseResult.GetRequiredValue(currentName),
                parseResult.GetRequiredValue(name),
                parseResult.GetValue(x),
                parseResult.GetValue(y));
            await _service.UpdateConnectorAsync(request, cancellationToken);
            WriteResult(
                new
                {
                    sprite = request.SpriteId,
                    previousName = request.CurrentName,
                    connector = request.Name,
                    request.X,
                    request.Y,
                },
                parseResult.GetValue(json),
                $"Updated connector '{request.CurrentName}' to '{request.Name}' on '{request.SpriteId}'.");
            return CliExitCode.Success;
        }));
        return command;
    }

    private Command CreateConnectorRemoveCommand()
    {
        var project = ProjectArgument();
        var sprite = RequiredOption("--sprite", "Sprite ID.");
        var name = RequiredOption("--name", "Connector name.");
        var json = JsonOption();
        var command = new Command("remove", "Remove a named connector from a sprite.");
        command.Arguments.Add(project);
        command.Options.Add(sprite);
        command.Options.Add(name);
        command.Options.Add(json);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(async () =>
        {
            var request = new RemoveConnectorRequest(
                parseResult.GetRequiredValue(project),
                parseResult.GetRequiredValue(sprite),
                parseResult.GetRequiredValue(name));
            await _service.RemoveConnectorAsync(request, cancellationToken);
            WriteResult(
                new { sprite = request.SpriteId, connector = request.Name, removed = true },
                parseResult.GetValue(json),
                $"Removed connector '{request.Name}' from '{request.SpriteId}'.");
            return CliExitCode.Success;
        }));
        return command;
    }

    private Command CreateSpriteCommand()
    {
        var spriteRoot = new Command("sprite", "Edit sprite metadata.");
        var project = ProjectArgument();
        var sprite = RequiredOption("--sprite", "Current sprite ID.");
        var newId = RequiredOption("--new-id", "New unique sprite ID.");
        var json = JsonOption();
        var rename = new Command("rename", "Rename a sprite.");
        rename.Arguments.Add(project);
        rename.Options.Add(sprite);
        rename.Options.Add(newId);
        rename.Options.Add(json);
        rename.SetAction((parseResult, cancellationToken) => ExecuteAsync(async () =>
        {
            var request = new RenameSpriteRequest(
                parseResult.GetRequiredValue(project),
                parseResult.GetRequiredValue(sprite),
                parseResult.GetRequiredValue(newId));
            await _service.RenameSpriteAsync(request, cancellationToken);
            WriteResult(
                new { previousId = request.SpriteId, id = request.NewId },
                parseResult.GetValue(json),
                $"Renamed '{request.SpriteId}' to '{request.NewId}'.");
            return CliExitCode.Success;
        }));
        spriteRoot.Subcommands.Add(rename);
        spriteRoot.Subcommands.Add(CreateSpriteRegionCommand());
        return spriteRoot;
    }

    private Command CreateSpriteRegionCommand()
    {
        var project = ProjectArgument();
        var sprite = RequiredOption("--sprite", "Sprite ID.");
        var x = new Option<int>("--x") { Description = "Source X coordinate.", Required = true };
        var y = new Option<int>("--y") { Description = "Source Y coordinate.", Required = true };
        var width = new Option<int>("--width") { Description = "Region width.", Required = true };
        var height = new Option<int>("--height") { Description = "Region height.", Required = true };
        var json = JsonOption();
        var command = new Command("region", "Update a sprite's source region.");
        command.Arguments.Add(project);
        command.Options.Add(sprite);
        command.Options.Add(x);
        command.Options.Add(y);
        command.Options.Add(width);
        command.Options.Add(height);
        command.Options.Add(json);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(async () =>
        {
            var request = new UpdateSpriteRegionRequest(
                parseResult.GetRequiredValue(project),
                parseResult.GetRequiredValue(sprite),
                parseResult.GetValue(x),
                parseResult.GetValue(y),
                parseResult.GetValue(width),
                parseResult.GetValue(height));
            await _service.UpdateSpriteRegionAsync(request, cancellationToken);
            WriteResult(
                new { sprite = request.SpriteId, request.X, request.Y, request.Width, request.Height },
                parseResult.GetValue(json),
                $"Updated region for '{request.SpriteId}'.");
            return CliExitCode.Success;
        }));
        return command;
    }

    private Command CreateExportCommand()
    {
        var project = ProjectArgument();
        var format = RequiredOption("--format", "Export format: native or phaser-json-hash.");
        format.AcceptOnlyFromAmong("native", "phaser-json-hash");
        var output = RequiredOption("--output", "Destination directory.");
        var json = JsonOption();
        var command = new Command("export", "Export a native atlas project.");
        command.Arguments.Add(project);
        command.Options.Add(format);
        command.Options.Add(output);
        command.Options.Add(json);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(async () =>
        {
            var request = new ExportAtlasRequest(
                parseResult.GetRequiredValue(project),
                parseResult.GetRequiredValue(format),
                parseResult.GetRequiredValue(output));
            var result = await _service.ExportAsync(request, cancellationToken: cancellationToken);
            WriteResult(
                new { format = result.Format, files = result.GeneratedFiles, diagnostics = result.Diagnostics },
                parseResult.GetValue(json),
                $"Exported {result.Format}: {string.Join(", ", result.GeneratedFiles)}");
            return CliExitCode.Success;
        }));
        return command;
    }

    private Command CreateRepackCommand()
    {
        var project = ProjectArgument();
        var output = RequiredOption("--output", "Destination directory for the repacked project and images.");
        var padding = new Option<int?>("--padding") { Description = "Transparent padding around each sprite (default: 2)." };
        var maximumWidth = new Option<int?>("--max-width") { Description = "Maximum atlas width (default: 4096)." };
        var maximumHeight = new Option<int?>("--max-height") { Description = "Maximum atlas height (default: 4096)." };
        var noPowerOfTwo = new Option<bool>("--no-power-of-two") { Description = "Do not round atlas dimensions to powers of two." };
        var json = JsonOption();
        var command = new Command("repack", "Create a deterministic packed atlas without rotating sprites.");
        command.Arguments.Add(project);
        command.Options.Add(output);
        command.Options.Add(padding);
        command.Options.Add(maximumWidth);
        command.Options.Add(maximumHeight);
        command.Options.Add(noPowerOfTwo);
        command.Options.Add(json);
        command.SetAction((parseResult, cancellationToken) => ExecuteAsync(async () =>
        {
            var request = new RepackAtlasRequest(
                parseResult.GetRequiredValue(project),
                parseResult.GetRequiredValue(output),
                new AtlasPackingOptions
                {
                    Padding = parseResult.GetValue(padding) ?? 2,
                    MaximumWidth = parseResult.GetValue(maximumWidth) ?? 4096,
                    MaximumHeight = parseResult.GetValue(maximumHeight) ?? 4096,
                    PowerOfTwo = !parseResult.GetValue(noPowerOfTwo),
                });
            var result = await _service.RepackAsync(request, cancellationToken: cancellationToken);
            WriteResult(
                new
                {
                    width = result.Project.Atlas.Size.Width,
                    height = result.Project.Atlas.Size.Height,
                    files = result.GeneratedFiles,
                },
                parseResult.GetValue(json),
                $"Repacked atlas to {result.Project.Atlas.Size.Width}x{result.Project.Atlas.Size.Height}: " +
                string.Join(", ", result.GeneratedFiles));
            return CliExitCode.Success;
        }));
        return command;
    }

    private int WriteApplicationInfo()
    {
        Console.WriteLine(_applicationInfo.Name);
        Console.WriteLine(_applicationInfo.Description);
        Console.WriteLine($"Native project extension: {_applicationInfo.NativeProjectExtension}");
        Console.WriteLine($"Native format version: {AtlasFormat.CurrentVersion}");
        Console.WriteLine("Implementation status: native format, detection, connectors, validation, and export available");
        return (int)CliExitCode.Success;
    }

    private static async Task<int> ExecuteAsync(Func<Task<CliExitCode>> action)
    {
        try
        {
            return (int)await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return (int)CliExitCode.Cancelled;
        }
        catch (Exception exception) when (exception is AtlasProjectFormatException or ArgumentException or KeyNotFoundException)
        {
            Console.Error.WriteLine(exception.Message);
            return (int)CliExitCode.InvalidProject;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(exception.Message);
            return (int)CliExitCode.IoFailure;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return (int)CliExitCode.ProcessingFailure;
        }
    }

    private static void WriteResult(object value, bool json, string humanMessage)
    {
        Console.WriteLine(json ? JsonSerializer.Serialize(value, JsonOptions) : humanMessage);
    }

    private static Argument<string> ProjectArgument() => new("project")
    {
        Description = "Path to a .saf.json project.",
    };

    private static Option<string> RequiredOption(string name, string description) => new(name)
    {
        Description = description,
        Required = true,
    };

    private static Option<bool> JsonOption() => new("--json")
    {
        Description = "Write machine-readable JSON to stdout.",
    };
}
