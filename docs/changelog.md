# Changelog

## 2026-08-13

- Fixed the Windows file-picker extension failure by limiting picker filters to validated dot-prefixed Windows extensions.
- Added portable Save As behavior that copies referenced source/atlas assets atomically.
- Added editable sprite regions in MAUI and the `sprite region` CLI command.
- Added MAUI canvas sprite bounds, connector dots and labels, click placement, and drag movement with zoom-safe coordinate conversion.
- Added dirty-state protection, undo/redo, progress reporting, and cancellation to the editor workflow.
- Added MAUI export-format and repack-option controls plus configurable detection dimension/pixel safety limits.
- Normalized corrupt PNG decoder failures into stable recoverable data errors.
- Expanded Application, CLI, and Client regression coverage.
