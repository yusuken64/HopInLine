Task ID:
TEST-001

Model:
haiku

Title:
Create the HopInLine.Tests project with a SQLite in-memory fixture

Purpose:
There are zero tests. `azure-pipelines.yml` runs `dotnet test` against `**/*Tests/*.csproj`, which matches nothing, so the pipeline reports a green no-op. This task builds only the harness — the fixture and data builders every later test task depends on. It writes no behavioral tests itself, which is what lets four test-writing tasks run in parallel afterwards.

Dependencies:
INFRA-001, MODEL-001

Consumes:
INFRA-001: `net10.0` target framework, so the test project matches
MODEL-001: the `Line` and `Participant` shapes the builders construct

Produces:
The test harness consumed by TEST-REPO-001, TEST-CONC-001, TEST-AUTH-001, FMT-001 and INT-001:
- `HopInLine.Tests.SqliteDbFixture` — disposable, gives a migrated `IDbContextFactory<ApplicationDbContext>`
- `HopInLine.Tests.TestData` — `TestData.Line(...)` and `TestData.Participant(...)` builders

Files:
- `HopInLine.Tests/HopInLine.Tests.csproj` (new)
- `HopInLine.Tests/SqliteDbFixture.cs` (new)
- `HopInLine.Tests/TestData.cs` (new)
- `HopInLine.Tests/SmokeTests.cs` (new)
- `HopInLine.sln` (edit — add the project)

Requirements:
1. Create `HopInLine.Tests/HopInLine.Tests.csproj` targeting `net10.0`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<IsPackable>false</IsPackable>`. Package references: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Microsoft.EntityFrameworkCore.Sqlite` (10.x). Project reference to `..\HopInLine\HopInLine.csproj`.
2. Add the project to `HopInLine.sln` with `dotnet sln add HopInLine.Tests/HopInLine.Tests.csproj`.
3. `SqliteDbFixture` — use the **SQLite provider with an in-memory connection string**, not the EF InMemory provider, because the InMemory provider does not enforce relational constraints or produce realistic ordering:
   ```csharp
   public sealed class SqliteDbFixture : IDisposable
   {
       private readonly SqliteConnection _connection;
       public IDbContextFactory<ApplicationDbContext> Factory { get; }

       public SqliteDbFixture()
       {
           _connection = new SqliteConnection("DataSource=:memory:");
           _connection.Open();            // MUST stay open — closing it destroys the database
           // build DbContextOptions with UseSqlite(_connection)
           // create the schema, then expose a factory over the same options
       }

       public ApplicationDbContext CreateContext() => ...;
       public void Dispose() => _connection.Dispose();
   }
   ```
   Implement `IDbContextFactory<ApplicationDbContext>` with a tiny private class that closes over the options, so tests can hand the fixture straight to `SQLLineRepository`.
4. Create the schema with `context.Database.EnsureCreated()`, not `Migrate()`. The migrations target SQL Server and contain raw T-SQL that SQLite cannot run. Add a comment saying exactly that, so nobody "fixes" it later.
5. `TestData` — static builders with sensible defaults and optional overrides:
   - `TestData.Line(string id = "TESTLINE", bool autoAdvanceLine = false, bool autoReAdd = false, bool autoRestartTimerOnAdvance = true, TimeSpan? interval = null, string manageToken = "test-token")` — returns a valid `Line` with `NextPosition = 1`, empty participant list, `IsPaused = false`.
   - `TestData.Participant(string name, int position, bool removed = false)` — returns a valid `Participant` with a fixed colour so assertions are deterministic.
   Do not use `DateTime.Now` anywhere; use `DateTime.UtcNow` or an explicit fixed date.
6. `SmokeTests.cs` — exactly two tests proving the harness works, and nothing about app behavior:
   - `Fixture_CreatesSchema` — insert a `Line` via the factory, read it back in a *second* context from the same fixture, assert the id round-trips.
   - `Fixture_IsIsolatedPerInstance` — two separate `SqliteDbFixture` instances do not see each other's data.

Constraints:
- Write **no** behavioral tests about positions, pausing, advancing, concurrency, or authorization. Those belong to TEST-REPO-001, TEST-CONC-001, TEST-AUTH-001 and FMT-001; adding them here would collide with those tasks.
- Do NOT edit any file under `HopInLine/`.
- Do NOT reference `Microsoft.EntityFrameworkCore.InMemory`.
- Do not add Moq, FluentAssertions, or any other package beyond the five listed.
- The `SqliteConnection` must be opened in the constructor and disposed only in `Dispose`.

Acceptance Criteria:
- `dotnet test` discovers and passes exactly 2 tests.
- `dotnet sln list` includes `HopInLine.Tests`.
- The fixture can be passed directly where `IDbContextFactory<ApplicationDbContext>` is expected.
- Deleting the `_connection.Open()` line makes the smoke tests fail — confirming the connection is genuinely load-bearing.

Validation:
`dotnet build` completes with 0 errors and `dotnet test` reports 2 passed, 0 failed.

Handoff:
Paste the full `SqliteDbFixture` and `TestData` source. Every later test task copies from this text, so it must be exact. State the package versions used.
