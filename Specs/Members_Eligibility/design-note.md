# DESIGN-NOTE.md — Member Eligibility Check (Phase 1)
# Location : specs/Members_Eligibility/design-note.md
# Committed : YES
# Status    : DRAFT
# Author    : Amit Raut | Date: 2026-09-10
# Depends on: spec.md DRAFT (see Open Questions — one divergence flagged below), risk-classification.md COMPLETE
#
# PURPOSE   : Capture the technical design decisions, security and
#             privacy considerations, audit implications, and approval
#             gates BEFORE task breakdown and implementation.
#             Step 5 of the SDAD Golden Path — Plan.
#             Produced alongside PLAN.md and TASKS.md.
#
# SOURCE    : Derived from design-note-template.md and sdad-golden-path.md
#             Step 5 in the SDAD Golden Repo Kit.
# ─────────────────────────────────────────────────────────────

---

## Feature

Member Eligibility Check (Phase 1)

---

## Summary of Approach

Implement the check as a single new, thin controller (`EligibilityController`) that injects `PriorAuthDbContext` directly and queries `Members`/`HealthPlans` with EF Core LINQ — no separate service/repository class, matching the "K2 simplicity" decision and the existing codebase convention that controllers own their own data access (CLAUDE.md Architecture; spec.md OQ-06). The comparison (`member.PlanCode == healthPlan.PlanCode`) happens in memory against data already in the local database; Phase 1 makes no outbound calls to any payer or eligibility API (spec.md FR-08, Non-Goals). Only the check's outcome — never the underlying PHI — is persisted, in a new minimal `EligibilityRecord` table.

---

## Components Affected

**Backend (all new files — zero edits to existing controllers/entities/DTOs, per spec.md Constraint 1):**
- `backend/PriorAuth.API/Controllers/EligibilityController.cs` — new. `[ApiController]`, `[Route("api/[controller]")]`, single `POST` action, constructor-injects `PriorAuthDbContext` and `ILogger<EligibilityController>` (same shape as `AuthorizationsController`).
- `backend/PriorAuth.API/DTOs/EligibilityDtos.cs` — new. `EligibilityCheckRequest(string PatientId, int HealthPlanId, string? CorrelationId)`, `EligibilityCheckResponse(string Status, string CorrelationId, DateTime CheckedAt, string? ErrorCode, string? ErrorMessage)`.
- `backend/PriorAuth.API/Models/EligibilityRecord.cs` — new entity, `[Table("eligibility_records")]`, no relationships/FKs to any existing entity.

**Backend (additive edits to existing files — one line each, no existing line changed):**
- `backend/PriorAuth.API/Data/PriorAuthDbContext.cs` — add `public DbSet<EligibilityRecord> EligibilityRecords => Set<EligibilityRecord>();`. No `OnModelCreating` change needed — `EligibilityRecord` has no navigation properties (see Data Handling Design).
- `database/init.sql` — add the `CREATE TABLE eligibility_records (...)` statement and the one synthetic seed row (`PT001240`, `plan_code NULL`) that spec.md §8/OQ-04 requires. The project has no EF Core Migrations tooling (confirmed — no `Migrations/` folder), so schema changes are hand-written SQL here, same as every existing table.

**Frontend (additive edits to existing files):**
- `frontend/src/api/client.ts` — add one method to the `api` object (e.g. `checkEligibility(patientId, healthPlanId)`), per CLAUDE.md §5.
- `frontend/src/types/index.ts` — add the matching TypeScript types for the request/response.
- `frontend/src/pages/NewAuthorizationPage.tsx` — wizard step 4 (member selection) fires the check once both a health plan (step 3) and a member (step 4) are selected, and renders the result as a non-blocking inline indicator. `canProceed()` is not touched by the result (spec.md FR-05, AC-07).

**Test project (new — none exists today):**
- CLAUDE.md's own Commands section states "There is no test project in this repo yet." This feature is the first to need automated tests (spec.md AC-01–AC-11), so a new xUnit test project (e.g. `backend/PriorAuth.API.Tests`) is required — see Open Questions.

---

## Data Handling Design

- The request carries only `patientId` and `healthPlanId` — identifiers, not PHI payloads. `correlationId` is optional and generated with `Guid.NewGuid()` when omitted (spec.md FR-07).
- The controller loads the `Member` and `HealthPlan` rows via EF Core LINQ (`.AsNoTracking()`, read-only) strictly to (a) confirm existence and (b) read the two `plan_code` values to compare. This is the only point at which PHI (the `Member` row's name, DOB, address, phone, email, etc.) is touched — it stays in a local variable for the duration of the request and is never returned in the response, never written to `EligibilityRecord`, and never passed to `ILogger`.
- `EligibilityRecord` (the only new persisted state) stores exactly four fields: `correlationId` (Guid), `status` (`ELIGIBLE`/`INELIGIBLE`/`ERROR`), `checkedAt` (UTC timestamp), `dataSource` (string constant, `"LOCAL_DB"` for Phase 1 — a forward-compatible marker for when Phase 2 adds a real-time payer source, per spec.md FR-08/Non-Goals; no Phase 2 behavior is implemented now). It intentionally does **not** store `patientId`, `healthPlanId`, or any PHI field — see the divergence from spec.md flagged in Open Questions below.
- Comparison is an exact, case-sensitive string match on `plan_code` (spec.md Constraint 7) — no normalization, no partial matching.

---

## Security / Privacy Considerations

Referencing `risk-classification.md`:

| Risk question | Answer | Mitigation in this design |
|---|---|---|
| Involves PHI or identity data? | YES | PHI is read transiently for the lookup only; never persisted, never logged, never returned beyond the pass/fail status. `EligibilityRecord` carries no identifiers at all. |
| Affects authentication/authorization? | NO | No auth exists anywhere in this app today (CORS-only); this feature introduces none. |
| Affects clinical/claims/billing/payment logic? | NO | Plan-code string comparison only — no clinical decisioning. |
| Affects audit logging or compliance evidence? | YES | New `EligibilityRecord` table is the compliance evidence; schema is deliberately minimal (see Audit section). |
| Introduces new external tool/MCP access? | NO | Local DB only, per spec.md FR-08. |
| Relies on production-like data? | YES | All test data is synthetic seed data from `database/init.sql` (spec.md §8), including the new `PT001240` row — no production data used anywhere. |
| Is the change reversible? | YES | Purely additive — see Rollback below. |
| Are tests sufficient? | YES (planned) | See Test Strategy — new test project required before this can be called done. |

Because the "Involves PHI" and "Affects audit logging" rows are both YES, `risk-classification.md`'s own risk-question table calls for privacy/security review and audit-evidence review as required actions.

---

## Audit and Observability Considerations

- One `EligibilityRecord` row is written per check, regardless of outcome (`ELIGIBLE`, `INELIGIBLE`, or `ERROR`) — satisfying spec.md FR-06/AC-08's "exactly one audit record" requirement.
- Fields included: `correlationId`, `status`, `checkedAt`, `dataSource`. Fields explicitly excluded: `patientId`, `healthPlanId`, and every PHI field in CLAUDE.md §4.1's list (name, DOB, address, phone, email, memberCode, groupNumber, ipaCode, languageCode, gender).
- A single `ILogger.LogInformation` call (e.g. `"Eligibility check completed: {CorrelationId} {Status}"`) mirrors the existing, already-compliant precedent in `AuthorizationsController.Create` (`"Authorization created: {RefNumber}"`) — identifier + outcome only, never PHI, never `patientId`/`healthPlanId`.
- Confirmed: correlationId used ✅. No raw PHI anywhere in the audit trail or logs ✅. No full request/response payload is logged or persisted ✅.
- **Divergence from spec.md (flagged, not silently resolved):** spec.md's current AC-08, its NFR "Compliance" row, and OQ-10 all require the audit record to contain `patientId` and `healthPlanId` (OQ-10 resolved that those two fields are *not* PHI and are therefore safe to store). This design note instead excludes them entirely, per this task's explicit key decision ("EligibilityRecord: correlationId, status, checkedAt, dataSource only"). This is a stricter, simpler, and arguably safer posture, but it means **AC-08 as currently written in spec.md will fail against this design** (an audit-record inspection would not find `patientId`/`healthPlanId`). See Open Questions.

---

## Test Strategy

- **No test project exists yet** (CLAUDE.md Commands section) — this feature requires creating one (proposed: `backend/PriorAuth.API.Tests`, xUnit, referencing `PriorAuth.API`).
- **Integration tests** against `POST /api/eligibility/check`, covering every row in spec.md §8 Test Data / AC-01 through AC-11: `ELIGIBLE` (AC-01), `INELIGIBLE` on mismatch (AC-02), `INELIGIBLE` on `NULL` plan_code (AC-03, requires the new `PT001240` seed row), `ERROR MBR-001` for unknown member (AC-04), `ERROR PLN-001` for unknown health plan (AC-05), `ERROR MBR-001` when both are unknown (AC-11, member-precedence per FR-04/OQ-09).
- **Performance assertion** for AC-06 — elapsed-time check that a local-DB round trip stays under 500ms.
- **Audit assertion** — after any check, query `EligibilityRecords` and assert exactly one new row with the four allowed fields populated and no others (AC-08, adjusted per the divergence noted above).
- **Frontend** — a component/manual QA check that the wizard's step 4 → step 5 transition (`canProceed()`) is unaffected by any returned status (AC-07).
- **Compliance gates** — `/hipaa-check` (AC-09) and `dotnet build` with zero warnings (AC-10) run as part of PR readiness, not as unit tests.
- All test data is synthetic, sourced only from `database/init.sql` seed rows (existing + the one new `PT001240` row) — no production-like data is introduced (risk-classification.md requirement).

---

## Rollback / Failure Handling

- The change is **purely additive**: new controller, new DTOs, new entity, one new `DbSet` line, one new SQL table + seed row, and additive (non-breaking) frontend changes. No existing controller, entity, DTO, route, or Program.cs registration is modified — nothing else in the system can regress.
- `EligibilityRecord` has no foreign keys to or from any existing table, so it can be dropped independently with zero cascading impact.
- Rollback = revert the feature branch/PR, drop the `eligibility_records` table and its seed row, remove the frontend API call and UI indicator. Existing PA-creation flow (wizard steps 1–3, 5–6, and submission) is untouched and keeps working even if this feature is fully reverted.
- Failure handling: an unhandled DB error returns HTTP 500 `SYS-001` (spec.md §4.4) rather than crashing the request; because the check is advisory-only (FR-05) and called from a separate endpoint, a failure here never blocks or corrupts PA request creation — the wizard simply shows no eligibility result and the agent proceeds as before this feature existed.

---

## Open Questions

- **[BLOCKING]** This design's `EligibilityRecord` schema (correlationId, status, checkedAt, dataSource only) contradicts spec.md AC-08, its NFR "Compliance" row, and OQ-10, all of which currently require `patientId`/`healthPlanId` in the audit record. Recommend updating spec.md to match this narrower, PHI-identifier-free design before task breakdown, since implementing to this design note as written will fail AC-08 as currently worded.
- **[NON-BLOCKING]** `dataSource` is a new field with no enumerated value list anywhere in spec.md. Assumption: a single constant, `"LOCAL_DB"`, for all of Phase 1.
- **[NON-BLOCKING]** No test framework is specified anywhere in CLAUDE.md (no test project exists yet). Assumption: xUnit, as the standard choice for an ASP.NET Core 8 project, in a new `backend/PriorAuth.API.Tests` project.
- **[NON-BLOCKING]** The Approval Gates below include Security review at this task's explicit instruction, even though `risk-classification.md`'s own "Required Human Review Gates" checklist has Security review **unchecked** (only Tech lead, QA/validation, and Product owner are checked there) — despite that same file's risk-question table saying "Privacy/security review required if yes" for the PHI row, which is answered YES. Recommend reconciling `risk-classification.md`'s checkbox list to check Security (and likely Privacy) review, so the two sections of that file agree with each other and with this design note.
- **[NON-BLOCKING]** Exact column types/lengths for `eligibility_records` (e.g. `status VARCHAR(20)`) are a `database/init.sql`-authoring detail left to task breakdown, not pinned here.

---

## Approval Gates

- [X] Architect / Tech lead review
- [X] Security review — per this task's instruction; note the mismatch with `risk-classification.md`'s own checkbox flagged above
- [ ] Privacy review
- [ ] Domain SME review
- [X] QA / validation review
