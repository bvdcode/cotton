FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# SDK + Node
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
RUN set -eux \
    && apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_25.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && apt-get purge -y --auto-remove gnupg \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

ARG BUILD_CONFIGURATION=Release
ARG BUILD_VERSION=dev
ENV APP_VERSION=$BUILD_VERSION
COPY ["Cotton.Server/Cotton.Server.csproj", "Cotton.Server/"]
COPY ["cotton.client/cotton.client.esproj", "cotton.client/"]
COPY ["Cotton.Shared/Cotton.Shared.csproj", "Cotton.Shared/"]
COPY ["Cotton.Storage/Cotton.Storage.csproj", "Cotton.Storage/"]
COPY ["Cotton.Topology/Cotton.Topology.csproj", "Cotton.Topology/"]
COPY ["Cotton.Database/Cotton.Database.csproj", "Cotton.Database/"]
COPY ["Cotton.Validators/Cotton.Validators.csproj", "Cotton.Validators/"]
COPY ["Cotton.Localization/Cotton.Localization.csproj", "Cotton.Localization/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "./Cotton.Server/Cotton.Server.csproj"

COPY . .

WORKDIR "/src/Cotton.Server"

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "./Cotton.Server.csproj" \
      -c $BUILD_CONFIGURATION \
      -o /app/publish \
      /p:UseAppHost=false \
      --no-restore

FROM base AS final
WORKDIR /app
ARG BUILD_VERSION=dev
ENV APP_VERSION=$BUILD_VERSION
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Cotton.Server.dll"]
