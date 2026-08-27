Task ID:
SCHED-001

Model:
haiku

Title:
Add LineAdvancementBackgroundService as a real BackgroundService

Purpose:
This fixes the two worst behavioral bugs at once. `LineAdvancementService` is registered with `AddSingleton`, not `AddHostedService`, so `ExecuteAsync` never runs — auto-advancing lines stay frozen after every restart and only advance while some page happens to be open. And its constructor subscribes to its own `LineUpdated` event and unconditionally calls `StartLineAdvancement`; since `PauseTimerAsync` notifies clients *after* stopping the timer, the notification restarts it, which is why Pause and Stop Timer do not work. The replacement holds no per-line state at all, so neither bug can recur: notification means "tell the UI" and nothing else.

Dependencies:
REPO-002, NOTIF-001

Consumes:
REPO-002: `GetDueLineIdsAsync(DateTime utcNow, CancellationToken)` and `TryAdvanceAsync(string lineId)`
NOTIF-001: `ILineChangeNotifier.NotifyAsync(string lineId)`

Produces:
`HopInLine.Data.Line.LineAdvancementBackgroundService` — registered by PROG-002 via `AddHostedService`.

Files:
- `HopInLine/Data/Line/LineAdvancementBackgroundService.cs` (new)

Requirements:
1. Create `public sealed class LineAdvancementBackgroundService : BackgroundService`.
2. Constructor takes `IServiceScopeFactory scopeFactory` and `ILogger<LineAdvancementBackgroundService> logger`. Use `IServiceScopeFactory`, **not** a direct `ILineRepository` injection — a hosted service is a singleton, and injecting a scoped service into it either throws at startup or captures it for the process lifetime.
3. `ExecuteAsync(CancellationToken stoppingToken)` is exactly this shape:
   ```csharp
   while (!stoppingToken.IsCancellationRequested)
   {
       try
       {
           using var scope = _scopeFactory.CreateScope();
           var repo = scope.ServiceProvider.GetRequiredService<ILineRepository>();
           var notifier = scope.ServiceProvider.GetRequiredService<ILineChangeNotifier>();

           var dueIds = await repo.GetDueLineIdsAsync(DateTime.UtcNow, stoppingToken);
           foreach (var id in dueIds)
           {
               if (stoppingToken.IsCancellationRequested) break;
               if (await repo.TryAdvanceAsync(id))
               {
                   await notifier.NotifyAsync(id);
               }
           }
       }
       catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
       {
           break;
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Line advancement sweep failed.");
       }

       try { await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); }
       catch (OperationCanceledException) { break; }
   }
   ```
4. The `try/catch` must be **inside** the `while` loop, not around it. If it wraps the loop, one transient database error kills the scheduler for the remaining process lifetime and every auto-advancing line silently freezes — which is the failure mode this task exists to fix.
5. Log at Information once on entry (`"Line advancement service started."`) and once on exit (`"Line advancement service stopping."`). These two lines are the operator's only evidence the hosted service is actually registered.
6. Notify only when `TryAdvanceAsync` returned `true`. Notifying on a `false` return would push a redundant re-render to every viewer every second.
7. Never call `StartLineAdvancement`, `StopLineAdvancement`, or `ResumeLineAdvancement`, and never keep a dictionary, timer, or any per-line field. The class must have exactly two readonly fields: the scope factory and the logger. All scheduling state lives in the `NextAdvanceAt` column.
8. Do not subscribe to `ILineChangeNotifier`. The scheduler publishes; it never listens. Subscribing is precisely the bug being removed.

Constraints:
- **Do not modify or delete `LineAdvancementService.cs` or `LineAdvancementTask.cs`.** They stay on disk until HUB-001 deletes them. This is an add-alongside task, so the build stays green.
- Do NOT register this in `Program.cs`; PROG-002 owns DI and will add `AddHostedService<LineAdvancementBackgroundService>()`. Until it does, this class never runs — that is expected.
- Do NOT use `Task.Run`, `Thread`, `System.Timers.Timer`, or `PeriodicTimer`. A plain `while` + `Task.Delay` is what is specified.
- Do NOT call `ILineService`; go through `ILineRepository`. The service layer will enforce manage tokens, and the scheduler has no token.
- Do not swallow an exception without logging it.
- Do not add a configurable poll interval, appsettings binding, or `IOptions`. One second, hardcoded.

Acceptance Criteria:
- The class derives from `BackgroundService` and overrides only `ExecuteAsync`.
- It has exactly two fields, both readonly, neither of them a `DbContext`, repository, or notifier.
- A thrown exception on one sweep is logged and the next sweep still runs one second later.
- Cancelling the stopping token exits the loop without logging an error.
- A line that is due advances exactly once per due deadline, and viewers are notified once.
- A paused line is never advanced, even when its old deadline is long past.
- `dotnet build` succeeds with 0 errors.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
Paste the full class. State the exact DI registration line PROG-002 must add, and confirm the class holds no per-line state.
