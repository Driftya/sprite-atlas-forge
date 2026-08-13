# Sprite Atlas Forge native format v1

Sprite Atlas Forge stores projects as UTF-8 JSON descriptors ending in `.saf.json`. The descriptor is canonical; the PNG remains a separate, ordinary image asset. The checked-in JSON Schema identifier is `urn:driftya:sprite-atlas-forge:schema:v1` and its source is [`docs/schema/sprite-atlas-forge-v1.schema.json`](schema/sprite-atlas-forge-v1.schema.json).

## Compatibility

- `formatVersion` is currently `1`.
- Readers ignore unknown additive JSON members.
- Readers reject unknown format versions and invalid required data with a path-aware diagnostic.
- Writers use camel-case names, two-space indentation, ordinal property ordering, and top-to-bottom/left-to-right sprite ordering.
- Asset paths are relative, use `/`, and cannot traverse outside the project directory.
- Saves use a temporary file in the destination directory followed by an atomic move.

## Coordinates

All coordinates are integer pixels. Image coordinates start at the top-left. `sourceRegion` addresses the imported source PNG, while `frame` addresses the current atlas PNG. Connector coordinates are relative to the sprite's untrimmed local bounds. A connector can lie on the boundary (`0..width`, `0..height`) but not outside it.

Connector names and sprite IDs are case-insensitively unique within their respective scopes. Connector array order is preserved for predictable editor behavior but is not its identity.

## Custom properties

The `properties` object holds game-specific primitive metadata. Version 1 supports null, string, number, and boolean values. Generic atlas behavior never interprets fields such as `position`, `weight`, or `minPopulation`.

## Repacking

Original-sheet mode is the default and leaves pixels untouched. Repacking is explicit. A repacked descriptor records its algorithm, padding, power-of-two choice, and maximum dimensions under `atlas.packing`. Version 1 never rotates sprites. Connectors stay in logical sprite-local coordinates when frames move.

The current fallback algorithm is `deterministic-shelf-v1`. It is isolated behind `IAtlasPacker` because the evaluated NuGet packers did not meet the combined adoption, maintenance, and feature gates. SkiaSharp performs PNG decoding, composition, and encoding.

## Phaser JSON Hash export

The Phaser exporter writes standard frame, source-size, trim, atlas-size, and image metadata. Native `connectors`, `tags`, and `properties` are included as additional per-frame JSON members so metadata is not silently discarded. Consumers that require a strict schema may ignore or strip those additive fields.
