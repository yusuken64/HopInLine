Task ID:
SERVICE-001

Model:
haiku

Title:
Fix LineService's broken methods and switch it to ILineChangeNotifier

Purpose:
`DeleteLineAsync` contains three bugs in eleven lines: it does not `await` `GetLineAsync`, so `line` is a `Task` and the null check is always true; it then calls the synchronous `RemoveLine`; and it never notifies anyone, leaving viewers on a live view of a deleted queue. `CreateLineAsync` loops up to 100 times looking for a free id and then **uses the colliding id anyway**, producing a raw database error. `RestartTimer` never persists. `StopTimerAsync` mutates `AutoAdvanceLine` and never saves.

Dependencies:
NOTIF-001, IDGEN-001, REPO-002

Consumes:
NOTIF-001: `ILineChangeNotifier.NotifyAsync(string lineId)`
IDGEN-001: `IdGenerator.GenerateUniqueId()`, `IdGenerator.GenerateManageToken()`, `IdGenerator.NormalizeId(string)`
REPO-002: the four timer transitions now living in the repository, plus `TryAdvanceAsync`

Produces:
A `LineService` whose every mutation notifies exactly once — the behavior SERVICE-002 layers authorization onto and VIEW-001 depends on.

Files:
- `HopInLine/Data/Line/LineService.cs` (edit)
- `HopInLine/Data/Line/LineViewModel.cs` (delete)

Requirements:
1. Constructor: replace the `LineUpdatedNotifier` dependency with `ILineChangeNotifier`. Replace `new ParticipantFactory()` at roughly line 14 with a call to the now-static `ParticipantFactory.Create(...)` at each use site, and delete the field.
2. Fix `DeleteLineAsync` (roughly lines 79-89) completely:
   - `await` the `GetLineAsync` call so `line` is a `Line?`
   - if null, return without throwing
   - call the **async** `RemoveLineAsync(lineId, CancellationToken.None)`, not the synchronous `RemoveLine`
   - call `await _notifier.NotifyAsync(lineId)` afterwards so open viewers are told
3. Fix the id-collision loop in `CreateLineAsync` (roughly lines 33-40). After the loop, if the final candidate still collides, throw `InvalidOperationException("Could not allocate a unique line id.")`. Do not fall through and use a colliding id. Check the attempt counter explicitly against the maximum rather than relying on the loop condition.
4. In `CreateLineAsync`, set `line.ManageToken = IdGenerator.GenerateManageToken()` before persisting, and return a `LineDto` that includes it. The creator needs the token to build the manage link.
5. Normalize every incoming line id at the top of every public method: `lineId = IdGenerator.NormalizeId(lineId);`. Ids are shared by hand and arrive with stray whitespace and mixed case.
6. Delete `StartTimerAsync` and `StopTimerAsync` entirely. No button binds to them, and `StopTimerAsync` is the one that mutates `AutoAdvanceLine` without saving.
7. Delete `ToLiveViewModel` (roughly line 226) and delete the file `HopInLine/Data/Line/LineViewModel.cs`. These two are the only references to that type in the entire repository — the method is the sole consumer and nothing calls the method — so both go together. Confirm with a repository-wide search for `LineViewModel` returning zero hits afterwards.
8. Delete `RestartTimer` (the unpersisted one, roughly lines 112-125). Route callers to the repository's `RestartTimerAsync`, which now persists.
9. Every mutating method must end with exactly one `await _notifier.NotifyAsync(lineId)`, after the repository call has completed and saved. Audit all of them — add the call where missing, and remove any duplicate so a single action does not push two re-renders.
10. Never subscribe to the notifier from this class. Publishing only.
11. Replace any `DateTime.Now` in this file with `DateTime.UtcNow`.

Constraints:
- Do NOT add `manageToken` parameters or authorization checks. SERVICE-002 does that in the next wave, and doing it here would break every existing caller at once.
- Do NOT make `LineService` implement `ILineService` yet. SERVICE-002 owns that. Keep the current public signatures so `ViewLine.razor`, `EditLine.razor`, `StartLine.razor`, and `ListLines.razor` all keep compiling.
- Do NOT edit `SQLLineRepository.cs` or `ILineRepository.cs`; REPO-001 and REPO-002 own them.
- Do NOT delete `LineUpdatedNotifier.cs` — `LineAdvancementService` still injects it until HUB-001. Just stop using it here.
- Do NOT delete `LineHub.cs`, and do not change any Razor file.
- Do not add a rate limit or participant cap; SERVICE-002 owns those.

Acceptance Criteria:
- `DeleteLineAsync` awaits its load, calls `RemoveLineAsync`, and notifies. A viewer of a deleted line receives a notification.
- Exhausting the id-collision attempts throws `InvalidOperationException`, never a raw `DbUpdateException`.
- A newly created line has a non-empty `ManageToken`, and the returned `LineDto` carries it.
- `StartTimerAsync`, `StopTimerAsync`, `RestartTimer`, and `ToLiveViewModel` no longer exist, and a repository-wide search for `LineViewModel` returns zero hits.
- No reference to `LineUpdatedNotifier` remains in this file.
- Every mutating method contains exactly one `NotifyAsync` call.
- Zero occurrences of `DateTime.Now` in the file.
- `dotnet build` succeeds with 0 errors.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
List every public method and the single line number where it calls `NotifyAsync`. Confirm `LineViewModel.cs` was deleted and the search came back clean.
