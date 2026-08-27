Task ID:
TEST-CONC-001

Model:
haiku

Title:
Tests proving concurrent advances produce exactly one advance

Purpose:
The original scheduler had a check-then-act race in `StartLineAdvancement` that leaked duplicate, unstoppable timer tasks, and nothing anywhere used a concurrency token, so two simultaneous advances would both read the same state and both write — a lost update that skips a participant's turn. `TryAdvanceAsync` re-checks the deadline inside a transaction and relies on the `Version` concurrency token to make the loser fail. These tests prove that actually holds.

Dependencies:
TEST-001, REPO-002

Consumes:
TEST-001: `SqliteDbFixture` and `TestData`
REPO-002: `TryAdvanceAsync` with its in-transaction deadline re-check and `DbUpdateConcurrencyException` handling
MODEL-001: the `Version` concurrency token and the `SaveChangesAsync` override that refreshes it

Produces:
(nothing downstream consumes this)

Files:
- `HopInLine.Tests/ConcurrencyTests.cs` (new)

Requirements:
1. Construct a `SqliteDbFixture` per test and a real `SQLLineRepository` over `fixture.Factory`. Because the fixture keeps one shared in-memory connection open, all contexts created from the factory see the same database — which is what makes a genuine race reproducible.
2. `SequentialAdvance_SecondCallReturnsFalse`: arrange a due line, `await TryAdvanceAsync` twice in sequence, assert `true` then `false`, and assert the queue advanced exactly once by checking the front participant. This is the deterministic version of the race and must pass reliably.
3. `ConcurrentAdvance_AdvancesExactlyOnce`: arrange a due line with three participants and `AutoReAdd = true`. Launch 5 concurrent `TryAdvanceAsync` calls with `Task.WhenAll`, then assert:
   - exactly **one** call returned `true`
   - the front participant moved by exactly one position, not five
   Accept `DbUpdateConcurrencyException` and SQLite's busy/locked `DbException` as a losing outcome — catch them per task and treat them as `false`. SQLite serializes writers, so a loser may surface as a lock error rather than a concurrency exception; either is a correct "did not advance" result. **What must never happen is two `true` results.**
4. `ConcurrencyToken_ChangesOnEveryUpdate`: load a line, save a modification, and assert `Version` differs from its previous value. This pins MODEL-001's `SaveChangesAsync` override — without it every concurrency check silently passes and `TryAdvanceAsync`'s protection is inert.
5. `StaleWrite_Throws`: load the same line into two separate contexts, save a change through the first, then save a different change through the second and assert `DbUpdateConcurrencyException` is thrown. This is the direct test that the token is wired up.
6. `PausedLine_ConcurrentAdvance_NeverAdvances`: pause a line whose deadline is past, fire 5 concurrent `TryAdvanceAsync`, assert every result is `false` and the queue is unchanged.
7. Use `Task.WhenAll` over tasks started with the repository's async methods directly. Do not use `Parallel.For` or `Task.Run` with blocking waits.
8. Each concurrent test must assert on a **count of successes**, not on which specific call won. Which task wins is nondeterministic and asserting on it would be flaky.

Constraints:
- Do NOT use `Thread.Sleep` or a real-time wait anywhere. Set `NextAdvanceAt` to a past instant directly.
- Do NOT retry a losing advance. `TryAdvanceAsync` deliberately does not retry — a loser means another actor already advanced, and retrying would double-advance, which is the exact bug under test.
- Do NOT write position-integrity or timer-transition tests; TEST-REPO-001 owns those. Do NOT write authorization tests; TEST-AUTH-001 owns those.
- Do NOT modify anything under `HopInLine/`. If a test reveals a product bug, report it in Handoff rather than fixing it.
- Do NOT edit `SqliteDbFixture.cs` or `TestData.cs`.
- Do not mark any test `[Fact(Skip = ...)]` to make the suite pass. If a concurrency test cannot be made reliable, report exactly why in Handoff.

Acceptance Criteria:
- `dotnet test` runs green with 5 new tests, and passes 20 consecutive runs without a flake.
- `ConcurrentAdvance_AdvancesExactlyOnce` observes exactly one `true` on every run.
- Removing `IsConcurrencyToken()` from the model makes `StaleWrite_Throws` fail.
- Removing the in-transaction deadline re-check from `TryAdvanceAsync` makes `SequentialAdvance_SecondCallReturnsFalse` fail.
- No test exceeds two seconds.

Validation:
`dotnet build` completes with 0 errors and `dotnet test` reports 0 failures across 3 consecutive runs.

Handoff:
State the observed success count for each concurrent test, which exception types you had to treat as a losing outcome, and whether any test showed flakiness across the 20 runs.
