# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS with-node
RUN set -eux \
	&& apt-get update \
	&& apt-get install -y --no-install-recommends curl ca-certificates gnupg \
	&& curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
	&& apt-get install -y --no-install-recommends nodejs \
	&& apt-get purge -y --auto-remove gnupg \
	&& rm -rf /var/lib/apt/lists/*


FROM with-node AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Cotton.Server/Cotton.Server.csproj", "Cotton.Server/"]
COPY ["cotton.client/cotton.client.esproj", "cotton.client/"]
RUN dotnet restore "./Cotton.Server/Cotton.Server.csproj"
COPY . .
WORKDIR "/src/Cotton.Server"
RUN dotnet build "./Cotton.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Cotton.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cotton.Server.dll"]
