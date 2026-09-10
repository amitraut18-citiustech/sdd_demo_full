# TASKS.md — Member Eligibility Check (Phase 1)
# Location : specs/Members_Eligibility/tasks.md
# Committed : YES
# Status    : READY
# Author    : Amit Raut | Date: 2026-09-10
# Depends on: plan.md DRAFT
#
# PURPOSE: Dependency-ordered, executable task list.
#          Each task is small enough for one Claude Code session.
#          Each task has a clear done condition.
#          Claude executes these in order — one task per session.
#
# RULES:
#   - Never skip a task
#   - Never combine tasks from different phases
#   - Mark [x] when done before starting the next task
#   - Run /spec-review after every phase
#   - Run /hipaa-check after Phase 3
# ─────────────────────────────────────────────────────────────

---

## ⚠ Unresolved blockers carried over from plan.md — do not silently resolve

1. **`database/init.sql` is not an authorized file in plan.md's scope**, yet the `eligibility_records` table (Phase 2) and the `PT001240` seed row required for AC-03 (spec.md OQ-04) can only be created there — this repo has no EF Core Migrations tooling. Phase 2 cannot be verified end-to-end, and AC-03 cannot be tested, until plan.md's scope is explicitly extended or an EF Migration is adopted (plan.md §3.2/§7).
2. **spec.md AC-08 vs. design-note.md's `EligibilityRecord` schema conflict is still open**: the entity built in Phase 1 stores only `correlationId`, `status`, `checkedAt`, `dataSource` (no `patientId`/`healthPlanId`), so AC-08 as currently worded in spec.md (which requires those two fields) will not pass as written (design-note.md Open Questions, plan.md Risk table).

Both are called out again at the phase where they bite, below. This task list does not invent a resolution for either.

---

## Progress

- Phase 1 — Model Layer     : [ ] Not started
- Phase 2 — Data Layer      : [ ] Not started
- Phase 3 — Controller      : [ ] Not started
- Phase 4 — Frontend        : Deferred — out of scope (plan.md §5; not part of this task list)
- Phase 5 — Verification    : [ ] Not started

---

## Phase 1 — Model Layer
*No dependencies — start here*
*Pattern references: read `Models/Entities.cs` and `DTOs/Dtos.cs` before any task (plan.md §1)*

---

### Task 1.1 — Add `EligibilityRecord` entity
**File:** `backend/PriorAuth.API/Models/Entities.cs`
**Action:** Append class at the bottom of the file (plan.md §3.1) — do not touch any existing class

```
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

No relationships or FKs to any existing entity (plan.md §3.1).
**Pattern reference:** `[Table]`/`[Column]` attribute style used by every entity in this file; closest structural analog is `AuthorizationStatusHistory` (a standalone audit-style record), though `EligibilityRecord` has no FK back to another table.

**Done when:** `dotnet build` passes with zero errors
**AC covered:** AC-08 (audit record shape — see blocker 2 above: this shape does not yet satisfy AC-08 as spec.md currently words it)
**Status:** [ ] Done

---

### Task 1.2 — Add `EligibilityCheckRequest` DTO
**File:** `backend/PriorAuth.API/DTOs/Dtos.cs`
**Action:** Append record at the bottom of the file — do not touch any existing record

```
public record EligibilityCheckRequest(string PatientId, int HealthPlanId, string? CorrelationId);
```

**Pattern reference:** `CreateAuthorizationRequest` in the same file — record type, required fields first, optional fields nullable.

**Done when:** `dotnet build` passes
**AC covered:** AC-01 (request contract — plan.md §4.1/§4.2)
**Status:** [ ] Done

---

### Task 1.3 — Add `EligibilityCheckResponse` DTO
**File:** `backend/PriorAuth.API/DTOs/Dtos.cs`
**Action:** Append record after `EligibilityCheckRequest`

```
public record EligibilityCheckResponse(
    string Status,
    string CorrelationId,
    DateTime CheckedAt,
    string? ErrorCode,
    string? ErrorMessage);
```

**Pattern reference:** existing response records in the same file (e.g. `AuthorizationDetailDto`) — record type, nullable fields declared `?`.

**Done when:** `dotnet build` passes
**AC covered:** AC-01, AC-02, AC-03, AC-04, AC-05, AC-06, AC-11 (every AC that inspects this response shape)
**Status:** [ ] Done

**→ Run /spec-review after this task. AC-01 should show PASS for the request/response contract shape (not yet the runtime behavior — no controller exists until Phase 3).**

---

## Phase 2 — Data Layer
*Depends on: Phase 1 complete*
*Pattern reference: read `Data/PriorAuthDbContext.cs` before starting*
*⚠ Blocked end-to-end by blocker 1 above — the `eligibility_records` table itself cannot be created by any file this plan authorizes. The task below only wires the C# side; it does not create the table.*

---

### Task 2.1 — Register `EligibilityRecord` in `PriorAuthDbContext`
**File:** `backend/PriorAuth.API/Data/PriorAuthDbContext.cs`
**Action:** One addition only

```
Add DbSet property (with the other DbSet<T> declarations):
public DbSet<EligibilityRecord> EligibilityRecords => Set<EligibilityRecord>();

No OnModelCreating change — EligibilityRecord has no navigation properties
to configure (plan.md §2.2). Do not modify any existing OnModelCreating block.
```

**Pattern reference:** existing `DbSet<T>` declarations in the same file, e.g. `DbSet<AuthorizationStatusHistory> AuthorizationStatusHistory => Set<AuthorizationStatusHistory>();`

**Done when:** `dotnet build` passes. (Note: this does not mean the table exists in the running database — see blocker 1. Running the endpoint against a live database is not possible until that's resolved.)
**AC covered:** AC-08
**Status:** [ ] Done

**→ Run /spec-review after this task.**

---

## Phase 3 — Controller
*Depends on: Phases 1 and 2 complete*
*Pattern reference: read `Controllers/AuthorizationsController.cs` before starting*

---

### Task 3.1 — Create `EligibilityController`
**File:** `backend/PriorAuth.API/Controllers/EligibilityController.cs` ← NEW FILE
**Action:** Create new controller (plan.md §4.1/§4.2)

```
[ApiController]
[Route("api/[controller]")]
Constructor: inject PriorAuthDbContext + ILogger<EligibilityController>
  (same shape as AuthorizationsController — no separate service class)

[HttpPost("check")] — accepts EligibilityCheckRequest, returns
ActionResult<EligibilityCheckResponse>:

  1. Validate patientId non-empty and healthPlanId present — 400 if malformed
  2. correlationId = request.CorrelationId ?? Guid.NewGuid().ToString()
  3. Look up Member by patientId, AsNoTracking()
       not found → status = ERROR, errorCode = MBR-001   (member checked first)
       found     → continue
  4. Look up HealthPlan by healthPlanId, AsNoTracking()
       not found → status = ERROR, errorCode = PLN-001
       found     → compare member.PlanCode == healthPlan.PlanCode (exact, case-sensitive)
                     equal      → status = ELIGIBLE
                     not equal  → status = INELIGIBLE  (includes NULL plan_code)
  5. Persist one EligibilityRecord: correlationId, status, checkedAt = DateTime.UtcNow,
     dataSource = "LOCAL_DB"
  6. ONE ILogger entry per call, no PHI, no patientId/healthPlanId:
     _logger.LogInformation("Eligibility check completed: {CorrelationId} {Status}",
                             correlationId, status)
  7. Return EligibilityCheckResponse(status, correlationId, checkedAt, errorCode, errorMessage)
```

**Done when:** `dotnet build` passes
**AC covered:** AC-01, AC-02, AC-03, AC-04, AC-05, AC-06, AC-08, AC-11
**Status:** [ ] Done

---

### Task 3.2 — Confirm no `Program.cs` change is needed
**File:** `backend/PriorAuth.API/Program.cs`
**Action:** No edit (plan.md §2.2 — explicit) — verify only

```
No service class is introduced (K2 simplicity — design-note.md), so there is
no AddScoped line to add. MapControllers() already auto-discovers
EligibilityController via [ApiController] reflection; EF Core already
auto-discovers the new DbSet by convention. Confirm zero diff to this file.
```

**Done when:** `dotnet build` passes. `dotnet run` starts without error. `git diff` on `Program.cs` is empty.
**AC covered:** AC-10
**Status:** [ ] Done

**→ Run /hipaa-check. Must show COMPLIANT before Phase 4 (still N/A/deferred — see Progress).**
**→ Run /spec-review. All ACs should be PASS or GAP — no FAIL. Expect AC-03 to show GAP (blocker 1: no seed data) and AC-08 to show GAP or FAIL (blocker 2: schema conflict) until those are resolved.**

---

## Phase 4 — Frontend

**Deferred — out of scope for this task list.** plan.md §5 explicitly defers all frontend wiring (`frontend/src/api/client.ts`, `frontend/src/types/index.ts`, `frontend/src/pages/NewAuthorizationPage.tsx` step 4) to a follow-up plan, since plan.md's dependency order stops at Step 3 (Controller) with no Step 4. No tasks are defined here; AC-07 (wizard `canProceed()` unaffected by result) cannot be verified until that follow-up plan exists.

---

## Phase 5 — Verification
*Run after Phase 1–3 complete. Backend-only — AC-07 is excluded (Phase 4 not in scope).*

---

### Task 5.1 — Runtime AC verification

Test each AC in Swagger (`http://localhost:5000/swagger`) using the exact records from spec.md §8 Test Data:

| AC | Test Input | Expected | Pass? |
|----|------------|----------|-------|
| AC-01 | patientId=PT001234, healthPlanId=1 | HTTP 200, status=ELIGIBLE | [ ] |
| AC-02 | patientId=PT001234, healthPlanId=2 | HTTP 200, status=INELIGIBLE | [ ] |
| AC-03 | patientId=PT001240, healthPlanId=1 | HTTP 200, status=INELIGIBLE | [ ] BLOCKED — PT001240 does not exist until blocker 1 is resolved |
| AC-04 | patientId=PT999999, healthPlanId=1 | HTTP 200, status=ERROR, errorCode=MBR-001 | [ ] |
| AC-05 | patientId=PT001234, healthPlanId=999 | HTTP 200, status=ERROR, errorCode=PLN-001 | [ ] |
| AC-06 | any valid request | response time < 500ms | [ ] |
| AC-08 | any request | exactly one `eligibility_records` row written | [ ] — see blocker 2 re: expected columns |
| AC-11 | patientId=PT999999, healthPlanId=999 | HTTP 200, status=ERROR, errorCode=MBR-001 | [ ] |

**Status:** [ ] Done

---

### Task 5.2 — Final skill checks

```
/hipaa-check    → must show OVERALL STATUS: COMPLIANT
/spec-review    → must show no FAIL results (GAP acceptable only for AC-03/AC-08 pending blockers 1/2)
```

| Check | Result | Pass? |
|-------|--------|-------|
| /hipaa-check | COMPLIANT / NON-COMPLIANT | [ ] |
| /spec-review | FAIL count: [N] | [ ] |

**Status:** [ ] Done

---

### Task 5.3 — Fill EVAL.md

Open `specs/Members_Eligibility/eval.md`
Fill in: build output, AC results (Task 5.1 table), `/spec-review` paste, `/hipaa-check` paste.

**Done when:** All non-blocked ACs (AC-01, AC-02, AC-04, AC-05, AC-06, AC-11) are PASS. AC-03/AC-08 recorded as GAP with blocker references. `eval.md` committed.
**Status:** [ ] Done

---

### Task 5.4 — Update CLAUDE.md

Add new patterns introduced by this feature to CLAUDE.md.

```
## Patterns Added — 2026-09-10
- Inline-controller audit table: a controller with no service layer can
  own a dedicated, minimal audit entity (EligibilityRecord) with no FKs
  to existing tables — Controllers/EligibilityController.cs,
  Models/Entities.cs
```

**Done when:** CLAUDE.md committed with new patterns.
**Status:** [ ] Done
