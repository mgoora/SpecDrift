# SpecDrift never builds or executes the project it analyses — it only needs its own
# runtime. The build stage exists solely to publish the CLI itself.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore from just the project files first, so this layer is cached across
# source-only changes.
COPY src/SpecDrift.Core/SpecDrift.Core.csproj src/SpecDrift.Core/
COPY src/SpecDrift.Cli/SpecDrift.Cli.csproj src/SpecDrift.Cli/
RUN dotnet restore src/SpecDrift.Cli/SpecDrift.Cli.csproj

COPY src/ src/
RUN dotnet publish src/SpecDrift.Cli/SpecDrift.Cli.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final

# A dedicated, unprivileged user — SPECDRIFT_API_KEY (if the caller passes one) is
# supplied at `docker run` time via -e, never baked into an image layer.
RUN groupadd --gid 1000 specdrift \
 && useradd --uid 1000 --gid specdrift --no-create-home --shell /usr/sbin/nologin specdrift

WORKDIR /app
COPY --from=build /app .

USER specdrift
ENTRYPOINT ["dotnet", "SpecDrift.Cli.dll"]
