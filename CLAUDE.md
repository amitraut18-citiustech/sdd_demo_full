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

---

## 4. Compliance Rules
*Non-negotiable — violation blocks PR*

### 4.1 HIPAA

**NEVER log these fields in any `ILogger` call** (confirmed from `Member` in `Models/Entities.cs`):
- `patientId`, `firstName`, `lastName`, `dateOfBirth`, `gender`, `languageCode`
- `addressLine1`, `addressLine2`, `city`, `state`, `zipCode`
- `phone`, `emailAddress`, `memberCode`, `groupNumber`, `ipaCode`

Also avoid logging the equivalent identifying fields on `Provider`/`Site` (`npi`, `tin`, `phone`, `fax`, `emailAddress`, `cellPhone`, address fields) — confirmed from `Models/Entities.cs`. Note: this schema has no `ssn` field.

**NEVER hardcode:** connection strings, API keys, passwords, tokens in code.
`appsettings.json` currently ships a plaintext local-dev Postgres connection string — acceptable only as the existing local-dev default already in the repo. Every place that needs it reads it via `builder.Configuration.GetConnectionString("DefaultConnection")` (confirmed `Program.cs`); never inline a connection string or secret directly in C#.

**ALWAYS write:** one structured audit log entry per sensitive operation
- Confirmed existing pattern: `AuthorizationsController.Create` logs `_logger.LogInformation("Authorization created: {RefNumber}", refNumber)` — reference number only, no PHI. Follow this shape (identifier only, never the PHI fields above).
- Status transitions are persisted as rows in `authorization_status_history` (`previousStatus`, `newStatus`, `changedAt` — confirmed `AuthorizationStatusHistory` entity), but `UpdateStatus` does **not** currently emit an `ILogger` call or a `correlationId`.
- **Gap:** no `correlationId` concept exists anywhere in this codebase today (not in entities, DTOs, or controllers). Any new sensitive operation (e.g. an eligibility check) must be the first to introduce it — do not assume one already exists to reuse.

**ALWAYS generate:** correlationId if not provided — use `Guid.NewGuid()`. This is a rule for new code; nothing in the current codebase does this yet.

---

## 5. Naming Conventions
*Confirmed from reading existing files — not invented*

| Artifact | Pattern | Confirmed from |
|---|---|---|
| Controller | `[Domain]sController.cs`; small lookup controllers share one file, larger domains get their own | `LookupControllers.cs` (Members, Providers, Sites, HealthPlans, DiagnosisCodes, ProcedureCodes), `AuthorizationsController.cs` |
| DTO | `[Feature]Dto` record; request bodies use an explicit `...Request` suffix instead | `DTOs/Dtos.cs` — e.g. `MemberDto`, `AuthorizationSummaryDto`, `CreateAuthorizationRequest`, `UpdateStatusRequest` |
| EF Entity | `[Domain]` class, `[Table("snake_case")]`, each property `[Column("snake_case")]`, nullable props use `?` | `Models/Entities.cs` |
| React page | `[Name]Page.tsx` | `pages/DashboardPage.tsx`, `pages/NewAuthorizationPage.tsx` |
| React component | `[Name].tsx` | `components/SearchSelect.tsx` |
| API client method | added to the `api` object in `client.ts` | `api/client.ts` |
| TypeScript type | added to `types/index.ts`, mirrors the backend DTO field-for-field in camelCase | `types/index.ts` |

---

## 6. Route Conventions
*Confirmed from existing controllers*

```
Pattern  : /api/[controller]
Attribute: [ApiController] + [Route("api/[controller]")] on every controller
Examples : GET   /api/members            GET  /api/members/{id}
           GET   /api/providers          GET  /api/sites
           GET   /api/healthplans        GET  /api/diagnosiscodes
           GET   /api/procedurecodes
           GET   /api/authorizations?status=&page=&pageSize=
           GET   /api/authorizations/{id:int}
           POST  /api/authorizations
           PATCH /api/authorizations/{id:int}/status
           GET   /api/authorizations/{id:int}/history
           POST  /api/eligibility/check  ← new feature
```

No controller uses the `[action]` token — sub-resource actions are a literal path segment after the id (`/status`, `/history`), confirmed from `AuthorizationsController.cs`.

---

## 7. Dependency Injection Rules
*Confirmed from Program.cs*

This codebase registers **zero custom services** — there is no service/repository layer. Controllers take `PriorAuthDbContext` (and `ILogger<T>` where needed) directly via constructor injection.

```csharp
// Confirmed pattern — Program.cs
builder.Services.AddDbContext<PriorAuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// If a new feature needs a service class, this is the only registration
// pattern to introduce — nothing else exists in this codebase to follow:
builder.Services.AddScoped<IInterface, Implementation>();

// NEVER
builder.Services.AddSingleton<...>()   // not used anywhere in this codebase — stateful, concurrency bugs
builder.Services.AddTransient<...>()   // not used anywhere in this codebase — new DB connection per call
new MyService()                         // bypasses DI — no existing controller does this
```

---

## 8. What Claude Must Do When Uncertain

1. STOP generation immediately
2. Write the uncertainty to `OPEN_QUESTIONS.md` tagged `[BLOCKING]` or `[NON-BLOCKING]`
3. `[BLOCKING]`     — Do not proceed. Surface to the human.
4. `[NON-BLOCKING]` — State assumption explicitly. Continue. Flag for review.

**Never resolve ambiguity silently.**
**Never pick one interpretation without surfacing the alternatives.**

---

## 9. Approved Packages
*No additions without updating this section*

### Backend
*Confirmed from `backend/PriorAuth.API/PriorAuth.API.csproj`*
- Npgsql.EntityFrameworkCore.PostgreSQL 8.0.4
- Microsoft.EntityFrameworkCore.Design 8.0.4
- Swashbuckle.AspNetCore 6.6.2

### Frontend
*Confirmed from `frontend/package.json`*
- react 18.3.1
- react-dom 18.3.1
- react-router-dom 6.26.0
- typescript 5.5.3 (dev)
- vite 5.4.0 (dev)
- @vitejs/plugin-react 4.3.1 (dev)
- @types/react 18.3.3, @types/react-dom 18.3.0 (dev)
