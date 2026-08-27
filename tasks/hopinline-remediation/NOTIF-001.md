Task ID:
NOTIF-001

Model:
haiku

Title:
Implement LineChangeNotifier as an in-process singleton pub/sub

Purpose:
`LineUpdatedNotifier.cs:10` already exposes a `LineUpdated` event, but nothing subscribes to it — instead `ViewLine.razor` runs on the server and opens a SignalR **client** connection to a hub inside its own process, standing up a second protocol stack and JSON-round-tripping every `LineDto`, per viewer. This task implements the real in-process path as a NEW type alongside the old one, so nothing breaks until VIEW-001 switches over and HUB-001 deletes the old code.

Dependencies:
CONTRACT-001

Consumes:
CONTRACT-001: the `ILineChangeNotifier` interface
MODEL-001: `LineDto` including `NextAdvanceAt` and `ManageToken`

Produces:
`HopInLine.Data.Line.LineChangeNotifier` — the singleton implementation registered by PROG-002 and subscribed to by VIEW-001.

Files:
- `HopInLine/Data/Line/LineChangeNotifier.cs` (new)

Requirements:
1. Create `LineChangeNotifier : ILineChangeNotifier`. It is a **singleton**, so it must be thread-safe and must never hold a `DbContext` in a field.
2. Constructor takes `IDbContextFactory<ApplicationDbContext> contextFactory` and `ILogger<LineChangeNotifier> logger`.
3. Backing store:
   ```csharp
   private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Func<LineDto, Task>>> _subscribers = new();
   ```
   Outer key is the line id, inner key is a subscription token.
4. `Subscribe(string lineId, Func<LineDto, Task> handler)`:
   - `GetOrAdd` the inner dictionary for `lineId`
   - generate `var token = Guid.NewGuid()` and add the handler under it
   - return an `IDisposable` whose `Dispose` removes that token from the inner dictionary, and — if the inner dictionary is then empty — removes the line's entry from `_subscribers`. **Subscriptions must not leak**; a page view that is never unsubscribed keeps its component alive forever.
   - make `Dispose` idempotent: calling it twice must not throw.
   - Implement the `IDisposable` as a small private nested class or via a private `Subscription` record. Do not use a lambda-based disposable helper you have to write from scratch beyond that.
5. `NotifyAsync(string lineId)`:
   - if `_subscribers` has no entry for `lineId`, return immediately without touching the database — this is the common case for a line with no viewers, and loading the line would be wasted work
   - otherwise open a short-lived context from the factory, load the line **with `AsNoTracking()` and its participants included**, and project it to a `LineDto`
   - if the line no longer exists, log at Debug and return without invoking handlers
   - invoke every handler, each inside its own `try/catch`, logging any exception at Warning with the line id. **One failing handler must never prevent the others from running** — a disposed Blazor circuit will throw, and that must not break every other viewer of the same line.
   - snapshot the handler collection before iterating (`.ToArray()` on `.Values`) so a concurrent unsubscribe during the loop cannot throw.
6. Do not swallow `OperationCanceledException` silently — log it at Debug. Never catch and ignore without logging.
7. Add an XML comment stating that handlers are invoked on whatever thread called `NotifyAsync` (typically a background scheduler thread), so Blazor components must marshal via `InvokeAsync`.

Constraints:
- **Do not modify or delete `LineUpdatedNotifier.cs`, `LineChangedEventArgs.cs`, or `LineHub.cs`.** They stay live until HUB-001 removes them. This is an add-alongside task.
- Do NOT register anything in `Program.cs`; PROG-002 owns DI.
- Do NOT reference `Microsoft.AspNetCore.SignalR` or `HubConnection` anywhere in this file.
- Do NOT call any `ILineService` or `ILineRepository` member from the notifier. It reads the database directly and only for projection. Injecting the service would create a cycle, because the service calls the notifier.
- Never use `lock` around an `await`.
- Do not add an `event` — the interface is `Subscribe`/`NotifyAsync` only.

Acceptance Criteria:
- `LineChangeNotifier` implements `ILineChangeNotifier` with no other public members.
- Disposing a returned subscription removes exactly that handler and leaves other subscribers to the same line intact.
- Disposing the last subscriber for a line removes the line's key from `_subscribers`, so the dictionary does not grow without bound.
- A handler that throws is logged, and every other handler for the same line still runs.
- `NotifyAsync` for a line with zero subscribers performs no database query.
- No field of type `DbContext` or `ApplicationDbContext` exists on the class.
- `dotnet build` succeeds with 0 errors.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
Paste the full class source. SCHED-001 and VIEW-001 both code against it. State explicitly how `Dispose` guarantees the handler is removed and how the empty-line-key cleanup works.
