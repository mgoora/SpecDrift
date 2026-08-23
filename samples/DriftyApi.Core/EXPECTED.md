# DriftyApi.Core (ASP.NET Core) — expected drift

`samples/openapi.yaml` is frozen as of Phase 1 and shared with `DriftyApi/` — the
contract doesn't differ by dialect, only the implementations do. Everything below is
the same ten drifts as `samples/DriftyApi/EXPECTED.md`, planted here in ASP.NET Core
idiom instead of Web API 2's. This is what Spec Drift's integration test asserts
against for the Core dialect — proving the dialect abstraction rather than merely
declaring it.

All code locations are relative to `samples/DriftyApi.Core/`. See
`samples/DriftyApi/EXPECTED.md` for the same ten drifts as they appear in the Web API 2
implementation.

---

### SD001 — undocumented, unimplemented endpoint

**Spec** `#/paths/~1api~1clients~1{id}~1documents/get`
**Code** — none. No action in `Controllers/ClientsController.cs` handles
`GET /api/clients/{id}/documents`; the only route under `.../documents` is the `POST`
added for SD002.

The spec documents `GET /api/clients/{id}/documents` from Phase 1 onward; it was never
implemented. A correct report: **High, Certain** — endpoint declared in the spec has no
matching action in the code.

---

### SD002 — undocumented endpoint added to the code

**Spec** — none. `POST /api/clients/{id}/documents` does not appear anywhere in
`openapi.yaml`; the only operation on that path is the unimplemented `GET` from SD001.
**Code** `Controllers/ClientsController.cs:54-70` (`AddClientDocument`,
`[HttpPost("{id:guid}/documents")]` at line 54)

A document-upload action was added straight to the controller during maintenance,
without a spec update. A correct report: **High, Certain** — action in the code has no
matching endpoint in the spec.

---

### SD003 — search's HTTP method disagrees

**Spec** `#/paths/~1api~1clients~1search/get`
**Code** `Controllers/ClientsController.cs:116` (`[HttpPost("search")]` on
`SearchClients`)

The spec still declares `GET /api/clients/search`; the controller now only accepts
`POST` on that route (a bare `GET` against it returns `404 Not Found` — ASP.NET Core
routing, unlike Web API 2, doesn't distinguish "wrong verb" from "no route" once no
route template matches). A correct report: **High, Certain** — route matches, HTTP
method differs.

---

### SD004 — undocumented `riskLevel` search parameter

**Spec** — none under `#/paths/~1api~1clients~1search/get/parameters`.
**Code** `Controllers/ClientsController.cs:120` (`RiskLevel? riskLevel = null`
in `SearchClients`)

The code binds and filters on a `riskLevel` query parameter the spec never declares. A
correct report: **Medium, Certain** — parameter in the code, missing from the spec.

---

### SD005 — documented `kycSegment` search parameter no longer bound

**Spec** `#/paths/~1api~1clients~1search/get/parameters/2`
**Code** — none. `SearchClients`'s parameter list
(`Controllers/ClientsController.cs:119-120`) no longer has a `kycSegment`
parameter; a caller that supplies `?kycSegment=...` has it silently ignored.

A correct report: **Medium, Certain** — parameter in the spec, missing from the code.

---

### SD006 — search `lei` required-ness disagrees

**Spec** `#/paths/~1api~1clients~1search/get/parameters/1` (`required: false`)
**Code** `Controllers/ClientsController.cs:122-126` (400 returned when `lei`
is absent)

The spec marks `lei` optional for search; the code now rejects a search request that
omits it. A correct report: **Medium, Certain** — parameter required-ness disagrees
between spec and code.

---

### SD007 — undocumented `409` on duplicate LEI

**Spec** `#/paths/~1api~1clients/post/responses` (only `201` and `400` declared)
**Code** `Controllers/ClientsController.cs:40-44`

`POST /api/clients` now returns `409 Conflict` (via `Problem(statusCode:
StatusCodes.Status409Conflict, ...)`) when the supplied LEI is already registered; the
spec declares no `409` for this operation. A correct report: **Medium, Certain** —
status code returned in the code, not declared in the spec.

---

### SD008 — documented `403` on delete never returned

**Spec** `#/paths/~1api~1clients~1{id}/delete/responses/403`
**Code** — none. `DeleteClient`
(`Controllers/ClientsController.cs:100-114`) has no permission check of any
kind — the project has no auth system — so no code path can ever produce a `403`.

A correct report: **Low, Certain** — status code declared in the spec, never returned
by the code.

---

### SD009 — search returns full `Client`, not the trimmed `ClientSummary`

**Spec** `#/paths/~1api~1clients~1search/get/responses/200/content/application~1json/schema`
→ `ClientSearchResult.items` → `#/components/schemas/ClientSummary`
**Code** `Models/ClientSearchResult.cs:5` (`Items` typed as `List<Client>`),
populated at `Controllers/ClientsController.cs:140-145`

The spec promises search results shaped like the trimmed `ClientSummary` (six fields,
no `documents`); the implementation serializes the full `Client`, including
`avoxId`, `dateOfIncorporation`, and `documents`. A correct report: **Medium, Certain**
— response type disagrees between the spec and the code.

---

### SD100 — delete's description lies about being permanent

**Spec** `#/paths/~1api~1clients~1{id}/delete/description` — "Permanently removes the
client record... This action cannot be undone."
**Code** `Controllers/ClientsController.cs:109-111`

`DeleteClient` no longer removes the record. It sets `KycStatus` to `Archived` and
still returns `204 No Content`; the client remains fully retrievable via `GET
/api/clients/{id}` afterward. Route, method, parameters, and status code all still
agree with the spec — nothing here is structurally decidable. Only comparing the
spec's prose against what the handler actually does reveals it.

A correct report: **Low, Probable** (LLM-assisted, SD100) — the spec's description
appears to contradict what the handler does.

---

## Summary

| Code | Kind | Severity | Confidence |
|---|---|---|---|
| SD001 | Missing endpoint | High | Certain |
| SD002 | Undocumented endpoint | High | Certain |
| SD003 | HTTP method mismatch | High | Certain |
| SD004 | Undocumented parameter | Medium | Certain |
| SD005 | Missing parameter | Medium | Certain |
| SD006 | Required-ness mismatch | Medium | Certain |
| SD007 | Undocumented status code | Medium | Certain |
| SD008 | Undelivered status code | Low | Certain |
| SD009 | Response type mismatch | Medium | Certain |
| SD100 | Description contradicts behavior | Low | Probable |
