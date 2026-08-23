# Agent rules — Spec Drift

Save as `CLAUDE.md` in the repository root before starting. For other agents, rename to `AGENTS.md`.
These rules apply to **every** session, not just the first. Re-read them when a session starts.

---

## What this project is

A .NET CLI that compares an OpenAPI specification against C# web API source and reports where they have diverged. It supports **two dialects**: ASP.NET Web API 2 (.NET Framework) and ASP.NET Core. It reads. It never writes to the code or the spec it is analysing.

## The prime directive

**Parse first. Ask the model last.**

Static analysis decides everything it is capable of deciding. The LLM handles only the residue that structural comparison genuinely cannot resolve.

Before writing any code that sends source to a model, answer this in writing: *why can Roslyn not decide this?* If there is no good answer, use Roslyn.

## The dialect rule

**One walker, two descriptors. Never two walkers.**

Both dialects produce the same `ApiSurface`. Their differences are a closed set of attribute names, base classes, binding conventions and return types — data, not behaviour. That data lives in a `DialectDescriptor`. The traversal logic exists once.

- If you are about to duplicate traversal logic per dialect, **stop and say so**. That is a design failure, not a pragmatic shortcut.
- If a difference genuinely cannot be expressed as descriptor data, say so explicitly and propose the smallest possible behavioural hook. Do not add one silently.
- **Build and prove one dialect end to end before adding the second.** An abstraction validated against one real case and then a second is sound. One designed against two imagined cases is speculation.
- Detected dialect appears in the report header. A wrong inference must be visible, never silent.

## Honesty rules

These are the reason the project exists. Violating them is worse than shipping fewer features.

1. **No claim without a citation.** Every finding names the spec location (JSON Pointer) and the code location (`file:line`). A finding that cannot cite both sides must not be emitted.
2. **`Unknown` is a first-class result.** When the comparer or the model cannot reach a verdict, report it as undetermined. Never close a gap by guessing.
3. **Deterministic findings are `Certain`. LLM findings are `Probable`.** Never label an LLM verdict `Certain`, however confident the model sounds.
4. **The report declares its own blind spots** — detected dialect, skipped constructs, unparsed files.

Some constructs are known to be undeterminable and must be reported as such, not guessed at: Web API 2 actions returning `HttpResponseMessage` assembled elsewhere, conventionally registered routes, and anything bound by reflection. Reporting these honestly is the product, not a gap in it.

## Scope discipline

The brief's out-of-scope list is binding.

- **No NuGet package without asking.** Name it, say what it replaces, wait.
- **Do not build for a future requirement.** No plugin points, no abstractions "for later", no config for things with one value. The `DialectDescriptor` is the one sanctioned abstraction and it exists because two real cases demand it.
- **Do not refactor code you were not asked to touch.** Mention it and move on.
- **If a requirement seems to need an out-of-scope feature, it doesn't.** Say so and stop.

## Two toolchains

- Spec Drift itself builds with `dotnet build`.
- `samples/DriftyApi/` is .NET Framework 4.8 and builds **only** with MSBuild / Visual Studio, after `nuget restore`. `dotnet build` will not work on it.
- `samples/DriftyApi.Core/` builds with `dotnet build`.

Never write a script or instruction that assumes one command builds everything. Never "fix" the Framework fixture by converting it to SDK-style — being a genuine legacy project is its entire purpose.

## Working rhythm

- **Plan before building.** The first response to any non-trivial task is a plan, then a stop.
- **Vertical slices.** Each ends with something that runs and tests that pass. Never leave the repo unable to build.
- **Stop at the end of each slice.** Report what changed, what is next, what you are unsure about. Do not chain slices.
- **Commit per concern.** One logical change, imperative subject, no "misc fixes".
- **Verify, don't assume.** Check `dotnet --list-sdks` before targeting a framework. Check a package exists before importing it. Run the tests before saying they pass.

## When you are uncertain

Ask. Do not pick a plausible interpretation and proceed silently.

Ask specifically when: the brief is ambiguous, two designs are genuinely close, a rule here conflicts with the brief, or something in the codebase contradicts what you were told.

"I assumed X" in a summary means you should have asked. State assumptions **before** acting, not after.

## Testing

- Every stage gets unit tests. The comparer gets tests per drift code, per dialect.
- `ILlmClient` is faked in tests. **No test makes a network call.**
- Both fixtures are integration tests, asserted against their `EXPECTED.md`.
- A test asserting current buggy behaviour is worse than no test. Fix the bug.

## Security

- API key from `SPECDRIFT_API_KEY` only. Never a flag, never a file, never a default, never a literal.
- No key in logs, error messages, commits, or images.
- Container runs as a non-root user.
- The analysed project is untrusted input: never execute it, never evaluate it. Roslyn parses syntax; it does not run code.

## Style

- Standard .NET conventions. Nullable reference types on. Warnings as errors.
- Names say what things are: `ApiSurface`, `DriftFinding`, `DialectDescriptor`, `SemanticQuestion` — not `Manager`, `Helper`, `Processor`.
- Comments explain *why*, never *what*. Delete any comment restating the line below it.
- No dead code, no commented-out blocks, no `TODO` without a name on it.

## Definition of done

A slice is done when it builds, its tests pass, it runs, and you have said out loud what it does not yet handle.
