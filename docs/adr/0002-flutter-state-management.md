# ADR 0002 — State management in the Flutter parent app

- **Status:** Accepted
- **Date:** 2026-09-29

## Context

The parent app shares three pieces of state between screens: the logged-in parent (which also drives which
screens are allowed), the parent's children (list screen + the enrolment form's dropdown), and the parent's
enrolments (list with search/filter). Everything else is local to one screen.

## Options considered

| Option | For | Against |
|---|---|---|
| `setState` only | No package | Shared data would be passed through constructors everywhere |
| **Provider + `ChangeNotifier`** | Recommended in the official Flutter docs for app state; tiny API (`context.watch` / `context.read`); notifiers are plain Dart classes that are easy to unit test | Relies on `BuildContext`; errors if a provider is missing are found at run time |
| Riverpod | Compile-time safety, no `BuildContext`, better for large apps | More concepts (providers of providers, `ref`, often code generation) than three notifiers need |
| Bloc | Very explicit events/states | Most boilerplate of the four |

## Decision

**Provider** with three `ChangeNotifier` classes in `lib/state/`: `AuthState`, `ChildrenState`, `EnrolmentsState`.
They are created once in `SkcaApp` with `MultiProvider`. go_router uses `AuthState` as its `refreshListenable`, so
logging in or out immediately re-runs the redirect that protects screens.

## Consequences

- Screens call `context.watch<T>()` to rebuild on change and `context.read<T>()` to call methods.
- The notifiers depend on `ApiClient`, which takes an injected `http.Client`, so tests swap in `MockClient` and
  `InMemoryTokenStorage` without any mocking framework.
- If the app grew much larger (many screens, derived/async state everywhere), migrating to Riverpod would be the
  natural next step.
