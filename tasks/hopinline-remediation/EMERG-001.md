Task ID:
EMERG-001

Model:
haiku

Title:
Fix resource leaks, the invalid-line crash, and the stuck loading overlay

Purpose:
Three user-visible defects, all in the UI layer:
- `ViewLine.razor` defines `DisposeAsync()` but never declares `@implements IAsyncDisposable`, so Blazor never calls it. Every visit permanently leaks a WebSocket and a 1-second timer that keeps firing on a dead component.
- `ViewLine.razor:157` calls `LineDto.FromLine(lineData)` before null-checking `lineData`, so an unknown line ID throws and kills the circuit. The `if (line == null)` check on the next line is unreachable.
- Every action brackets its work with `ShowOverlay()` / `HideOverlay()` with no `try/finally`, so any exception leaves a permanent full-screen scrim at `z-index: 9999` and the app appears frozen with no recovery but a reload.

Dependencies:
(none)

Consumes:
(nothing)

Produces:
(nothing downstream consumes this)

Files:
- `HopInLine/Pages/Line/ViewLine.razor` (edit)
- `HopInLine/Pages/Index.razor` (edit)

Requirements:
1. Add `@implements IAsyncDisposable` to the top of `ViewLine.razor`, next to the existing `@page` directive.
2. Rewrite `DisposeAsync` to be null-safe and to dispose everything:
   ```csharp
   public async ValueTask DisposeAsync()
   {
       timer?.Stop();
       timer?.Dispose();
       if (hubConnection is not null)
       {
           try { if (line is not null) await hubConnection.SendAsync("LeaveLineGroup", line.Id); }
           catch { /* connection may already be closed */ }
           await hubConnection.DisposeAsync();
       }
   }
   ```
   The existing version dereferences `line.Id` unguarded, which throws when the line failed to load.
3. In `OnInitializedAsync`, null-check the loaded entity BEFORE converting it:
   ```csharp
   var lineData = await lineService.GetLineByIdAsync(lineId);
   if (lineData is null)
   {
       errorMessage = "Line not found.";
       OverlayService.HideOverlay();
       return;
   }
   line = LineDto.FromLine(lineData);
   ```
   Delete the now-redundant `if (line == null)` block that followed.
4. In `ViewLine.razor`, wrap the body of every method that calls `OverlayService.ShowOverlay()` in `try { ... } finally { OverlayService.HideOverlay(); }`, removing the trailing `HideOverlay()` calls. Applies to: `AddParticipant`, `AdvanceLine`, `MoveParticipantUp`, `MoveParticipantDown`, `RemoveParticipant`, `ReAddRemovedParticipant`, `DeleteRemovedParticipant`, `StartTimer`, `StopTimer`, `ResumeTimer`, `PauseTimer`, `RestartTimer`.
5. Apply the same `try/finally` treatment to `OnInitializedAsync` in `ViewLine.razor` and to `NavigateToJoinLine` in `Index.razor`.

Constraints:
- Do NOT remove the `HubConnection`, the `System.Timers.Timer`, or the `host.docker.internal` branch. VIEW-001 replaces all of that later; this task only stops the bleeding.
- Do NOT change any markup, styling, or routing.
- Do NOT change `LineDto.FromLine` itself.

Acceptance Criteria:
- `ViewLine.razor` declares `@implements IAsyncDisposable`.
- Navigating to `/view-line/ZZZZZZZZ` shows "Line not found." instead of the Blazor error banner.
- Navigating into a line and back ten times leaves ten *fewer* live connections than before the change — verified by confirming `DisposeAsync` is reached (temporary log line, removed before completion).
- No `ShowOverlay()` call site lacks a matching `finally`.
- Forcing an exception inside `AddParticipant` still clears the overlay.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
List every method you wrapped in `try/finally`, and confirm the `@implements` directive is present.
