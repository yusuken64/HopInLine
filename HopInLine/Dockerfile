FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["HopInLine.csproj", "./"]
RUN dotnet restore "HopInLine.csproj"
COPY . .
RUN dotnet publish "HopInLine.csproj" -c Release -o /app/out --no-restore

# One-off migration runner, used by the compose "migrate" service (target: migrator)
FROM build AS migrator-build
RUN dotnet tool install --global dotnet-ef --version 10.0.*
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle --project HopInLine.csproj --configuration Release \
    --self-contained -r linux-x64 -o /app/efbundle

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
COPY --from=migrator-build /app/efbundle /efbundle
USER app
ENTRYPOINT ["/efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# curl is only for the container health check
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /home/app
WORKDIR /app
COPY --from=build /app/out ./
RUN chown -R app:app /app
EXPOSE 8080
USER app
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/healthz || exit 1
ENTRYPOINT ["dotnet", "HopInLine.dll"]
