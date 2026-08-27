Task ID:
INFRA-003

Model:
haiku

Title:
Repository hygiene, secret removal, and CI pipeline fixes

Purpose:
Three unrelated but individually trivial problems: the SQL Server SA password is committed in three files; Visual Studio state (`.suo`, `DocumentLayout.json`) and Copilot index databases under `.vs/` are tracked in git; and `azure-pipelines.yml` triggers on `master` while the repository's default branch is `main`, so CI has never run — and if it did, it installs SDK 6.x, which cannot build this project.

Dependencies:
(none)

Consumes:
(nothing)

Produces:
`HopInLine/.env.example` documenting the `SA_PASSWORD` variable that INFRA-002's `docker-compose.yml` reads.

Files:
- `.gitignore` (edit)
- `HopInLine/.dockerignore` (edit)
- `HopInLine/.env.example` (new)
- `HopInLine/appsettings.json` (edit)
- `HopInLine/appsettings.Development.json` (edit)
- `azure-pipelines.yml` (edit)

Requirements:
1. `.gitignore` — append `.vs/`, `.env`, and `*.user`. Then untrack the already-committed Visual Studio state:
   `git rm -r --cached .vs`
   (This removes it from the index only; the files stay on disk.)
2. `HopInLine/.dockerignore` — append `.vs/` and `.git/` so neither is copied into the build context.
3. Create `HopInLine/.env.example` — **in the `HopInLine/` directory, not the repository root.** `docker-compose.yml` lives in `HopInLine/`, and Compose only auto-loads `.env` from the directory containing the compose file.
   ```
   # Copy to .env in this directory and set a strong password before running docker compose.
   SA_PASSWORD=ChangeMe_StrongPassword123!
   ```
4. `HopInLine/appsettings.json` — replace the `DefaultConnection` value with a placeholder containing no password:
   `"Server=db;Database=hopinline;User=sa;Password=;Encrypt=True;TrustServerCertificate=True"`
   Leave `ApiBaseUrl`, `Logging`, and `AllowedHosts` untouched.
5. `HopInLine/appsettings.Development.json` — remove the `ConnectionStrings` section entirely. Local development supplies it via user secrets (the csproj already has a `UserSecretsId`). Leave `DetailedErrors` and `Logging` in place.
6. `azure-pipelines.yml` — change `trigger: - master` to `trigger: - main`, and change the `UseDotNet@2` input `version: '6.x.x'` to `version: '10.x'`. Leave the restore/build/test/publish steps as they are.

Constraints:
- Do NOT edit `docker-compose.yml`; INFRA-002 owns it.
- Do NOT commit a real `.env` file — only `.env.example`.
- Do not delete any file from disk with `git rm` without `--cached`.
- Do not change `AllowedHosts` or add authentication settings.

Acceptance Criteria:
- `git status` shows the `.vs/` files as deleted-from-index, and `git check-ignore .vs/x` reports them ignored.
- Searching the tracked file set for `YourStrong!Passw0rd` returns hits only in `docker-compose.yml` (INFRA-002 clears that one).
- `HopInLine/.env.example` exists, sits beside `docker-compose.yml`, and `.env` is gitignored.
- `azure-pipelines.yml` triggers on `main` and requests SDK `10.x`.
- The app still starts in Development when the connection string is supplied via user secrets.

Validation:
`dotnet build` completes with 0 errors. Confirm `git ls-files .vs` returns nothing.

Handoff:
Confirm which files still contain the SA password, and state the user-secrets command a developer must now run to set their local connection string.
