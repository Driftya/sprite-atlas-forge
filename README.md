<p align="center">
  <img src="docs/assets/icon/icon.png" alt="Sprite Atlas Forge logo" width="160">
</p>

# Sprite Atlas Forge

[![CI](https://github.com/Driftya/sprite-atlas-forge/actions/workflows/ci.yml/badge.svg)](https://github.com/Driftya/sprite-atlas-forge/actions/workflows/ci.yml)
[![Release](https://github.com/Driftya/sprite-atlas-forge/actions/workflows/release.yml/badge.svg)](https://github.com/Driftya/sprite-atlas-forge/actions/workflows/release.yml)
[![Tests](https://img.shields.io/badge/tests-103%2F103%20passed-brightgreen)](https://github.com/Driftya/sprite-atlas-forge/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/Driftya/sprite-atlas-forge?display_name=tag&sort=semver&label=release)](https://github.com/Driftya/sprite-atlas-forge/releases/latest)

Sprite Atlas Forge is a Windows-first reverse texture packer. It takes an existing PNG spritesheet with either transparency or a border-connected background, detects its sprites, lets users author connector metadata, saves a native `.saf.json` project, optionally repacks the image, and exports Phaser, Unity 6, and Godot 4 atlases.

Phases 0–3 are implemented. The deterministic untrimmed repacker and Phaser, Unity 6, and Godot 4 exporters are also working. The Windows MAUI client supports open/detect/save/save-as, editable sprite regions, approval-gated consumer export, string metadata tags, a zoomable/pannable atlas canvas with direct sprite selection and drag handles, click/drag plus numeric connector editing, dirty-state protection, undo/redo, validation, cancellable progress, repacking, and named-format export.

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
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- sprite region .\assets\modules.saf.json --sprite habitat_01 --x 16 --y 24 --width 128 --height 64
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- connector add .\assets\modules.saf.json --sprite habitat_01 --name next --x 120 --y 32
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- connector update .\assets\modules.saf.json --sprite habitat_01 --current-name next --name attachment --x 96 --y 32
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- repack .\assets\modules.saf.json --output .\artifacts\repacked --padding 2
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- export .\assets\modules.saf.json --format phaser-json-hash --output .\artifacts\phaser
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- export .\assets\modules.saf.json --format unity-6-spritesheet --output .\artifacts\unity
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- export .\assets\modules.saf.json --format godot-4-atlas-textures --output .\artifacts\godot
```

Unity export writes the atlas PNG and a Unity 6 multi-sprite texture `.meta` file. Godot export writes the atlas PNG and one Godot 4 `AtlasTexture` `.tres` resource per approved sprite. Both formats preserve the atlas frame rectangles; Sprite Atlas Forge-only connector, tag, property, and metadata values remain in the native project.

Add `--json` to processing commands for machine-readable stdout. Detection currently supports PNG input. Repacking never rotates sprites and preserves connector coordinates.

Generated-art detection defaults to automatic background selection, background tolerance 12, alpha threshold 8, minimum area 64, two-pixel grouping distance, and a one-pixel mask-opening cleanup. Automatic mode analyzes the image's alpha histogram and starts with its least-destructive background/foreground split. It tests a bounded set of stronger cutoffs only when that mask contains an anomalous sheet-scale component, selecting the last cutoff that causes a major connectivity collapse. It then combines that component mask with a constrained magic-wand pass: higher-alpha cores act as markers and connected soft-alpha detail is recovered for at most eight pixels. Multiple cores cause a split only when a low-density vertical gutter crosses the component and substantial marker content exists on both sides. This layout-aware validation separates neighboring sprites joined by generated shadow or watermark pixels without turning a sprite's windows, engines, or highlights into extra regions. For a fully opaque sheet, Auto instead flood-fills the smoothly varying background from the image border. The cleanup removes isolated opaque specks and severs thin artifact bridges before connected-component detection. Normal merge distances are measured between actual component pixels instead of their rectangular bounds, so diagonally interlocking sprites are not fused merely because their boxes overlap; true containment still groups details inside transparent holes. A below-minimum fragment such as a small engine plume is attached only when it has exactly one nearby qualifying sprite, so it cannot become a noise bridge between two sprites. `Alpha only` keeps the entered alpha threshold exact and does not use seeded refinement. The MAUI property panel exposes persistently labeled detection controls before **Open image**. Unusually large merge distances intentionally fall back to broad transitive rectangle grouping and can combine an entire sheet.

For an opaque generated sheet, start with `auto`. If the source is known to be opaque, `border-connected` makes that choice explicit. Lower the tolerance if background removal enters a sprite; raise it if pieces of the background remain:

```powershell
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- detect .\opaque-sheet.png --output .\opaque-sheet.saf.json --background-mode border-connected --background-tolerance 12
```

Border-connected removal assumes every image-edge pixel is background. Add a small background margin around sprites that touch the source edge, or use `alpha-only` for a genuinely transparent sheet.

The MAUI sprite panel also supports manual recovery: **Add sprite** creates and selects a centered region with a unique `sprite_NNN` ID, which can then be corrected through **Source region**. Sprites can be selected directly on the image; the selected source region exposes eight drag handles for exact edge/corner resizing, with the numeric editor as a fallback. Dragged borders snap to their original edge, overlapping sprite edges, and image boundaries, with cyan alignment guides. At higher zoom levels, hold the right mouse button and drag to pan, or keep it held while scrolling to zoom around the pointer. **Save selected sprite as PNG** writes a lossless crop of the displayed atlas frame through the Windows Save dialog. **Delete selected** or the Delete key removes the current region; Delete remains available for normal editing while a text field has focus. These operations participate in undo/redo; source-region resizing and adding sprites to an already repacked atlas are intentionally blocked. Sprite bounds are rendered in one lightweight drawing surface, so changing zoom does not recreate a native control and label for every sprite.

For deliberately tiny pixel art, disable cleanup and filtering:

```powershell
dotnet run --project src/Driftya.SpriteAtlasForge.CliApplication -- detect .\pixel-art.png --output .\pixel-art.saf.json --minimum-area 1 --merge-distance 0 --noise-reduction-radius 0
```

Detection also defaults to a maximum source size of 16,384×16,384 and 67,108,864 total pixels. These limits are independently configurable with `--max-width`, `--max-height`, and `--max-pixels`; the pixel cap bounds the detector's bitmap, mask, and flood-fill queue memory before those working buffers are allocated.

CLI exit codes are stable: `0` success, `1` invalid command arguments, `3` invalid project data, `4` I/O or access failure, `5` cancellation, and `6` processing failure. Commands never prompt interactively, including in JSON mode.

The native v1 contract and compatibility rules are documented in [docs/native-format.md](docs/native-format.md), with its JSON Schema in [docs/schema/sprite-atlas-forge-v1.schema.json](docs/schema/sprite-atlas-forge-v1.schema.json).

Run the Windows desktop client:

```powershell
dotnet run --project src/Driftya.SpriteAtlasForge.ClientApplication -f net10.0-windows10.0.19041.0
```

## Windows releases

The production desktop build is an unpackaged, self-contained Windows x64 application. Publish it locally with:

```powershell
dotnet publish .\src\Driftya.SpriteAtlasForge.ClientApplication\Driftya.SpriteAtlasForge.ClientApplication.csproj `
  --framework net10.0-windows10.0.19041.0 `
  --configuration Release `
  --output .\.artifacts\release-smoke-test `
  -p:RuntimeIdentifierOverride=win-x64 `
  -p:WindowsPackageType=None `
  -p:WindowsAppSDKSelfContained=true
```

Distribute the complete publish directory, not the executable by itself. CI builds the same directory on pushes and pull requests. A `vX.Y.Z` tag also creates a draft GitHub Release containing a versioned ZIP and SHA-256 checksum after the full verification suite passes. See [the v1.0.0 release guide](docs/release/v1.0.0.md) for the exact release and smoke-test procedure.

Build, run every test project, collect Cobertura reports, and enforce the checked-in per-project line-coverage thresholds:

```powershell
.\eng\verify.ps1
```

Coverage reports are written to `.artifacts/coverage/`. The script uses TUnit's built-in Microsoft Testing Platform coverage extension, so no Coverlet package or global report tool is required.

## NuGet version policy

Package versions are centralized in `Directory.Packages.props` and float within an approved major version, for example `10.*`, `4.*`, and `2.*`. A normal restore can therefore consume compatible feature, patch, and security releases without silently crossing a major-version boundary.

Major upgrades are manual: review release notes and migration impact, change the major wildcard intentionally, restore, and run the full verification commands. NuGet audit checks are enabled, and Dependabot is configured to propose non-major updates while ignoring major updates.

Because floating versions are intentional, this application repository does not use a committed NuGet lock file. Release builds should record the resolved dependency graph in their build artifacts for traceability.

Hold Shift while dragging a sprite-region handle to temporarily disable magnetic snapping and its alignment guides.

Blue-dominant engine glow uses a larger bounded recovery radius than ordinary soft edges, preserving low-alpha flare tails without treating neutral generation shadows as sprite extent.
