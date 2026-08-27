Task ID:
LIST-001

Model:
haiku

Title:
Delete ListLines and prune the over-fetching repository members

Purpose:
`/list-lines` enumerates every line id to any anonymous visitor. Since the line id is the join code and — until SERVICE-002 — was the only thing gating management, that page handed every queue in the system to anyone who loaded it. Keeping it would defeat the manage-token scheme entirely, because an attacker no longer has to guess an id. Its backing query is also the worst in the codebase: `GetAllLines()` `Include`s every participant of every line purely to render a count.

Dependencies:
SERVICE-002

Consumes:
SERVICE-002: `ILineService`, which deliberately has no "list all lines" member

Produces:
(nothing downstream consumes this)

Files:
- `HopInLine/Pages/Line/ListLines.razor` (delete)
- `HopInLine/Shared/NavMenu.razor` (edit)
- `HopInLine/Data/Line/ILineRepository.cs` (edit)
- `HopInLine/Data/Line/SQLLineRepository.cs` (edit)
- `HopInLine/Data/Line/LineService.cs` (edit)

Requirements:
1. Delete `HopInLine/Pages/Line/ListLines.razor`.
2. Remove the nav entry that links to it from `NavMenu.razor` — the `<div class="nav-item">` block at roughly lines 17-21. Leave the other nav entries and the `NavMenu.razor.css` untouched.
3. Remove `GetAllLines()` and `GetAllLinesAsync(CancellationToken)` from `ILineRepository`, and their implementations from `SQLLineRepository`.
4. Remove the corresponding pass-through members from `LineService` if any exist.
5. Also prune from `ILineRepository` and `SQLLineRepository` the two synchronous duplicates that REPO-001 deliberately left in place: `void AddLine(Line line)` and `void RemoveLine(string id)`. Async equivalents (`AddLineAsync`, `RemoveLineAsync`) already exist and SERVICE-001 repointed the only caller of `RemoveLine`. Before deleting each, search the repository for its name and confirm the only remaining hits are the interface declaration and its implementation. **If any other caller exists, leave that member and report it in Handoff** rather than rewriting the caller.
6. Search the whole repository for `list-lines`, `ListLines`, `GetAllLines`, `AddLine(`, and `RemoveLine(` and confirm no reference survives except the async members that are staying.

Constraints:
- Do NOT replace `ListLines` with an authenticated or admin-gated variant. There is no account system, and adding one is explicitly out of scope. The page is removed, not relocated.
- Do NOT delete `ILineRepository.cs` itself, and do not remove any member other than the four named above.
- Do NOT delete `NavMenu.razor` or its stylesheet; remove only the one nav item.
- Do NOT edit `Program.cs`; PROG-002 owns it.
- Do not change any method that VIEW-001, EDIT-001, or START-001 calls.
- Do not add a redirect from `/list-lines`. The route should simply not exist.

Acceptance Criteria:
- `/list-lines` returns the not-found experience; the route is gone.
- The nav menu no longer shows a lines-directory entry and renders correctly.
- No `Include`-every-participant query remains in `SQLLineRepository`.
- A repository-wide search for `GetAllLines` returns zero hits.
- `dotnet build` succeeds with 0 errors.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
List every member removed from `ILineRepository`, and name any member you left in place because a caller still existed.
