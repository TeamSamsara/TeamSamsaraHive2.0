# ====================
# Build stage
# ====================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore "src/TeamSamsara.Hive.Bot/TeamSamsara.Hive.Bot.csproj"

COPY src/TeamSamsara.Hive.Core/. TeamSamsara.Hive.Core/
COPY src/TeamSamsara.Hive.Bot/. TeamSamsara.Hive.Bot/

WORKDIR /src/TeamSamsara.Hive.Bot
RUN dotnet build "TeamSamsara.Hive.Bot.csproj" -c Release -o /app/build

# ====================
# Publish stage
# ====================
FROM build AS publish
RUN dotnet publish "TeamSamsara.Hive.Bot.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ====================
# Runtime stage
# ====================
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Program.cs hardcodes DataDirectory = "/data" (a fixed, platform-agnostic
# path - see its comment). This is NOT /app/data. Whatever platform runs
# this container (Render, Railway, etc.) must mount its persistent volume
# at exactly /data for the SQLite files to survive restarts/redeploys.
RUN mkdir -p /data

COPY --from=publish /app/publish .
COPY src/TeamSamsara.Hive.Bot/appsettings.json .

# Non-root user - must own both /app (the app files) and /data (the SQLite
# directory the app writes to at startup), or Directory.CreateDirectory("/data")
# in Program.cs throws UnauthorizedAccessException.
RUN useradd -m -u 1000 hiveuser && \
    chown -R hiveuser:hiveuser /app /data

USER hiveuser

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_ENVIRONMENT=Production
ENV TZ=UTC

ENTRYPOINT ["dotnet", "TeamSamsara.Hive.Bot.dll"]