# ADR 0005 — Deployment platform

- **Status:** Accepted
- **Date:** 2026-09-29

## Context

We need public URLs for the API (with `/swagger` and `/health`), the React app and a PostgreSQL database, on
**free tiers**, deployable by a student without a working international credit card, with secrets kept out of git.
The API is already containerised (`backend/Dockerfile`, multi-stage, non-root) and tested locally with Docker.

## Options considered (API)

| Option | Result |
|---|---|
| Render (free web service) | Planned first (`render.yaml` was written). **Blocked**: the free plan still requires card verification, and the card was not accepted. |
| Hugging Face Spaces (Docker) | Docker Spaces are now a **paid** feature. |
| Azure for Students | Card-free with a university email and a natural fit for .NET, but slower to set up before the deadline. |
| **Railway** | Deploys straight from the GitHub repo using our Dockerfile, no card for the trial, auto-deploys on push. |

Database: **Neon** (serverless PostgreSQL, free, Singapore region, TLS required).
Web: **Vercel** (free Hobby plan, builds `web/` with Vite on every push).

## Decision

- **API → Railway** from GitHub, root directory `backend`, config in `backend/railway.json`
  (Dockerfile build, health check `/health`, restart on failure). The app listens on Railway's `$PORT`.
- **Database → Neon.** EF Core migrations and the demo seed run automatically at API startup
  (`Database:MigrateOnStartup`, `Seed:Enabled`), so there is no separate migration step.
- **Web → Vercel**, root `web`, `VITE_API_BASE_URL` set in the Vercel project; `web/vercel.json` rewrites all paths
  to `index.html` so deep links work.
- **Mobile →** `flutter build apk --release --dart-define=API_BASE_URL=<Railway URL>`.
- **Secrets** (`ConnectionStrings__Default`, `Jwt__Key`, `GEMINI_API_KEY`, `Seed__DemoPassword`) live only in the
  Railway variables; `Cors__AllowedOrigins__0` is the Vercel URL.

## Consequences

- Every push to `main` runs CI (GitHub Actions) and redeploys both the API (Railway) and web app (Vercel).
- Railway's trial credit is limited; the service stops when it runs out, so usage must be checked before demos
  and marking. Moving to Azure App Service or any Docker host needs no code change — only the same environment variables.
- The container filesystem is not persistent: uploaded child photos are lost on redeploy. A production version
  would store them in object storage (e.g. Azure Blob / S3); the `IPhotoStorage` interface makes that a one-class change.
- Neon's free tier suspends idle compute; the first query after a pause takes a second or two.
