# Spec Drift

A .NET CLI that compares an OpenAPI specification against a C# web API's source code
and reports where the two have diverged — endpoints the spec promises that the code
doesn't implement, parameters the code accepts that the spec never mentions, status
codes returned but undocumented, and the rarer case where everything lines up
structurally but the spec's own prose no longer describes what the handler does.

It analyses **two dialects**: ASP.NET Web API 2 (.NET Framework) and ASP.NET Core.
Legacy Web API 2 estates are where drift is worst and where almost no tooling exists
for it, so Web API 2 is the primary target, not an afterthought.

It is a **drift detector**, not a generator or a linter. It reads the spec and the
code; it never writes to either.

## What it isn't

No web UI, no dashboard, no CI/CD integration, no auto-fixing of the spec, no
languages beyond C#, no frameworks beyond the two dialects above. If a request seems
to need one of these, it doesn't — see [`PROMPT.md`](docs/ai-workflow/PROMPT.md) for the brief this was
built against.

## Quick start

```powershell
dotnet build SpecDrift.sln
dotnet run --project src/SpecDrift.Cli -- --spec openapi.yaml --project src/Api --no-llm
```

Prerequisites: the .NET SDK matching the projects' target framework (`net10.0` here —
check with `dotnet --list-sdks` before assuming). `--no-llm` runs the fully
deterministic pipeline with no API key and no network access; drop it once you have
`SPECDRIFT_API_KEY` set to also get SD100 (see [Semantic review](#semantic-review-sd100)).

## CLI contract

```
specdrift --spec <path> --project <path>
          [--dialect WebApi2|Core] [--out report.md]
          [--no-llm] [--min-severity Low|Medium|High]
```

| Flag | Meaning |
|---|---|
| `--spec` | Path to the OpenAPI document (JSON or YAML). |
| `--project` | Path to the root of the C# project to analyse. |
| `--dialect` | Overrides dialect auto-detection. Omit to let it infer from the project's shape. |
| `--out` | Write the report to a file instead of stdout. |
| `--no-llm` | Skip semantic review (SD100) — no API key needed, no network call made. |
| `--min-severity` | Only findings at or above this severity affect the report and the exit code. Default `Low`. |

**Exit codes:** `0` — no findings at or above `--min-severity`. `1` — there are. `2` —
tool error (bad arguments, an unparseable spec, a failed dialect detection, a failed
LLM call). The API key comes from the `SPECDRIFT_API_KEY` environment variable only —
never a flag, never a file, never a default — and a missing key with the LLM stage
enabled is a clear error naming the variable, not a stack trace.

On Windows, `scripts\set-api-key.ps1` sets it once for your user account,
prompting for the key with hidden input so it never lands in a file or in
your shell history.

## Two toolchains

Spec Drift itself builds with `dotnet build` / `dotnet test`, targeting whatever SDK
`dotnet --list-sdks` reports (`net10.0` as built). The **projects it analyses** are a
separate concern with their own toolchains, and neither is built by Spec Drift itself
— it only reads their source as text:

- `samples/DriftyApi/` is ASP.NET Web API 2 on .NET Framework 4.8, a classic
  `.csproj` + `packages.config` project. It builds only with MSBuild or Visual
  Studio — see [`samples/README.md`](samples/README.md) for the exact commands.
  `dotnet build` will not work on it, and that's intentional: converting it to
  SDK-style would defeat its entire purpose as a genuine legacy fixture.
- `samples/DriftyApi.Core/` is the same API on ASP.NET Core, SDK-style, and builds
  with plain `dotnet build`.

No single command builds everything, and none should — that would mean the tool had
started assuming something about the projects it's supposed to analyse blind.

## Docker

```bash
docker build -t specdrift .
docker run --rm -v "$(pwd)":/work -e SPECDRIFT_API_KEY specdrift \
  --spec /work/openapi.yaml --project /work/src/Api
```

Multi-stage build — the final image carries only the .NET runtime, never the SDK —
and runs as a dedicated non-root user. `SPECDRIFT_API_KEY` is supplied at `docker run`
time only; it is never an `ARG` or `ENV` default, so it can never end up baked into an
image layer.

## Detection rules

Deterministic (stages 1–3 — spec reader, code reader, comparer):

| Code | Finding | Severity |
|---|---|---|
| SD001 | Endpoint in the spec, no matching action in the code | High |
| SD002 | Action in the code, absent from the spec | High |
| SD003 | Route matches but the HTTP method differs | High |
| SD004 | Parameter in the code, missing from the spec | Medium |
| SD005 | Parameter in the spec, missing from the code | Medium |
| SD006 | Parameter required-ness disagrees | Medium |
| SD007 | Status code returned in the code, not declared in the spec | Medium |
| SD008 | Status code declared in the spec, never returned in the code | Low |
| SD009 | Response type disagrees between the spec and the code | Medium |

LLM-assisted (stage 4 — semantic review):

| Code | Finding | Severity |
|---|---|---|
| SD100 | The spec's `summary`/`description` appears to contradict what the handler does | Low |

Every deterministic finding is `Certain`. SD100 is always `Probable` — an LLM verdict
is never labelled `Certain`, however confident the model sounds.

## Sample output

Both fixtures share one `samples/openapi.yaml`, deliberately left stale after Phase 1
while six months of (simulated) maintenance happened to the code alone — see
[`samples/README.md`](samples/README.md) and each fixture's own `EXPECTED.md` for the
full story behind each of the ten planted drifts. Run with `--no-llm`, both dialects
produce the same nine deterministic findings — proof the dialect abstraction actually
works, not just that it compiles:

```powershell
dotnet run --project src/SpecDrift.Cli -- --spec samples/openapi.yaml --project samples/DriftyApi --no-llm
dotnet run --project src/SpecDrift.Cli -- --spec samples/openapi.yaml --project samples/DriftyApi.Core --no-llm
```

<details>
<summary><strong>Real output — Web API 2 fixture</strong></summary>

```markdown
# Spec Drift Report

**Dialect** WebApi2 (detected) · **Spec version** 1.0.0

## Summary

- High: 3
- Medium: 5
- Low: 1
- Total: 9

## Findings

### SD001 · Missing endpoint `GET /api/clients/{id}/documents`
**Severity** High · **Confidence** Certain · **Dialect** WebApi2
**Spec** `#/paths/~1api~1clients~1{id}~1documents/get`

The specification declares `GET /api/clients/{id}/documents`, but no action in the analysed project handles it.

---

### SD002 · Undocumented endpoint `POST api/clients/{id:guid}/documents`
**Severity** High · **Confidence** Certain · **Dialect** WebApi2
**Code** `Controllers/ClientsController.cs:46`

`AddClientDocument` handles `POST api/clients/{id:guid}/documents`, but the specification never declares this endpoint.

---

### SD003 · HTTP method mismatch on `/api/clients/search`
**Severity** High · **Confidence** Certain · **Dialect** WebApi2
**Code** `Controllers/ClientsController.cs:144`
**Spec** `#/paths/~1api~1clients~1search/get`

The specification declares `GET /api/clients/search`, but the code now handles this route with `POST` instead.

---

### SD004 · Undocumented parameter `riskLevel`
**Severity** Medium · **Confidence** Certain · **Dialect** WebApi2
**Code** `Controllers/ClientsController.cs:148`
**Spec** `#/paths/~1api~1clients~1search/get`

The action binds a query parameter `riskLevel` that the specification does not declare.

---

### SD005 · Missing parameter `kycSegment`
**Severity** Medium · **Confidence** Certain · **Dialect** WebApi2
**Code** `Controllers/ClientsController.cs:144`
**Spec** `#/paths/~1api~1clients~1search/get/parameters/2`

The specification declares a query parameter `kycSegment` that the action does not bind.

---

### SD006 · Required-ness mismatch on `lei`
**Severity** Medium · **Confidence** Certain · **Dialect** WebApi2
**Code** `Controllers/ClientsController.cs:147`
**Spec** `#/paths/~1api~1clients~1search/get/parameters/1`

The specification marks `lei` as optional, but the code makes it required.

---

### SD007 · Undocumented status code `409`
**Severity** Medium · **Confidence** Certain · **Dialect** WebApi2
**Code** `Controllers/ClientsController.cs:84`
**Spec** `#/paths/~1api~1clients/post`

The code returns `409`, which the specification does not declare for this operation.

---

### SD009 · Response type mismatch: `ClientSearchResult.items`
**Severity** Medium · **Confidence** Certain · **Dialect** WebApi2
**Code** `Models/ClientSearchResult.cs:7`
**Spec** `#/components/schemas/ClientSearchResult/properties/items`

The specification's `ClientSearchResult.items` is `array<ClientSummary>`, but the code's is `array<Client>`.

---

### SD008 · Undelivered status code `403`
**Severity** Low · **Confidence** Certain · **Dialect** WebApi2
**Code** `Controllers/ClientsController.cs:127`
**Spec** `#/paths/~1api~1clients~1{id}/delete/responses/403`

The specification declares `403` for this operation, but no resolvable path in the code returns it.

---

## Limitations

- Controllers/ClientsController.cs:96: 'ClientsController.CreateClient' returns a result whose status code could not be determined statically.
- Semantic review (SD100) was skipped (--no-llm).
```

</details>

<details>
<summary><strong>Real output — ASP.NET Core fixture</strong></summary>

```markdown
# Spec Drift Report

**Dialect** Core (detected) · **Spec version** 1.0.0

## Summary

- High: 3
- Medium: 5
- Low: 1
- Total: 9

## Findings

### SD001 · Missing endpoint `GET /api/clients/{id}/documents`
**Severity** High · **Confidence** Certain · **Dialect** Core
**Spec** `#/paths/~1api~1clients~1{id}~1documents/get`

The specification declares `GET /api/clients/{id}/documents`, but no action in the analysed project handles it.

---

### SD002 · Undocumented endpoint `POST api/clients/{id:guid}/documents`
**Severity** High · **Confidence** Certain · **Dialect** Core
**Code** `Controllers/ClientsController.cs:54`

`AddClientDocument` handles `POST api/clients/{id:guid}/documents`, but the specification never declares this endpoint.

---

### SD003 · HTTP method mismatch on `/api/clients/search`
**Severity** High · **Confidence** Certain · **Dialect** Core
**Code** `Controllers/ClientsController.cs:116`
**Spec** `#/paths/~1api~1clients~1search/get`

The specification declares `GET /api/clients/search`, but the code now handles this route with `POST` instead.

---

### SD004 · Undocumented parameter `riskLevel`
**Severity** Medium · **Confidence** Certain · **Dialect** Core
**Code** `Controllers/ClientsController.cs:120`
**Spec** `#/paths/~1api~1clients~1search/get`

The action binds a query parameter `riskLevel` that the specification does not declare.

---

### SD005 · Missing parameter `kycSegment`
**Severity** Medium · **Confidence** Certain · **Dialect** Core
**Code** `Controllers/ClientsController.cs:116`
**Spec** `#/paths/~1api~1clients~1search/get/parameters/2`

The specification declares a query parameter `kycSegment` that the action does not bind.

---

### SD006 · Required-ness mismatch on `lei`
**Severity** Medium · **Confidence** Certain · **Dialect** Core
**Code** `Controllers/ClientsController.cs:119`
**Spec** `#/paths/~1api~1clients~1search/get/parameters/1`

The specification marks `lei` as optional, but the code makes it required.

---

### SD007 · Undocumented status code `409`
**Severity** Medium · **Confidence** Certain · **Dialect** Core
**Code** `Controllers/ClientsController.cs:42`
**Spec** `#/paths/~1api~1clients/post`

The code returns `409`, which the specification does not declare for this operation.

---

### SD009 · Response type mismatch: `ClientSearchResult.items`
**Severity** Medium · **Confidence** Certain · **Dialect** Core
**Code** `Models/ClientSearchResult.cs:5`
**Spec** `#/components/schemas/ClientSearchResult/properties/items`

The specification's `ClientSearchResult.items` is `array<ClientSummary>`, but the code's is `array<Client>`.

---

### SD008 · Undelivered status code `403`
**Severity** Low · **Confidence** Certain · **Dialect** Core
**Code** `Controllers/ClientsController.cs:97`
**Spec** `#/paths/~1api~1clients~1{id}/delete/responses/403`

The specification declares `403` for this operation, but no resolvable path in the code returns it.

---

## Limitations

- Semantic review (SD100) was skipped (--no-llm).
```

</details>

Notice the WebApi2 report's Limitations section names one construct the code reader
genuinely can't resolve (`CreateClient`'s success path is built through a private
`Problem()`-style helper several statements removed from the value it returns); the
Core report has none, because the same operation is written in an idiom — `Problem(
statusCode: ..., ...)` with a named argument — that resolves cleanly. That's not the
tool being inconsistent between dialects; it's the tool being honest about what each
dialect's real code actually lets it know.

## Semantic review (SD100)

With an API key set, one more pass runs after the deterministic comparison: every
matched endpoint that has both a spec description and readable handler code becomes a
`SemanticQuestion`, all of them batched into a single call to
`claude-haiku-4-5` (plain `HttpClient` against Anthropic's Messages API — no SDK
dependency), asking whether the description still describes what the code does. A
`contradicts` verdict becomes an SD100 finding, always `Low` severity and `Probable`
confidence; `consistent` and `unknown` produce nothing. `ILlmClient` is faked in every
test in the suite — nothing in `dotnet test` ever makes a network call, and this
session never had a `SPECDRIFT_API_KEY` to run it against the real API with, so the
one thing not yet verified end to end is a real call to Anthropic itself.

## Design principles

**Why parsing beats prompting.** An LLM asked "does this endpoint match its
implementation?" will answer confidently whether or not it actually looked closely
enough to be right, and its confidence carries no information about whether it's
correct. A Roslyn syntax tree either has a `[Route("{id}")]` attribute on this method
or it doesn't; an OpenAPI document either declares a `404` response or it doesn't.
Nine of this tool's ten finding codes are answerable that way, exhaustively and
identically on every run. The model is reserved for exactly the one question static
analysis is structurally unable to answer — whether a paragraph of English prose still
describes what a method body does — and even there its answer is labelled `Probable`,
never treated as equal in kind to a citation-backed structural fact.

**Why one walker with dialect descriptors beats two walkers.** Web API 2 and ASP.NET
Core controllers are the same idea wearing different attribute names: a route prefix
attribute, an HTTP-method attribute (sometimes carrying its own route segment, per the
dialect), a way to declare a response's shape, a handful of status-returning helper
methods. Every one of those differences turned out to be expressible as data — a base
class name, an attribute name, a lookup table from a status-constant's name to its
number — consumed by one traversal that has no idea which dialect it's currently
reading. The two-hop discovery that forced this into place: Core's `[HttpGet("{id}")]`
carries its route inline where Web API 2 needs a separate `[Route(...)]`, and Core's
`[ProducesResponseType(type, code)]` can pin a specific status per attribute where Web
API 2's `[ResponseType(type)]` never pins one at all — both differences became one
extra field on `DialectDescriptor` and a few lines of dialect-neutral logic, not a
second code path. A second walker would have meant proving every finding twice, in two
implementations that silently drift from each other exactly like the artifacts they're
supposed to be checking.

**Why every finding cites its source.** "Your search endpoint has undocumented
parameters" is an assertion. "`Controllers/ClientsController.cs:148` binds
`riskLevel`, which `#/paths/~1api~1clients~1search/get` never declares" is a fact
someone can check in fifteen seconds without trusting the tool at all. A finding that
can't name a `file:line` on the code side or a JSON Pointer on the spec side — or, for
existence findings, whichever single side actually has something to point at — doesn't
get emitted. That constraint is what makes the report something a reviewer can audit
rather than something they have to take on faith.

**Why the tool is allowed to say "I don't know."** A Web API 2 action that builds its
`HttpResponseMessage` through a private helper several statements away from its
`return` has a status code that is not statically determinable — not "hard to
determine with more effort," genuinely not knowable from the syntax tree alone. A tool
that guessed in that situation would occasionally be right and would have no way to
tell its reader which occasions those were, which is worse than reporting nothing.
Every one of those constructs is named in the report's own Limitations section instead
of being silently absorbed into a finding or a non-finding. `Unknown` isn't a bug in
this tool; refusing to manufacture false confidence is the entire reason it exists.

## Known undeterminable cases

These are structural facts about C# and the two frameworks, not gaps the tool failed
to close:

- A Web API 2 action's `HttpResponseMessage` assembled through a helper method or a
  local variable several statements removed from its `return` — the status code isn't
  present at the point the tool can see it.
- Routes registered conventionally (in `WebApiConfig`, or ASP.NET Core's default
  routing with no attributes at all) rather than by attribute.
- Anything bound by reflection or wired up dynamically at runtime.

Each shows up in the report's own Limitations section for the run that hit it, never
silently folded into a finding.

## Testing

```powershell
dotnet test SpecDrift.sln
```

108 tests, all deterministic — `ILlmClient` is faked everywhere, so none of them ever
make a network call. Both fixtures are integration tests in their own right, asserted
directly against their `EXPECTED.md`: `DriftyApiWebApi2IntegrationTests` and
`DriftyApiCoreIntegrationTests` run the real pipeline against the real sample projects
and check that the nine deterministic codes each fire exactly once, with the right
severity and the right cited facts — not a mocked stand-in for either fixture.

## Project layout

```
src/
  SpecDrift.Core/        Model, SpecReading, CodeReading, Comparing, Reporting, SemanticReview
  SpecDrift.Cli/         Argument parsing and pipeline composition
tests/
  SpecDrift.Core.Tests/  Unit tests per stage, plus both fixtures' integration tests
  SpecDrift.Cli.Tests/   CLI argument-parsing tests
samples/
  DriftyApi/             Web API 2 fixture (.NET Framework 4.8) + EXPECTED.md
  DriftyApi.Core/        ASP.NET Core fixture (SDK-style) + EXPECTED.md
  openapi.yaml           The one spec both fixtures share
scripts/
  set-api-key.ps1        Sets SPECDRIFT_API_KEY for the current Windows user
docs/ai-workflow/        How the tool was built with an AI coding agent
CLAUDE.md                Rules the coding agent follows in every session
Dockerfile
```
