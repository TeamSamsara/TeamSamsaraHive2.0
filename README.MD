# Team Samsara Hive v2

**Complete rebuild of the Team Samsara Hive Discord bot with Analytics, Notifications, and Ticket systems.**

[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Discord.Net](https://img.shields.io/badge/Discord.Net-3.16.0-blue)](https://github.com/discord-net/Discord.Net)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

---

## Overview

Team Samsara Hive v2 is a comprehensive Discord bot platform designed for esports organizations. It provides advanced analytics, intelligent notifications, and a priority-based ticket management system with Discord channel integration.

### Key Features

- **📊 Analytics System**
  - Twitch stream monitoring and statistics
  - Discord server activity tracking
  - Daily snapshots and historical reporting
  - Player performance metrics

- **🔔 Notification System**
  - Stream live/offline notifications
  - Priority ticket alerts with smart anti-spam
  - Analytics report delivery
  - Role-based notification routing

- **🎫 Ticket System**
  - Discord channel-per-ticket approach
  - Priority-based ticketing (Urgent, High, Normal, Low)
  - Auto-escalation for unclaimed tickets
  - Live voice channel stats display
  - Abuse prevention with cooldowns
  - User feedback collection

---

## 🏗️ Architecture

### Project Structure

```
TeamSamsara.Hive.v2/
├── src/
│   ├── TeamSamsara.Hive.Core/       # Shared infrastructure
│   │   ├── Abstractions/            # Base classes
│   │   ├── EventBus/                # Event system
│   │   └── Interfaces/              # Contracts
│   │
│   └── TeamSamsara.Hive.Bot/        # Main application
│       ├── Integrations/            # External APIs
│       │   ├── Twitch/
│       │   └── Discord/
│       ├── Analytics/               # Analytics system
│       │   ├── TwitchAnalytics/
│       │   └── DiscordAnalytics/
│       ├── NotificationSystem/      # Notifications
│       └── TicketSystem/            # Ticket management
│
├── Dockerfile
├── docker-compose.yml
└── README.md
```

### Clean Architecture Principles

- **One orchestrator per system** - Clear ownership and responsibility
- **Services do ONE thing** - Single Responsibility Principle
- **No service-to-service calls** - Services never call other services directly
- **EventBus for cross-system communication** - Loose coupling between systems
- **Base classes for consistency** - BaseOrchestrator, BaseService, BaseRepository

---

## 🚀 Quick Start

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (for deployment)
- Discord Bot Token ([Create one here](https://discord.com/developers/applications))
- Twitch API Credentials ([Get them here](https://dev.twitch.tv/console))

### Local Development Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/YourOrg/TeamSamsara.Hive.v2.git
   cd TeamSamsara.Hive.v2
   ```

2. **Create your development config:**
   ```bash
   cd src/TeamSamsara.Hive.Bot
   cp appsettings.json appsettings.Development.json
   ```

3. **Edit `appsettings.Development.json`** with your credentials:
   ```json
   {
     "Config": {
       "Twitch": {
         "ClientId": "YOUR_TWITCH_CLIENT_ID",
         "ClientSecret": "YOUR_TWITCH_CLIENT_SECRET"
       },
       "Discord": {
         "BotToken": "YOUR_DISCORD_BOT_TOKEN",
         "NotificationChannels": {
           "StreamNotificationChannelId": "YOUR_CHANNEL_ID"
         }
       }
     }
   }
   ```

4. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

5. **Build the solution:**
   ```bash
   dotnet build
   ```

6. **Run the bot:**
   ```bash
   cd src/TeamSamsara.Hive.Bot
   dotnet run --environment Development
   ```

---

## 🐳 Docker Deployment

### Production Deployment

1. **Create data directories:**
   ```bash
   mkdir -p data logs
   ```

2. **Create `appsettings.Production.json`** on your server with production credentials.

3. **Build and start:**
   ```bash
   docker-compose up -d --build
   ```

4. **View logs:**
   ```bash
   docker-compose logs -f hive-bot
   ```

### Docker Commands

```bash
# Start the bot
docker-compose up -d

# View logs (follow)
docker-compose logs -f

# Restart
docker-compose restart

# Stop
docker-compose down

# Rebuild and start
docker-compose up -d --build

# Check status
docker-compose ps
```

---

## ⚙️ Configuration

### Environment-Specific Configs

- **appsettings.json** - Base template (committed to Git)
- **appsettings.Development.json** - Local development (NOT committed)
- **appsettings.Staging.json** - Staging server (NOT committed)
- **appsettings.Production.json** - Production server (NOT committed)

### Key Settings

#### Discord Configuration
```json
"Discord": {
  "BotToken": "YOUR_BOT_TOKEN",
  "NotificationChannels": {
    "StreamNotificationChannelId": "CHANNEL_ID",
    "UrgentTicketChannelId": "CHANNEL_ID",
    "AdminChannelId": "CHANNEL_ID"
  },
  "Roles": {
    "AdminRoleId": "ROLE_ID",
    "StaffRoleId": "ROLE_ID"
  }
}
```

#### Ticket System Configuration
```json
"TicketSettings": {
  "DiscordChannels": {
    "ActiveTicketsCategoryId": "CATEGORY_ID",
    "ClosedTicketsCategoryId": "CATEGORY_ID",
    "VoiceStatsChannels": {
      "UrgentStatsChannelId": "CHANNEL_ID",
      "HighStatsChannelId": "CHANNEL_ID"
    }
  },
  "ResponseTimes": {
    "UrgentResponseMinutes": 5,
    "HighResponseMinutes": 15
  }
}
```

---

## 🎫 Ticket System Features

### Discord Channel Integration

Each ticket gets its own Discord text channel with:

- **Smart naming:** `🚨urgent-00001-⬆️✅john`
  - 🚨 = Priority emoji
  - ⬆️ = Escalated
  - ✅john = Assigned to staff member

- **Auto-positioning:** Urgent tickets at top of category

- **Live stats:** 5 voice channels showing queue status
  ```
  🚨 Urgent: 3 | Avg Response: 2m
  ⚠️ High: 7 | Avg Response: 8m
  📋 Normal: 12 | Avg Response: 45m
  ⬆️ Escalated: 2
  ⏰ Oldest: 23m
  ```

- **Channel lifecycle:**
  1. Created → Active Tickets category
  2. Assigned → Name updated with staff member
  3. Escalated → Moved to top + notification
  4. Closed → Moved to Closed Tickets (read-only)
  5. Deleted → After 7 days retention

### Slash Commands

```
/ticket create [title] [description] [category] [priority]
/ticket assign [ticket-id] [@staff-member]
/ticket close [ticket-id] [resolution-notes]
/ticket reopen [ticket-id]
/ticket addnote [ticket-id] [note]
/ticket list [status]
/ticket view [ticket-id]
```

### Priority Levels

- **🚨 Urgent** - 5 min response time, immediate notification
- **⚠️ High** - 15 min response time, channel notification
- **📋 Normal** - 2 hour response time
- **⏰ Low** - 24 hour response time

### Abuse Prevention

- Max 2 urgent tickets per day
- Max 5 high tickets per week
- Auto-detection of priority abuse
- Warning system (3 warnings before action)

---

## 📊 Analytics Features

### Twitch Monitoring
- Real-time stream status tracking (60-second checks)
- Viewer count sampling during streams
- Stream session analytics
- Daily snapshots at 3 AM UTC

### Discord Analytics
- Server activity sampling (5-minute intervals)
- Voice session tracking
- User engagement scoring
- Channel activity metrics
- Daily server snapshots

### Reports
- Stream performance reports
- Server activity leaderboards
- User statistics
- Channel usage analytics

---

## 🔔 Notification System

### Smart Notifications

- **Stream notifications** - Live/offline alerts with role mentions
- **Urgent tickets** - Channel ping + DM to online staff
- **Escalations** - Escalated ticket alerts
- **Daily reports** - Analytics summaries

### Anti-Spam Features

- Role ping cooldowns (5 minutes)
- DM rate limiting (3 per hour per user)
- Notification grouping (batch multiple urgent tickets)
- Quiet hours support (optional)

---

## 🛠️ Development

### Tech Stack

- **.NET 8.0** - Application framework
- **Discord.Net 3.16.0** - Discord API wrapper
- **Entity Framework Core 8.0** - ORM
- **SQLite** - Database
- **FluentValidation** - Input validation
- **Serilog** - Structured logging

### Adding a New Feature

1. Define models (Entities, DTOs, ViewModels)
2. Create repository interface and implementation
3. Create service(s) following single responsibility
4. Update orchestrator to coordinate services
5. Publish events for cross-system communication
6. Add tests

### Architecture Patterns

**Base Classes:**
- `BaseOrchestrator` - System coordinator
- `BaseService` - Business logic unit
- `BaseRepository` - Data access
- `BaseIntegration` - External API wrapper

**Event System:**
```csharp
// Publishing
await _eventBus.PublishAsync(new StreamWentLiveEvent { ... });

// Subscribing (in orchestrator constructor)
_eventBus.Subscribe<StreamWentLiveEvent>(OnStreamWentLive);
```

---

## 📝 Project Status

### Completed ✅
- Core infrastructure (Base classes, EventBus, utilities)
- Architecture design
- Complete parts lists
- Master implementation task list
- Configuration setup
- Docker deployment setup

### In Progress 🚧
- Phase 1: Integrations (TwitchIntegration, DiscordIntegration)

### Planned 📋
- Phase 2: Events definition
- Phase 3: Analytics system
- Phase 4: Notification system
- Phase 5: Ticket system
- Phase 6: Integration & testing

---

## 🤝 Contributing

This is a private project for Team Samsara. For internal contributors:

1. Create a feature branch from `main`
2. Follow existing code patterns (BaseService, BaseRepository, etc.)
3. Write services that do ONE thing
4. Never call services from other services
5. Use EventBus for cross-system communication
6. Test thoroughly before merging

---

## 📄 License

MIT License - See [LICENSE](LICENSE) file for details

---

## 📞 Contact

**Team Samsara**
- Discord: [Join our server]
- GitHub: [TeamSamsara]

---

## 🙏 Acknowledgments

- Discord.Net community
- Twitch API documentation
- Entity Framework Core team
- All contributors to Team Samsara Hive v2

---

**Built with ❤️ by Team Samsara**