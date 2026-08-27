Task ID:
MODEL-001

Model:
haiku

Title:
Additive domain model + EF mapping changes (concurrency token, advance deadline, manage token, column limits)

Purpose:
Establish the data contract every other task builds on. All changes are ADDITIVE — after this task the project must still compile with zero new errors, because no existing call site is allowed to change.

Dependencies:
(none)

Consumes:
(nothing)

Produces:
The domain model contract consumed by CONTRACT-001, MIG-001, REPO-001, SCHED-001, TEST-001:
- `Line.NextAdvanceAt` (DateTime?) — authoritative advance deadline
- `Line.ManageToken` (string) — per-line secret
- `Line.Version` (Guid) — concurrency token
- `LineDto.NextAdvanceAt`, `LineDto.ManageToken`

Files:
- `HopInLine/Data/Line/Line.cs` (edit)
- `HopInLine/Data/Line/ApplicationDbContext.cs` (edit)

Requirements:
1. On the `Line` entity add:
   - `public DateTime? NextAdvanceAt { get; set; }`
   - `public string ManageToken { get; set; } = string.Empty;`
   - `public Guid Version { get; set; }`
2. Change `public TimeSpan? UnpauseRemaining { get; internal set; }` to a **public** setter on both `Line` and `LineDto`. The `internal set` currently prevents System.Text.Json from populating it.
3. On `LineDto` add `NextAdvanceAt` (DateTime?) and `ManageToken` (string). In `LineDto.FromLine`, map both from the source line, and change `LastUpdated = DateTime.Now` to `LastUpdated = line.LastUpdated`. `DateTime.Now` is the only non-UTC timestamp in the codebase and must go.
4. In `ApplicationDbContext.OnModelCreating`, add for `Line`:
   - `.Property(l => l.Version).IsConcurrencyToken()`
   - `.Property(l => l.AutoAdvanceInterval).HasConversion(v => (int)v.TotalSeconds, v => TimeSpan.FromSeconds(v))`
   - `.Property(l => l.UnpauseRemaining).HasConversion(v => v.HasValue ? (int?)v.Value.TotalSeconds : null, v => v.HasValue ? TimeSpan.FromSeconds(v.Value) : null)`
   - `.HasIndex(l => l.NextAdvanceAt)`
   - `.Property(l => l.Name).HasMaxLength(100)`, `Description` 500, `ManageToken` 64
   For `Participant`: `Name` 60, `Description` 200, `Color` 7.
5. Override `SaveChangesAsync` (and `SaveChanges`, delegating) on `ApplicationDbContext`. For every `Line` entry in state `Added` or `Modified`: set `Version = Guid.NewGuid()` and `LastUpdated = DateTime.UtcNow`. This is what makes the concurrency token work and finally makes `LastUpdated` a column something writes.

Constraints:
- Do NOT change the C# type of `AutoAdvanceInterval` or `UnpauseRemaining`. They stay `TimeSpan`/`TimeSpan?`. Only the EF mapping changes. This is deliberate: it lifts the 24-hour SQL `time` ceiling without touching LineService, SQLLineRepository, ViewLine, EditLine, or StartLine.
- Do NOT remove or rename any existing property, including `CountDownStart`.
- Do NOT create the EF migration. MIG-001 owns that.
- Do NOT add validation attributes that would break existing object construction.

Acceptance Criteria:
- `Line` exposes `NextAdvanceAt`, `ManageToken`, `Version`; `UnpauseRemaining` has a public setter on both `Line` and `LineDto`.
- `LineDto.FromLine` copies `LastUpdated` from the entity; no `DateTime.Now` remains anywhere in `Line.cs`.
- All six string properties have explicit max lengths configured.
- Saving a modified `Line` assigns a new `Version` and a UTC `LastUpdated`.
- No existing file outside the two listed had to be edited.

Validation:
`dotnet build` completes with 0 errors. Warning count must not increase.

Handoff:
State the final names and types of the three new `Line` properties, and confirm that `AutoAdvanceInterval` remains a `TimeSpan` in C# while mapping to an integer seconds column.
