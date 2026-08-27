FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["HopInLine.csproj", "./"]
RUN dotnet restore "HopInLine.csproj"
COPY . .
RUN dotnet publish "HopInLine.csproj" -c Release -o /app/out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /home/app
WORKDIR /app
COPY --from=build /app/out ./
RUN chown -R app:app /app
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "HopInLine.dll"]
