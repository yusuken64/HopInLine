Task ID:
WARN-001

Model:
haiku

Title:
Fix the remaining nullability warnings and enforce them as errors

Purpose:
The build emits 74 warnings, almost all nullability, which is enough noise that a real one is invisible — CS8073 in `EditLine` and CS8602 in `ViewLine` both flagged genuine bugs and both went unnoticed for the project's whole history. Once the count is zero, turning nullable warnings into errors means the next one cannot be ignored. This runs late deliberately: fixing warnings in files that other tasks are rewriting would just create conflicts.

Dependencies:
PROG-002, VIEW-001, EDIT-001, START-001, LIST-001, SERVICE-002, REPO-002, DEADCODE-001, CARD-001, UX-001

Consumes:
All preceding code tasks. Every file that is going to be rewritten has been rewritten, and several warnings disappeared with the deleted files.

Produces:
`<WarningsAsErrors>Nullable</WarningsAsErrors>` — the guard INT-001 verifies.

Files:
- `HopInLine/HopInLine.csproj` (edit)
- Any remaining file with a nullability warning (edit)

Requirements:
1. Run `dotnet build` and capture the full warning list. Expect it to be well below 74 — several warning-heavy files were deleted by DEADCODE-001, LIST-001, SERVICE-001, and HUB-001.
2. Fix every remaining `CS86xx` and `CS8073` warning. Choose the fix by category, in this order of preference:
   - the value genuinely cannot be null at that point → restructure so the compiler can see it (an early return, a local variable, a pattern match). Prefer this.
   - the value genuinely can be null → handle it: a null check, `?.`, or `??` with a sensible default.
   - the declaration is wrong → change the declared type to `T?` and handle the nullable case at each use.
3. Do **not** silence warnings. Specifically: no `!` null-forgiving operator, no `#pragma warning disable`, no `[SuppressMessage]`, no `#nullable disable`. Each of those converts a compiler-detected bug into a runtime `NullReferenceException`. If a warning looks genuinely unfixable without one, leave it and report it in Handoff rather than suppressing it.
4. For a Razor `[Parameter]` that the framework always supplies, declare it `= default!` **only** in a property initializer, which is the conventional and readable form for a required parameter — this is the single permitted use of `!` in this task, and only on `[Parameter]`/`[Inject]` members. Every other `!` is prohibited.
5. Fix CS0168 (declared-and-unused exception variable) and CS0169 (unused private field) wherever they remain. An `unused ex` in a catch block means an exception is being swallowed with no logging — add the log rather than deleting the variable.
6. Once the count is zero, add to `HopInLine.csproj`:
   `<WarningsAsErrors>Nullable</WarningsAsErrors>`
   Use `WarningsAsErrors` scoped to `Nullable`, **not** `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. The broad form promotes unrelated warnings — including package deprecation notices from a future NuGet update — and would break the build for reasons that have nothing to do with code quality.
7. Add the same property to `HopInLine.Tests/HopInLine.Tests.csproj` only if the test project builds with zero nullability warnings. If it does not, fix them first; the test project is small.

Constraints:
- Do NOT change any behavior. This task is warning-driven only. If fixing a warning appears to require a behavioral change, the warning has found a real bug — report it in Handoff and make the minimum correct fix, stating exactly what you changed.
- Do NOT use `!`, `#pragma warning disable`, `#nullable disable`, or `[SuppressMessage]` except for the `[Parameter]`/`[Inject]` initializer case in requirement 4.
- Do NOT set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- Do NOT delete a file to make a warning go away.
- Do NOT reformat, reorder members, or make unrelated tidying edits. A diff full of formatting noise makes the real changes unreviewable.
- Do not change any public signature.

Acceptance Criteria:
- `dotnet build` reports **0 warnings and 0 errors** for both projects.
- `dotnet test` still passes with the same test count as before this task — proof no behavior changed.
- `HopInLine.csproj` contains `<WarningsAsErrors>Nullable</WarningsAsErrors>` and does not contain `TreatWarningsAsErrors`.
- Introducing a deliberate `string x = null;` anywhere now fails the build rather than warning.
- A repository-wide search finds no `#pragma warning disable`, no `#nullable disable`, and no `!` null-forgiving operator outside `[Parameter]`/`[Inject]` initializers.

Validation:
`dotnet build` completes with 0 errors and 0 warnings, and `dotnet test` reports 0 failures.

Handoff:
State the warning count before and after, and list every warning that turned out to flag a real bug along with what you changed. Name any warning you could not fix without a suppression.
