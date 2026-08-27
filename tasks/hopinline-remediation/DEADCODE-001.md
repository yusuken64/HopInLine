Task ID:
DEADCODE-001

Model:
haiku

Title:
Delete unreferenced files

Purpose:
`InMemoryLineRepository` implements `ILineRepository` but throws `NotImplementedException` from 8 of its 20 members — a landmine for anyone who swaps the DI registration to it. `Pages/Line/Component.razor` is a zero-byte file. Neither is referenced anywhere.

Dependencies:
(none)

Consumes:
(nothing)

Produces:
(nothing downstream consumes this)

Files:
- `HopInLine/Data/Line/InMemoryLineRepository.cs` (delete)
- `HopInLine/Pages/Line/Component.razor` (delete)

Requirements:
1. Before deleting, verify with a repository-wide search that neither type is referenced:
   - search for `InMemoryLineRepository` — the only expected hit is its own file
   - search for `Component.razor` and for a `<Component` element in any `.razor` file — expect no hits
   If either search finds a real consumer, STOP and report it instead of deleting.
2. Delete both files.

Constraints:
- Do NOT delete `ILineRepository.cs`. The interface stays; LIST-001 prunes members from it later.
- Do NOT delete `LineViewModel.cs` — removing it requires also editing `LineService.ToLiveViewModel`, and SERVICE-001 owns that file.
- Do NOT delete `LineHub.cs`, `LineAdvancementService.cs`, `LineAdvancementTask.cs`, `LineUpdatedNotifier.cs`, or `LineChangedEventArgs.cs`. They are still live at this point in the sequence; HUB-001 removes them once nothing references them.
- Do not touch `Program.cs`.

Acceptance Criteria:
- Both files are gone from the working tree and the git index.
- A repository-wide search for `InMemoryLineRepository` returns zero hits.
- `dotnet build` produces the same error count (0) and no new warnings.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
Confirm both searches came back clean before deletion, and report the resulting warning count.
