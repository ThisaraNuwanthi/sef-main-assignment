# ADR 0001 — State management in the React web app

- **Status:** Accepted
- **Date:** 2026-09-29

## Context

The Admin/Coach web app has two very different kinds of state:

1. **Client state** — who is logged in (user, role, JWT). Small, rarely changes, needed on every page and by the router.
2. **Server state** — classes, enrolments, workflows, the dashboard report. It lives in the API, can go stale,
   and every screen needs loading/error/empty handling, caching, re-fetching after a change, and (for the workflow
   review page) polling while the agents run.

## Options considered

| Option | For | Against |
|---|---|---|
| Redux Toolkit (+ RTK Query) | Industry standard, devtools | Most boilerplate; slices/actions for one auth object is overkill |
| Zustand + hand-written fetching | Tiny API | Still need to write caching, loading flags, refetch and polling by hand |
| **React Context (auth) + TanStack Query (server data)** | Each tool does one job; Query gives caching, `isPending`/`error`, `invalidateQueries`, `refetchInterval` | Two concepts to learn |
| `useEffect` + `useState` everywhere | No dependencies | Duplicated, bug-prone fetching logic on every page |

## Decision

- **Auth:** a plain React Context (`src/auth/AuthContext.tsx`) holding the user, with the session in `localStorage`.
- **Server data:** **TanStack Query v5**. Query keys include the filters (`['classes', filters]`), mutations call
  `invalidateQueries` after a change, the workflow page polls with `refetchInterval` only while the status is
  `Queued`/`Running`, and `placeholderData: keepPreviousData` keeps the table visible while the next page loads.

## Consequences

- Pages contain almost no fetching code; loading/error/empty states come straight from `useQuery`.
- On logout the query cache is cleared so one user never sees another's data.
- Storing the JWT in `localStorage` is readable by any script on the page (XSS). Mitigated by React's automatic
  escaping (parent notes are rendered as text), no `dangerouslySetInnerHTML`, and an 8-hour token lifetime. An
  httpOnly cookie would be safer but needs CSRF protection and same-site hosting for API and web — not justified
  for this project's scope.
