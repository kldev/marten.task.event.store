# syntax=docker/dockerfile:1
# Two images from one build: `--target app` (the Blazor host) and `--target migrator` (schema + seed, run once before
# the app; ADR-0002). Tailwind is not run here: wwwroot/css/app.css is committed (SkipTailwind=true).

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/Halcyon.Domain/Halcyon.Domain.csproj src/Halcyon.Domain/
COPY src/Halcyon.Database/Halcyon.Database.csproj src/Halcyon.Database/
COPY src/Halcyon.App/Halcyon.App.csproj src/Halcyon.App/
COPY src/Halcyon.DbMigrator/Halcyon.DbMigrator.csproj src/Halcyon.DbMigrator/
RUN dotnet restore src/Halcyon.App/Halcyon.App.csproj && dotnet restore src/Halcyon.DbMigrator/Halcyon.DbMigrator.csproj
COPY src/ src/
# Wolverine's handler code is generated here and compiled into the image (ADR-0016), so the app loads it by name
# (TypeLoadMode.Static) instead of compiling it with Roslyn on first use - those assemblies are never unloaded, and
# Ambit's API grew into an OOM kill. `codegen write` opens no connection: the address only satisfies the configuration.
# -p:WolverineStaticCode=true then switches Static on and leaves Roslyn out (Directory.Build.props); it changes the
# package references, so that publish restores again (from the cache).
RUN ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Postgres="Host=codegen.invalid;Database=codegen;Username=codegen;Password=codegen" \
    dotnet run --project src/Halcyon.App/Halcyon.App.csproj --no-restore --no-launch-profile -p:SkipTailwind=true \
    -- codegen write
# No .git in the context: the commit shown in the app comes from here (up.sh passes `git rev-parse HEAD`).
ARG GIT_SHA=""
RUN dotnet publish src/Halcyon.App/Halcyon.App.csproj -c Release -o /out/app -p:SkipTailwind=true \
    -p:WolverineStaticCode=true -p:SourceRevisionId="${GIT_SHA}" \
 && dotnet publish src/Halcyon.DbMigrator/Halcyon.DbMigrator.csproj -c Release -o /out/migrator --no-restore

FROM mcr.microsoft.com/dotnet/runtime:11.0 AS migrator
WORKDIR /app
COPY --from=build /out/migrator .
USER $APP_UID
ENTRYPOINT ["dotnet", "Halcyon.DbMigrator.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS app
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /out/app .
USER $APP_UID
EXPOSE 8080
# No curl in the image: bash's /dev/tcp asks /health and expects 200.
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=3 \
  CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && head -n1 <&3 | grep -q ' 200 '"]
ENTRYPOINT ["dotnet", "Halcyon.App.dll"]
