# AGENTS.md

Repository guidance for engineers and coding agents.

## Documentation Source of Truth

1. Start with `README.md` for current architecture/runtime entry points.
2. Use `docs/idea.md` for product intent, implemented gameplay rules, and UX direction.
3. Use `docs/concept/` images when making UI/UX updates so implementation stays aligned with concept direction.
4. Use `CONTRIBUTING.md` (root) for engineering standards and coding principles.

## Where New Documentation Goes

- Add new docs to `docs/`.
- Do not add long-form product or architecture docs in repository root.
- Exception: `CONTRIBUTING.md` remains in repository root.
- Suggested structure:
  - `docs/architecture.md`
  - `docs/game-rules.md`
  - `docs/changelog.md`

## Change Discipline

When changing behavior or UI flows:

1. Update code.
2. Update `docs/idea.md` if product behavior changed.
3. Update `README.md` if endpoints, architecture, setup, or user-visible capabilities changed.
4. If applicable, add/update supporting docs under `docs/`.

## Test Discipline

When making code changes, run the focused tests and both production hosts before finishing:

- Application tests:
  - `dotnet run --project tests/Driftya.SpriteAtlasForge.Application.Tests/Driftya.SpriteAtlasForge.Application.Tests.csproj --no-restore`
- CLI build:
  - `dotnet build src/Driftya.SpriteAtlasForge.CliApplication/Driftya.SpriteAtlasForge.CliApplication.csproj --nologo`
- Windows MAUI build:
  - `dotnet build src/Driftya.SpriteAtlasForge.ClientApplication/Driftya.SpriteAtlasForge.ClientApplication.csproj -f net10.0-windows10.0.19041.0 --nologo`

If your change touches additional projects with tests, run those relevant test projects too.
If any test cannot be run, clearly state that and why in your final update.


## Required CodeMeridian Usage

Use CodeMeridian proactively. Prefer graph tools over terminal scans when the graph can answer the question.

### Trigger rules

| Situation | Tool to call |
|-----------|--------------|
| Before any non-trivial edit | `build_minimal_context` |
| Before editing a specific method/class | `resolve_exact_symbol`, then `get_context_for_editing` |
| Before a refactor | `find_impact` and `find_test_shield` |
| Before deleting code | `find_unreferenced` |
| Before starting a feature | `analyze_feature_implementation_path`, then `find_implementation_surface` for exact targets |
| Before trusting exact file targets | `check_graph_freshness` or `find_graph_drift` |
| "How do X and Y relate?" | `find_connection` |
| Looking for duplicate/refactor risk | `find_duplicate_candidates` or `find_similar_nodes` |
| Looking for missing tests | `find_coverage_gaps` |
| Searching docs/decisions | `search_documentation` |
| Working with config | `find_config_definitions` and `find_config_usage` |
| Looking for keyword-related context | `find_related_knowledge` |

Store durable findings with `ingest_document` when they may help future sessions.

projectContext can be found in meridian.json in field project.
