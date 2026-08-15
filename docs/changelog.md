# Changelog

## v1.0.1

- Added opt-in recovery for faint and detached sprite details in Auto detection, available for a whole sheet or an individual selected sprite.
- Added the `--recover-detached-details` CLI option and real-image regression coverage for generated ship-module ornaments.
- Preserved Delete-key editing while hiding the transient Windows accelerator hint.
- Added Left/Right sprite selection when no resize border is active, while retaining arrow-key border nudging.

## v1.0.0

- Prepared the Windows MAUI client for its first production release with v1.0.0 assembly and display metadata.
- Replaced the default .NET application icon and splash artwork with the Sprite Atlas Forge brand icon.
- Added GitHub CI for full verification and a self-contained Windows x64 build artifact.
- Added tag-driven draft GitHub Releases with a versioned ZIP, dependency inventory, and SHA-256 checksum.
- Documented the release, smoke-test, tagging, and publication procedure.

## 2026-08-15

- Added opt-in Auto detached-detail recovery over the original low-alpha silhouette, plus an undoable selected-sprite recovery action; nearby details are assigned to their nearest unambiguous sprite.
- Kept the Delete keyboard shortcut while hiding WinUI's transient accelerator hint, and added Left/Right navigation to cycle between sprites when no canvas border is selected.

## 2026-08-14

- Fixed Windows Save As by using an initialized storage picker and suggesting the complete native project filename.
- Added approval-gated Unity 6 multi-sprite and Godot 4 `AtlasTexture` export formats to the CLI and desktop editor.
- Added red border-side selection and arrow-key one-pixel sprite-region correction in the Windows canvas editor.
- Fixed arrow-key border nudging when child controls hold focus and prevented approval changes from re-entering the native checkbox event.
- Improved Auto detection so small isolated edge details within eight pixels of exactly one sprite are included in that sprite's detected bounds.

## 2026-08-13

- Fixed the Windows file-picker extension failure by limiting picker filters to validated dot-prefixed Windows extensions.
- Added portable Save As behavior that copies referenced source/atlas assets atomically.
- Added editable sprite regions in MAUI and the `sprite region` CLI command.
- Added MAUI canvas sprite bounds, connector dots and labels, click placement, and drag movement with zoom-safe coordinate conversion.
- Added dirty-state protection, undo/redo, progress reporting, and cancellation to the editor workflow.
- Added MAUI export-format and repack-option controls plus configurable detection dimension/pixel safety limits.
- Normalized corrupt PNG decoder failures into stable recoverable data errors.
- Added generated-art detection cleanup that filters small opaque artifacts, breaks thin noise bridges, and groups disconnected content contained inside a sprite's transparent regions.
- Added automatic border-connected background removal for fully opaque generated sheets, with shared MAUI and CLI mode/tolerance controls.
- Improved automatic detection with an alpha-histogram cutoff for multicolored low-alpha watermark noise that connects otherwise separate sprites.
- Labeled every desktop detection-cleanup input with its meaning and units, warned that large merge distances can combine a whole sheet, and added regression coverage for continuous colored alpha 1-3 noise.
- Fixed Auto detection for generated sheets whose semi-transparent shadow or watermark matte occupies the middle of the alpha histogram, with `ship-modules-02.png` retained as a real regression fixture.
- Made high-alpha refinement connectivity-gated and added `ship-modules-01.png` as a paired fixture so Auto preserves semi-transparent module silhouettes instead of keeping only their opaque cores.
- Combined Auto's alpha components with bounded seeded magic-wand refinement, splitting coarse regions that contain multiple confident cores and restoring each sprite's softer edge pixels without reconnecting neighboring markers.
- Made marker splitting layout-aware: Auto now requires a low-density vertical gutter and substantial content on both sides, preventing small high-alpha details inside one sprite from becoming standalone regions.
- Made normal merge distances compare labeled component pixels rather than bounding rectangles, preserving separate diagonal/interlocking sprites while retaining explicit containment grouping.
- Added undoable MAUI actions to create a manual sprite region and delete the selected sprite.
- Added direct image selection and eight drag handles for undoable source-region resizing on original (unrepacked) sheets.
- Replaced per-sprite MAUI border/label controls with one drawn overlay so repeated zoom no longer rebuilds the full sprite visual tree.
- Preserved small nearby engine/plume fragments by attaching below-minimum components only when exactly one qualifying sprite is nearby, without allowing them to bridge two sprites.
- Fixed canvas sprite clicks being swallowed by the full-size connector layer while retaining draggable connector controls.
- Added a right-sidebar action to save the selected displayed sprite frame as a lossless PNG crop.
- Added zoom-stable snapping to image and overlapping sprite edges when resizing, including cyan alignment guides.
- Added right-button drag panning with coalesced scroll updates for the zoomed atlas canvas.
- Added Delete-key removal for the selected sprite while preserving Delete inside text inputs.
- Added snapping back to a sprite's own original edge and right-button wheel zoom anchored at the pointer.
- Extended bounded Auto recovery for blue-dominant engine glow so low-alpha flare tails reach the detected sprite edge without pulling in neutral shadows.
- Added a hold-Shift bypass for sprite-border snapping during canvas resize drags.
- Expanded Application, CLI, and Client regression coverage.
