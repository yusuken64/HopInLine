Task ID:
README-001

Model:
haiku

Title:
Document the join/manage link split, local setup, and how to run the tests

Purpose:
The access model changed in a way that is not discoverable from the UI: a line now has two URLs with different privileges, and losing the manage link is unrecoverable by design. The connection string also moved out of `appsettings.json` into user secrets and a compose `.env`, so a fresh clone no longer runs without a documented setup step.

Dependencies:
START-001, INFRA-002, INFRA-003, TEST-001

Consumes:
START-001: the `/view-line/{id}` and `/manage/{id}/{token}` URL shapes
INFRA-002: the `docker compose` workflow on port 8080
INFRA-003: `HopInLine/.env.example` and the user-secrets requirement
TEST-001: the `HopInLine.Tests` project

Produces:
(nothing downstream consumes this)

Files:
- `README.md` (edit)

Requirements:
1. Add a **Line access model** section explaining the two links plainly:
   - `/view-line/{id}` — share this. Anyone with it can see the queue and join it. They cannot advance, reorder, remove, edit, or delete.
   - `/manage/{id}/{token}` — keep this. Anyone with it has full control of the line.
   State explicitly that the manage token is shown **once**, when the line is created, and cannot be recovered — there are no accounts, so there is nothing to authenticate a recovery against. Recommend copying it somewhere before leaving the page.
2. Add a **Running locally** section:
   - `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your connection string>" --project HopInLine` — required, because the password was removed from `appsettings.json` and the `ConnectionStrings` section was removed from `appsettings.Development.json`.
   - `dotnet ef database update --project HopInLine` to apply migrations.
   - `dotnet run --project HopInLine`.
3. Add a **Running with Docker** section:
   - `cd HopInLine`
   - `cp .env.example .env` and set a strong `SA_PASSWORD` — note that `.env` is gitignored and must never be committed.
   - `docker compose up --build`
   - the app is at `http://localhost:8080` (not 80 — the container runs as the non-root `app` user and listens on 8080).
4. Add a **Running the tests** section: `dotnet test`. Mention that the tests use the EF Core **SQLite** in-memory provider, so no SQL Server instance is needed, and that the migrations are SQL-Server-specific — which is why the test fixture uses `EnsureCreated` rather than `Migrate`.
5. Add a short **How auto-advance works** section: a line stores a single `NextAdvanceAt` deadline; a background service sweeps once a second for due lines and advances them. Note the two consequences that matter to a user: advancement continues with **no browser open**, and pausing clears the deadline while storing the remaining time, so resuming continues rather than restarting.
6. Keep whatever the README already says about what the app is for. Add sections; do not rewrite the existing description.

Constraints:
- Do NOT include a real password, connection string, or manage token anywhere in the README. Use `<placeholder>` form.
- Do NOT document a manage-link recovery procedure. None exists, and describing one would be wrong.
- Do NOT document `/list-lines`; LIST-001 deleted it. If the README currently mentions it, remove that mention.
- Do NOT document port 80 or `ASPNETCORE_URLS`.
- Do not add badges, a licence, a contributing guide, or a changelog.
- Do not describe planned or aspirational features — document only what the code does after the preceding tasks.

Acceptance Criteria:
- Following the "Running locally" steps on a clean clone gets the app running, with no step omitted.
- Following the Docker steps reaches the app on `localhost:8080`.
- The README contains no password, no connection string with credentials, and no reference to `/list-lines` or port 80.
- The two-link model is described including the "shown once, unrecoverable" warning.
- `dotnet test` is documented along with why no SQL Server is needed.

Validation:
`dotnet build` completes with 0 errors. Verify each documented command runs as written on a clean clone.

Handoff:
List the sections added and confirm you executed each documented command successfully.
