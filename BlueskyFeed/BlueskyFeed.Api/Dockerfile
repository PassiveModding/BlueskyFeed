FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BlueskyFeed/BlueskyFeed.Api/BlueskyFeed.Api.csproj", "BlueskyFeed/BlueskyFeed.Api/"]
COPY ["BlueskyFeed/BlueskyFeed.ServiceDefaults/BlueskyFeed.ServiceDefaults.csproj", "BlueskyFeed/BlueskyFeed.ServiceDefaults/"]
COPY ["BlueskyFeed/BlueskyFeed.Auth/BlueskyFeed.Auth.csproj", "BlueskyFeed/BlueskyFeed.Auth/"]
COPY ["BlueskyFeed/BlueskyFeed.Common/BlueskyFeed.Common.csproj", "BlueskyFeed/BlueskyFeed.Common/"]
RUN dotnet restore "BlueskyFeed/BlueskyFeed.Api/BlueskyFeed.Api.csproj"
COPY . .
WORKDIR "/src/BlueskyFeed/BlueskyFeed.Api"
RUN dotnet build "BlueskyFeed.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "BlueskyFeed.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BlueskyFeed.Api.dll"]
