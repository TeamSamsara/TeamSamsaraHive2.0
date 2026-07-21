# ====================
# Team Samsara Hive v2 - Dockerfile
# Multi-stage build for .NET 8
# ====================

# ====================
# Stage 1: Build
# ====================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/TeamSamsara.Hive.Core/TeamSamsara.Hive.Core.csproj", "TeamSamsara.Hive.Core/"]
COPY ["src/TeamSamsara.Hive.Bot/TeamSamsara.Hive.Bot.csproj", "TeamSamsara.Hive.Bot/"]

# Restore dependencies
RUN dotnet restore "TeamSamsara.Hive.Bot/TeamSamsara.Hive.Bot.csproj"

# Copy source code
COPY src/TeamSamsara.Hive.Core/. TeamSamsara.Hive.Core/
COPY src/TeamSamsara.Hive.Bot/. TeamSamsara.Hive.Bot/

# Build project
WORKDIR "/src/TeamSamsara.Hive.Bot"
RUN dotnet build "TeamSamsara.Hive.Bot.csproj" -c Release -o /app/build

# ====================
# Stage 2: Publish
# ====================
FROM build AS publish
RUN dotnet publish "TeamSamsara.Hive.Bot.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ====================
# Stage 3: Runtime
# ====================
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Create data directory for database
RUN mkdir -p /app/data

# Copy published application
COPY --from=publish /app/publish .

# Copy appsettings.json (base template)
# Environment-specific configs will be mounted as volumes or set via environment variables
COPY src/TeamSamsara.Hive.Bot/appsettings.json .

# Set environment to Production by default
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_ENVIRONMENT=Production

# Run as non-root user for security
RUN useradd -m -u 1000 hiveuser && \
    chown -R hiveuser:hiveuser /app
USER hiveuser

# Health check (optional - checks if process is running)
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD pgrep -f "TeamSamsara.Hive.Bot" || exit 1

# Entry point
ENTRYPOINT ["dotnet", "TeamSamsara.Hive.Bot.dll"]