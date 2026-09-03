# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ABC Healthcare Prior Authorization System — a demo app for the SDD + Claude Code CLI workshop. React SPA + ASP.NET Core 8 Web API + PostgreSQL. It currently implements PA request intake (dashboard, 6-step new-request wizard) and all lookup endpoints. **Member Eligibility verification is intentionally not implemented** — it's the feature the workshop adds via Spec-Driven Development (a `SPEC.md` defines it, this file governs Claude Code behavior while building it).

## Commands

### Database (Docker/PostgreSQL)
```bash
docker compose up -d            # start Postgres, seeds via database/init.sql on first run
docker compose ps                # check pa_db is "healthy"
docker compose down -v           # reset: drop volume + recreate with fresh seed data
```

### Backend (`backend/PriorAuth.API`)
```bash
cd backend/PriorAuth.API
dotnet run                       # http://localhost:5000, Swagger at /swagger
dotnet build
```
There is no test project in this repo yet.

### Frontend (`frontend`)
```bash
cd frontend
npm install
npm run dev                      # http://localhost:5173, proxies /api -> localhost:5000 (vite.config.ts)
npm run build                    # tsc then vite build
```
There is no lint config or test runner set up in the frontend yet (ESLint is only a recommended VS Code extension, not configured).

All three pieces (Postgres, API, Vite dev server) must run concurrently for the app to work end-to-end.

## Architecture

**Data flow:** PostgreSQL (`database/init.sql` — schema + seed data, snake_case columns) → EF Core entities (`backend/PriorAuth.API/Models/Entities.cs`, mapped via `[Table]`/`[Column]` attributes) → controllers project entities into `record` DTOs (`DTOs/Dtos.cs`, camelCase) → frontend `api` client (`frontend/src/api/client.ts`) → typed as matching interfaces (`frontend/src/types/index.ts`, hand-kept in sync with the DTOs — no codegen).

**Backend** is a single-project minimal-hosting ASP.NET Core app (`Program.cs`) — no service/repository layer; controllers inject `PriorAuthDbContext` directly and use EF Core LINQ queries. Two controller files:
- `Controllers/LookupControllers.cs` — one small `ControllerBase` per lookup entity (Members, Providers, Sites, HealthPlans, DiagnosisCodes, ProcedureCodes). Search endpoints filter case-insensitively on a single field and always cap results (`.Take(20)`/`.Take(30)`/`.Take(50)`).
- `Controllers/AuthorizationsController.cs` — CRUD for the core `Authorization` entity, including `MapToDetail`, the manual entity→DTO mapping used by both `GetById` and `Create`. `Create` also fans out into `AuthorizationProcedures`/`AuthorizationDiagnoses` join rows and generates the reference number as `PA-{yyyyMMdd}-{5-digit-random}` (not from `auth_ref_seq`, which exists in the schema but is unused by app code).

Entity relationships all use nullable FKs with `DeleteBehavior.SetNull` (configured in `PriorAuthDbContext.OnModelCreating`), except the `AuthorizationProcedure`/`AuthorizationDiagnosis` → code-table FKs, which use `DeleteBehavior.Restrict`.

**Frontend** is a 3-route SPA (`App.tsx`: `/`, `/new`, `/authorization/:id`) with no state management library — each page owns its own `useState`. `NewAuthorizationPage.tsx` is a single-component 6-step wizard driven by a `WizardState` object built up across steps (program → provider → health plan → member → diagnosis/CPT → site+notes+review); step validity gates are in `canProceed()`. The reusable `SearchSelect<T>` generic component (`components/SearchSelect.tsx`) implements debounced (300ms) search-as-you-type with a selected/chip state, used for provider, member, and site pickers; diagnosis/procedure code pickers in step 5 reimplement similar search-dropdown logic inline instead of reusing `SearchSelect`, since those steps need multi-select accumulation rather than single-selection replacement.

Connection string, CORS origins (`localhost:5173`/`3000`), and the Vite dev proxy target (`localhost:5000`) assume the default local dev ports from the Quick Start above — check `appsettings.json`, `Program.cs`, and `vite.config.ts` together if changing any port.
