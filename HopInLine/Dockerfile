# Build on the runner's own architecture and cross-compile for the target, so the arm64 image
# doesn't need slow emulation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY ["HopInLine.csproj", "./"]
RUN dotnet restore "HopInLine.csproj" -a $TARGETARCH
COPY . .
RUN dotnet publish "HopInLine.csproj" -c Release -a $TARGETARCH -o /app/out --no-restore

# One-off SQL Server migration runner, used by docker-compose.sqlserver.yml (target: migrator)
FROM build AS migrator-build
RUN dotnet tool install --global dotnet-ef --version 10.0.*
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle --project HopInLine.csproj --context ApplicationDbContext \
    --configuration Release --self-contained -r linux-x64 -o /app/efbundle

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
COPY --from=migrator-build /app/efbundle /efbundle
USER app
ENTRYPOINT ["/efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
LABEL org.opencontainers.image.source="https://github.com/yusuken64/HopInLine"
LABEL org.opencontainers.image.description="HopInLine: collaborative queue management"
LABEL org.opencontainers.image.licenses="MIT"
# curl is only for the container health check
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data/keys \
    && chown -R app:app /data /home/app
WORKDIR /app
COPY --from=build /app/out ./
RUN chown -R app:app /app
# Works out of the box: a SQLite database and data-protection keys in /data.
# Mount a volume at /data to keep them. Override these to use SQL Server instead.
ENV Database__Provider=Sqlite \
    ConnectionStrings__DefaultConnection="Data Source=/data/hopinline.db" \
    DataProtection__KeysPath=/data/keys
VOLUME /data
EXPOSE 8080
USER app
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/healthz || exit 1
ENTRYPOINT ["dotnet", "HopInLine.dll"]
