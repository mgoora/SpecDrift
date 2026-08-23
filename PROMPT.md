# Spec Drift — build prompt (v2, dual dialect)

> Paste everything below the line into a fresh Claude Code session in an empty directory,
> with `CLAUDE.md` already in place.
>
> The **Tuning knobs** block is the part to edit between attempts. Change a knob, restart the
> session, compare. Leave the rest alone until you know why you're changing it.

---

## Tuning knobs

| Knob | Current value | Try instead |
|---|---|---|
| Dialects | ASP.NET Web API 2 **and** ASP.NET Core | One only, for a faster first loop |
| First dialect built | Web API 2 | Core |
| Spec format | OpenAPI 3.x, JSON or YAML, local file | Remote URL |
| LLM involvement | Only for semantic checks the parser cannot decide | Disabled (`--no-llm`) / broader |
| Output | Single markdown report | + JSON for CI consumption |
| Exit code | Non-zero if any High-severity finding | Always zero / configurable |

---

## The brief

Build a .NET CLI tool called **Spec Drift**. It takes an OpenAPI specification and a C# web API project, and reports where the two have diverged — endpoints the spec promises that the code doesn't implement, parameters the code accepts that the spec never mentions, status codes returned but undocumented.

It is a **drift detector**. Not a generator, not a linter. It never modifies anything.

It analyses **both** ASP.NET Web API 2 (.NET Framework) and ASP.NET Core projects. Legacy Web API 2 estates are where drift is worst and where nobody has built tooling, so Web API 2 is the primary target, not an afterthought.

## Prime directive

**Parse first. Ask the model last.**

Anything decidable by static analysis is decided by static analysis. The LLM handles only the residue — comparisons where structural matching genuinely cannot reach a verdict, such as whether a spec description still describes what the handler does.

Before writing code that sends source to a model, answer in writing: *why can Roslyn not decide this?* If there is no good answer, use Roslyn.

## Stack

- C# on modern .NET for the tool itself. **Run `dotnet --list-sdks` and target what is installed** — do not assume.
- `Microsoft.OpenApi.Readers` — spec parsing
- `Microsoft.CodeAnalysis.CSharp` (Roslyn) — source parsing. Roslyn reads any C# source as text; the analysed project's target framework is irrelevant to the tool's own.
- `System.CommandLine` — CLI
- `xUnit` — tests
- One HTTP call to an LLM, behind an `ILlmClient` interface so it can be faked

No other NuGet packages without asking first.

**Two toolchains, one repo.** Spec Drift builds with `dotnet build`. The Web API 2 fixture under `samples/` builds only with MSBuild / Visual Studio. Never assume one command builds everything, and say so in the README.

## Dialect support — the central design constraint

Both dialects produce the **same** `ApiSurface`. The differences are a small, closed set of attribute names and return conventions.

**Build one Roslyn walker driven by a dialect descriptor. Do not build two walkers.** If you find yourself duplicating traversal logic per dialect, stop and tell me — that is a design failure.

| Concern | Web API 2 (.NET Framework) | ASP.NET Core |
|---|---|---|
| Controller base | `ApiController` | `ControllerBase`, `Controller` |
| Route prefix | `[RoutePrefix("api/clients")]` | `[Route("api/[controller]")]` |
| Action route | `[Route("{id}")]` | `[HttpGet("{id}")]`, `[Route]` |
| HTTP method | `[HttpGet]`…`[HttpDelete]`, **or inferred from method-name prefix** when no attribute is present | `[HttpGet(...)]` attributes only |
| Declared response type | `[ResponseType(typeof(T))]` | `[ProducesResponseType(typeof(T), 200)]` |
| Return types | `IHttpActionResult`, `HttpResponseMessage`, `T` | `IActionResult`, `ActionResult<T>`, `T` |
| Binding | `[FromBody]`, `[FromUri]`, convention | `[FromBody]`, `[FromQuery]`, `[FromRoute]`, `[FromHeader]` |
| Route tokens | none | `[controller]`, `[action]` must be expanded |
| Status helpers | `Ok()`, `NotFound()`, `BadRequest()`, `Conflict()`, `StatusCode(HttpStatusCode.X)` | `Ok()`, `NotFound()`, `Conflict()`, `StatusCode(409)`, `Problem()` |
| Project shape | classic `.csproj` + `packages.config` | SDK-style `.csproj` |

**Detection:** infer from project file shape and controller base class. `--dialect WebApi2|Core` overrides. **The report always states which dialect was detected** — a wrong inference must be visible, not silent.

**Known undeterminable cases.** These are expected `Unknown` results, not gaps to paper over:
- Web API 2 actions returning `HttpResponseMessage` built by a helper method — status codes are not statically knowable
- Routes registered conventionally in `WebApiConfig` rather than by attribute
- Anything bound by reflection or registered dynamically

Report these as Undetermined and list them in the report's limits section. Handling them honestly is the point of the tool.

## Explicitly out of scope

Do not build these, and do not leave hooks for them:

- Web UI, dashboard, HTTP server
- CI/CD integration, GitHub Action, pipeline config
- Auto-fixing or rewriting the spec
- Languages other than C#; frameworks beyond the two dialects above
- Database, cache, persistence
- Auth, multi-tenancy, telemetry
- Plugin architecture or extensibility layer

If a requirement seems to need one of these, it doesn't. Say so and stop.

## Pipeline

Four stages, each independently testable:

1. **Spec reader** → OpenAPI into a normalised `ApiSurface` (route template, method, parameters with name/location/required, request body shape, declared response codes and types). Every element keeps its JSON Pointer, e.g. `#/paths/~1api~1clients~1{id}/get`.
2. **Code reader** → one Roslyn walker + a `DialectDescriptor`, producing the same `ApiSurface` from controller actions. Every element keeps `file path : line number`.
3. **Comparer** → matches by normalised route + method, emits deterministic findings. Where it cannot decide, emits a `SemanticQuestion` rather than guessing.
4. **Semantic reviewer** → takes only the `SemanticQuestion` list, batches into one LLM call, returns verdicts. Skipped entirely under `--no-llm`.

## Detection rules

Deterministic (stages 1–3):

| Code | Finding | Severity |
|---|---|---|
| SD001 | Endpoint in spec, no matching action in code | High |
| SD002 | Action in code, absent from spec | High |
| SD003 | Route matches but HTTP method differs | High |
| SD004 | Parameter in code, missing from spec | Medium |
| SD005 | Parameter in spec, missing from code | Medium |
| SD006 | Parameter required-ness disagrees | Medium |
| SD007 | Status code returned in code, not declared in spec | Medium |
| SD008 | Status code declared in spec, never returned in code | Low |
| SD009 | Response type disagrees between spec and code annotation | Medium |

LLM-assisted (stage 4):

| Code | Finding | Severity |
|---|---|---|
| SD100 | Spec `summary`/`description` appears to contradict what the handler does | Low |

Do not invent additional codes. Propose and wait.

## Non-negotiable output rules

These are the point of the tool, not decoration.

1. **Every finding cites both sides.** Spec: JSON Pointer. Code: `RelativePath.cs:LineNumber`. A finding that cannot cite both is a bug.
2. **Every finding carries a confidence:** `Certain` (deterministic) or `Probable` (LLM). Never label an LLM verdict `Certain`.
3. **`Unknown` is a first-class result.** When neither the comparer nor the model can decide, the report says so under "Undetermined". Never resolve ambiguity by guessing.
4. **The report declares its own blind spots** — detected dialect, skipped constructs, unparsed files.

## CLI contract

```
specdrift --spec <path> --project <path>
          [--dialect WebApi2|Core] [--out report.md]
          [--no-llm] [--min-severity Low|Medium|High]
```

- API key from `SPECDRIFT_API_KEY` only. Never a flag, never a file, never in source.
- Exit `0` if no findings at or above `--min-severity`, `1` if there are, `2` on tool error.
- Missing key with LLM enabled → clear error naming the variable, not a stack trace.

## Report format

Markdown. Header states detected dialect and spec version. Then summary counts, findings by severity, Undetermined, limits.

```
### SD004 · Undocumented parameter `riskLevel`
**Severity** Medium · **Confidence** Certain · **Dialect** WebApi2
**Code** `Controllers/ClientsController.cs:112`
**Spec** `#/paths/~1api~1clients~1search/get`

The action binds a query parameter `riskLevel` that the specification does not declare.
```

## Sample fixtures

Two, sharing one domain so the comparison is clean:

- `samples/DriftyApi/` — ASP.NET Web API 2 on .NET Framework 4.8, IIS-hosted, built separately (see its own prompt)
- `samples/DriftyApi.Core/` — the same API on ASP.NET Core, with the same planted drifts

Each has `openapi.yaml` and `EXPECTED.md`. The second fixture is what proves the dialect abstraction works rather than merely existing.

## Docker

Multi-stage build, non-root user, no secrets in the image.

```
docker run --rm -v $(pwd):/work -e SPECDRIFT_API_KEY specdrift \
  --spec /work/openapi.yaml --project /work/src/Api
```

## README

Write it last, once the tool works. It must contain a **Design principles** section covering, in your own words: why parsing beats prompting, why one walker with dialect descriptors beats two walkers, why every finding cites its source, and why the tool is allowed to say "I don't know." Include real output from both fixtures.

Take this seriously. It is what a reader judges first.

## How to work

**Step 1: plan only. Write no code.**

Give me: project layout, the shape of `ApiSurface`, the shape of `DialectDescriptor` and how it drives the walker, the interface for each of the four stages, and the build order. Flag anything ambiguous or wrong in this brief. Then **stop and wait for approval.**

After approval, build in vertical slices. Each ends runnable with tests passing:

1. CLI skeleton + arg parsing
2. Spec reader + tests
3. Code reader, **Web API 2 only**, `DialectDescriptor` in place but single-valued
4. Comparer SD001–SD003 + report writer — first end-to-end run
5. Remaining deterministic codes SD004–SD009
6. Web API 2 fixture wired up as integration test
7. **Second dialect: ASP.NET Core descriptor + detection + `--dialect` flag.** No new walker.
8. Core fixture as second integration test
9. `ILlmClient`, `--no-llm`, SD100
10. Dockerfile
11. README

**Build one dialect end-to-end before adding the second.** An abstraction designed against one real case and then proven against a second is sound; one designed against two imagined cases is speculation. Slice 3 keeps the descriptor single-valued on purpose.

Stop after each slice. Say what changed, what's next, what you're unsure about.

## Done means

- `dotnet test` green
- Both fixtures produce every finding in their `EXPECTED.md`, no false positives
- Dialect auto-detection correct on both; `--dialect` override works
- `--no-llm` runs with no API key set
- Container builds and runs
- README written, with real output from both fixtures

---

**Start with Step 1. Plan only. No code yet.**
