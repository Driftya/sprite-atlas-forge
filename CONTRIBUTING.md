# Contributing

This repository follows pragmatic engineering rules designed to keep delivery fast and code quality high.

## Core Principles

- `KISS`: choose the simplest solution that satisfies current requirements.
- `DRY`: avoid duplicated logic; extract shared behavior when duplication is proven.
- `SOLID`: apply where it improves maintainability, not as ceremony.
- `Clean Code`: clear naming, small focused functions, explicit error paths, and readable intent.

## Platform Standards

- All projects target `.NET 10`.
- The desktop client uses `.NET MAUI` and supports Windows in v1.
- The CLI and MAUI client share Application/Infrastructure services in-process.
- Runtime architecture boundaries should stay aligned with Onion layering:
  - `Domain` pure rules
  - `Application` use-cases/orchestration
  - `Infrastructure` external systems
  - `CliApplication` command-line transport and composition
  - `ClientApplication` MAUI presentation and desktop composition

## .NET Guidelines

- Prefer async I/O end-to-end.
- Keep endpoint handlers thin; move logic into application services.
- Validate input early and return consistent problem details.
- Keep domain rules deterministic and unit-testable.
- Use DI for dependencies; avoid hidden static state.

## Desktop Guidelines (.NET MAUI)

- Prefer composition over inheritance.
- Keep pages and controls focused and presentational when possible.
- Keep state local first; lift only when shared.
- Keep editor state in view models or Application session models rather than code-behind.
- Use semantic properties and keyboard alternatives for interactive canvas operations.
- Keep styling changes localized and avoid layout regressions.

## NuGet Guidelines

- Centralize package versions in `Directory.Packages.props`.
- Float within an approved major version (`10.*`, `8.*`, `2.*`) so minor and patch updates are automatic on restore.
- Keep major upgrades manual and review release notes, licensing, migration impact, and golden-output changes.
- Prefer stable packages; prerelease dependencies require a documented blocker and removal plan.
- Run NuGet audit and the full verification suite after dependency changes.

## Pattern Guidance (Refactoring.Guru-aligned)

Use patterns based on the problem, not by default.

### Creational

- `Factory Method` / `Abstract Factory`:
  - Use when object creation branches by runtime type/context.
- `Builder`:
  - Use for complex object assembly (for example composed mutation payloads).

### Structural

- `Adapter`:
  - Use at boundaries when integrating third-party or legacy APIs.
- `Facade`:
  - Use to provide simple entrypoints over multi-step orchestration.

### Behavioral

- `Strategy`:
  - Use for interchangeable algorithms (scoring, policy variants).
- `Template Method`:
  - Use when flow is fixed but a few steps vary.
- `Observer`:
  - Already reflected in event push patterns (SignalR updates).
- `Command`:
  - Use for queue messages and deferred operations.

If introducing a new pattern, document the reason and tradeoff in the PR.

## Refactoring Rules

- Refactor in small, reviewable steps.
- Preserve behavior first; improve structure second.
- Add or update tests around changed behavior.
- Avoid broad rewrites unless explicitly requested.

## Documentation Rules

When behavior changes:

1. Update relevant code.
2. Update `docs/idea.md` for product behavior changes.
3. Update `docs/architecture.md` for runtime/endpoint/flow changes.
4. Update `docs/game-rules.md` for gameplay/governance changes.
5. Add an entry in `docs/changelog.md`.

## Commit/PR Expectations

- Prefer small commits with focused intent.
- Include test/build evidence in PR notes.
- Call out risks, migrations, and follow-up tasks explicitly.

## Required verification

Run the focused tests and both production-host builds before finishing a code change:

```powershell
dotnet run --project tests/Driftya.SpriteAtlasForge.Application.Tests/Driftya.SpriteAtlasForge.Application.Tests.csproj --no-restore
dotnet build src/Driftya.SpriteAtlasForge.CliApplication/Driftya.SpriteAtlasForge.CliApplication.csproj --nologo
dotnet build src/Driftya.SpriteAtlasForge.ClientApplication/Driftya.SpriteAtlasForge.ClientApplication.csproj -f net10.0-windows10.0.19041.0 --nologo
```
