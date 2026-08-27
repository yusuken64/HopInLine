Task ID:
PROG-002

Model:
haiku

Title:
Rewire dependency injection and switch off prerendering

Purpose:
This task makes the app runnable again and fixes the two registration bugs behind the worst symptoms. `LineAdvancementService` was registered with `AddSingleton`, not `AddHostedService`, so its `ExecuteAsync` never ran — auto-advancing lines stayed frozen after every restart. And `AddDbContext` is scoped, which in Blazor Server means a scope lasting the entire circuit, so one `DbContext` lived for a whole user session. This is the last task before integration, so nothing else may touch `Program.cs` after it.

Dependencies:
HUB-001, REPO-001, SCHED-001, NOTIF-001, SERVICE-002, FACTORY-001, PROG-001, LIST-001

Consumes:
HUB-001: the four deleted registration lines, leaving a `Program.cs` that builds but cannot start
REPO-001: `SQLLineRepository`'s `IDbContextFactory` constructor
SCHED-001: `LineAdvancementBackgroundService`
NOTIF-001: `LineChangeNotifier`
SERVICE-002: `LineService : ILineService`
FACTORY-001: `ParticipantFactory.Create` as a static member

Produces:
A startable application — the precondition for INT-001's end-to-end verification.

Files:
- `HopInLine/Program.cs` (edit)
- `HopInLine/Pages/_Host.cshtml` (edit)

Requirements:
1. Replace `builder.Services.AddDbContext<ApplicationDbContext>(...)` at roughly line 13 with
   `builder.Services.AddDbContextFactory<ApplicationDbContext>(...)`, keeping the same `UseSqlServer(connectionString)` configuration.
   `AddDbContextFactory` registers only `IDbContextFactory<ApplicationDbContext>` — it does **not** register `ApplicationDbContext` itself. There is exactly one place that resolves the context directly: the migration bootstrap at roughly line 48, `var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();`. That line will throw at startup. Change it to use the factory instead:
   ```csharp
   var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
   using var db = factory.CreateDbContext();
   ```
   Leave the `db.Database.Migrate()` call that follows exactly as PROG-001 left it. Do **not** additionally call `AddDbContext` — after this change nothing injects the context directly, so registering both would just reintroduce the circuit-lifetime context this task exists to remove.
2. Register `LineChangeNotifier` as a **singleton**:
   `builder.Services.AddSingleton<ILineChangeNotifier, LineChangeNotifier>();`
   It must be a singleton — its subscriber dictionary is the shared bus. Registering it scoped would give every circuit its own empty bus and no viewer would ever receive an update.
3. Register the scheduler with `AddHostedService`, which is the actual fix for frozen lines:
   `builder.Services.AddHostedService<LineAdvancementBackgroundService>();`
   Do **not** also `AddSingleton` it. `AddSingleton` alone never starts `ExecuteAsync`, which was the original bug.
4. Register the service and repository as scoped against their interfaces:
   ```csharp
   builder.Services.AddScoped<ILineRepository, SQLLineRepository>();
   builder.Services.AddScoped<ILineService, LineService>();
   ```
   `ILineRepository` must be resolvable from a scope created by the hosted service — SCHED-001 resolves it via `IServiceScopeFactory`, which works with a scoped registration.
5. Delete the `AddSingleton<ParticipantFactory>()` registration. FACTORY-001 made `Create` static and nothing injects the type.
6. In `Pages/_Host.cshtml`, change `render-mode="ServerPrerendered"` to `render-mode="Server"`. Prerendering runs `OnInitializedAsync` twice, which with VIEW-001's notifier subscription orphans one subscription per page load, and it also means the first render has no JS runtime available.
7. After editing, verify the app actually starts: `dotnet run` must reach "Now listening on" with no `InvalidOperationException` about an unregistered or captive dependency. A captive-dependency error here means a singleton is injecting something scoped — fix the registration, not the consuming class.
8. Confirm the two Information log lines from SCHED-001 ("Line advancement service started.") appear on startup. Their absence means the hosted service is still not registered, which is the exact bug this task fixes.

Constraints:
- Do NOT re-add `AddSignalR()` or `MapHub<LineHub>`; HUB-001 removed them deliberately.
- Do NOT remove `AddServerSideBlazor()`, `MapBlazorHub()`, or the `_Host` fallback. The Blazor Server hosting model stays — do not migrate to `MapRazorComponents`.
- Do NOT re-add `db.Database.EnsureDeleted()` or the pending-migrations check. PROG-001 removed them; `EnsureDeleted` destroyed all data on every migration.
- Do NOT change the `UseHsts`/`UseHttpsRedirection`/`UseForwardedHeaders` middleware ordering PROG-001 established.
- Do NOT inject a scoped service into a singleton to make an error go away. Use `IServiceScopeFactory`.
- Do NOT add `AddRateLimiter`; it cannot see Blazor circuit invocations, so it would only look like protection.
- Do not add authentication or authorization middleware. Authorization lives in `LineService`.

Acceptance Criteria:
- `dotnet run` starts cleanly and logs "Line advancement service started."
- No `AddDbContext` call remains, and startup does not throw resolving `ApplicationDbContext`.
- No `InvalidOperationException` for an unregistered service and no captive-dependency error.
- `ILineChangeNotifier` resolves to the same instance across two different circuits (verifiable because a change made in one browser reaches another).
- An auto-advancing line continues to advance with **no browser open at all** — the regression test for the missing `AddHostedService`.
- Restarting the app mid-countdown resumes advancement on schedule.
- `render-mode="Server"` appears in `_Host.cshtml` and `OnInitializedAsync` runs once per page load.
- Applying a new migration does not delete data.
- `dotnet build` succeeds with 0 errors.

Validation:
`dotnet build` completes with 0 errors and `dotnet run` reaches "Now listening on" without an exception.

Handoff:
Paste the final service-registration block and the rewritten migration-bootstrap block. Confirm `AddDbContext` is gone, that nothing injects `ApplicationDbContext` directly, and that the scheduler's startup log line appeared.
