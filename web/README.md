# SKCA Enrol — Admin & Coach web app

React 19 + TypeScript + Vite. Talks **only** to the ASP.NET Core API.

| Concern | Choice |
|---|---|
| Routing | React Router 7, `ProtectedRoute` checks login + role |
| Auth state | React Context (`src/auth/AuthContext.tsx`), session in localStorage |
| Server data | TanStack Query (caching, loading/error states, polling) |
| Charts | Recharts |
| Tests | Vitest + React Testing Library |

```bash
cp .env.example .env.local   # set VITE_API_BASE_URL if the API is not on :5056
npm install
npm run dev                  # http://localhost:5173
npm test                     # unit/component tests
npm run build                # production build in dist/
```

Demo logins: `admin@skca.lk`, `coach.nimal@skca.lk` (password: the `Seed:DemoPassword` you set for the API).
Parents use the Flutter mobile app, so this site refuses parent accounts.
