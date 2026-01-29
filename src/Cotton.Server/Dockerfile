# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


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
COPY ["Cotton.Server/Cotton.WebDav.csproj", "Cotton.WebDav/"]
COPY ["cotton.client/cotton.client.esproj", "cotton.client/"]
COPY ["Cotton.Shared/Cotton.Shared.csproj", "Cotton.Shared/"]
COPY ["Cotton.Storage/Cotton.Storage.csproj", "Cotton.Storage/"]
COPY ["Cotton.Topology/Cotton.Topology.csproj", "Cotton.Topology/"]
COPY ["Cotton.Database/Cotton.Database.csproj", "Cotton.Database/"]
COPY ["Cotton.Validators/Cotton.Validators.csproj", "Cotton.Validators/"]
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