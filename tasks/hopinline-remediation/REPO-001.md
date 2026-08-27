Task ID:
REPO-001

Model:
haiku

Title:
Convert SQLLineRepository to IDbContextFactory and fix sync-over-async

Purpose:
`Program.cs` registers the context with `AddDbContext`, which is scoped — and in Blazor Server a DI scope lasts the whole circuit, so a single `DbContext` lives for the entire user session, accumulating tracked entities. That long-lived context is why Cancel in `EditLine` does not cancel: the edit form binds to a tracked entity, and navigating away hands the same dirty instance to the next page. Converting the repository to create a short-lived context per call fixes the lifetime problem and is also required before the background scheduler can touch the database from outside any circuit.

Dependencies:
MODEL-001

Consumes:
MODEL-001: the `SaveChangesAsync` override that stamps `Version` and `LastUpdated` — each short-lived context inherits it automatically

Produces:
`SQLLineRepository` with an `IDbContextFactory<ApplicationDbContext>` constructor, consumed by PROG-002 (DI registration) and REPO-002 (timer arithmetic).

Files:
- `HopInLine/Data/Line/SQLLineRepository.cs` (edit)

Requirements:
1. Change the constructor to take `IDbContextFactory<ApplicationDbContext> contextFactory` and store it in a readonly field. Delete the `ApplicationDbContext _context` field.
2. In **every** public method, open a fresh context at the top and dispose it at the end:
   ```csharp
   await using var context = await _contextFactory.CreateDbContextAsync();
   ```
   Use `using var context = _contextFactory.CreateDbContext();` in any method that is not async. Replace every existing `_context.` reference with `context.`.
3. There are three synchronous `SaveChanges()` calls, at roughly lines 17, 197 and 255. Handle them as follows — do not deviate:
   - **Line 197, inside `AddParticipantAsync`**: this one is the bug. It is a blocking call inside an `async` method. Change it to `await context.SaveChangesAsync();`.
   - **Line 17, inside `public void AddLine(Line line)`** and **line 255, inside `public void RemoveLine(string id)`**: these methods are declared synchronous, so a synchronous `SaveChanges()` is correct. **Leave them synchronous.** Use `using var context = _contextFactory.CreateDbContext();` and keep `context.SaveChanges();`. Do not rename them, do not make them async, and do not delete them — `LineService.cs` still calls `RemoveLine`, and that file belongs to SERVICE-001. SERVICE-001 repoints the caller and LIST-001 prunes the now-unused members from `ILineRepository`.
   - Never write `.GetAwaiter().GetResult()` or `.Result` or `.Wait()` anywhere in this file.
4. Four methods wrap their work in `using var tx = await _context.Database.BeginTransactionAsync();` — at roughly lines 28, 64, 91 and 123. For each one, count the `SaveChangesAsync` calls inside the transaction. If there is exactly **one**, delete the `BeginTransactionAsync` line and its matching `tx.CommitAsync()` — a single `SaveChangesAsync` is already atomic and the wrapper takes no additional locks. If there are **two or more**, leave the transaction exactly as it is. Report the count you found for each of the four in Handoff.
5. Unify the position scheme. `AddParticipantAsync` currently does
   `participant.Position = line.Participants.Any() ? line.Participants.Max(p => p.Position) + 1 : 0;`
   while `AdvanceLineAsync` and `ReAddRemovedParticipantAsync` use `line.NextPosition++`. Change `AddParticipantAsync` to use `participant.Position = line.NextPosition++;` so there is exactly one rule: **every insertion into the active queue takes `Position = line.NextPosition++`**. Leave `MoveParticipantUp`/`MoveParticipantDown` swapping positions as they do now.
6. Every query that loads a line for read-only display must use `.AsNoTracking()`. Queries that load a line in order to mutate it must NOT use `AsNoTracking()`.
7. Add `CancellationToken cancellationToken = default` parameters only if `ILineRepository` already declares them. Do not add new parameters otherwise.

Constraints:
- Do NOT edit `Program.cs`. PROG-002 changes `AddDbContext` to `AddDbContextFactory`. Until then the app will fail at startup with "no service for IDbContextFactory" — that is expected and does not block this task, because Validation is a build, not a run.
- Do NOT change pause/resume/restart/advance timer arithmetic or touch `NextAdvanceAt`. REPO-002 owns that.
- Do NOT edit `ILineRepository.cs`, and do NOT add, remove, or rename any member of it. Every change here must be body-only, so `LineService.cs` continues to compile untouched.
- Do NOT edit `LineService.cs`. SERVICE-001 owns it.
- Do not add authorization checks; SERVICE-002 owns those.
- Never hold two contexts open at once in one method.

Acceptance Criteria:
- `SQLLineRepository.cs` contains zero references to a field-level `DbContext`; the only remaining synchronous `SaveChanges()` calls are the two inside `AddLine` and `RemoveLine`.
- No `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` appears in the file.
- Every public method creates and disposes its own context.
- `ILineRepository.cs` and `LineService.cs` are unmodified.
- `AddParticipantAsync` assigns `Position` from `NextPosition++`; no method computes a position from `Max(Position) + 1`.
- No `BeginTransactionAsync` remains around a lone `SaveChangesAsync`.
- `dotnet build` succeeds with 0 errors and no more warnings than before.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
State the number of contexts created per public method (must be exactly one), the `SaveChangesAsync` count you found inside each of the four transactions and which ones you therefore removed, and confirm that `ILineRepository.cs` and `LineService.cs` were not touched.
