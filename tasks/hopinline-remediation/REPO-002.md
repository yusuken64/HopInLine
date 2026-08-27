Task ID:
REPO-002

Model:
haiku

Title:
Rewrite pause/resume/restart/advance as NextAdvanceAt arithmetic, and add TryAdvanceAsync

Purpose:
Timer state is currently derived by adding `AutoAdvanceInterval` to `CountDownStart` in three different places — `LineService`, `SQLLineRepository`, and `LineAdvancementTask` — and EF cannot translate `DateTime + TimeSpan` to SQL Server, so none of it can be queried. `RestartTimer` never persists its change and `StopTimerAsync` mutates `AutoAdvanceLine` without calling `SaveChanges` at all. Making the stored `NextAdvanceAt` the single authoritative deadline turns the scheduler into one indexed query and makes advancement idempotent and safe with more than one app instance running.

Dependencies:
REPO-001, MIG-001

Consumes:
REPO-001: the `IDbContextFactory` constructor and the unified `Position = NextPosition++` rule
MIG-001: the `NextAdvanceAt` column, its index, and the `Version` concurrency token in the database
MODEL-001: `Line.NextAdvanceAt`, `Line.Version`, and the public `UnpauseRemaining` setter

Produces:
- `SQLLineRepository.TryAdvanceAsync(string lineId)` returning `bool` — consumed by SCHED-001
- `SQLLineRepository.GetDueLineIdsAsync(DateTime utcNow, CancellationToken)` returning `IReadOnlyList<string>` — consumed by SCHED-001
- The `NextAdvanceAt` invariants relied on by VIEW-001's client countdown

Files:
- `HopInLine/Data/Line/SQLLineRepository.cs` (edit)
- `HopInLine/Data/Line/ILineRepository.cs` (edit — add the two new members)

Requirements:
1. Establish and enforce exactly these four transitions. Every one of them must call `SaveChangesAsync`:
   - **Pause** (`PauseTimerAsync`): if `NextAdvanceAt` is null, do nothing and return. Otherwise set `UnpauseRemaining = NextAdvanceAt.Value - DateTime.UtcNow` (clamped to `TimeSpan.Zero` if negative), `NextAdvanceAt = null`, `IsPaused = true`.
   - **Resume** (`ResumeTimerAsync`): set `NextAdvanceAt = DateTime.UtcNow + (UnpauseRemaining ?? AutoAdvanceInterval)`, then `UnpauseRemaining = null`, `IsPaused = false`, `CountDownStart = DateTime.UtcNow`.
   - **Restart** (`RestartTimerAsync`): set `NextAdvanceAt = DateTime.UtcNow + AutoAdvanceInterval`, `UnpauseRemaining = null`, `IsPaused = false`, `CountDownStart = DateTime.UtcNow`.
   - **Advance** (`AdvanceLineAsync`): after moving the participant, set
     `NextAdvanceAt = AutoRestartTimerOnAdvance ? DateTime.UtcNow + AutoAdvanceInterval : null` and
     `IsPaused = !AutoRestartTimerOnAdvance`, and `CountDownStart = DateTime.UtcNow`.
   If `AutoAdvanceLine` is false, `NextAdvanceAt` must always be null.
2. Delete every remaining expression that computes a deadline as `CountDownStart + AutoAdvanceInterval`. `CountDownStart` keeps only one meaning from now on: **when the current turn started**, for display. Nothing may derive a deadline from it.
3. Use `DateTime.UtcNow` exclusively in this file. There must be zero occurrences of `DateTime.Now`.
4. Add `Task<IReadOnlyList<string>> GetDueLineIdsAsync(DateTime utcNow, CancellationToken cancellationToken)`:
   ```csharp
   await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
   return await context.Lines
       .AsNoTracking()
       .Where(l => l.NextAdvanceAt != null
                && l.NextAdvanceAt <= utcNow
                && l.AutoAdvanceLine
                && !l.IsPaused
                && l.Participants.Any(p => !p.Removed))
       .Select(l => l.Id)
       .ToListAsync(cancellationToken);
   ```
   It returns ids, not entities, so the scheduler cannot accidentally mutate a detached graph.
5. Add `Task<bool> TryAdvanceAsync(string lineId)`. This is the only method in the codebase that needs an explicit transaction:
   - open a context and `BeginTransactionAsync`
   - load the line **tracked**, with participants
   - **re-check the deadline inside the transaction**: if the line is null, or `NextAdvanceAt` is null, or `NextAdvanceAt > DateTime.UtcNow`, or `IsPaused`, or `!AutoAdvanceLine`, roll back and return `false`. This re-check is what makes a double advance impossible.
   - perform the same advance logic as `AdvanceLineAsync`, apply the transition from requirement 1, `SaveChangesAsync`, commit, return `true`
   - catch `DbUpdateConcurrencyException`, roll back, and return `false`. The `Version` concurrency token means a racing advance loses here rather than double-advancing. Do not retry.
   - do not catch any other exception; let it propagate to the scheduler's logger.
6. Add both new members to `ILineRepository`.
7. Fix `AdvanceLineAsync`'s re-add ordering so a participant is never served twice consecutively: when `AutoReAdd` is true the advanced participant is re-inserted at `Position = NextPosition++`, which places it last. Verify this is what the code does and correct it if it inserts anywhere else.

Constraints:
- Do NOT edit `LineService.cs`; SERVICE-001 owns it. Only add to `ILineRepository`, never rename or remove a member, so `LineService` keeps compiling.
- Do NOT add authorization or manage-token checks. SERVICE-002 owns those.
- Do NOT delete `LineAdvancementTask.cs` or `LineAdvancementService.cs`; HUB-001 removes them.
- Keep `TimeSpan` as the C# type for `AutoAdvanceInterval` and `UnpauseRemaining`. The EF value converter stores them as integer seconds; do not convert manually anywhere in this file.
- `TryAdvanceAsync` is the **only** place a transaction is permitted. Do not reintroduce one elsewhere.
- Do not clamp or validate `AutoAdvanceInterval` here; that belongs to SERVICE-002.

Acceptance Criteria:
- Pausing a line with 30 seconds remaining then resuming 10 minutes later leaves ~30 seconds remaining, not a negative or zero value.
- A paused line is never returned by `GetDueLineIdsAsync`.
- A line whose only participants are `Removed` is never returned by `GetDueLineIdsAsync`.
- Calling `TryAdvanceAsync` twice in a row for the same due line returns `true` then `false`, and the queue advanced exactly once.
- `AutoRestartTimerOnAdvance = false` leaves `NextAdvanceAt` null and `IsPaused` true after one advance.
- Zero occurrences of `DateTime.Now` and zero occurrences of `CountDownStart +` in the file.
- Every one of the four transitions is followed by a `SaveChangesAsync` on the same context.
- `dotnet build` succeeds with 0 errors.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
Paste the final `TryAdvanceAsync` and `GetDueLineIdsAsync` source verbatim — SCHED-001 calls both. State the exact `NextAdvanceAt`/`IsPaused`/`UnpauseRemaining` values after each of the four transitions, as a table.
