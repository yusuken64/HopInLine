Task ID:
INFRA-001

Model:
haiku

Title:
Upgrade target framework to net10.0 and bump packages

Purpose:
`net7.0` reached end of support in May 2024, so the app runs on an unpatched runtime and all packages are pinned to `7.0.8`. Doing this before any migration is authored means EF tooling and the generated migration code match the target framework.

Dependencies:
(none)

Consumes:
(nothing)

Produces:
Target framework contract consumed by INFRA-002 (container base images) and MIG-001 (EF tooling version).

Files:
- `HopInLine/HopInLine.csproj` (edit)

Requirements:
1. Change `<TargetFramework>net7.0</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>`.
2. Bump `Microsoft.EntityFrameworkCore.SqlServer` and `Microsoft.EntityFrameworkCore.Tools` from `7.0.8` to the latest 10.x. Keep the existing `<PrivateAssets>`/`<IncludeAssets>` metadata on the Tools reference.
3. Bump `Microsoft.AspNetCore.SignalR.Client` from `7.0.8` to the latest 10.x. **Keep this package** — `ViewLine.razor` still uses `HubConnection` at this point in the sequence. HUB-001 removes the reference once the last consumer is gone.
4. Delete the `<ItemGroup><Folder Include="Migrations\" /></ItemGroup>` block; the folder has real content, so the placeholder is meaningless.
5. Leave `Microsoft.VisualStudio.Azure.Containers.Tools.Targets` at its current version.
6. Keep `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<UserSecretsId>`, and `<DockerDefaultTargetOS>` unchanged.

Constraints:
- Do NOT add `<TreatWarningsAsErrors>` or `<WarningsAsErrors>`. WARN-001 owns that, after the 74 existing nullability warnings are fixed.
- Do NOT change any C# or Razor source file. If the framework bump surfaces a genuine compile ERROR, fix only the minimum needed to build and report exactly what you changed in Handoff.
- Keep the existing Blazor Server hosting model (`AddServerSideBlazor`, `MapBlazorHub`, `_Host.cshtml`). It is still supported on net10 — do NOT migrate to `MapRazorComponents`.
- Do not add or remove any other package.

Acceptance Criteria:
- `HopInLine.csproj` targets `net10.0`.
- All three `Microsoft.*` package references are on 10.x.
- `Microsoft.AspNetCore.SignalR.Client` is still present.
- No `<Folder Include="Migrations\" />` remains.
- `dotnet build -c Release` succeeds with 0 errors and no more than the 74 pre-existing warnings.

Validation:
`dotnet build -c Release` completes with 0 errors. Record the resulting warning count for comparison by later tasks.

Handoff:
State the exact package versions selected, the post-upgrade warning count, and any source file you were forced to touch to resolve a compile error.
