# DriftyApi — sample fixture build prompt

> Paste below the line into a **fresh** Claude Code session, in `samples/` inside the Spec Drift repo.
> Keep `CLAUDE.md` in the repo root; it applies here too.
>
> This fixture is the integration test and the demo. Its value is entirely in the **planted drift** —
> a spec and a codebase that agree perfectly prove nothing.

---

## Tuning knobs

| Knob | Current | Try instead |
|---|---|---|
| Framework | .NET Framework 4.8, Web API 2 | 4.7.2 if 4.8 dev pack unavailable |
| Persistence | In-memory singleton, seeded | JSON file |
| PATCH semantics | JSON Merge Patch (RFC 7386) | JSON Patch (RFC 6902) |
| Planted drifts | 10, listed below | Fewer for a faster loop |
| Seed data | 6 clients | More for search testing |

---

## The brief

Build **DriftyApi** — a small ASP.NET Web API 2 application on .NET Framework 4.8, hosted on IIS, exposing a client KYC registry. It exists to be analysed by Spec Drift, so it must be small, readable, and drifted on purpose.

No database. No auth. No logging framework. An in-memory repository seeded at startup is correct here, not a shortcut.

## Build in three phases, stopping between each

### Phase 1 — the specification

Write `samples/openapi.yaml` first. OpenAPI 3.0.3. This is the contract, written as if by an architect before any code exists. Version it `1.0.0`.

**Endpoints**

| # | Method | Path | Purpose |
|---|---|---|---|
| 1 | GET | `/api/clients/{id}` | Retrieve one client |
| 2 | POST | `/api/clients` | Create a client |
| 3 | PATCH | `/api/clients/{id}` | Partially update a client |
| 4 | DELETE | `/api/clients/{id}` | Remove a client |
| 5 | GET | `/api/clients/search` | Search clients |

**Client schema**

| Field | Type | Notes |
|---|---|---|
| `id` | string (uuid) | server-assigned, read-only |
| `companyName` | string | required, 1–200 chars |
| `lei` | string | required, exactly 20 chars, pattern `^[A-Z0-9]{18}[0-9]{2}$` |
| `kycSegment` | enum | `Retail`, `Corporate`, `Institutional`, `FinancialInstitution` |
| `kycStatus` | enum | `NotStarted`, `InProgress`, `PendingReview`, `Approved`, `Rejected`, `Archived` |
| `avoxId` | string | optional, nullable |
| `riskLevel` | enum | `Low`, `Medium`, `High`, `Prohibited` |
| `dateOfIncorporation` | string (date) | required |
| `documents` | array of Document | may be empty |

**Document schema**

| Field | Type | Notes |
|---|---|---|
| `name` | string | required |
| `grouping` | enum | `Identity`, `Ownership`, `Financial`, `Regulatory` |
| `type` | enum | `CertificateOfIncorporation`, `ArticlesOfAssociation`, `AuditedAccounts`, `W8BEN`, `PassportCopy`, `UtilityBill` |

**Search parameters:** `companyName` (partial match), `lei` (exact), `kycStatus`, `page` (default 1), `pageSize` (default 20, max 100). Returns a paged envelope: `items`, `totalCount`, `page`, `pageSize`.

Declare realistic status codes per endpoint, `ProblemDetails`-shaped errors, and write meaningful `summary` and `description` text on every operation. The descriptions matter — one of them will later become a lie, and the tool has to notice.

**Stop here. Show me the spec. Wait for approval.**

### Phase 2 — the implementation, faithful to the spec

Build the project so it matches `openapi.yaml` exactly. No drift yet.

**Stack**
- .NET Framework 4.8, classic `.csproj` (not SDK-style), `packages.config`
- `Microsoft.AspNet.WebApi` 5.3.x, `Newtonsoft.Json`
- Attribute routing: `[RoutePrefix]` on the controller, `[Route]` per action
- Actions return `IHttpActionResult`, annotated with `[ResponseType(typeof(T))]`
- PATCH via JSON Merge Patch: bind to `JObject`, merge non-absent fields onto the existing entity

**Structure**
```
samples/DriftyApi/
  DriftyApi.csproj
  packages.config
  Web.config
  Global.asax  +  Global.asax.cs
  App_Start/WebApiConfig.cs
  Controllers/ClientsController.cs
  Models/Client.cs, Document.cs, ClientSearchResult.cs, ProblemDetails.cs
  Data/InMemoryClientRepository.cs      // seeded with 6 clients, varied segments/statuses/risk
```

**Important:** `dotnet new` cannot scaffold .NET Framework web projects. Hand-author the `.csproj`, `packages.config`, `Web.config` and `Global.asax`. Include the correct project type GUIDs so it opens in Visual Studio and runs under IIS Express.

Note in the README that building requires MSBuild with the .NET Framework 4.8 targeting pack and `nuget restore` — `dotnet build` will not work on this project.

**Stop here. Confirm it builds and the endpoints behave as specified. Wait for approval.**

### Phase 3 — plant the drift

Now simulate six months of maintenance. **Change only the code. Do not touch `openapi.yaml`.** The spec staying frozen while the code moves on is the entire premise.

| # | Code | Plant this |
|---|---|---|
| 1 | SD001 | Spec documents `GET /api/clients/{id}/documents`; add it to the spec in Phase 1 and never implement it |
| 2 | SD002 | Add `POST /api/clients/{id}/documents` to the controller only |
| 3 | SD003 | Change search to `[HttpPost]` in code while the spec still says GET |
| 4 | SD004 | Search accepts a `riskLevel` parameter the spec never declares |
| 5 | SD005 | Spec declares a `kycSegment` search parameter the code no longer binds |
| 6 | SD006 | Spec marks search `lei` optional; code now rejects the request when it is absent |
| 7 | SD007 | Create returns `409 Conflict` on duplicate LEI; spec declares no 409 |
| 8 | SD008 | Spec declares `403 Forbidden` on delete; code never returns it |
| 9 | SD009 | Search returns full `Client` objects while the spec promises a trimmed `ClientSummary` |
| 10 | SD100 | Delete's spec description says the client is permanently removed; the code sets `kycStatus` to `Archived` and returns 204 — a soft delete |

Number 10 is the one that matters most. No parser can catch it: the route, method, parameters and status code all agree. Only reading the description against the implementation reveals it. That is the sole justification for the LLM stage, so make it unambiguous in the code and plausible in the prose.

### Deliverable: `samples/EXPECTED.md`

For each planted drift: the code, the spec location as a JSON Pointer, the code location as `file:line`, and one sentence on what a correct report should say. Spec Drift's integration test asserts against this file, so it must be precise.

## Done means

- `openapi.yaml` validates as OpenAPI 3.0.3
- The project builds under MSBuild and runs on IIS Express
- All five endpoints respond correctly, and the drifted behaviour is genuinely present in the code
- `EXPECTED.md` lists all ten drifts with exact locations
- A short `samples/README.md` explains the fixture's purpose and warns that the spec is intentionally stale

---

**Start with Phase 1. Spec only. No project files yet.**
