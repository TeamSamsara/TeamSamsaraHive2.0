# Team Samsara Hive v2

Complete rebuild of the Team Samsara Hive Discord bot with Analytics, Notifications, and Ticket systems.

## Project Structure

```
TeamSamsara.Hive.v2/
├── src/
│   ├── TeamSamsara.Hive.Core/       # Shared infrastructure
│   └── TeamSamsara.Hive.Bot/        # Main bot application
└── docs/                            # Documentation
```

## Systems

### Analytics System
- Twitch stream monitoring and analytics
- Discord server activity tracking
- Daily snapshots and reporting

### Notification System  
- Stream live/offline notifications
- Ticket notifications
- Analytics reports

### Ticket System
- Priority-based support tickets
- Auto-escalation
- Abuse prevention
- User feedback

## Configuration

Configuration files are environment-specific:
- `appsettings.json` - Base configuration (committed)
- `appsettings.Development.json` - Development config (NOT committed)
- `appsettings.Staging.json` - Staging config (NOT committed)
- `appsettings.Production.json` - Production config (NOT committed)

## Development

1. Copy `appsettings.json` to `appsettings.Development.json`
2. Fill in your credentials
3. Run: `dotnet run --project src/TeamSamsara.Hive.Bot`

## Architecture

Built with clean architecture principles:
- **Core Layer**: Base classes, EventBus, utilities
- **Bot Layer**: Business logic, orchestrators, services

### Key Patterns
- One orchestrator per system
- Services do ONE thing
- No service-to-service calls
- EventBus for cross-system communication

## Tech Stack

- .NET 8.0
- Discord.Net 3.16.0
- Entity Framework Core 8.0
- SQLite