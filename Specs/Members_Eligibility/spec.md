# SPEC.md — Member Eligibility Check (Phase 1)
# Location : specs/Members_Eligibility/spec.md
# Committed : YES
# Status    : DRAFT
# Author    : Amit Raut | Date: 2026-09-10
# Branch    : feature/member-eligibility-check
#
# PURPOSE: Define WHAT the feature does and WHY — not HOW.
# HOW is answered in PLAN.md.
# EVERY acceptance criterion must trace to a verifiable test.
# ─────────────────────────────────────────────────────────────

---

## 1. Overview

### 1.1 Problem Statement

Non-clinical intake agents creating a prior authorization (PA) request have no way to confirm, at the point of intake, that the member they selected is actually enrolled in the health plan they selected. Requests are currently submitted blind with respect to plan enrollment, and mismatches are only discovered later — after the request has already been created — causing rework. (`specs/Members_Eligibility/intent.md` — Problem)

### 1.2 Proposed Solution

Add an eligibility check to step 4 (member selection) of the existing PA request wizard. Once an agent has selected a health plan (step 3) and a member (step 4), the system looks up both records in the local database and compares `member.plan_code` to `health_plan.plan_code`, returning `ELIGIBLE`, `INELIGIBLE`, or `ERROR`. Phase 1 is a local database lookup only — no real-time payer API integration. (intent.md — Proposed Solution)

### 1.3 Success Criteria

The agent sees an eligibility result (`ELIGIBLE`/`INELIGIBLE`/`ERROR`) within 500ms of selecting a member and health plan, every check is captured in exactly one audit record containing a correlation ID and no PHI, and `dotnet build` / `/hipaa-check` both pass cleanly. (intent.md — Success Criteria)

---

## 2. Functional Requirements

### 2.1 Core Behaviour

When an agent has chosen a health plan and a member during PA request creation, the system checks whether the member is enrolled in that plan by comparing plan codes already stored in the database. The result is shown to the agent as advisory information only — it never blocks or gates continuing the wizard. Every check, regardless of outcome, is recorded once for audit purposes without storing any protected health information (PHI) beyond the identifiers needed to look the record back up.

FR-01: The system shall provide an eligibility check that accepts a member identifier and a health plan identifier and returns one of `ELIGIBLE`, `INELIGIBLE`, or `ERROR`.

FR-02: The system shall classify the result as `ELIGIBLE` when the member exists and `member.plan_code` equals `health_plan.plan_code`.

FR-03: The system shall classify the result as `INELIGIBLE` when the member exists but `member.plan_code` does not equal `health_plan.plan_code`, including when `member.plan_code` is `NULL`.

FR-04: The system shall classify the result as `ERROR` when the member cannot be found, the health plan cannot be found, or both. The member lookup is checked first — if both the member and the health plan cannot be found, the system returns the member-not-found error (`MBR-001`).

FR-05: The system shall treat the eligibility result as advisory only — the agent must be able to continue the PA wizard regardless of the returned status.

FR-06: The system shall persist exactly one audit record per eligibility check, containing a correlation ID, the identifiers checked, the resulting status, and a timestamp — and no PHI fields.

FR-07: The system shall generate a correlation ID using `Guid.NewGuid()` when the caller does not supply one.

FR-08: The system shall use only data already present in the local database (Members, HealthPlans) for the check — no outbound calls to any external payer or eligibility service.

### 2.2 User Flows

**Flow 1 — Eligibility check during PA wizard step 4:**
1. Agent has already selected a health plan in wizard step 3.
2. Agent selects a member in wizard step 4 (via the existing member `SearchSelect`).
3. Frontend calls `POST /api/eligibility/check` with the selected `patientId` and `healthPlanId`.
4. System looks up the member and health plan and compares plan codes.
5. System persists one audit record for the check.
6. System returns the status (`ELIGIBLE` / `INELIGIBLE` / `ERROR`) to the frontend.
7. Frontend displays the result next to the member selection as an advisory indicator.
8. Agent proceeds to step 5 regardless of the result.

**Flow 2 — Member or health plan not found:**
1. Agent selects a member and health plan combination where one side cannot be resolved (e.g., a stale/removed record).
2. Frontend calls `POST /api/eligibility/check`.
3. System cannot find the member and/or the health plan.
4. System returns `ERROR` with an error code identifying which lookup failed.
5. System still persists one audit record for the attempted check.
6. Agent sees the error indicator but can still proceed with the wizard (FR-05).

---

## 3. Acceptance Criteria
*Each criterion: testable, binary (pass/fail), observable (Swagger / log / test)*
*No "should", "mostly", or "approximately"*

| ID    | Given | When | Then | Verified By |
|-------|-------|------|------|-------------|
| AC-01 | Member `PT001234` (`plan_code=CIGNA`) and health plan `healthPlanId=1` (Cigna Health, `plan_code=CIGNA`) | `POST /api/eligibility/check` is called | Response is HTTP 200 with `status=ELIGIBLE` | Swagger |
| AC-02 | Member `PT001234` (`plan_code=CIGNA`) and health plan `healthPlanId=2` (Aetna Better Health, `plan_code=AETNA`) | `POST /api/eligibility/check` is called | Response is HTTP 200 with `status=INELIGIBLE` | Swagger |
| AC-03 | Member with `plan_code=NULL` (see OQ-04) and any valid `healthPlanId` | `POST /api/eligibility/check` is called | Response is HTTP 200 with `status=INELIGIBLE` | Swagger |
| AC-04 | Unknown `patientId` (e.g. `PT999999`) and a valid `healthPlanId` | `POST /api/eligibility/check` is called | Response is HTTP 200 with `status=ERROR`, `errorCode=MBR-001` | Swagger |
| AC-05 | Valid `patientId` and unknown `healthPlanId` (e.g. `999`) | `POST /api/eligibility/check` is called | Response is HTTP 200 with `status=ERROR`, `errorCode=PLN-001` | Swagger |
| AC-06 | Any valid eligibility check request against the local database | The endpoint is called | Total response time is under 500ms | Automated test with elapsed-time assertion |
| AC-07 | Agent is on PA wizard step 4 and has selected a member and health plan | The eligibility result (any status) is returned | The wizard's "Next" action to step 5 remains enabled — the result never gates `canProceed()` | Manual QA / component test |
| AC-08 | Any eligibility check | Is executed | Exactly one audit record is written containing correlationId, patientId, healthPlanId, status, and timestamp — `patientId`/`healthPlanId` are bare identifiers, not treated as PHI for this audit table (see OQ-10); no other PHI field (name, DOB, address, phone, email, etc.) appears | Audit record inspection |
| AC-09 | Any generated code for this feature | Is scanned | No PHI field names (CLAUDE.md §4.1) appear in any `ILogger` call | `/hipaa-check` → COMPLIANT |
| AC-10 | All code changes for this feature | `dotnet build` is run | Zero errors, zero warnings | Build output |
| AC-11 | Unknown `patientId` (e.g. `PT999999`) AND unknown `healthPlanId` (e.g. `999`) | `POST /api/eligibility/check` is called | Response is HTTP 200 with `status=ERROR`, `errorCode=MBR-001` (member-missing takes precedence per FR-04) | Swagger |

---

## 4. API Contract

### 4.1 Endpoint
```
POST /api/eligibility/check
Content-Type: application/json
```
Route follows the existing `/api/[controller]` convention (CLAUDE.md §6).

### 4.2 Request Schema
```json
{
  "patientId": "PT001234",
  "healthPlanId": 1,
  "correlationId": null
}
```

| Field | Type | Required | Validation | Notes |
|-------|------|----------|------------|-------|
| patientId | string | Yes | Non-empty; must match `members.patient_id` format used elsewhere (e.g. `PT001234`) | Same identifier already collected in wizard step 4 |
| healthPlanId | integer | Yes | Positive integer | Same identifier already collected in wizard step 3 |
| correlationId | string (GUID) | No | Valid GUID if supplied | Generated via `Guid.NewGuid()` if not provided (CLAUDE.md §4.1) |

### 4.3 Response Schema
```json
{
  "status": "ELIGIBLE",
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "checkedAt": "2026-09-10T18:32:00Z",
  "errorCode": null,
  "errorMessage": null
}
```

### 4.4 Error Responses

| HTTP Status | Error Code | Trigger |
|-------------|------------|---------|
| 200 | `MBR-001` | Member not found for the given `patientId` (returned in body as `status=ERROR`, not as an HTTP error). Member is checked first — if both `patientId` and `healthPlanId` fail to resolve, `MBR-001` is returned. |
| 200 | `PLN-001` | Health plan not found for the given `healthPlanId`, and the member was found (returned in body as `status=ERROR`) |
| 400 | — | Malformed request body (missing `patientId`, missing/non-integer `healthPlanId`) |
| 500 | `SYS-001` | Unhandled internal error (e.g. database unavailable) |

---

## 5. Non-Functional Requirements

| Category | Requirement |
|----------|-------------|
| Performance | Response time under 500ms for the local database lookup (intent.md Success Criteria #2) |
| Security | No PHI in `ILogger` calls or error messages (CLAUDE.md §4.1). The audit record (a DB table, not a log) may contain `patientId` and `healthPlanId` as bare lookup identifiers — see OQ-10 — but no other PHI field (name, DOB, address, phone, email, etc.) |
| Compliance | Exactly one audit record per check, containing correlationId + timestamp + operation result + patientId + healthPlanId (intent.md Behavior; CLAUDE.md §4.1, as clarified by OQ-10); `/hipaa-check` must return COMPLIANT |
| Reliability | A missing member or health plan returns `status=ERROR` in a normal 200 response, not an HTTP 4xx/5xx — only genuine unhandled failures return HTTP 500 `SYS-001` |

---

## 6. Constraints
*What MUST be respected — derived from CLAUDE.md*

1. No modification to existing controllers, entities, or DTOs (`AuthorizationsController.cs`, `LookupControllers.cs`, `Models/Entities.cs`, `DTOs/Dtos.cs`) — this feature is additive only.
2. Route follows the `/api/[controller]` convention from CLAUDE.md §6 — `POST /api/eligibility/check`.
3. If a service class is introduced, it must be registered with `AddScoped` in `Program.cs` (CLAUDE.md §7) — never `AddSingleton`, `AddTransient`, or manual `new`.
4. EF Core LINQ only — no raw SQL (CLAUDE.md §2.1/§3.1).
5. No PHI field names may appear in any `ILogger` call — CLAUDE.md §4.1 list: `patientId`, `firstName`, `lastName`, `dateOfBirth`, `gender`, `languageCode`, `addressLine1`, `addressLine2`, `city`, `state`, `zipCode`, `phone`, `emailAddress`, `memberCode`, `groupNumber`, `ipaCode`.
6. No new NuGet or npm packages without updating CLAUDE.md §9.
7. Plan-code comparison is an exact string match against the values already stored in the database — no case-normalization or fuzzy matching invented beyond what the seed data already uses (values are stored upper-case, e.g. `CIGNA`).
8. A correlation ID must be generated with `Guid.NewGuid()` whenever the caller doesn't supply one (CLAUDE.md §4.1).
9. Build must pass with zero errors and zero warnings (CLAUDE.md §3.1).

---

## 7. Non-Goals
*MINIMUM 4 entries — what this spec explicitly does NOT include*

- ❌ Real-time health plan / payer API integration — deferred to Phase 2 (intent.md Non-Goals)
- ❌ Blocking PA submission on `INELIGIBLE` or `ERROR` status — the check is advisory only (intent.md Behavior; Non-Goals)
- ❌ Date-based eligibility (effective/termination date checks) (intent.md Non-Goals)
- ❌ Benefit-level or procedure-specific coverage eligibility (intent.md Non-Goals)
- ❌ Caching of eligibility results across requests (not mentioned in intent.md — out of scope for Phase 1)
- ❌ Any React UI change beyond displaying the advisory result inline in wizard step 4 (no new wizard steps, no navigation changes)

---

## 8. Test Data
*Exact records that must exist for ACs to pass*

| patientId | member.plan_code | healthPlanId (name) | health_plan.plan_code | Expected Result |
|-----------|-------------------|----------------------|------------------------|------------------|
| PT001234 | CIGNA | 1 (Cigna Health) | CIGNA | ELIGIBLE (AC-01) |
| PT001234 | CIGNA | 2 (Aetna Better Health) | AETNA | INELIGIBLE (AC-02) |
| PT001240 *(new — see OQ-04)* | NULL | 1 (Cigna Health) | CIGNA | INELIGIBLE (AC-03) |
| PT999999 *(does not exist)* | N/A | 1 (Cigna Health) | CIGNA | ERROR `MBR-001` (AC-04) |
| PT001234 | CIGNA | 999 *(does not exist)* | N/A | ERROR `PLN-001` (AC-05) |
| PT999999 *(does not exist)* | N/A | 999 *(does not exist)* | N/A | ERROR `MBR-001` (AC-11 — both missing, member precedence) |

All rows except `PT001240` already exist in `database/init.sql` seed data. `PT001240` is a required new seed row (decision confirmed — see OQ-04) and must be added to `database/init.sql` as part of implementation, e.g.:
```sql
('PT001240', 'Test', 'NullPlan', '1990-01-01', 'M', 'EN', NULL, NULL, 'Los Angeles', 'CA', '90001', NULL, NULL, 'MBR10007', 'GRP001', 'IPA01', NULL)
```

---

## 9. Open Questions
*Claude writes here when uncertain — human resolves before PLAN.md is written*

| ID | Question | Tag | Assumption | Status |
|----|----------|-----|------------|--------|
| OQ-01 | intent.md doesn't specify the exact request field names for the check. | NON-BLOCKING | Request takes `patientId` (string) + `healthPlanId` (int), matching the identifiers the wizard already collects in steps 3–4 and the fields already used on `CreateAuthorizationRequest`. | OPEN |
| OQ-02 | intent.md doesn't define an error-code taxonomy for the `ERROR` status. | NON-BLOCKING | Two codes: `MBR-001` (member not found, matching SPEC.md template's own example) and `PLN-001` (health plan not found). | OPEN |
| OQ-03 | intent.md doesn't state whether plan-code comparison is case-sensitive. | NON-BLOCKING | Exact, case-sensitive string equality — all seed values are already stored upper-case, so no normalization is applied. | OPEN |
| OQ-04 | No member in the current seed data (`database/init.sql`) has `plan_code = NULL`, so AC-03 (the NULL-mismatch branch called out explicitly in intent.md's eligibility table) cannot be exercised against existing data. | BLOCKING | **Decision (human, 2026-09-10):** add a synthetic seed row (`PT001240`, `plan_code NULL`) to `database/init.sql` — see section 8 Test Data for the exact row. | RESOLVED |
| OQ-05 | intent.md says "one DB record per check" but doesn't specify where that record lives. | NON-BLOCKING | A new table (e.g. `eligibility_checks`) is created, following the existing `authorization_status_history` precedent, storing correlationId, patientId, healthPlanId, status, and checkedAt. Whether `patientId` is permitted in that table is resolved by OQ-10. | OPEN |
| OQ-06 | intent.md doesn't say whether this introduces a new service class or lives inline in a controller. | NON-BLOCKING | Follows the existing architecture (no service/repository layer — CLAUDE.md Architecture) — implemented inline in a new `EligibilityController` injecting `PriorAuthDbContext` directly. | OPEN |
| OQ-07 | intent.md doesn't specify UI placement or copy for the advisory result in wizard step 4. | NON-BLOCKING | A small inline badge/banner rendered next to the member `SearchSelect`, wired so it never affects `canProceed()`. | OPEN |
| OQ-08 | intent.md's "within 500ms" (Success Criteria #2) doesn't state which percentile or measurement environment. | NON-BLOCKING | Interpreted as typical (not p99) latency against the local dev/test database, measured per-request in an automated test. | OPEN |
| OQ-09 | FR-04 requires an `ERROR` result when both the member and health plan cannot be found, but section 4.4 didn't originally state which error code wins. | BLOCKING | **Decision (human, 2026-09-10):** member is checked first — if both are missing, treat it as member-missing and return `MBR-001`. Reflected in FR-04, §4.4, and AC-11. | RESOLVED |
| OQ-10 | CLAUDE.md §4.1 lists `patientId` among the "NEVER log" PHI fields, and its "ALWAYS write" audit rule says the audit entry "must not contain any PHI field listed above" — which appeared to conflict with AC-08 requiring `patientId` in the audit record. | BLOCKING | **Decision (human, 2026-09-10):** `patientId` is not treated as PHI for the purpose of this audit table — it's a bare identifier, not name/DOB/address/etc., and is already stored elsewhere in the schema (e.g. `authorizations.patient_id`). The CLAUDE.md §4.1 "NEVER log" prohibition applies to `ILogger` calls, not to DB-stored identifiers. `patientId`/`healthPlanId` may appear in the eligibility audit table. Reflected in AC-08 and the NFR Security/Compliance rows. *Note: CLAUDE.md §4.1 itself remains ambiguous on this point for future specs — consider clarifying it there too.* | RESOLVED |
