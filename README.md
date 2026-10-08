HopInLine is a collaborative queue management app. See a demo: https://www.youtube.com/watch?v=jDm82qOTa94

## Line Access Model

A line has two URLs with different privileges:

- **`/view-line/{id}`** — share this. Anyone with this URL can see the queue and join it. They cannot advance, reorder, remove, edit, or delete.
- **`/manage/{id}/{token}`** — keep this. Anyone with it has full control of the line.

**Start A Line** walks through four short steps: line info and type, your info, initial people, and invite links. Every field is optional, so clicking Next all the way through still gives you a working line. Settings can be changed later from **Edit**. Joining needs no info either: a blank name joins as a guest, and the last name you used is remembered in the browser.

**Important:** The manage link is saved in your browser's recent lines, but there are no accounts, so it can't be recovered if you lose it on that browser. Bookmark it or copy it from the manage page.

## Abuse Protection

- **Rate limits.** Every call that touches the server is limited:
  - Per IP on all HTTP requests (300/min), which covers page loads and new connections.
  - Per browser tab and per IP inside the app, for creating lines, any change, and loads or token checks.
  - Per line on joins (20/min). Managers adding people are exempt.

  Limits are in memory, so each server instance counts separately.
- **Idle-line cleanup.** Lines with no user activity for 30 days are deleted. Set `LineRetention:IdleDays` to change that. Background auto-advance doesn't count as activity.

## Running Locally

1. Set the database connection string in user secrets:
   ```
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your connection string>" --project HopInLine
   ```
   Example connection string (local SQL Server with `sa` user):
   ```
   Server=localhost;Database=hopinline;User=sa;Password=<your password>;Encrypt=True;TrustServerCertificate=True
   ```

2. Apply migrations:
   ```
   dotnet ef database update --project HopInLine
   ```

3. Run the app:
   ```
   dotnet run --project HopInLine
   ```

The app will be at `http://localhost:5000` (or the URL shown in the console).

## Running with Docker

1. Change to the HopInLine directory:
   ```
   cd HopInLine
   ```

2. Create `.env` from the example and set a strong password:
   ```
   cp .env.example .env
   ```
   Edit `.env` and replace `ChangeMe_StrongPassword123!` with a strong password of your own. The `.env` file is gitignored and must never be committed.

3. Build and run:
   ```
   docker compose up --build
   ```

The app will be at `http://localhost:8080` (note: the container runs as the non-root `app` user and listens on 8080, not 80).

## Running the Tests

Run all tests:
```
dotnet test
```

Tests use the EF Core **SQLite in-memory provider**, so no SQL Server instance is needed. Migrations target SQL Server and contain T-SQL that SQLite cannot run, so the test fixture uses `EnsureCreated` to build the schema directly rather than `Migrate`.

## How Auto-Advance Works

When a line has auto-advance enabled, the app stores a single `NextAdvanceAt` deadline. A background service sweeps once per second for lines past their deadline and advances them automatically.

Two consequences matter to users:

- **Advancement continues with no browser open.** The service runs in the background and does not require the page to be loaded or a browser to be open.
- **Pausing stores remaining time.** When you pause, the app clears the deadline but saves how much time was left. Resuming continues the countdown from that point rather than restarting it.
