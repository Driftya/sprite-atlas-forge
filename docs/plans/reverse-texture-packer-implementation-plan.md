# Reverse Texture Packer Implementation Plan

Status: In progress — Phases 0–3 complete; Phases 4–5 implemented for the untrimmed v1 path; Phase 6 remains
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

- [x] Treat `Driftya.SpriteAtlasForge.slnx` as the source of truth for projects.
- [x] Use the five existing production projects; do not add another production project without a demonstrated boundary that the existing solution cannot represent.
- [x] Treat `ClientApplication` as a .NET MAUI desktop application.
- [x] Support Windows in v1.
- [x] Keep platform-neutral Domain, Application, and Infrastructure contracts so Mac Catalyst can be evaluated later.
- [x] Do not support Android or iOS in v1.
- [x] Make the CLI a thin public host over reusable Application use cases.
- [x] Make the MAUI client call those same Application use cases in-process.
- [x] Do not make `ClientApplication` reference or launch `CliApplication` during normal operation.
- [x] Preserve the imported spritesheet by default; repacking is an explicit operation.
- [x] Stabilize the native format before implementing consumer-specific exporters.
- [x] Model sprite connection points as a named `connectors` array, not fixed `anchor` and `next` properties.
- [x] Keep v1 a sprite extraction and metadata-authoring tool, not a general image editor.

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
| `Driftya.SpriteAtlasForge.Domain.Tests` | Domain invariants and value-object coverage | Domain |
| `Driftya.SpriteAtlasForge.Application.Tests` | Application orchestration and port-contract coverage | Application |
| `Driftya.SpriteAtlasForge.Infrastructure.Tests` | Persistence, image processing, packing, exporting, and DI integration coverage | Infrastructure |
| `Driftya.SpriteAtlasForge.CliApplication.Tests` | Command parsing and CLI workflow coverage | CLI and Infrastructure for end-to-end fixtures |
| `Driftya.SpriteAtlasForge.ClientApplication.Tests` | Platform-neutral workspace view-model behavior coverage | Application plus linked production view-model source; do not bootstrap WinUI in unit tests |

Dependency rules:

- [x] Domain must not reference MAUI, image libraries, JSON serializers, filesystems, or CLI packages.
- [x] Application must not reference MAUI or concrete image libraries.
- [x] Infrastructure must not contain UI or command-line behavior.
- [x] CLI and MAUI must contain no sprite-detection, packing, serialization, or exporter business logic.
- [x] Share DI registration from Infrastructure so both hosts resolve the same implementations and options.

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
| Unit tests | [TUnit](https://github.com/thomhurst/TUnit) | Retain the test framework already selected by the boilerplate. Use its native assertions, Microsoft Testing Platform runner, and built-in coverage extension before adding assertion/mocking/reporting packages. | Existing solution dependency with active development and .NET 10 support; all five test hosts and Cobertura collection verified on 2026-08-13. |

### Conditional libraries requiring a spike

| Area | Candidate | Use it when | Required proof before adoption |
| --- | --- | --- | --- |
| Sprite detection and connected components | [OpenCvSharp](https://github.com/shimat/opencvsharp) | Prefer its thresholding, morphology, and `ConnectedComponentsWithStats` operations if it removes our flood-fill/merge implementation without harming the desktop distribution. | Verify Windows x64/ARM64 runtime packages, published size, startup/memory cost, alpha-channel behavior, deterministic regions, cancellation boundaries, and conversion cost between OpenCV and Skia buffers. It has about 6.1k GitHub stars, Apache-2.0 licensing, and active 2026 releases. |
| Structured rolling file logs | [Serilog](https://github.com/serilog/serilog) | Add only when release diagnostics require local structured files beyond the built-in debug/console providers. Keep `Microsoft.Extensions.Logging` as the application abstraction. | Confirm sink licenses, bounded retention, redaction of user paths, shutdown flushing, and no impact on machine-readable CLI stdout. Serilog has about 8k GitHub stars and active releases. |
| Rich human CLI rendering | [Spectre.Console](https://github.com/spectreconsole/spectre.console) | Add only for progress bars/tables that materially improve interactive CLI use. `System.CommandLine` remains the parser, and `--json` must bypass rich rendering. | Verify redirected-output and no-color behavior, package dependency/license review, and that it does not interfere with stdout/stderr contracts. The project has about 11.6k GitHub stars and is .NET Foundation/MIT. |

### Rectangle-packing dependency gap

There is not currently a C# atlas-packing package that clearly combines high adoption, recent releases, strong maintenance, deterministic behavior, and the exact constraints required here.

- [x] Evaluate [RectpackSharp](https://github.com/ThomasMiz/RectpackSharp); reject it for v1 because its maintenance/adoption evidence did not clear the dependency gate.
- [x] Evaluate [StbRectPackSharp](https://github.com/StbSharp/StbRectPackSharp); reject it because adoption and maintenance evidence are limited.
- [x] Search NuGet and GitHub immediately before Phase 4; no better maintained exact-fit package was found.
- [x] Reject candidates that cannot provide padding, bounds, no-rotation mode, stable item identity, deterministic results, and failure reporting.
- [x] Isolate the small `deterministic-shelf-v1` fallback behind `IAtlasPacker` and cover its bounds, overlap, padding, determinism, and failure behavior.

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

- [x] Use integer pixel coordinates.
- [x] Define `(0, 0)` as the top-left of the relevant image or sprite.
- [x] Store `sourceRegion` relative to the imported source image.
- [x] Store `frame` relative to the emitted atlas image.
- [x] Store every connector relative to the sprite's logical, untrimmed local bounds.
- [x] Keep connector coordinates stable when the sprite is moved during repacking.
- [ ] Record trim offsets and original size before enabling transparent-edge trimming, so connectors and runtime placement remain correct.
- [x] Permit connector positions on the sprite boundary; reject points outside the logical bounds unless a future format version explicitly supports external points.
- [x] Require connector names to be non-empty and unique within a sprite using an ordinal, case-insensitive comparison.
- [x] Preserve connector array order for predictable editing and diffs; never use order as connector identity.

## 5. Public application surface

Expose focused use cases behind an Application facade rather than exposing Infrastructure types directly.

- [x] Create a project from an imported image.
- [x] Load native v1 projects and report unsupported versions explicitly; add migrations when a second format version exists.
- [x] Save a native project atomically.
- [x] Detect sprite regions with explicit detection options.
- [ ] Add, update, remove, merge, and split sprite regions.
- [x] Rename a sprite with duplicate-ID validation.
- [x] Add, move, rename, and remove connectors through shared Application use cases.
- [x] Validate a complete project and return structured diagnostics.
- [x] Repack sprites with explicit packing options.
- [x] Export through a named exporter.
- [x] Report progress for long detection, packing, and export operations.
- [x] Accept `CancellationToken` for every operation that performs I/O or potentially long-running image work.

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
atlasforge connector update <project> --sprite <id> --current-name <name> --name <new-name> --x <x> --y <y>
atlasforge connector remove <project> --sprite <id> --name <name>
atlasforge repack <project> --output <directory> [packing options]
atlasforge export <project> --format <native|phaser-json-hash> --output <directory>
```

CLI requirements:

- [x] Keep command handlers thin and delegate immediately to Application use cases.
- [ ] Provide `--help` and examples for every command.
- [x] Support `--json` for machine-readable results and diagnostics.
- [x] Write normal results to stdout and errors/diagnostics to stderr.
- [x] Use stable documented exit codes for success, invalid arguments, invalid project data, I/O/access failure, cancellation, and processing failure.
- [x] Never emit interactive prompts when `--json` or a non-interactive flag is active.
- [x] Avoid partial output through staging and atomic moves.
- [x] Resolve relative paths against the process working directory.
- [x] Make the same input, options, and tool version produce deterministic descriptor output.
- [x] Add cancellation handling through `System.CommandLine` action cancellation tokens.
- [x] Use stable `System.CommandLine` 2.x and keep all parser types inside the CLI host.

## 7. MAUI desktop experience

### 7.1 V1 workspace

- [x] Replace the current project/task dashboard template with an atlas workspace.
- [x] Remove SQLite-backed task/project/category/tag template behavior unless a specific atlas requirement justifies persistence beyond `.saf.json` files.
- [x] Provide Open Image, Open Project, Save, Save As, Detect, Repack, Validate, and Export actions.
- [ ] Support Windows file picker and drag/drop for PNG and `.saf.json` files.
- [x] Show a central canvas with zoom, pan, selection, sprite bounds, labels, and connectors. The canvas supports 25–800% zoom and two-axis panning.
- [ ] Show a sprite list with search, ID, warning state, and visibility toggle. Selection and IDs are implemented; search, warning, and visibility controls remain.
- [ ] Show a property panel for region, connector list, tags, and custom properties. Regions and connectors are implemented; tags and custom-property editing remain.
- [ ] Show structured validation errors that navigate to the affected sprite or field.
- [x] Show progress and allow cancellation for detection, repacking, and export.
- [x] Track dirty state and confirm before discarding unsaved edits.
- [x] Add undo/redo for metadata and region edits before enabling complex manual editing.
- [x] Keep all editor state in view models/application session models, not code-behind.

### 7.2 Connector editor

- [x] Let the user enter connector-placement mode for the selected sprite by entering a connector name before clicking.
- [x] Add a connector by clicking the sprite and assigning a unique name.
- [x] Render each connector as a clear dot with a label and selected state.
- [x] Move a connector by drag or exact numeric X/Y input.
- [x] Rename and delete a selected connector through MAUI, CLI, and the shared Application operation.
- [x] Snap to integer pixels by default through integer connector coordinates and numeric inputs.
- [x] Clamp or reject invalid coordinates consistently with Domain validation. Coordinates outside logical sprite bounds are rejected.
- [x] Keep keyboard navigation and a numeric-input alternative for accessibility.
- [x] Ensure zoom and pan transforms do not change saved sprite-local coordinates.
- [x] Save and reload connectors without coordinate drift.

### 7.3 Platform targeting

- [x] Change the client target frameworks to Windows-only for v1.
- [x] Remove Android and iOS startup/resources after verifying that nothing product-specific depends on them.
- [x] Keep dormant Mac Catalyst bootstrap files for a possible future macOS target; they are not compiled by the Windows-only v1 target.
- [x] Keep platform-specific file pickers, drag/drop, shell integration, and packaging inside ClientApplication.
- [ ] Verify Windows high-DPI scaling, large images, keyboard use, and dark/light themes.

No `docs/concept/` assets currently exist in the repository. If concept images are added before UI implementation, review them before changing the workspace layout as required by repository guidance.

## 8. Image processing and reverse packing rules

### Detection

- [x] Accept PNG input in v1; return a clear unsupported-format diagnostic for other files.
- [x] Decode the image through Infrastructure using SkiaSharp.
- [x] Build a visible-pixel mask using a configurable alpha threshold.
- [x] Automatically distinguish transparent sheets from fully opaque sheets and remove smoothly varying opaque backgrounds connected to the image border.
- [x] Derive an automatic alpha cutoff for transparent sheets containing multicolored low-alpha generation or watermark noise.
- [x] Expose explicit alpha-only and border-connected modes plus a shared background color tolerance through CLI and MAUI.
- [x] Apply configurable binary-mask opening to remove isolated generation noise and sever thin opaque bridges before component detection.
- [x] Detect connected components deterministically.
- [x] Calculate a bounding rectangle for each component.
- [x] Ignore components below a configurable minimum area.
- [x] Support a configurable merge distance for disconnected pieces that belong to one logical sprite.
- [x] Group overlapping and contained component bounds even at zero merge distance so opaque details inside transparent holes stay with their sprite.
- [x] Apply optional source padding while clamping to image bounds.
- [x] Sort detected results deterministically, initially top-to-bottom then left-to-right.
- [x] Surface shared detection defaults through Application; CLI exposes overrides and MAUI uses the same defaults.
- [ ] Preserve manual names/connectors when re-detection can match a previous region confidently; otherwise report an explicit conflict instead of silently losing metadata.

### Original-sheet mode

- [x] Make original-sheet mode the default.
- [x] Reuse the imported PNG as the emitted atlas when no pixel transformation is requested.
- [x] Set each sprite `frame` equal to its detected `sourceRegion`.
- [x] Generate native metadata without altering image pixels.

### Repack mode

- [x] Make repacking opt-in.
- [x] Define padding, maximum dimensions, power-of-two behavior, rotation policy, and deterministic ordering as explicit options.
- [x] Disable sprite rotation in v1 unless the native connector transform and all exporters support it correctly.
- [x] Produce a new atlas image and update `frame` while preserving `sourceRegion`.
- [ ] Add transparent-edge trimming only after original-size and trim-offset semantics are covered by tests.
- [x] Evaluate current packing libraries; none met the combined maintenance/adoption/constraint gates, so isolate the tested `deterministic-shelf-v1` fallback behind `IAtlasPacker`.
- [x] Record the selected algorithm and options in output metadata for reproducibility.

## 9. Export model

- [x] Treat the native descriptor as the canonical model; never edit an exporter-specific model in the UI.
- [x] Discover exporters by a stable format identifier.
- [x] Validate exporter selection before writing files.
- [x] Return a manifest of generated files and diagnostics.
- [x] Keep exporters deterministic and cover their mapping with focused golden assertions.
- [x] Implement native output first.
- [x] Implement Phaser JSON Hash as the first consumer exporter after native v1 is stable.
- [x] Map frame, source-size, trim, and atlas metadata explicitly in the Phaser adapter.
- [x] Emit connectors, tags, and properties as documented ignorable custom per-frame Phaser metadata.
- [x] Never discard native-only metadata silently; report the custom-field policy in export diagnostics.

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

- [x] Implement rectangle, size, sprite ID, connector, sprite, source image, atlas output, and atlas project types.
- [x] Implement invariants and structured validation diagnostics.
- [x] Implement the v1 native serializer and atomic project store.
- [x] Define unknown-field and version-migration behavior.
- [x] Add representative `.saf.json` golden fixtures.
- [x] Add round-trip, invalid-document, duplicate-ID, invalid-region, and invalid-connector tests.

Exit criteria:

- [x] A project containing multiple named connectors can round-trip without data loss.
- [x] Invalid coordinates and unsupported versions produce actionable diagnostics.

### Phase 2 — Detection vertical slice

- [x] Implement narrow image-processing ports with SkiaSharp-backed Infrastructure and ClientApplication adapters.
- [x] Review OpenCvSharp against the alpha-mask scope and Windows native deployment cost; reject it for v1 because the required bounded four-neighbor operation does not justify a second native image stack.
- [x] Record the decision and implement the bounded deterministic connected-component fallback behind `ISpriteDetector`.
- [x] Normalize detector output into deterministic Domain/Application region ordering.
- [x] Implement import/detect/save Application use cases.
- [x] Expose detection through the CLI.
- [x] Add tiny generated image fixtures for transparency, disconnected pieces, noise, padding, and image-edge clamping.

Exit criteria:

- [x] One CLI command converts a PNG spritesheet into a valid native descriptor without changing the source PNG.
- [x] Repeated runs produce equivalent ordered regions and deterministic JSON.

### Phase 3 — Windows editor and connector authoring

- [x] Build the Windows workspace shell and load/save flow.
- [x] Build the zoomable/pannable sprite viewport with atlas-size-aware dimensions.
- [x] Add detection review and basic region correction.
- [x] Add undoable manual sprite creation and selected-sprite deletion for detection recovery.
- [x] Add sprite naming and duplicate detection.
- [x] Add connector create, move, rename, delete, canvas, and numeric editing.
- [x] Add dirty-state, validation, undo/redo, progress, and cancellation behavior.
- [x] Verify that MAUI and CLI use the same defaults and Application operations.

Exit criteria:

- [x] A user can open a spritesheet, detect sprites, name them, place multiple connectors, save, close, and reopen with identical coordinates.
- [x] The same native project validates identically in MAUI and CLI through the shared validator.

### Phase 4 — Repacking

- [x] Finalize deterministic packing behavior and dependency choice.
- [x] Implement global padding and atlas-size constraints.
- [x] Compose and encode the new atlas image.
- [x] Update frames without changing logical connector coordinates.
- [ ] Add trim metadata and trimming only after the untrimmed path is stable.
- [x] Expose identical repack options in CLI and MAUI.
- [x] Add deterministic packing golden assertions and boundary/failure tests.

Exit criteria:

- [x] Repacking never overlaps frames or writes outside atlas bounds for the implemented untrimmed path.
- [ ] Connector positions remain logically correct after moving and trimming sprites. Moving is covered; trimming is intentionally deferred.

### Phase 5 — Phaser export

- [x] Finalize the Phaser JSON Hash mapping for untrimmed, non-rotated v1 sprites.
- [x] Implement the exporter strategy and output manifest.
- [x] Add CLI and MAUI format selection.
- [x] Add deterministic Phaser mapping assertions; a real Phaser consumer smoke fixture remains before declaring the phase complete.
- [x] Document unsupported and custom metadata behavior.

Exit criteria:

- [ ] A generated Phaser atlas loads with correct frame and trim data.
- [x] Native connector metadata remains available according to the documented export policy.

### Phase 6 — Windows release hardening

- [ ] Test large images on representative Windows hardware. Detection now rejects images over configurable 16,384×16,384 or 67,108,864-pixel defaults before allocating component buffers; measured release-machine validation remains.
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

- [x] Domain invariants for rectangles, sprite IDs, connector names, connector bounds, and duplicate IDs.
- [x] Native format round-trip and version compatibility tests.
- [x] Golden JSON tests with deterministic ordering.
- [x] Detection tests using small generated PNG fixtures, including transparent, noisy, enclosed-detail, and opaque-gradient backgrounds.
- [ ] Re-detection metadata-preservation tests.
- [x] Packing overlap, bounds, padding, determinism, and failure tests for the untrimmed path.
- [x] Exporter deterministic mapping tests.
- [x] Application vertical-slice tests using isolated temporary files and real adapters.
- [ ] CLI integration tests for arguments, exit codes, stdout/stderr, JSON mode, cancellation, and partial-output cleanup. Deterministic detect-command integration is covered; the remaining error/cancellation matrix is not.
- [x] View-model tests for selection, dirty state, undo/redo, validation, region correction, canvas coordinate transforms, connector edits, cancellation, repack, Save As, and export.

### Manual Windows checks

- [ ] Open files through picker and drag/drop.
- [ ] Zoom/pan and place connectors at 100%, fractional zoom, and high-DPI scaling.
- [ ] Cancel detection/repack/export without corrupting project state.
- [ ] Recover from missing source image and unwritable output directory.
- [ ] Confirm connector coordinates after save/reload and repack.
- [ ] Verify keyboard-only editing and visible focus.
- [ ] Verify dark and light themes.

### Verification command

Run the full solution build, all five focused test projects, Cobertura collection, and per-project line-coverage gates with:

```powershell
.\eng\verify.ps1
```

The thresholds intentionally differ by boundary: Domain 80%, Application 75%, Infrastructure 80%, CLI 70%, and the linked production Client view-model source 80%. Reports are ignored build artifacts under `.artifacts/coverage/`. Raise gates as uncovered error paths receive focused tests; do not lower them to make a change pass.

Latest verified results (2026-08-13):

| Production boundary | Tests | Line coverage | Branch coverage | Line gate |
| --- | ---: | ---: | ---: | ---: |
| Domain | 15 | 88.3% | 86.2% | 80% |
| Application | 13 | 86.6% | 64.5% | 75% |
| Infrastructure | 21 | 92.8% | 78.4% | 80% |
| CLI | 11 | 97.9% | 92.3% | 70% |
| Client workspace view model and picker rules | 18 | 85.7% | 55.9% | 80% |

## 12. Cross-cutting completion checklist

- [ ] Public types and commands use clear atlas terminology.
- [x] All async I/O accepts and propagates cancellation.
- [x] All file writes are staged and recoverable.
- [ ] Errors are structured in Application and rendered by each host.
- [x] Same options have the same defaults in native format, CLI, and MAUI.
- [ ] Format changes include migration, fixtures, tests, and documentation.
- [x] Exporter limitations are documented for users.
- [x] No MAUI or image-library types cross into Domain/Application.
- [x] No host duplicates core processing logic.
- [x] README and product docs are updated in the same change as user-visible behavior.

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

- [x] Validate SkiaSharp pixel access, coordinate transforms, PNG composition, and MAUI image rendering integration; measured high-end memory validation remains in Phase 6.
- [x] Reject OpenCvSharp for v1 because the bounded alpha connected-component operation does not justify a second native image stack.
- [x] Use the isolated, tested `deterministic-shelf-v1` fallback after the evaluated rectangle-packing packages failed the combined maintenance and feature gates.
- [x] Finalize `.saf.json` and `urn:driftya:sprite-atlas-forge:schema:v1` for native v1.
- [x] Emit connectors as documented additive custom Phaser frame fields.
- [ ] Define the metadata-preservation matching rule used when users re-run detection.
- [ ] Define large-image limits from measured Windows behavior, not arbitrary defaults.

These decisions are intentionally delayed because they depend on evidence. They must be recorded in the relevant documentation and tests when made.
