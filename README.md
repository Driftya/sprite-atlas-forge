# Sprite Atlas Forge

Sprite Atlas Forge is a Windows-first reverse texture packer. It takes an existing transparent PNG spritesheet, detects its sprites, lets users author connector metadata, saves a native `.saf.json` project, optionally repacks the image, and exports Phaser JSON Hash atlases.

Phases 0–2 are implemented. The deterministic untrimmed repacker and Phaser exporter are also working, while the interactive Windows editor remains in progress. The MAUI client currently supports open/detect/save, a size-aware zoom/pan viewport, sprite rename, numeric connector create/move/rename/delete, validation, image preview, repack, and Phaser export.

See [the implementation plan](docs/plans/reverse-texture-packer-implementation-plan.md) for the complete checklist and design decisions.

## Solution architecture

| Project | Responsibility |
| --- | --- |
| `Driftya.SpriteAtlasForge.Domain` | Framework-free atlas rules, geometry, sprites, connectors, and packing metadata |
| `Driftya.SpriteAtlasForge.Application` | Shared use-case facade, ports, requests, progress, and diagnostics |
| `Driftya.SpriteAtlasForge.Infrastructure` | Native JSON, SkiaSharp processing, deterministic packing, exporters, and DI |
| `Driftya.SpriteAtlasForge.CliApplication` | Automation and language-neutral command-line host |
| `Driftya.SpriteAtlasForge.ClientApplication` | Windows .NET MAUI desktop editor |
| `Driftya.SpriteAtlasForge.Domain.Tests` | Domain invariants and value-object tests |
| `Driftya.SpriteAtlasForge.Application.Tests` | Application orchestration tests using narrow in-memory fakes |
| `Driftya.SpriteAtlasForge.Infrastructure.Tests` | Native JSON, SkiaSharp, packing, exporter, and DI integration tests |
| `Driftya.SpriteAtlasForge.CliApplication.Tests` | CLI parsing and end-to-end command tests |
| `Driftya.SpriteAtlasForge.ClientApplication.Tests` | Platform-neutral MAUI view-model behavior tests |

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

Inspect the CLI:

```powershell
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- info
```

Create and validate a native project:

```powershell
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- detect .\assets\modules.png --output .\assets\modules.saf.json --minimum-area 4 --merge-distance 1
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- validate .\assets\modules.saf.json
```

Author metadata, repack, and export:

```powershell
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- sprite rename .\assets\modules.saf.json --sprite sprite_001 --new-id habitat_01
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- connector add .\assets\modules.saf.json --sprite habitat_01 --name next --x 120 --y 32
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- connector update .\assets\modules.saf.json --sprite habitat_01 --current-name next --name attachment --x 96 --y 32
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- repack .\assets\modules.saf.json --output .\artifacts\repacked --padding 2
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- export .\assets\modules.saf.json --format phaser-json-hash --output .\artifacts\phaser
```

Add `--json` to processing commands for machine-readable stdout. Detection currently supports PNG input. Repacking never rotates sprites and preserves connector coordinates.

The native v1 contract and compatibility rules are documented in [docs/native-format.md](docs/native-format.md), with its JSON Schema in [docs/schema/sprite-atlas-forge-v1.schema.json](docs/schema/sprite-atlas-forge-v1.schema.json).

Run the Windows desktop client:

```powershell
dotnet run --project src/Driftya.SpriteAtlasForge.ClientApplication -f net10.0-windows10.0.19041.0
```

Build, run every test project, collect Cobertura reports, and enforce the checked-in per-project line-coverage thresholds:

```powershell
.\eng\verify.ps1
```

Coverage reports are written to `.artifacts/coverage/`. The script uses TUnit's built-in Microsoft Testing Platform coverage extension, so no Coverlet package or global report tool is required.

## NuGet version policy

Package versions are centralized in `Directory.Packages.props` and float within an approved major version, for example `10.*`, `4.*`, and `2.*`. A normal restore can therefore consume compatible feature, patch, and security releases without silently crossing a major-version boundary.

Major upgrades are manual: review release notes and migration impact, change the major wildcard intentionally, restore, and run the full verification commands. NuGet audit checks are enabled, and Dependabot is configured to propose non-major updates while ignoring major updates.

Because floating versions are intentional, this application repository does not use a committed NuGet lock file. Release builds should record the resolved dependency graph in their build artifacts for traceability.
