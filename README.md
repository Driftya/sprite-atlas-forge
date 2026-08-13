# Sprite Atlas Forge

Sprite Atlas Forge is a Windows-first reverse texture packer. It will take an existing transparent spritesheet, detect its sprites, let users author regions and connector metadata, save a native `.saf.json` project, optionally repack the image, and export formats such as Phaser.

Implementation is currently at Phase 0: the solution architecture, shared dependency-injection boundary, CLI host, and minimal Windows MAUI workspace are established. Detection and native atlas authoring begin in the next phases.

See [the implementation plan](docs/plans/reverse-texture-packer-implementation-plan.md) for the complete checklist and design decisions.

## Solution architecture

| Project | Responsibility |
| --- | --- |
| `Driftya.SpriteAtlasForge.Domain` | Framework-free atlas rules and value objects; populated in Phase 1 |
| `Driftya.SpriteAtlasForge.Application` | Use-case contracts and shared application information |
| `Driftya.SpriteAtlasForge.Infrastructure` | External adapters and shared DI registration |
| `Driftya.SpriteAtlasForge.CliApplication` | Automation and language-neutral command-line host |
| `Driftya.SpriteAtlasForge.ClientApplication` | Windows .NET MAUI desktop editor |
| `Driftya.SpriteAtlasForge.Application.Tests` | Domain/Application-oriented automated tests |

Both hosts call the same Application and Infrastructure services in-process. The desktop client does not launch the CLI executable.

## Requirements

- .NET 10 SDK
- Windows 10 version 1809 or newer
- .NET MAUI Windows workload for the desktop client

Install the workload when needed:

```powershell
dotnet workload install maui-windows
```

## Build and run

Restore and build the solution:

```powershell
dotnet restore Driftya.SpriteAtlasForge.slnx
dotnet build Driftya.SpriteAtlasForge.slnx --no-restore --nologo
```

Run the CLI foundation:

```powershell
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- info
```

Run the Windows desktop client:

```powershell
dotnet run --project src/Driftya.SpriteAtlasForge.ClientApplication -f net10.0-windows10.0.19041.0
```

Run tests:

```powershell
dotnet run --project tests/Driftya.SpriteAtlasForge.Application.Tests/Driftya.SpriteAtlasForge.Application.Tests.csproj --no-restore
```

## NuGet version policy

Package versions are centralized in `Directory.Packages.props` and float within an approved major version, for example `10.*`, `8.*`, and `2.*`. A normal restore can therefore consume compatible feature, patch, and security releases without silently crossing a major-version boundary.

Major upgrades are manual: review release notes and migration impact, change the major wildcard intentionally, restore, and run the full verification commands. NuGet audit checks are enabled, and Dependabot is configured to propose non-major updates while ignoring major updates.

Because floating versions are intentional, this application repository does not use a committed NuGet lock file. Release builds should record the resolved dependency graph in their build artifacts for traceability.
