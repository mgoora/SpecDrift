# SpecDrift never builds or executes the project it analyses — it only needs its own
# runtime. The build stage exists solely to publish the CLI itself.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final

WORKDIR /app
COPY --from=build /app .

# Run as the unprivileged `app` user that every .NET 8+ image ships with, rather
# than creating one: .NET 10 images are Ubuntu-based and UID/GID 1000 is already
# taken there. SPECDRIFT_API_KEY (if the caller passes one) is supplied at
# `docker run` time via -e, never baked into an image layer.
USER $APP_UID
ENTRYPOINT ["dotnet", "SpecDrift.Cli.dll"]
