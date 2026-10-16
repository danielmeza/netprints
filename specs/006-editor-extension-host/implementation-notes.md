# Implementation notes: Editor Extension Host (P3)

Decisions, measurements and checkpoint reports of the P3 batches, in the order they happen. Each batch adds its own
entries; a decision is written as "Decision: …" with its reason. Tasks are in [tasks.md](tasks.md).

## Batch P3-S2c (tasks and analysis)

- Decision: batches are labelled by sub-phase (A1–A5, B1–B7, C1a–C1f, C2a–C2f, D1a–D1g, D2a–D2d, E1–E5, F1–F2,
  G1–G5), with letters inside C1, C2, D1 and D2 so a batch label never reads as a sub-phase name.
- Decision: a test-first task commits its red test together with the change that makes it green, so every commit
  builds and passes (bisect); both runs are recorded here.
- Decision: checkpoint tasks carry no units of their own, except T133, which also applies the scope rule.
- Decision: the NPS rules (T004) land inactive in the repository build and are switched on together with the sweep
  (T005), so no commit fails the build.
- Decision: the guide and ADR text of each feature lands in the sub-phase that builds it; sub-phase G keeps only the
  release notes, the guide images, the quickstart run and the docs site check.

## Owner questions taken by default

The plan left these product questions to the owner (research R3, R13, R23, R31). Each task below applies the default
and is marked "default pending owner" in tasks.md.

| Question | Default | Task |
|---|---|---|
| The blank-line rule: may `internal` and `protected` fields group? | yes, every non-public field and const may group | T004 |
| Pin labels with no resource | the raw key | T043 |
| Node categories | one "NetPrints" category for the built-ins | T044 |
| Live language switch | no; a language change applies after a restart | T046 |
| External terminal | Linux detection order behind `run.linuxTerminal`; the Windows console closes at exit; macOS through `open -a Terminal` | T123 |

## R5-0 baselines

(T006)

## M5 spike

(T007)

## Pack spike

(T026)

## Publish gate

(T073)

## Checkpoint reports

(T016, T037, T057, T076, T098, T110, T126, T133, T152)

## Reviews

(T017, T038, T058, T077, T099, T111, T127, T134, T153, T155)
