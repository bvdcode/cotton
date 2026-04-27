# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ARG STL_THUMB_VERSION=0.5.0

RUN set -eux \
    && apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl gnupg \
    && install -d -m 0755 /etc/apt/keyrings \
    && curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc \
        | gpg --dearmor -o /etc/apt/keyrings/postgresql.gpg \
    && chmod a+r /etc/apt/keyrings/postgresql.gpg \
    && . /etc/os-release \
    && echo "deb [signed-by=/etc/apt/keyrings/postgresql.gpg] https://apt.postgresql.org/pub/repos/apt ${VERSION_CODENAME}-pgdg main" \
        > /etc/apt/sources.list.d/pgdg.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends postgresql-client-18 libgl1 libegl1 libosmesa6 libfontconfig1 \
    && case "$(dpkg --print-architecture)" in amd64|arm64|armhf) STL_THUMB_DEB_ARCH="$(dpkg --print-architecture)" ;; *) echo "Unsupported architecture for stl-thumb .deb"; exit 1 ;; esac \
    && STL_THUMB_DEB_URL="https://github.com/unlimitedbacon/stl-thumb/releases/download/v${STL_THUMB_VERSION}/stl-thumb_${STL_THUMB_VERSION}_${STL_THUMB_DEB_ARCH}.deb" \
    && curl -fsSL "$STL_THUMB_DEB_URL" -o /tmp/stl-thumb.deb \
    && apt-get install -y --no-install-recommends /tmp/stl-thumb.deb \
    && test -f /usr/lib/libstl_thumb.so \
    && ldconfig \
    && rm -f /tmp/stl-thumb.deb \
    && apt-get purge -y --auto-remove curl gnupg \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 8080


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS with-node
RUN set -eux \
	&& apt-get update \
	&& apt-get install -y --no-install-recommends curl ca-certificates gnupg \
	&& curl -fsSL https://deb.nodesource.com/setup_25.x | bash - \
	&& apt-get install -y --no-install-recommends nodejs \
	&& apt-get purge -y --auto-remove gnupg \
	&& rm -rf /var/lib/apt/lists/*

FROM with-node AS build
ARG BUILD_CONFIGURATION=Release
ARG BUILD_VERSION=dev
WORKDIR /src
ENV APP_VERSION=$BUILD_VERSION
COPY ["Cotton.Server/Cotton.Server.csproj", "Cotton.Server/"]
COPY ["cotton.client/cotton.client.esproj", "cotton.client/"]
COPY ["Cotton.Shared/Cotton.csproj", "Cotton.Shared/"]
COPY ["Cotton.Storage/Cotton.Storage.csproj", "Cotton.Storage/"]
COPY ["Cotton.Topology/Cotton.Topology.csproj", "Cotton.Topology/"]
COPY ["Cotton.Database/Cotton.Database.csproj", "Cotton.Database/"]
COPY ["Cotton.Validators/Cotton.Validators.csproj", "Cotton.Validators/"]
COPY ["Cotton.Localization/Cotton.Localization.csproj", "Cotton.Localization/"]
RUN dotnet restore "./Cotton.Server/Cotton.Server.csproj"
COPY . .
WORKDIR "/src/Cotton.Server"
RUN dotnet build "./Cotton.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG BUILD_VERSION=dev
ENV APP_VERSION=$BUILD_VERSION
RUN dotnet publish "./Cotton.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
ARG BUILD_VERSION=dev
ENV APP_VERSION=$BUILD_VERSION
COPY --from=publish /app/publish .
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
ENTRYPOINT ["dotnet", "Cotton.Server.dll"]