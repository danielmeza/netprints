# Architecture Decision Records

Short records of decisions that are expensive to reverse or easy to re-litigate without a written
rationale. One file per decision, numbered sequentially (`0001-*.md`, `0002-*.md`, ...), never
renumbered or deleted once merged — a superseded decision gets a new ADR that says so and links
back.

Use the lightweight format: Status, Context, Decision, Consequences. Keep it to what a future
contributor needs to not redo the argument; put implementation detail in `specs/` or code comments
instead.
