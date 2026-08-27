Task ID:
INFRA-002

Model:
haiku

Title:
Modernize the Dockerfile and docker-compose for net10 and non-root port 8080

Purpose:
The Dockerfile does `COPY . ./` before `dotnet restore`, so every source edit invalidates the restore layer and re-downloads all packages. It pins net7 base images and forces `ASPNETCORE_URLS=http://+:80`. From net8 onward the `aspnet` base image runs as the non-root `app` user, which cannot bind port 80. That same user change also breaks the DataProtection volume, which is mounted at `/root/.aspnet/DataProtection-Keys` — a path `app` cannot write. Losing those keys invalidates every Blazor circuit on restart. The compose file also hardcodes the SA password twice.

Dependencies:
INFRA-001, INFRA-003

Consumes:
INFRA-001: `net10.0` target framework, which fixes the required base image tags
INFRA-003: `HopInLine/.env.example` documenting `SA_PASSWORD`, and `.dockerignore` excluding `.vs/` and `.git/`

Produces:
(nothing downstream consumes this)

Files:
- `HopInLine/Dockerfile` (edit)
- `HopInLine/docker-compose.yml` (edit)

Requirements:
These are the current file contents. The build context is `.`, and because `docker-compose.yml` lives in `HopInLine/`, the context **is** the `HopInLine/` directory — so `HopInLine.csproj` sits at the root of the context, with no directory prefix.

1. Replace the entire `Dockerfile` with:
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
   WORKDIR /src
   COPY ["HopInLine.csproj", "./"]
   RUN dotnet restore "HopInLine.csproj"
   COPY . .
   RUN dotnet publish "HopInLine.csproj" -c Release -o /app/out --no-restore

   FROM mcr.microsoft.com/dotnet/aspnet:10.0
   WORKDIR /app
   COPY --from=build /app/out ./
   EXPOSE 8080
   ENTRYPOINT ["dotnet", "HopInLine.dll"]
   ```
   Note what is deliberately absent: no `ENV ASPNETCORE_URLS`, no `EXPOSE 80`, and no `USER` instruction. The net10 `aspnet` image already defaults to `ASPNETCORE_HTTP_PORTS=8080` and to the non-root `app` user.
2. In `docker-compose.yml`, on the `web` service:
   - change `ports` from `"8080:80"` to `"8080:8080"`
   - delete the `ASPNETCORE_URLS=http://0.0.0.0:80` environment entry entirely
   - keep `ASPNETCORE_ENVIRONMENT=Docker` and `depends_on: - db` as they are
   - change the connection string entry to
     `- ConnectionStrings__DefaultConnection=Server=db;Database=hopinline;User=sa;Password=${SA_PASSWORD};Encrypt=True;TrustServerCertificate=True`
3. Fix the DataProtection volume path. Change
   `- dataprotection:/root/.aspnet/DataProtection-Keys`
   to
   `- dataprotection:/home/app/.aspnet/DataProtection-Keys`
   That is where the non-root `app` user's `HOME` points, so this is the path .NET will actually use. Keep the volume name `dataprotection` and its entry in the top-level `volumes:` block.
4. On the `db` service, change `SA_PASSWORD: "YourStrong!Passw0rd"` to `SA_PASSWORD: "${SA_PASSWORD}"`. Leave `ACCEPT_EULA: "Y"`, the image tag, `container_name`, the `1433` port mapping, and the `sqlvolume` mount unchanged.
5. Delete the top-level `version: "3.8"` line. It is obsolete in Compose v2 and emits a warning on every command.

Constraints:
- Do NOT create or commit a real `.env` file. Only `HopInLine/.env.example` is committed, and INFRA-003 already created it.
- Do NOT add a `USER` instruction, and do NOT change the port back to 80 to work around a bind permission error. If the app cannot bind 8080, the cause is a leftover `ASPNETCORE_URLS` override, not the user.
- Do NOT add a healthcheck or restart policy, and do not add, rename, or remove any service or volume.
- Do NOT edit `appsettings.json`, `.dockerignore`, or `.gitignore`; INFRA-003 owns those.
- Do not change the `ENTRYPOINT` assembly name or the `sqlvolume` mount path.

Acceptance Criteria:
- `docker build` completes; a second build after touching only a `.razor` file shows `CACHED` on the `dotnet restore` step.
- With `HopInLine/.env` present, `docker compose up` from `HopInLine/` starts both containers and the app answers at `http://localhost:8080`.
- `docker exec <web-container> whoami` prints `app`.
- `docker compose config` shows the SA password resolved from the environment and emits no `version` deprecation warning.
- Restarting the `web` container preserves DataProtection keys — the mounted directory is non-empty and owned by `app`.
- Searching the tracked file set for `YourStrong!Passw0rd` returns zero hits.

Validation:
`dotnet build` completes with 0 errors, and `docker build` completes successfully.

Handoff:
Confirm the restore layer is cached on an incremental build, that `whoami` returned `app`, and that the DataProtection directory is writable by that user.
