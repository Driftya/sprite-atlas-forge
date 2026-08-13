# Contributing

This repository follows pragmatic engineering rules designed to keep delivery fast and code quality high.

## Core Principles

- `KISS`: choose the simplest solution that satisfies current requirements.
- `DRY`: avoid duplicated logic; extract shared behavior when duplication is proven.
- `SOLID`: apply where it improves maintainability, not as ceremony.
- `Clean Code`: clear naming, small focused functions, explicit error paths, and readable intent.

## Platform Standards

- Backend targets `.NET 10`.
- Frontend uses `React` + `TypeScript` + `Vite`.
- Runtime architecture boundaries should stay aligned with Onion layering:
  - `Domain` pure rules
  - `Application` use-cases/orchestration
  - `Infrastructure` external systems
  - `Web` transport and composition

## Backend Guidelines (.NET 10)

- Prefer async I/O end-to-end.
- Keep endpoint handlers thin; move logic into application services.
- Validate input early and return consistent problem details.
- Keep domain rules deterministic and unit-testable.
- Use DI for dependencies; avoid hidden static state.

## Frontend Guidelines (React)

- Prefer composition over inheritance.
- Keep components focused and presentational when possible.
- Keep state local first; lift only when shared.
- Derive computed values with memoization only when needed.
- Use semantic HTML and ARIA where interaction needs it.
- Keep styling changes localized and avoid layout regressions.

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
