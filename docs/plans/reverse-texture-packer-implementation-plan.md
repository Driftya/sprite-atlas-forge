# Reverse Texture Packer Implementation Plan

Status: In progress — Phase 0 complete  
Last updated: 2026-08-13  
Target: Windows desktop first; macOS may be added later

## 1. Purpose

Build Sprite Atlas Forge as a reverse texture packer:

1. Open an existing transparent spritesheet.
2. Detect the individual sprites already present in the image.
3. Review and correct the detected regions.
4. Name sprites and author per-sprite metadata, including connection points.
5. Save the result in a readable, versioned Sprite Atlas Forge format.
6. Optionally repack the sprites into a new atlas.
7. Export the native atlas to consumer formats such as Phaser.

The checked-in solution structure is the architectural starting point. `docs/idea.md` remains useful product background, but it is not a literal implementation specification. Ship-generation-specific ideas in that file must not leak into the generic atlas core unless they are expressed as optional metadata.

## 2. Confirmed scope and decisions

- [ ] Treat `Driftya.SpriteAtlasForge.slnx` as the source of truth for projects.
- [ ] Use the five existing production projects; do not add another production project without a demonstrated boundary that the existing solution cannot represent.
- [ ] Treat `ClientApplication` as a .NET MAUI desktop application.
- [ ] Support Windows in v1.
- [ ] Keep platform-neutral Domain, Application, and Infrastructure contracts so Mac Catalyst can be evaluated later.
- [ ] Do not support Android or iOS in v1.
- [ ] Make the CLI a thin public host over reusable Application use cases.
- [ ] Make the MAUI client call those same Application use cases in-process.
- [ ] Do not make `ClientApplication` reference or launch `CliApplication` during normal operation.
- [ ] Preserve the imported spritesheet by default; repacking is an explicit operation.
- [ ] Stabilize the native format before implementing consumer-specific exporters.
- [ ] Model sprite connection points as a named `connectors` array, not fixed `anchor` and `next` properties.
- [ ] Keep v1 a sprite extraction and metadata-authoring tool, not a general image editor.

### Why the desktop client should not call the CLI executable

Calling the CLI from MAUI would create process-management, quoting, temporary-file, cancellation, progress-reporting, and error-parsing problems. Both hosts can provide the same behavior more reliably through a shared Application facade:

```text
                    Domain
                       ^
                       |
                  Application
                  ^         ^
                  |         |
           Infrastructure  |
              ^       ^     |
              |       |     |
             CLI    MAUI Client
```

Other applications get two integration choices:

- Invoke the CLI as a stable command-line process when language-neutral automation is needed.
- Reference the Application and Infrastructure assemblies when building another .NET application.

If process isolation becomes a real requirement later, add a process adapter behind an interface rather than coupling MAUI directly to CLI commands now.

## 3. Existing project responsibilities

| Existing project | Intended responsibility | Allowed dependencies |
| --- | --- | --- |
| `Driftya.SpriteAtlasForge.Domain` | Atlas model, sprite model, value objects, invariants, and format-independent rules | None |
| `Driftya.SpriteAtlasForge.Application` | Use cases, orchestration, ports, validation flow, progress, and cancellation | Domain |
| `Driftya.SpriteAtlasForge.Infrastructure` | Image decoding/encoding, filesystem access, native JSON persistence, detection implementation, packing implementation, and exporters | Application and Domain |
| `Driftya.SpriteAtlasForge.CliApplication` | Command parsing, DI composition, console output, exit codes, and automation contract | Application and Infrastructure |
| `Driftya.SpriteAtlasForge.ClientApplication` | Windows-first MAUI UI, editor interaction, view models, canvas, dialogs, and desktop composition root | Application and Infrastructure |
| `Driftya.SpriteAtlasForge.Application.Tests` | Initial automated test home for Domain/Application behavior and shared fixtures | Application; add narrowly justified references when integration coverage requires them |

Dependency rules:

- [ ] Domain must not reference MAUI, image libraries, JSON serializers, filesystems, or CLI packages.
- [ ] Application must not reference MAUI or concrete image libraries.
- [ ] Infrastructure must not contain UI or command-line behavior.
- [ ] CLI and MAUI must contain no sprite-detection, packing, serialization, or exporter business logic.
- [ ] Share DI registration from Infrastructure so both hosts resolve the same implementations and options.

## 3.1 Library-first implementation policy

Prefer a mature library over custom code for image decoding, image encoding, rendering, computer-vision operations, command parsing, validation, logging, and other non-product-specific infrastructure. Custom code should concentrate on Sprite Atlas Forge rules, format mapping, and orchestration.

GitHub stars are a useful adoption signal, not a quality guarantee. The star and release observations below were checked on 2026-08-13 and must be rechecked before the dependency is added.

### Dependency acceptance checklist

- [ ] Confirm that the package has a stable release compatible with .NET 10 and the required Windows target.
- [ ] Confirm recent maintainer activity, releases, issue responses, and CI—not only repository stars.
- [ ] Confirm an acceptable OSI-approved license and record any commercial-use, attribution, native-binary, or redistribution obligations.
- [ ] Check NuGet ownership, package-prefix reservation/signing, download adoption, deprecation status, and known vulnerabilities.
- [ ] Prefer Microsoft, .NET Foundation, or long-established ecosystem projects when capabilities are otherwise comparable.
- [ ] Prefer one library that covers several related requirements cleanly over multiple overlapping libraries.
- [ ] Measure native package size, startup time, memory use, trimming/AOT compatibility, and Windows x64/ARM64 packaging where applicable.
- [ ] Prove deterministic output for detection, packing, JSON, and export libraries with golden fixtures.
- [ ] Hide third-party types behind Infrastructure or host adapters so a library can be upgraded or replaced without changing Domain/Application contracts.
- [x] Centralize stable package versions and float within the approved major (`10.*`, `8.*`, `2.*`) so compatible minor/patch updates arrive automatically.
- [x] Keep major upgrades manual; do not use a committed NuGet lock file for this application solution because it would defeat the intended floating-version policy.
- [ ] Enable automated dependency and vulnerability checks, but do not auto-merge major upgrades or golden-output changes.
- [ ] Do not add a package when the relevant platform API or .NET base class library already solves the problem clearly.

### Approved default libraries

| Area | Library | Decision and intended use | Evidence checked 2026-08-13 |
| --- | --- | --- | --- |
| Runtime and desktop UI | [.NET 10 and .NET MAUI](https://github.com/dotnet/maui) | Use the existing Microsoft-supported MAUI project and Windows target. Follow the supported .NET 10 patch line. | Official Microsoft stack; .NET 10 is an active LTS release. |
| MVVM | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | Use observable properties, commands, messaging only where needed, and generated MVVM boilerplate. | Microsoft-maintained, .NET Foundation, about 3.7k GitHub stars. |
| MAUI helpers | [CommunityToolkit.Maui](https://github.com/CommunityToolkit/Maui) | Use maintained MAUI behaviors, converters, alerts, and platform helpers when they replace custom controls cleanly. | Microsoft community toolkit with MAUI-aligned releases. |
| Image I/O, pixel access, composition, and editor canvas | [SkiaSharp](https://github.com/mono/SkiaSharp) and [SkiaSharp.Views.Maui.Controls](https://www.nuget.org/packages/SkiaSharp.Views.Maui.Controls/) | Default graphics stack for PNG decode/encode, pixel buffers, cropping, composing atlas images, and the zoomable MAUI canvas. Keep it in Infrastructure/ClientApplication adapters. | About 5.4k GitHub stars, MIT licensed, broad desktop/mobile support, and actively released in 2026. |
| CLI parsing and help | [System.CommandLine](https://github.com/dotnet/command-line-api) | Use stable 2.x for commands, arguments, options, validation, help, completions, async handlers, and cancellation wiring. Do not use deprecated experimental binder/hosting packages. | Microsoft-owned, MIT licensed, stable 2.x, about 91M NuGet downloads, and actively patched in 2026. |
| JSON | [`System.Text.Json`](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/overview) | Use built-in serialization with source-generated contexts, explicit converters only where necessary, deterministic writer settings, and tolerant additive reads. | Ships with .NET; avoids an unnecessary serializer dependency. |
| Composition and configuration | `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options`, and `Microsoft.Extensions.Configuration` | Use the standard .NET abstractions shared by CLI and MAUI. Prefer the framework-provided versions aligned with .NET 10. | Microsoft-maintained .NET extensions used throughout the ecosystem. |
| Logging abstraction | `Microsoft.Extensions.Logging` | Use in Application-facing contracts and services so hosts choose their sinks without coupling core logic. | Standard Microsoft logging abstraction already supported by MAUI/.NET hosts. |
| Application input validation | [FluentValidation](https://github.com/FluentValidation/FluentValidation) | Use for non-trivial command/project/use-case request validation and structured failures. Keep essential Domain invariants inside Domain types. Do not add it for a handful of trivial null/range checks. | .NET Foundation, Apache-2.0, about 9.8k GitHub stars, established and actively maintained. |
| Unit tests | [TUnit](https://github.com/thomhurst/TUnit) | Retain the test framework already selected by the boilerplate. Use its native assertions and lifecycle features before adding assertion/mocking packages. | Existing solution dependency with active development and .NET 10 support; verify IDE, coverage, and CI behavior during Phase 0. |

### Conditional libraries requiring a spike

| Area | Candidate | Use it when | Required proof before adoption |
| --- | --- | --- | --- |
| Sprite detection and connected components | [OpenCvSharp](https://github.com/shimat/opencvsharp) | Prefer its thresholding, morphology, and `ConnectedComponentsWithStats` operations if it removes our flood-fill/merge implementation without harming the desktop distribution. | Verify Windows x64/ARM64 runtime packages, published size, startup/memory cost, alpha-channel behavior, deterministic regions, cancellation boundaries, and conversion cost between OpenCV and Skia buffers. It has about 6.1k GitHub stars, Apache-2.0 licensing, and active 2026 releases. |
| Structured rolling file logs | [Serilog](https://github.com/serilog/serilog) | Add only when release diagnostics require local structured files beyond the built-in debug/console providers. Keep `Microsoft.Extensions.Logging` as the application abstraction. | Confirm sink licenses, bounded retention, redaction of user paths, shutdown flushing, and no impact on machine-readable CLI stdout. Serilog has about 8k GitHub stars and active releases. |
| Rich human CLI rendering | [Spectre.Console](https://github.com/spectreconsole/spectre.console) | Add only for progress bars/tables that materially improve interactive CLI use. `System.CommandLine` remains the parser, and `--json` must bypass rich rendering. | Verify redirected-output and no-color behavior, package dependency/license review, and that it does not interfere with stdout/stderr contracts. The project has about 11.6k GitHub stars and is .NET Foundation/MIT. |

### Rectangle-packing dependency gap

There is not currently a C# atlas-packing package that clearly combines high adoption, recent releases, strong maintenance, deterministic behavior, and the exact constraints required here.

- [ ] Evaluate [RectpackSharp](https://github.com/ThomasMiz/RectpackSharp) behind `IAtlasPacker`; it is small, MIT licensed, purpose-built, and has tests, but its latest NuGet release observed during planning was from 2023 and the repository had about 122 stars.
- [ ] Evaluate [StbRectPackSharp](https://github.com/StbSharp/StbRectPackSharp) only as a secondary candidate; it is a small public-domain C# port of `stb_rect_pack`, but adoption is low and maintenance evidence is limited.
- [ ] Search NuGet and GitHub again immediately before Phase 4 because a better maintained package may exist by then.
- [ ] Reject a candidate that cannot provide padding, bounds, no-rotation mode, stable item identity, deterministic results, and failure reporting.
- [ ] If no library passes the gates, isolate the smallest proven packing algorithm inside Infrastructure, retain upstream attribution/license where applicable, and cover it heavily with property/boundary tests. This is the exception to the library-first rule, not the preferred outcome.

### Libraries not approved by default

- Do not add ImageSharp alongside SkiaSharp without a measured capability gap; two image stacks increase conversions, memory pressure, licensing review, and maintenance.
- Do not add OpenCvSharp merely for PNG loading or drawing; SkiaSharp already covers those needs.
- Do not add a second CLI parser alongside System.CommandLine.
- Do not add AutoMapper; the format and exporter mappings are important, explicit, and should remain readable and testable.
- Do not add a mediator/event-bus package for the initial use cases; direct application services are simpler.
- Do not add a mocking library until a test requires behavior that a small fake cannot express clearly.
- Do not add snapshot-test tooling solely for text files; direct golden-file comparisons are sufficient initially and avoid another license/update surface.
- Do not introduce prerelease packages in production projects without a documented blocker, expiry date, and migration issue.

## 4. Native Sprite Atlas Forge format

### 4.1 Format goals

- Human-readable JSON encoded as UTF-8.
- A visible integer `formatVersion` at the document root.
- Relative, `/`-separated asset paths for portable projects.
- Deterministic property and sprite ordering when writing files.
- Camel-case property names and two-space indentation.
- Strict validation for required fields and coordinates.
- Tolerant reading of unknown additive fields.
- A migration path when a breaking format change is unavoidable.
- A JSON Schema published beside the implementation once v1 is stable.
- Atomic save behavior so a failed write does not destroy a valid project.

Use `.saf.json` as the proposed descriptor extension. Keep the PNG as a separate asset so normal image tools and game pipelines can consume it directly.

### 4.2 Proposed v1 document shape

The exact contract must be finalized through golden-file tests, but implementation should begin from this shape:

```json
{
  "formatVersion": 1,
  "name": "generation-ship-modules",
  "source": {
    "image": "source/modules.png",
    "width": 2048,
    "height": 1024,
    "sha256": "..."
  },
  "atlas": {
    "image": "output/modules.png",
    "width": 2048,
    "height": 1024,
    "repacked": false
  },
  "sprites": [
    {
      "id": "habitat_03",
      "sourceRegion": {
        "x": 420,
        "y": 180,
        "width": 310,
        "height": 145
      },
      "frame": {
        "x": 420,
        "y": 180,
        "width": 310,
        "height": 145
      },
      "connectors": [
        { "name": "anchor", "x": 5, "y": 72 },
        { "name": "next", "x": 302, "y": 72 }
      ],
      "tags": ["habitat", "population"],
      "properties": {}
    }
  ]
}
```

The core format must stay game-agnostic. Fields such as `position`, `weight`, or `minPopulation` from `docs/idea.md` belong in `properties` or in a later explicitly versioned extension; they are not required atlas concepts.

### 4.3 Coordinate rules

- [ ] Use integer pixel coordinates.
- [ ] Define `(0, 0)` as the top-left of the relevant image or sprite.
- [ ] Store `sourceRegion` relative to the imported source image.
- [ ] Store `frame` relative to the emitted atlas image.
- [ ] Store every connector relative to the sprite's logical, untrimmed local bounds.
- [ ] Keep connector coordinates stable when the sprite is moved during repacking.
- [ ] Record trim offsets and original size before enabling transparent-edge trimming, so connectors and runtime placement remain correct.
- [ ] Permit connector positions on the sprite boundary; reject points outside the logical bounds unless a future format version explicitly supports external points.
- [ ] Require connector names to be non-empty and unique within a sprite using an ordinal, case-insensitive comparison.
- [ ] Preserve connector array order for predictable editing and diffs; never use order as connector identity.

## 5. Public application surface

Expose focused use cases behind an Application facade rather than exposing Infrastructure types directly.

- [ ] Create a project from an imported image.
- [ ] Load and migrate a native project.
- [ ] Save a native project atomically.
- [ ] Detect sprite regions with explicit detection options.
- [ ] Add, update, remove, merge, and split sprite regions.
- [ ] Rename a sprite with duplicate-ID validation.
- [ ] Add, move, rename, and remove connectors.
- [ ] Validate a complete project and return structured diagnostics.
- [ ] Repack sprites with explicit packing options.
- [ ] Export through a named exporter.
- [ ] Report progress for long detection, packing, and export operations.
- [ ] Accept `CancellationToken` for every operation that performs I/O or potentially long-running image work.

Suggested boundary types:

- `IAtlasForgeService`: stable facade consumed by CLI and MAUI.
- `IAtlasProjectStore`: load/save the native descriptor.
- `ISpriteDetector`: image-to-region detection port.
- `IAtlasPacker`: deterministic region packing port.
- `IAtlasExporter`: one strategy per output format.
- `IImageWorkspace`: narrowly scoped decode, pixel, crop, compose, and encode operations.

These are starting points, not a requirement to create one interface per class. Consolidate ports when separate abstractions do not improve testing or substitution.

## 6. CLI contract

The CLI is both a user tool and the language-neutral integration boundary for other applications.

Proposed command surface:

```text
atlasforge detect <image> --output <project.saf.json> [detection options]
atlasforge validate <project.saf.json> [--json]
atlasforge connector add <project> --sprite <id> --name <name> --x <x> --y <y>
atlasforge connector remove <project> --sprite <id> --name <name>
atlasforge repack <project> --output <directory> [packing options]
atlasforge export <project> --format <native|phaser-json-hash> --output <directory>
```

CLI requirements:

- [ ] Keep command handlers thin and delegate immediately to Application use cases.
- [ ] Provide `--help` and examples for every command.
- [ ] Support `--json` for machine-readable results and diagnostics.
- [ ] Write normal results to stdout and errors/diagnostics to stderr.
- [ ] Use stable documented exit codes for success, invalid arguments, invalid project data, I/O failure, cancellation, and processing failure.
- [ ] Never emit interactive prompts when `--json` or a non-interactive flag is active.
- [ ] Avoid partial output through staging and atomic moves.
- [ ] Resolve relative paths against a documented working directory.
- [ ] Make the same input, options, and tool version produce deterministic descriptor output.
- [ ] Add cancellation handling for Ctrl+C.
- [ ] Use stable `System.CommandLine` 2.x and keep all parser types inside the CLI host.

## 7. MAUI desktop experience

### 7.1 V1 workspace

- [ ] Replace the current project/task dashboard template with an atlas workspace.
- [ ] Remove SQLite-backed task/project/category/tag template behavior unless a specific atlas requirement justifies persistence beyond `.saf.json` files.
- [ ] Provide Open Image, Open Project, Save, Save As, Detect, Repack, Validate, and Export actions.
- [ ] Support Windows file picker and drag/drop for PNG and `.saf.json` files.
- [ ] Show a central canvas with zoom, pan, selection, sprite bounds, labels, and connectors.
- [ ] Show a sprite list with search, ID, warning state, and visibility toggle.
- [ ] Show a property panel for region, connector list, tags, and custom properties.
- [ ] Show structured validation errors that navigate to the affected sprite or field.
- [ ] Show progress and allow cancellation for detection, repacking, and export.
- [ ] Track dirty state and confirm before discarding unsaved edits.
- [ ] Add undo/redo for metadata and region edits before enabling complex manual editing.
- [ ] Keep all editor state in view models/application session models, not code-behind.

### 7.2 Connector editor

- [ ] Let the user enter connector-placement mode for the selected sprite.
- [ ] Add a connector by clicking the sprite and assigning a unique name.
- [ ] Render each connector as a clear dot with a label and selected state.
- [ ] Move a connector by drag or exact numeric X/Y input.
- [ ] Rename and delete a connector.
- [ ] Snap to integer pixels by default.
- [ ] Clamp or reject invalid coordinates consistently with Domain validation.
- [ ] Keep keyboard navigation and a numeric-input alternative for accessibility.
- [ ] Ensure zoom and pan transforms do not change saved sprite-local coordinates.
- [ ] Save and reload connectors without coordinate drift.

### 7.3 Platform targeting

- [ ] Change the client target frameworks to Windows-only for v1.
- [ ] Remove Android and iOS startup/resources after verifying that nothing product-specific depends on them.
- [ ] Decide whether to keep dormant Mac Catalyst files or restore them from version control when macOS work begins.
- [ ] Keep platform-specific file pickers, drag/drop, shell integration, and packaging behind ClientApplication services.
- [ ] Verify Windows high-DPI scaling, large images, keyboard use, and dark/light themes.

No `docs/concept/` assets currently exist in the repository. If concept images are added before UI implementation, review them before changing the workspace layout as required by repository guidance.

## 8. Image processing and reverse packing rules

### Detection

- [ ] Accept PNG input in v1; return a clear unsupported-format diagnostic for other files.
- [ ] Decode the image through Infrastructure.
- [ ] Build a visible-pixel mask using a configurable alpha threshold.
- [ ] Detect connected components deterministically.
- [ ] Calculate a bounding rectangle for each component.
- [ ] Ignore components below a configurable minimum area.
- [ ] Support a configurable merge distance for disconnected pieces that belong to one logical sprite.
- [ ] Apply optional source padding while clamping to image bounds.
- [ ] Sort detected results deterministically, initially top-to-bottom then left-to-right.
- [ ] Surface detection options in both CLI and MAUI with identical defaults.
- [ ] Preserve manual names/connectors when re-detection can match a previous region confidently; otherwise report an explicit conflict instead of silently losing metadata.

### Original-sheet mode

- [ ] Make original-sheet mode the default.
- [ ] Reuse the imported PNG as the emitted atlas when no pixel transformation is requested.
- [ ] Set each sprite `frame` equal to its detected `sourceRegion`.
- [ ] Generate native metadata without altering image pixels.

### Repack mode

- [ ] Make repacking opt-in.
- [ ] Define padding, maximum dimensions, power-of-two behavior, rotation policy, and deterministic ordering as explicit options.
- [ ] Disable sprite rotation in v1 unless the native connector transform and all exporters support it correctly.
- [ ] Produce a new atlas image and update `frame` while preserving `sourceRegion`.
- [ ] Add transparent-edge trimming only after original-size and trim-offset semantics are covered by tests.
- [ ] Evaluate a maintained packing library against a small deterministic in-house implementation before selecting a dependency.
- [ ] Record the selected algorithm and options in output metadata for reproducibility.

## 9. Export model

- [ ] Treat the native descriptor as the canonical model; never edit an exporter-specific model in the UI.
- [ ] Discover exporters by a stable format identifier.
- [ ] Validate exporter capabilities before writing files.
- [ ] Return a manifest of generated files and diagnostics.
- [ ] Keep exporters deterministic and cover them with golden files.
- [ ] Implement native output first.
- [ ] Implement Phaser JSON Hash as the first consumer exporter after native v1 is stable.
- [ ] Map frame, source-size, trim, and atlas metadata explicitly in the Phaser adapter.
- [ ] Decide and document whether connectors are emitted as ignorable custom Phaser metadata or as a separate companion file.
- [ ] Never discard native-only metadata silently; report unsupported fields when an exporter cannot represent them.

## 10. Delivery phases

### Phase 0 — Align the boilerplate

- [x] Replace placeholder `Class1.cs` files with intentional namespaces/types as later phases introduce them.
- [x] Replace `Console.WriteLine("Hello, World!")` with the CLI composition root.
- [x] Remove the MAUI task-management sample without carrying its models, repositories, SQLite database, or screens into atlas architecture.
- [x] Reduce MAUI targets to Windows.
- [x] Add shared Application/Infrastructure DI registration used by both hosts.
- [x] Add central package version management with approved-major floating versions.
- [x] Add automated dependency update and vulnerability checks with human review gates.
- [x] Correct repository test guidance that points to missing Mothership and `src/web` projects.
- [x] Document current setup and entry points in `README.md`.

Exit criteria:

- [x] The solution restores and builds with only intentional scaffold code.
- [x] Dependency directions match the project responsibility table.
- [x] The Windows client and CLI both start through DI composition roots.

### Phase 1 — Domain model and native format

- [ ] Implement rectangle, size, sprite ID, connector, sprite, source image, atlas output, and atlas project types.
- [ ] Implement invariants and structured validation diagnostics.
- [ ] Implement the v1 native serializer and atomic project store.
- [ ] Define unknown-field and version-migration behavior.
- [ ] Add representative `.saf.json` golden fixtures.
- [ ] Add round-trip, invalid-document, duplicate-ID, invalid-region, and invalid-connector tests.

Exit criteria:

- [ ] A project containing multiple named connectors can round-trip without data loss.
- [ ] Invalid coordinates and unsupported versions produce actionable diagnostics.

### Phase 2 — Detection vertical slice

- [ ] Implement the image workspace abstraction with SkiaSharp-backed Infrastructure and ClientApplication adapters.
- [ ] Spike OpenCvSharp connected-component detection against representative images and published Windows builds.
- [ ] If the spike passes the dependency gates, implement detection with OpenCvSharp thresholding/morphology/connected-component operations; otherwise record the failed gates before implementing the bounded fallback algorithm.
- [ ] Normalize library output into deterministic Domain/Application region ordering.
- [ ] Implement import/detect/save Application use cases.
- [ ] Expose detection through the CLI.
- [ ] Add tiny image fixtures for transparency, touching pixels, disconnected pieces, noise, padding, and image-edge cases.

Exit criteria:

- [ ] One CLI command converts a PNG spritesheet into a valid native descriptor without changing the source PNG.
- [ ] Repeated runs produce equivalent ordered regions and deterministic JSON.

### Phase 3 — Windows editor and connector authoring

- [ ] Build the Windows workspace shell and load/save flow.
- [ ] Build the zoomable/pannable sprite canvas.
- [ ] Add detection review and basic region correction.
- [ ] Add sprite naming and duplicate detection.
- [ ] Add connector create, move, rename, delete, and numeric editing.
- [ ] Add dirty-state, validation, undo/redo, progress, and cancellation behavior.
- [ ] Verify that MAUI and CLI use the same defaults and Application operations.

Exit criteria:

- [ ] A user can open a spritesheet, detect sprites, name them, place multiple connectors, save, close, and reopen with identical coordinates.
- [ ] The same native project validates identically in MAUI and CLI.

### Phase 4 — Repacking

- [ ] Finalize deterministic packing behavior and dependency choice.
- [ ] Implement global padding and atlas-size constraints.
- [ ] Compose and encode the new atlas image.
- [ ] Update frames without changing logical connector coordinates.
- [ ] Add trim metadata and trimming only after the untrimmed path is stable.
- [ ] Expose identical repack options in CLI and MAUI.
- [ ] Add packing golden fixtures and boundary/failure tests.

Exit criteria:

- [ ] Repacking never overlaps frames or writes outside atlas bounds.
- [ ] Connector positions remain logically correct after moving and trimming sprites.

### Phase 5 — Phaser export

- [ ] Finalize the Phaser JSON Hash mapping.
- [ ] Implement the exporter strategy and output manifest.
- [ ] Add CLI and MAUI format selection.
- [ ] Add golden Phaser descriptors and a small consumer smoke fixture.
- [ ] Document unsupported or companion metadata behavior.

Exit criteria:

- [ ] A generated Phaser atlas loads with correct frame and trim data.
- [ ] Native connector metadata remains available according to the documented export policy.

### Phase 6 — Windows release hardening

- [ ] Test large images and define practical memory/dimension limits.
- [ ] Add recoverable errors for corrupt PNG, invalid JSON, missing assets, read-only paths, and output collisions.
- [ ] Add structured logs without leaking user paths in telemetry.
- [ ] Verify high-DPI input coordinates and rendering.
- [ ] Verify keyboard accessibility and screen-reader labels for editor controls.
- [ ] Add Windows packaging, versioning, icon, and upgrade checks.
- [ ] Publish CLI as a versioned Windows executable alongside the desktop release.
- [ ] Update `README.md`, `docs/idea.md`, and supporting docs to match shipped behavior.

Exit criteria:

- [ ] A clean Windows machine can install/run the desktop app and CLI.
- [ ] The release has documented input/output contracts, known limits, and recovery behavior.

## 11. Test and verification plan

### Automated tests

- [ ] Domain invariants for rectangles, sprite IDs, connector names, connector bounds, and duplicate IDs.
- [ ] Native format round-trip and version compatibility tests.
- [ ] Golden JSON tests with deterministic ordering.
- [ ] Detection tests using small checked-in PNG fixtures.
- [ ] Re-detection metadata-preservation tests.
- [ ] Packing overlap, bounds, padding, determinism, and failure tests.
- [ ] Exporter golden-file tests.
- [ ] Application use-case tests with fake image/filesystem ports where appropriate.
- [ ] CLI integration tests for arguments, exit codes, stdout/stderr, JSON mode, cancellation, and partial-output cleanup.
- [ ] View-model tests for selection, dirty state, undo/redo, validation, and connector edits.

### Manual Windows checks

- [ ] Open files through picker and drag/drop.
- [ ] Zoom/pan and place connectors at 100%, fractional zoom, and high-DPI scaling.
- [ ] Cancel detection/repack/export without corrupting project state.
- [ ] Recover from missing source image and unwritable output directory.
- [ ] Confirm connector coordinates after save/reload and repack.
- [ ] Verify keyboard-only editing and visible focus.
- [ ] Verify dark and light themes.

### Commands to establish during Phase 0

The current repository instructions mention test projects that are not present in this solution. Replace them with commands based on the actual `.slnx`, including at minimum:

```powershell
dotnet run --project tests/Driftya.SpriteAtlasForge.Application.Tests/Driftya.SpriteAtlasForge.Application.Tests.csproj --no-restore
dotnet build src/Driftya.SpriteAtlasForge.CliApplication/Driftya.SpriteAtlasForge.CliApplication.csproj --nologo
dotnet build src/Driftya.SpriteAtlasForge.ClientApplication/Driftya.SpriteAtlasForge.ClientApplication.csproj -f net10.0-windows10.0.19041.0 --nologo
```

If the single test project becomes an unclear home for Infrastructure, CLI, or UI tests, propose separate test projects as a focused solution change. Do not add them preemptively.

## 12. Cross-cutting completion checklist

- [ ] Public types and commands use clear atlas terminology.
- [ ] All async I/O accepts and propagates cancellation.
- [ ] All file writes are staged and recoverable.
- [ ] Errors are structured in Application and rendered by each host.
- [ ] Same options have the same defaults in native format, CLI, and MAUI.
- [ ] Format changes include migration, fixtures, tests, and documentation.
- [ ] Exporter limitations are visible to users before export.
- [ ] No MAUI or image-library types cross into Domain/Application.
- [ ] No host duplicates core processing logic.
- [ ] README and product docs are updated in the same change as user-visible behavior.

## 13. Deferred work

Do not include these in v1 unless a validated user need changes the scope:

- Android and iOS clients.
- Mac Catalyst packaging.
- General painting, filters, or Photoshop-like editing.
- Animated sprite timelines.
- Rotated atlas frames.
- Branching/compatibility rules between connectors.
- Domain-specific ship-generation rules in the core format.
- Multiple source sheets in one project.
- Plugin loading from arbitrary third-party assemblies.
- Cloud sync or database-backed project storage.

## 14. Decisions to confirm during implementation

- [ ] Validate the SkiaSharp image/canvas choice with a small Windows spike covering memory use, pixel access, coordinate transforms, and MAUI rendering integration.
- [ ] Decide whether OpenCvSharp's reduction in custom detection code justifies its native deployment and memory cost.
- [ ] Select or implement the rectangle packer after determinism, maintenance, padding, and licensing evaluation.
- [ ] Finalize the native extension and JSON Schema URI before declaring format v1 stable.
- [ ] Decide whether Phaser receives connectors as custom fields or a companion file.
- [ ] Define the metadata-preservation matching rule used when users re-run detection.
- [ ] Define large-image limits from measured Windows behavior, not arbitrary defaults.

These decisions are intentionally delayed because they depend on evidence. They must be recorded in the relevant documentation and tests when made.
