Task ID:
VIEW-001

Model:
haiku

Title:
Rewrite ViewLine to use in-process notifications and a client-side countdown

Purpose:
This is the single highest-impact change in the plan. `ViewLine.razor` runs on the server and opens a SignalR **client** connection back to a hub in the same process — a second full protocol stack plus JSON round-tripping of every `LineDto`, per viewer — while `LineUpdatedNotifier` already exposed the in-process event that nothing subscribed to. On top of that it runs a server-side 1-second `System.Timers.Timer` per viewer calling `StateHasChanged`, which is never stopped, so every page view permanently leaks a WebSocket and a timer and pushes a re-render diff every second forever. Replacing both removes essentially all of the app's idle traffic.

Dependencies:
SERVICE-002, NOTIF-001, JS-001, FMT-001, EMERG-001, CARD-001, UX-001

Consumes:
NOTIF-001: `ILineChangeNotifier.Subscribe(lineId, handler)` returning `IDisposable`
JS-001: `window.hopInLine.startCountdown/stopCountdown/confirm`
FMT-001: `DurationFormat.Humanize(TimeSpan)`
SERVICE-002: `ILineService` including `ValidateManageTokenAsync`
UX-001: the `/not-found` route
EMERG-001: the existing `@implements IAsyncDisposable` and `try/finally` overlay blocks

Produces:
(nothing downstream consumes this)

Files:
- `HopInLine/Pages/Line/ViewLine.razor` (edit)

Requirements:
1. Add a second route alongside the existing one so the same component serves both audiences:
   ```
   @page "/view-line/{LineId}"
   @page "/manage/{LineId}/{ManageToken}"
   ```
   Add a `[Parameter] public string? ManageToken { get; set; }`.
2. Add a `bool canManage` field. In `OnInitializedAsync`, set it via `await LineService.ValidateManageTokenAsync(LineId, ManageToken ?? string.Empty)`. Wrap **every** management control — advance, move up, move down, remove, re-add, delete participant, pause, resume, restart, and the edit/delete-line links — in `@if (canManage) { ... }`. A visitor on the join URL must see the queue and the join box and nothing else.
3. Delete the loopback SignalR client entirely: the `HubConnection` field, its construction at roughly lines 167-176, the `host.docker.internal` branch, the `.On<...>` handler registration, `StartAsync`, and the SignalR `using` directives. Route every mutating call through `ILineService` directly instead of `hubConnection.SendAsync`, passing `ManageToken`.
4. Delete the `updateId >= updatedId` guard at roughly line 180. `LineChangedEventArgs.updateId` is never assigned anywhere, so the comparison is `0 >= 0` on every message — it is always true and protects nothing.
5. Subscribe to the notifier instead:
   - inject `ILineChangeNotifier Notifier`
   - in `OnInitializedAsync`, store `_subscription = Notifier.Subscribe(LineId, OnLineChanged)`
   - `OnLineChanged(LineDto dto)` must marshal onto the renderer: `await InvokeAsync(() => { line = dto; StateHasChanged(); });` Handlers are invoked from a background scheduler thread, so calling `StateHasChanged` directly is not valid.
   - dispose `_subscription` in `DisposeAsync`, alongside the disposals EMERG-001 added.
6. Delete the server-side `System.Timers.Timer` at roughly lines 195-208 — the field, its construction, its `Elapsed` handler, and its disposal. Replace with the client countdown:
   - render a `<span id="countdown-@LineId" aria-live="polite">` placeholder
   - in `OnAfterRenderAsync`, when `line?.NextAdvanceAt` is not null, call
     `await JS.InvokeVoidAsync("hopInLine.startCountdown", $"countdown-{LineId}", line.NextAdvanceAt.Value.ToString("O"))`
   - when `NextAdvanceAt` is null (paused, or auto-advance off), call `hopInLine.stopCountdown` for that element id and render the paused label from `DurationFormat.Humanize(line.UnpauseRemaining ?? TimeSpan.Zero)` instead
   - call `stopCountdown` in `DisposeAsync` too
   - use the round-trip `"O"` format specifier so the JS side parses an unambiguous UTC instant
7. Replace both `ToString(@"mm\:ss")` calls at roughly lines 216 and 233 with `DurationFormat.Humanize(...)`. That format specifier prints only the minutes component, so 90 minutes renders as `30:00`.
8. Add confirmations via `await JS.InvokeAsync<bool>("hopInLine.confirm", "...")`, proceeding only on `true`, for: Remove participant, Delete participant, and Delete line. All three currently fire immediately.
9. Replace the full-screen overlay on every action with per-button state: add a `bool busy` field, set it around each service call in a `try/finally`, and bind `disabled="@busy"` on the action buttons. Then delete the `ShowOverlay`/`HideOverlay` calls and the overlay markup from this page. The overlay only ever bracketed message *dispatch*, so it flickered rather than gated.
10. Fix the CS8602 at roughly line 212 by null-checking `line` before dereferencing it.
11. Accessibility: add an `<h1>` as the first element showing the line name (`App.razor`'s `<FocusOnNavigate Selector="h1">` matches nothing today), and fix the `<h3>`/`<h4>` order so no level is skipped.
12. If the line does not load, render an inline "line not found" message with a link to `/`. Do **not** navigate to `/error`, and do not let a null `line` reach `LineDto.FromLine` — EMERG-001 added that null check, so keep it.

Constraints:
- Do NOT delete `LineHub.cs`, `LineUpdatedNotifier.cs`, `LineAdvancementService.cs`, or `LineAdvancementTask.cs`, and do not remove the `Microsoft.AspNetCore.SignalR.Client` package. HUB-001 does all of that once this page is the last consumer gone.
- Do NOT call `IJSRuntime` from `OnInitializedAsync`. Prerendering is still enabled at this point in the sequence, and there is no JS runtime during the first render. `OnAfterRenderAsync` only.
- Do NOT reintroduce a server-side timer, `PeriodicTimer`, or any recurring `StateHasChanged`. The only re-render triggers are a notifier callback and a user action.
- Do NOT edit `ParticipantCard.razor` or `RemovedParticipantCard.razor`; CARD-001 owns them. Do not change the parameters you pass to them.
- Do NOT edit `_Host.cshtml`; PROG-002 changes the render mode.
- Do not remove the `@implements IAsyncDisposable` declaration EMERG-001 added — without it `DisposeAsync` is never called and the subscription leaks, which is the bug this task exists to fix.
- Do not add polling as a fallback for the notifier.

Acceptance Criteria:
- Browser dev tools show exactly **one** WebSocket (the Blazor circuit). Navigating in and out of a line ten times leaves the count at one.
- No network traffic flows on an idle open line between advances; the countdown ticks with zero server round-trips.
- Opening `/view-line/{id}` shows no management controls; `/manage/{id}/{token}` shows them all. A wrong token shows none.
- An advance by one viewer updates every other viewer's list within about a second.
- A 90-minute interval displays as `1:30:00`.
- Pausing stops the countdown and shows the frozen remaining time; resuming continues from that value.
- Remove, Delete participant, and Delete line each prompt for confirmation and abort cleanly when dismissed.
- Navigating away disposes the subscription — the notifier's dictionary no longer holds a handler for that line.
- Opening `/view-line/ZZZZZZZZ` shows "line not found", not a crashed circuit.
- No `HubConnection`, `System.Timers.Timer`, `ShowOverlay`, or `ToString(@"mm\:ss")` remains in the file.
- CS8602 at line 212 is gone.
- `dotnet build` succeeds with 0 errors.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
Confirm the file contains zero SignalR references and zero server-side timers. State how the subscription is disposed and where `stopCountdown` is called. Report the warning count before and after.
