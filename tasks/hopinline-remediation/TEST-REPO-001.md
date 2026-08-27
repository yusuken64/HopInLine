Task ID:
TEST-REPO-001

Model:
haiku

Title:
Tests for position integrity and timer transitions

Purpose:
Lock down the two bugs most likely to silently regress. The app had two incompatible position schemes — `Max(Position)+1` starting at 0 in `AddParticipantAsync` versus `NextPosition++` starting at 1 in `AdvanceLineAsync` — which collide permanently after the first advance and let the same participant be served twice in a row. And Pause was broken by a notification that restarted the timer, so a paused line advanced anyway.

Dependencies:
TEST-001, REPO-002

Consumes:
TEST-001: `SqliteDbFixture` and `TestData`
REPO-002: the four timer transitions, `GetDueLineIdsAsync`, `TryAdvanceAsync`, and the unified `Position = NextPosition++` rule

Produces:
(nothing downstream consumes this)

Files:
- `HopInLine.Tests/PositionIntegrityTests.cs` (new)
- `HopInLine.Tests/TimerTransitionTests.cs` (new)

Requirements:
1. Both test classes construct a `SqliteDbFixture` per test (implement `IDisposable` on the test class and dispose it) and build a real `SQLLineRepository` over `fixture.Factory`. Test the repository directly — not `LineService` — so these tests do not need a manage token and cannot be broken by SERVICE-002's authorization.
2. `PositionIntegrityTests` — write these:
   - `AddParticipant_AssignsSequentialPositions`: add three participants, assert positions are 1, 2, 3 and `NextPosition` is 4.
   - `AddThenAdvanceThenAdd_NeverDuplicatesPosition`: add two, advance, add a third, then assert the active participants' positions are all distinct. **This is the regression test for the two colliding schemes** — it fails on the original code.
   - `AutoReAdd_CyclesFairly`: three participants A, B, C on a line with `AutoReAdd = true`. Advance three times and assert the served order is A, B, C — and specifically that no participant is served twice consecutively. Assert on the ordered active list after each advance, not just the final state.
   - `ReAddRemovedParticipant_GoesToBack`: remove the front participant, re-add it, assert its position is greater than every other active participant's.
   - `AdvanceEmptyLine_DoesNotThrow`: advancing a line with no active participants completes without throwing and leaves `NextPosition` unchanged.
3. `TimerTransitionTests` — write these, asserting on `NextAdvanceAt`, `IsPaused`, and `UnpauseRemaining` together after each call:
   - `Pause_StoresRemainingAndClearsDeadline`: set a deadline 30 seconds out, pause, assert `NextAdvanceAt` is null, `IsPaused` is true, and `UnpauseRemaining` is between 25 and 30 seconds.
   - `Resume_RestoresRemaining`: pause with 30 seconds left, resume, assert `NextAdvanceAt` is roughly 30 seconds in the future — **not** a full interval and not in the past.
   - `Pause_PastDeadline_ClampsToZero`: pause a line whose deadline is already 10 minutes past; assert `UnpauseRemaining` is `TimeSpan.Zero`, never negative.
   - `PausedLine_IsNotDue`: pause a line whose old deadline is past, then assert `GetDueLineIdsAsync(DateTime.UtcNow, default)` does not contain it. **This is the regression test for the broken Pause.**
   - `LineWithOnlyRemovedParticipants_IsNotDue`.
   - `Restart_SetsFullInterval`: assert `NextAdvanceAt` is roughly one full interval out and `UnpauseRemaining` is null.
   - `AutoRestartTimerOnAdvanceFalse_AdvancesOnceThenPauses`: `TryAdvanceAsync` returns true, and afterwards `NextAdvanceAt` is null and `IsPaused` is true, so the line is no longer due.
   - `AutoAdvanceLineFalse_NeverDue`.
4. For any assertion on a computed instant, assert a **range** rather than an exact value — `Assert.InRange` with a couple of seconds of tolerance. Wall-clock advances between the arrange and assert steps, so exact equality is flaky.
5. Every test must be independent: no shared static state, no shared fixture between test classes, and no reliance on execution order.

Constraints:
- Do NOT use `Thread.Sleep` or `Task.Delay` to wait for a deadline. Set `NextAdvanceAt` directly to a past or future instant in the arrange step. A test that sleeps for a real interval is unusably slow and flaky.
- Do NOT write concurrency tests here; TEST-CONC-001 owns those. Do NOT write authorization tests; TEST-AUTH-001 owns those. Do NOT write duration-formatting tests; FMT-001 owns those.
- Do NOT modify anything under `HopInLine/`. If a test reveals a product bug, report it in Handoff — do not fix it here.
- Do NOT edit `SqliteDbFixture.cs` or `TestData.cs`. If you need a new default, pass an override argument instead.
- Do not use Moq or any mocking library; use the real repository over SQLite.

Acceptance Criteria:
- `dotnet test` runs all tests green, with at least 13 new tests added by this task.
- Reverting `AddParticipantAsync` to `Max(Position) + 1` makes `AddThenAdvanceThenAdd_NeverDuplicatesPosition` fail.
- Reverting the pause transition to leave `NextAdvanceAt` set makes `PausedLine_IsNotDue` fail.
- No test takes longer than a second, and the whole suite finishes in under 30 seconds.
- Running the suite three times in a row produces identical results.

Validation:
`dotnet build` completes with 0 errors and `dotnet test` reports 0 failures.

Handoff:
List every test name and its assertion. Report any product bug the tests uncovered without fixing it, and state the total suite count.
