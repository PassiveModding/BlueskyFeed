FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BlueskyFeed/BlueskyFeed.Jetstream/BlueskyFeed.Jetstream.csproj", "BlueskyFeed/BlueskyFeed.Jetstream/"]
COPY ["BlueskyFeed/BlueskyFeed.ServiceDefaults/BlueskyFeed.ServiceDefaults.csproj", "BlueskyFeed/BlueskyFeed.ServiceDefaults/"]
COPY ["BlueskyFeed/BlueskyFeed.Common/BlueskyFeed.Common.csproj", "BlueskyFeed/BlueskyFeed.Common/"]
RUN dotnet restore "BlueskyFeed/BlueskyFeed.Jetstream/BlueskyFeed.Jetstream.csproj"
COPY . .
WORKDIR "/src/BlueskyFeed/BlueskyFeed.Jetstream"
RUN dotnet build "BlueskyFeed.Jetstream.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "BlueskyFeed.Jetstream.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BlueskyFeed.Jetstream.dll"]
