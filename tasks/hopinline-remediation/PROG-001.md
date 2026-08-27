Task ID:
PROG-001

Model:
haiku

Title:
Stop destroying the database on startup; fix middleware and configuration wiring

Purpose:
`Program.cs:53` calls `db.Database.EnsureDeleted()` whenever a migration is pending — adding any migration wipes every line and participant. This is the single highest-severity defect in the codebase. This task also fixes the HTTPS middleware, which currently sends HSTS headers over plaintext in the HTTP-only container, and removes a configuration block that overrides command-line arguments.

Dependencies:
(none)

Consumes:
(nothing)

Produces:
(nothing downstream consumes this)

Files:
- `HopInLine/Program.cs` (edit)

Requirements:
1. Replace the startup migration block (currently lines ~46-56) with an unconditional migrate:
   ```csharp
   using (var scope = app.Services.CreateScope())
   {
       var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
       db.Database.Migrate();
   }
   ```
   **Delete the `EnsureDeleted()` call and the `GetPendingMigrations()` check entirely.** `Migrate()` is a no-op when there is nothing pending.
2. Delete the `builder.Configuration.AddJsonFile(...).AddJsonFile(...).AddEnvironmentVariables()` block (currently lines ~21-24). `WebApplication.CreateBuilder` already registers all three sources; re-adding them appends them last, which makes them override command-line arguments.
3. Replace the unconditional `app.UseHttpsRedirection()` and the `app.UseHsts()` inside the non-Development branch so both run ONLY when an HTTPS endpoint is configured. Determine this by reading `builder.Configuration["ASPNETCORE_HTTPS_PORTS"]` / `["ASPNETCORE_URLS"]` before `builder.Build()` and storing a `bool hasHttps` (true when either value is non-empty and contains `https`). Guard both calls with `if (hasHttps)`. Keep `app.UseExceptionHandler("/Error")` unconditional for non-Development.
4. Add `app.UseForwardedHeaders()` as the first middleware in the pipeline, and register the options before `Build()`:
   ```csharp
   builder.Services.Configure<ForwardedHeadersOptions>(o =>
       o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
   ```
   (`using Microsoft.AspNetCore.HttpOverrides;`)

Constraints:
- Do NOT touch any service registration: leave `AddScoped<LineService>`, `AddSingleton<ParticipantFactory>`, `AddSignalR`, `AddScoped<ILineRepository, SQLLineRepository>`, `AddDbContext`, `AddSingleton<LineUpdatedNotifier>`, `AddSingleton<LineAdvancementService>`, and `AddScoped<OverlayService>` exactly as they are. PROG-002 rewires DI later and must not conflict with this task.
- Do NOT touch `MapBlazorHub`, `MapHub<LineHub>`, or `MapFallbackToPage`.
- Do not add authentication or authorization middleware.

Acceptance Criteria:
- The string `EnsureDeleted` does not appear anywhere in the repository.
- `db.Database.Migrate()` runs unconditionally at startup.
- No `builder.Configuration.AddJsonFile` call remains.
- `UseHsts` and `UseHttpsRedirection` are both inside an `if (hasHttps)` guard.
- `UseForwardedHeaders()` is the first middleware registered.
- Running with `ASPNETCORE_URLS=http://+:8080` produces no "Failed to determine the https port for redirect" warning.

Validation:
`dotnet build` completes with 0 errors, and the app starts against a reachable SQL Server without dropping existing data.

Handoff:
Confirm `EnsureDeleted` is gone repository-wide and describe how `hasHttps` is computed.
