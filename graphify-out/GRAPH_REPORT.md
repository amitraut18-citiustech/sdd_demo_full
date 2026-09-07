# Graph Report - sdd_demo_full_amit  (2026-09-07)

## Corpus Check
- 30 files · ~9,141 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 291 nodes · 421 edges · 19 communities (13 shown, 1 thin omitted)
- Extraction: 98% EXTRACTED · 2% INFERRED · 0% AMBIGUOUS · INFERRED: 8 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `407e4d30`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Authorization
- NewAuthorizationPage.tsx
- Provider
- Dtos.cs
- LookupControllers.cs
- package.json
- Member
- compilerOptions
- Site
- ABC Healthcare — Prior Authorization System
- CLAUDE.md
- PriorAuth.API.csproj
- github
- SKILL.md

## God Nodes (most connected - your core abstractions)
1. `Authorization` - 31 edges
2. `PriorAuthDbContext` - 29 edges
3. `Member` - 19 edges
4. `Provider` - 19 edges
5. `Site` - 18 edges
6. `compilerOptions` - 15 edges
7. `AuthorizationDetailDto` - 12 edges
8. `ABC Healthcare — Prior Authorization System` - 11 edges
9. `AuthorizationProcedure` - 10 edges
10. `AuthorizationDiagnosis` - 10 edges

## Surprising Connections (you probably didn't know these)
- `AuthorizationsController` --references--> `PriorAuthDbContext`  [EXTRACTED]
  backend/PriorAuth.API/Controllers/AuthorizationsController.cs → backend/PriorAuth.API/Data/PriorAuthDbContext.cs
- `MembersController` --references--> `PriorAuthDbContext`  [EXTRACTED]
  backend/PriorAuth.API/Controllers/LookupControllers.cs → backend/PriorAuth.API/Data/PriorAuthDbContext.cs
- `ProvidersController` --references--> `PriorAuthDbContext`  [EXTRACTED]
  backend/PriorAuth.API/Controllers/LookupControllers.cs → backend/PriorAuth.API/Data/PriorAuthDbContext.cs
- `SitesController` --references--> `PriorAuthDbContext`  [EXTRACTED]
  backend/PriorAuth.API/Controllers/LookupControllers.cs → backend/PriorAuth.API/Data/PriorAuthDbContext.cs
- `HealthPlansController` --references--> `PriorAuthDbContext`  [EXTRACTED]
  backend/PriorAuth.API/Controllers/LookupControllers.cs → backend/PriorAuth.API/Data/PriorAuthDbContext.cs

## Import Cycles
- None detected.

## Communities (19 total, 1 thin omitted)

### Community 0 - "Authorization"
Cohesion: 0.05
Nodes (47): PriorAuthDbContext, AuthorizationDiagnoses, AuthorizationProcedures, Authorizations, DiagnosisCodes, HealthPlans, Members, ProcedureCodes (+39 more)

### Community 1 - "NewAuthorizationPage.tsx"
Cohesion: 0.12
Nodes (26): api, App(), SearchSelect(), SearchSelectProps, AuthorizationDetailPage(), DashboardPage(), emptyState(), NewAuthorizationPage() (+18 more)

### Community 2 - "Provider"
Cohesion: 0.06
Nodes (31): DiagnosisCode, Code, Description, HealthPlan, Entity, HealthPlanId, InsPlanCode, LineOfBusiness (+23 more)

### Community 3 - "Dtos.cs"
Cohesion: 0.14
Nodes (22): ActionResult, HttpGet, List, Task, AuthorizationsController, UpdateStatusRequest, DateTime, List (+14 more)

### Community 4 - "LookupControllers.cs"
Cohesion: 0.16
Nodes (16): ActionResult, HttpGet, List, Task, DiagnosisCodesController, HealthPlansController, MembersController, ProcedureCodesController (+8 more)

### Community 5 - "package.json"
Cohesion: 0.08
Nodes (24): dependencies, react, react-dom, react-router-dom, devDependencies, @types/react, @types/react-dom, typescript (+16 more)

### Community 6 - "Member"
Cohesion: 0.11
Nodes (19): Member, AddressLine1, AddressLine2, City, DateOfBirth, EmailAddress, FirstName, Gender (+11 more)

### Community 7 - "compilerOptions"
Cohesion: 0.12
Nodes (16): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleResolution, noEmit (+8 more)

### Community 8 - "Site"
Cohesion: 0.12
Nodes (16): Site, AddressLine1, AddressLine2, City, Fax, Name, Npi, Participating (+8 more)

### Community 9 - "ABC Healthcare — Prior Authorization System"
Cohesion: 0.13
Nodes (14): ABC Healthcare — Prior Authorization System, API Endpoints, Prerequisites, Project Structure, Quick Start (3 terminals), React + ASP.NET Core 8 Web API + PostgreSQL, Resetting the Database, Seed Data (+6 more)

### Community 10 - "CLAUDE.md"
Cohesion: 0.25
Nodes (6): Architecture, Backend (`backend/PriorAuth.API`), Commands, Database (Docker/PostgreSQL), Frontend (`frontend`), Project Overview

### Community 11 - "PriorAuth.API.csproj"
Cohesion: 0.33
Nodes (5): net8.0, Microsoft.EntityFrameworkCore.Design (8.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (8.0.4), Swashbuckle.AspNetCore (6.6.2), Microsoft.NET.Sdk.Web

### Community 12 - "github"
Cohesion: 0.40
Nodes (4): GITHUB_PERSONAL_ACCESS_TOKEN, npx, github, @modelcontextprotocol/server-github

## Knowledge Gaps
- **157 isolated node(s):** `npx`, `@modelcontextprotocol/server-github`, `GITHUB_PERSONAL_ACCESS_TOKEN`, `HealthPlans`, `ProcedureCodes` (+152 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 181 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **1 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `PriorAuthDbContext` connect `Authorization` to `Site`, `Provider`, `Dtos.cs`, `LookupControllers.cs`?**
  _High betweenness centrality (0.121) - this node is a cross-community bridge._
- **Why does `Authorization` connect `Authorization` to `Site`, `Provider`, `Dtos.cs`?**
  _High betweenness centrality (0.120) - this node is a cross-community bridge._
- **Why does `Member` connect `Member` to `Provider`?**
  _High betweenness centrality (0.069) - this node is a cross-community bridge._
- **What connects `npx`, `@modelcontextprotocol/server-github`, `GITHUB_PERSONAL_ACCESS_TOKEN` to the rest of the system?**
  _157 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Authorization` be split into smaller, more focused modules?**
  _Cohesion score 0.05053191489361702 - nodes in this community are weakly interconnected._
- **Should `NewAuthorizationPage.tsx` be split into smaller, more focused modules?**
  _Cohesion score 0.12162162162162163 - nodes in this community are weakly interconnected._
- **Should `Provider` be split into smaller, more focused modules?**
  _Cohesion score 0.0625 - nodes in this community are weakly interconnected._