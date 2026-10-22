# Build context is the repo root (see docker-compose.yml) so this can reference
# TenantSubscriptionApi.sln and the project folder by their real relative paths.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy just the project file first so `dotnet restore` is cached across rebuilds
# unless the dependency list itself changes — only later COPY layers bust on
# every source edit.
COPY src/TenantSubscriptionApi/TenantSubscriptionApi.csproj src/TenantSubscriptionApi/
RUN dotnet restore src/TenantSubscriptionApi/TenantSubscriptionApi.csproj

COPY src/TenantSubscriptionApi/ src/TenantSubscriptionApi/
WORKDIR /src/src/TenantSubscriptionApi
RUN dotnet publish -c Release -o /app/publish --no-restore

# No migration/seed step here — Program.cs already applies PublicDbContext
# migrations, seeds the Free plan + platform admin, and runs
# TenantMigrationPropagator as a hosted service on every startup. No separate
# entrypoint script is needed the way the reference implementation's hand-rolled
# migration scripts required one.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TenantSubscriptionApi.dll"]
