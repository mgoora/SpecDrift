# How Spec Drift was built

Spec Drift was built with an AI coding agent (Claude Code, with GitHub Copilot alongside
it) using a spec-first workflow: the decisions were written down before any code
existed, and the agent built against them in small, reviewed steps. This folder holds
the written inputs to that process, unchanged, so the result can be compared against
what was asked for.

## The three files

| File | Role |
|---|---|
| [`../../CLAUDE.md`](../../CLAUDE.md) | Standing rules, kept in the repository root because the agent re-reads them at the start of every session. They survive context resets: honesty rules, scope discipline, the one-walker dialect rule, security and testing rules. |
| [`PROMPT.md`](PROMPT.md) | The build brief: what the tool is and is not, the four-stage pipeline, the detection rules, the output rules and the order of work. |
| [`PROMPT-SAMPLE-API.md`](PROMPT-SAMPLE-API.md) | The brief for the Web API 2 test fixture: write the OpenAPI spec first, implement it faithfully, then freeze the spec and plant ten known drifts in the code, recorded in `EXPECTED.md` as the test oracle. |

## The process

1. **Specify.** The brief fixes the decisions that are expensive to change later —
   scope, the pipeline, every finding code and its severity — before any code exists.
   Both briefs open with a short table of tuning knobs, so an attempt can be repeated
   with one variable changed.
2. **Plan, then stop.** The first instruction in the brief is to propose the project
   layout, the core types and the build order, write no code, and wait for approval.
   Correcting a plan is cheaper than correcting an implementation.
3. **Build in vertical slices.** Eleven slices, each ending in something that builds,
   runs and passes its tests, with a stop for review after every one. One dialect was
   built end to end before the second was added, so the dialect abstraction was proven
   against a real second case rather than designed against two imagined ones.
4. **Verify against an oracle.** Each fixture's `EXPECTED.md` lists every planted drift
   with its exact location. The integration tests assert against it, so "done" is
   checked by a test rather than claimed.

## Rules that shaped the result

- **Parse first, ask the model last.** Before any source code may be sent to a model,
  the agent has to answer in writing why static analysis cannot decide the question.
- **Ask, don't guess.** When the brief is ambiguous, the agent stops and asks; an
  assumption reported after the fact counts as a failure.
- **No new dependency without asking.** This was the rule that fired most often during
  the build.
- **No network calls in tests.** The model client sits behind an interface and is faked
  in every test.

## Known gaps

- The work was reviewed slice by slice but committed in bulk, so the early git history
  does not show the individual slices. Later changes are committed one concern at a time.
- The semantic review (SD100) was built and tested against a fake model client and has
  not yet been run against the live API.
- The project is small. Failure modes that only appear on a large codebase — such as
  the agent contradicting its own earlier decisions as the context grows — had no room
  to show up here, so this process is proven at this size only.
