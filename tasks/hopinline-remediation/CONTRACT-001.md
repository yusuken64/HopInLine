Task ID:
CONTRACT-001

Model:
haiku

Title:
Define ILineService and ILineChangeNotifier interfaces (new files only)

Purpose:
This is the key parallelism-unlocking task. Today `ViewLine.razor` opens a SignalR client connection from the server back to a hub in its own process, and `LineUpdatedNotifier` couples notification to timer restarts — which is the root cause of Pause not working. Publishing the two target interfaces as NEW files, before any implementation changes, lets the UI tasks, the scheduler, the notifier implementation, and the service implementation all proceed in parallel against a stable contract.

Dependencies:
MODEL-001

Consumes:
MODEL-001: `Line.NextAdvanceAt`, `Line.ManageToken`, `LineDto.NextAdvanceAt`, `LineDto.ManageToken`

Produces:
The two contracts consumed by NOTIF-001, SCHED-001, SERVICE-001, SERVICE-002, VIEW-001, EDIT-001, START-001, LIST-001:
- `HopInLine.Data.Line.ILineChangeNotifier`
- `HopInLine.Data.Line.ILineService`

Files:
- `HopInLine/Data/Line/ILineChangeNotifier.cs` (new)
- `HopInLine/Data/Line/ILineService.cs` (new)

Requirements:
1. `ILineChangeNotifier` — an in-process publish/subscribe contract with no SignalR types anywhere in its signature:
   ```csharp
   public interface ILineChangeNotifier
   {
       IDisposable Subscribe(string lineId, Func<LineDto, Task> handler);
       Task NotifyAsync(string lineId);
   }
   ```
   Document in an XML comment that: the returned `IDisposable` MUST remove the handler; `NotifyAsync` loads the current line and invokes every handler for that id; and handlers may be invoked on a background thread, so Blazor components must marshal with `InvokeAsync`.

2. `ILineService` — every mutating method takes a `manageToken`; read methods do not:
   ```csharp
   public interface ILineService
   {
       Task<LineDto?> GetLineAsync(string lineId);

       Task<LineDto> CreateLineAsync(string name, string? description, bool autoReAdd,
                                     bool autoAdvanceLine, TimeSpan autoAdvanceInterval);

       Task AddParticipantAsync(string lineId, string participantName);

       Task UpdateLineAsync(string lineId, string manageToken, string name, string? description,
                            bool autoAdvanceLine, bool autoReAdd, bool autoRestartTimerOnAdvance,
                            TimeSpan autoAdvanceInterval);
       Task DeleteLineAsync(string lineId, string manageToken);

       Task AdvanceLineAsync(string lineId, string manageToken);
       Task MoveParticipantUpAsync(string lineId, string manageToken, string participantId);
       Task MoveParticipantDownAsync(string lineId, string manageToken, string participantId);
       Task RemoveParticipantAsync(string lineId, string manageToken, string participantId);
       Task ReAddRemovedParticipantAsync(string lineId, string manageToken, string participantId);
       Task DeleteRemovedParticipantAsync(string lineId, string manageToken, string participantId);

       Task PauseTimerAsync(string lineId, string manageToken);
       Task ResumeTimerAsync(string lineId, string manageToken);
       Task RestartTimerAsync(string lineId, string manageToken);

       Task<bool> ValidateManageTokenAsync(string lineId, string manageToken);
   }
   ```
3. Note in an XML comment on the interface that mutating methods throw `UnauthorizedAccessException` when `manageToken` does not match, and that `AddParticipantAsync` deliberately takes a **name string, not a `Participant` entity** — the current hub method accepts a client-supplied entity including `Position`, `TurnCount`, `Removed`, `LineId`, and the `Line` navigation property, which is a mass-assignment hole.
4. `CreateLineAsync` returns the created `LineDto` including its `ManageToken`, so the creator can be shown both the join link and the manage link.

Constraints:
- **Create only these two new files. Do not edit any existing file.** In particular do not touch `LineService.cs`, `LineUpdatedNotifier.cs`, `ILineRepository.cs`, or `Program.cs`. Nothing implements these interfaces yet, and that is correct — the build must stay green.
- No implementation, no abstract classes, no DI registration.
- No `Microsoft.AspNetCore.SignalR` using directive in either file.
- Use `LineDto`, never the `Line` entity, in any signature.

Acceptance Criteria:
- Both files exist in namespace `HopInLine.Data.Line` and contain only interface declarations plus XML comments.
- `ILineService` has exactly the 15 members listed; every mutating member takes `manageToken` as its second parameter.
- No existing file was modified.
- `dotnet build` succeeds with 0 errors and no new warnings.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
Paste both finished interface declarations verbatim. Downstream tasks code directly against this text, so it must be exact.
