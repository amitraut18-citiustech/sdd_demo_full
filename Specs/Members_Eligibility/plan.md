# PLAN.md — Member Eligibility Check (Phase 1)
# Location : specs/Members_Eligibility/plan.md
# Committed : YES
# Status    : DRAFT
# Author    : Amit Raut | Date: 2026-09-10
# Depends on: spec.md DRAFT, design-note.md DRAFT
#
# PURPOSE: Define HOW the feature will be built.
#          Answers: architecture decisions, file changes, data model, API design.
#          SPEC.md says what. PLAN.md says how.
#          TASKS.md breaks this into executable steps.
#
# CONSTITUTION CHECK: Every decision here must comply with CLAUDE.md.
#   Before approving this plan ask:
#   ✓ Does every new file follow naming conventions in CLAUDE.md section 5?
#   ✓ Does the route follow CLAUDE.md section 6?
#   ✓ Does DI registration follow CLAUDE.md section 7?
#   ✓ Does data access follow CLAUDE.md section 2.1?
#   ✓ Are all compliance rules in CLAUDE.md section 4 respected?
# ─────────────────────────────────────────────────────────────

---

## 1. Technical Approach

This plan implements the eligibility check as one new controller, `EligibilityController`, that injects `PriorAuthDbContext` directly (no service/repository layer — matching design-note.md's "K2 simplicity" decision and CLAUDE.md's documented architecture: "no service/repository layer; controllers inject `PriorAuthDbContext` directly"). The controller queries `Members` and `HealthPlans` read-only via EF Core LINQ, compares `plan_code` values in memory, writes one `EligibilityRecord` row per check, and returns `ELIGIBLE`/`INELIGIBLE`/`ERROR`.

Per this task's explicit file scope, the new `EligibilityRecord` entity and the two new DTOs are appended to the existing shared `Models/Entities.cs` and `DTOs/Dtos.cs` files rather than split into new per-feature files as design-note.md's Components Affected section proposed. This is actually a closer match to CLAUDE.md §2.1's documented convention ("DTO pattern: C# record types — all in one `Dtos.cs` file") and the observed fact that every existing entity already lives in one `Entities.cs` file. It remains fully additive: no existing class, record, or line in either file is changed — only new declarations are appended at the end.

Scope note: this plan covers backend Steps 1–3 only (Model → Data → Controller), matching the dependency order given for this plan. Frontend wiring (`api/client.ts`, `types/index.ts`, `NewAuthorizationPage.tsx` step 4) was scoped in design-note.md's Components Affected but is **not** part of this plan's file list or dependency order — it is deferred to a follow-up plan/task (see Section 5 and Section 7).

---

## 2. Files to Change

### 2.1 New Files — Create
| File | Purpose | Pattern Reference |
|------|---------|-------------------|
| `backend/PriorAuth.API/Controllers/EligibilityController.cs` | `POST /api/eligibility/check` — looks up member + health plan, compares `plan_code`, writes one `EligibilityRecord`, returns status | Read `Controllers/AuthorizationsController.cs` first — same constructor-injection shape (`PriorAuthDbContext` + `ILogger<T>`), same `[ApiController]`/`[Route("api/[controller]")]` pattern |

### 2.2 Existing Files — Modify
| File | Change | Risk |
|------|--------|------|
| `backend/PriorAuth.API/Models/Entities.cs` | Append `EligibilityRecord` class at end of file — no relationships/FKs to any existing entity | Low — additive only, no existing class touched |
| `backend/PriorAuth.API/DTOs/Dtos.cs` | Append `EligibilityCheckRequest` and `EligibilityCheckResponse` records at end of file | Low — additive only, no existing record touched |
| `backend/PriorAuth.API/Data/PriorAuthDbContext.cs` | Append one line: `public DbSet<EligibilityRecord> EligibilityRecords => Set<EligibilityRecord>();`. No `OnModelCreating` change — `EligibilityRecord` has no navigation properties to configure | Low — additive only, single new line |
| `backend/PriorAuth.API/Program.cs` | **No change required.** Listed as an allowed additive target, but this plan introduces no service class (K2 simplicity — design-note.md), so there is no `AddScoped` line to add; `MapControllers()` already auto-discovers the new controller via `[ApiController]` reflection, and EF Core auto-discovers the new `DbSet` via convention. This plan makes zero edits to `Program.cs`. | None — no diff |

### 2.3 Files — Do Not Touch
| File | Reason |
|------|--------|
| `backend/PriorAuth.API/Controllers/AuthorizationsController.cs` | Existing feature — read only, as the pattern reference for constructor injection and controller shape |
| `backend/PriorAuth.API/Controllers/LookupControllers.cs` | Existing feature — not in scope |
| Every existing class/record inside `Models/Entities.cs` and `DTOs/Dtos.cs` | Explicit instruction — only new declarations are appended; nothing existing is edited |
| `backend/PriorAuth.API/Data/PriorAuthDbContext.cs` `OnModelCreating` method body | Explicit instruction — no FK/relationship config needed or added for `EligibilityRecord` |

**Flagged gap — not authorized in this plan's scope:** `database/init.sql` is not listed in New, Modify, or Do-Not-Touch above, but it must change for this feature to run at all: (1) this repo has no EF Core Migrations tooling (confirmed — no `Migrations/` folder), so the `eligibility_records` table can only be created by hand-written SQL there, same as every existing table; (2) spec.md §8/OQ-04 requires one new synthetic seed row (`PT001240`, `plan_code NULL`) for AC-03. **This blocks Step 2 (Data Layer) from being runnable** until `database/init.sql` is explicitly authorized as an additive-modify target, or an EF Core Migration is adopted instead (which touches no existing file — see Section 7, Risk 1).

---

## 3. Data Model

### 3.1 New Entity
```csharp
[Table("eligibility_records")]
public class EligibilityRecord
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("correlation_id")]
    public Guid CorrelationId { get; set; }

    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [Column("checked_at")]
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    [Column("data_source")]
    public string DataSource { get; set; } = "LOCAL_DB";
}
```
No `patientId`/`healthPlanId`/PHI columns — per design-note.md's Data Handling Design and its flagged divergence from spec.md AC-08 (still unresolved — see that file's Open Questions).

### 3.2 Database Migration
- [x] New table required: `eligibility_records`
- [x] Migration needed: YES — but see the flagged gap in Section 2.3: no file is currently authorized to carry this change. Options: (a) extend this plan's scope to include `database/init.sql`, or (b) introduce `dotnet ef migrations add AddEligibilityRecords` (creates a new `Migrations/` folder — no existing file touched, but introduces EF Migrations tooling to a project that has never used it, which is itself a scope decision).
- [ ] If YES — migration script: **undetermined — blocked on the decision above**

---

## 4. API Design

### 4.1 Controller
```
Class    : EligibilityController
Route    : [Route("api/[controller]")]  → /api/eligibility
Action   : [HttpPost("check")]           → POST /api/eligibility/check
Returns  : ActionResult<EligibilityCheckResponse>
Injects  : PriorAuthDbContext, ILogger<EligibilityController>
```

### 4.2 Request → Response Flow
```
Request arrives (EligibilityCheckRequest: patientId, healthPlanId, correlationId?)
  → Validate required fields (patientId non-empty, healthPlanId present) — 400 if malformed
  → correlationId = request.CorrelationId ?? Guid.NewGuid().ToString()   (spec.md FR-07)
  → Look up Member by patientId, AsNoTracking()
      → not found → status = ERROR, errorCode = MBR-001   (member checked first — FR-04/OQ-09)
      → found     → continue
  → Look up HealthPlan by healthPlanId, AsNoTracking()
      → not found → status = ERROR, errorCode = PLN-001
      → found     → compare member.PlanCode == healthPlan.PlanCode (exact, case-sensitive)
                      → equal      → status = ELIGIBLE
                      → not equal  → status = INELIGIBLE (includes NULL plan_code)
  → Persist one EligibilityRecord (correlationId, status, checkedAt = DateTime.UtcNow, dataSource = "LOCAL_DB")
  → _logger.LogInformation("Eligibility check completed: {CorrelationId} {Status}", correlationId, status) — no PHI, no patientId/healthPlanId
  → Return EligibilityCheckResponse(status, correlationId, checkedAt, errorCode, errorMessage)
```

### 4.3 Error Handling
| Condition | Error Code | HTTP Status | Response |
|-----------|------------|-------------|----------|
| Member not found | MBR-001 | 200 | Status = ERROR in body |
| Member not found AND health plan not found | MBR-001 (member precedence) | 200 | Status = ERROR in body |
| Health plan not found (member found) | PLN-001 | 200 | Status = ERROR in body |
| Malformed request body | — | 400 | ASP.NET Core model-validation error |
| Unhandled exception | SYS-001 | 500 | Generic error — no details, no PHI |

---

## 5. Frontend Plan

**Deferred — out of scope for this plan.** design-note.md's Components Affected section scoped frontend changes (`frontend/src/api/client.ts`, `frontend/src/types/index.ts`, `frontend/src/pages/NewAuthorizationPage.tsx` step 4), but this plan's given file list and dependency order (Section 6) cover backend Steps 1–3 only, with no Step 4. A follow-up plan (or an added Step 4 to this one) is needed before the frontend can call `POST /api/eligibility/check`.

---

## 6. Dependency Order
*Tasks.md will use this — defines what can run in parallel*

```
Step 1: Model Layer       — Entities.cs (EligibilityRecord) + Dtos.cs (EligibilityCheckRequest/Response)
        No dependencies. Start here.
        ↓
Step 2: Data Layer        — PriorAuthDbContext.cs (DbSet<EligibilityRecord>)
        Depends on: Step 1 (entity must exist)
        Blocked on: the database/init.sql gap in Section 2.3 / 3.2 — the table must exist
        in the running Postgres instance before this step is testable end-to-end.
        ↓
Step 3: Controller        — EligibilityController.cs
        Depends on: Steps 1 and 2
        (No Program.cs change — see Section 2.2)
```

---

## 7. Risks and Mitigations

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| `database/init.sql` isn't in this plan's authorized file list, but the `eligibility_records` table and the `PT001240` seed row (spec.md OQ-04) can only be created there (no EF Migrations tooling exists in this repo) | High | Resolve before Step 2 starts: explicitly extend this plan's scope to include `database/init.sql`, or adopt EF Core Migrations. Flagged, not silently decided. |
| Existing local Postgres containers already have a volume from before this change — `database/init.sql` only runs on first container creation | Medium | `docker compose down -v && docker compose up -d` to force a reseed, per CLAUDE.md Commands — call this out explicitly in TASKS.md so it isn't missed during dev/testing |
| Design-note.md's `EligibilityRecord` schema (no `patientId`/`healthPlanId`) still conflicts with spec.md AC-08/OQ-10, which require those fields in the audit record | Medium | Already flagged as BLOCKING in design-note.md's Open Questions — spec.md AC-08 needs to be updated to match before this plan's Step 3 can be marked "AC-verified" |
| No test project exists in this repo yet (CLAUDE.md Commands) | Medium | A new `backend/PriorAuth.API.Tests` project (xUnit) is required to exercise AC-01–AC-11; not part of this plan's Steps 1–3 file list, needs its own task |
| CORS not configured for the new route | Low | Already configured project-wide in `Program.cs` (`ReactDev` policy, `AllowAnyMethod`) — the new route inherits it with zero changes |

---

## 8. Constitution Compliance Check

- [x] All new files follow naming conventions — CLAUDE.md section 5 (`EligibilityController.cs` matches `[Domain]Controller.cs`; `EligibilityCheckRequest`/`EligibilityCheckResponse` match the `[Feature]...Request`/`Dto` record convention; `EligibilityRecord` matches `[Table]`/`[Column]`-annotated entity convention)
- [x] Route follows pattern — CLAUDE.md section 6 (`POST /api/eligibility/check`, pre-approved by CLAUDE.md's own §6 example)
- [x] DI uses AddScoped only — CLAUDE.md section 7 (N/A here: no service class is introduced, so no `AddScoped` line is added at all — see Section 2.2)
- [x] No raw SQL in application code — CLAUDE.md section 2.1 (`EligibilityController` uses EF Core LINQ only; the only raw SQL is the pre-existing pattern of hand-written DDL in `database/init.sql`, same as every other table, not application-code SQL)
- [x] No PHI in audit logs — CLAUDE.md section 4.1 (`EligibilityRecord` and the single `ILogger` call carry only `correlationId`, `status`, `checkedAt`, `dataSource` — no PHI field from the §4.1 list)
- [x] No new packages added without updating CLAUDE.md section 9 (none introduced by Steps 1–3; a future xUnit test project, if added, would need a §9 update at that time)
- [x] All non-goals from SPEC.md section 7 are absent from this plan:
  - Real-time payer API integration — not present (local `PriorAuthDbContext` queries only, no `HttpClient`)
  - Blocking PA submission on `INELIGIBLE`/`ERROR` — not present (this plan implements only an independent, advisory endpoint; it makes no change to the wizard's `canProceed()` gating, since frontend is out of scope here)
  - Date-based eligibility — not present (no effective/termination date fields read or compared)
  - Benefit-level/procedure-specific eligibility — not present (no procedure-code logic)
  - Caching of results — not present (no cache layer added)
  - React UI changes — not present (Section 5 is fully deferred)
