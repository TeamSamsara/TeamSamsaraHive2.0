using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Configuration.Models.Enums;
using TeamSamsara.Hive.Bot.Configuration.Services;
using TeamSamsara.Hive.Bot.Integrations.Discord;
using TeamSamsara.Hive.Bot.Tickets.Models.DTOs;
using TeamSamsara.Hive.Bot.Tickets.Models.Entities;
using TeamSamsara.Hive.Bot.Tickets.Services;
using TeamSamsara.Hive.Core.Abstractions;
using TicketStatistics = TeamSamsara.Hive.Bot.Tickets.Services.TicketStatistics;

namespace TeamSamsara.Hive.Bot.Tickets;

public class TicketOrchestrator : BaseOrchestrator
{
    private readonly DiscordIntegration _discord;
    
    public override string SystemName => "Tickets";
    
    public TicketOrchestrator(
        ILogger<TicketOrchestrator> logger,
        IServiceProvider serviceProvider,
        DiscordIntegration discord)
        : base(logger, serviceProvider)
    {
        _discord = discord ?? throw new ArgumentNullException(nameof(discord));
    }
    
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        LogInfo("Initializing Ticket System...");
        
        // Register slash commands (WITHOUT /ticket create)
        await RegisterSlashCommandsAsync();
        
        // Hook up event handlers
        _discord.Client.SlashCommandExecuted += OnSlashCommandExecuted;
        _discord.Client.ModalSubmitted += OnModalSubmitted;
        _discord.Client.ButtonExecuted += OnButtonExecuted;
        
        // Post/update ticket embed with buttons
        await PostTicketEmbedAsync();
        
        // Validate configuration
        await ValidateConfigurationAsync();
        
        IsInitialized = true;
        LogInfo("✅ Ticket System initialized");
    }
    
    private async Task RegisterSlashCommandsAsync()
    {
        var guild = _discord.Client.Guilds.FirstOrDefault();
        if (guild == null)
        {
            LogWarning("No guild found for slash command registration");
            return;
        }
        
        var ticketCommand = new SlashCommandBuilder()
            .WithName("ticket")
            .WithDescription("Ticket system commands")
            // NO /ticket create - removed!
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("view")
                .WithDescription("View tickets")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("status", ApplicationCommandOptionType.String, "Filter by status", isRequired: false))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("assign")
                .WithDescription("Assign a ticket to staff")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("ticket_id", ApplicationCommandOptionType.String, "Ticket ID", isRequired: true)
                .AddOption("staff", ApplicationCommandOptionType.User, "Staff member to assign", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("resolve")
                .WithDescription("Mark a ticket as resolved")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("ticket_id", ApplicationCommandOptionType.String, "Ticket ID", isRequired: true)
                .AddOption("resolution", ApplicationCommandOptionType.String, "Resolution notes", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("close")
                .WithDescription("Close a ticket")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("ticket_id", ApplicationCommandOptionType.String, "Ticket ID", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("stats")
                .WithDescription("View ticket statistics")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("export")
                .WithDescription("Export all tickets to CSV")
                .WithType(ApplicationCommandOptionType.SubCommand));
        
        try
        {
            await guild.CreateApplicationCommandAsync(ticketCommand.Build());
            LogInfo("Registered /ticket command (without create subcommand)");
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to register /ticket command");
        }
    }
    
    private async Task PostTicketEmbedAsync()
    {
        try
        {
            using var scope = ServiceProvider.CreateScope();
            var getConfigService = scope.ServiceProvider.GetRequiredService<GetConfigValueService>();
            
            var channelId = await getConfigService.GetChannelIdAsync(ConfigKey.TicketEmbedChannelId);
            
            if (!channelId.HasValue)
            {
                LogWarning("⚠️ TicketEmbedChannelId not configured. Ticket embed will not be posted.");
                LogWarning("   Run: /config set-ticket-embed-channel");
                return;
            }
            
            // Get channel directly from Discord client
            var channel = await _discord.GetChannelAsync(channelId.Value);
            
            if (channel is not IMessageChannel messageChannel)
            {
                LogWarning("Channel {ChannelId} is not a message channel", channelId.Value);
                return;
            }
            
            var embed = new EmbedBuilder()
                .WithTitle("🎫 Create a Support Ticket")
                .WithDescription("Click a button below to create a ticket for your issue:")
                .WithColor(Color.Blue)
                .WithFooter("Select the category that best matches your issue")
                .Build();
            
            var components = new ComponentBuilder()
                .WithButton("🐛 Bug Report", "ticket_bug", ButtonStyle.Danger, row: 0)
                .WithButton("✨ Feature Request", "ticket_feature", ButtonStyle.Primary, row: 0)
                .WithButton("❓ General Question", "ticket_general", ButtonStyle.Secondary, row: 1)
                .WithButton("🔧 Technical Support", "ticket_technical", ButtonStyle.Success, row: 1)
                .Build();
            
            await messageChannel.SendMessageAsync(embed: embed, components: components);
            LogInfo("Posted ticket embed with buttons in channel {ChannelId}", channelId.Value);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to post ticket embed");
        }
    }
    
    private async Task OnButtonExecuted(SocketMessageComponent component)
    {
        if (!component.Data.CustomId.StartsWith("ticket_")) return;
        
        try
        {
            // Extract category from button CustomId (e.g., "ticket_bug" -> "bug")
            var category = component.Data.CustomId.Replace("ticket_", "");
            
            using var scope = ServiceProvider.CreateScope();
            var formService = scope.ServiceProvider.GetRequiredService<TicketFormService>();
            
            // Build modal with category embedded
            var modal = formService.BuildCreateTicketModal(category);
            await component.RespondWithModalAsync(modal);
            
            LogInfo("Displayed ticket modal for category {Category} to user {UserId}", category, component.User.Id);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to handle ticket button");
            await component.RespondAsync("❌ Failed to open ticket form. Please try again.", ephemeral: true);
        }
    }
    
    private async Task OnSlashCommandExecuted(SocketSlashCommand command)
    {
        if (command.Data.Name != "ticket") return;
        
        var subcommand = command.Data.Options.FirstOrDefault()?.Name;
        
        try
        {
            switch (subcommand)
            {
                case "view":
                    await HandleViewTicketsCommandAsync(command);
                    break;
                case "assign":
                    await HandleAssignTicketCommandAsync(command);
                    break;
                case "resolve":
                    await HandleResolveTicketCommandAsync(command);
                    break;
                case "close":
                    await HandleCloseTicketCommandAsync(command);
                    break;
                case "stats":
                    await HandleTicketStatsCommandAsync(command);
                    break;
                case "export":
                    await HandleExportTicketsCommandAsync(command);
                    break;
                default:
                    await command.RespondAsync("❌ Unknown subcommand", ephemeral: true);
                    break;
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error handling /ticket {Subcommand}", subcommand ?? "unknown");
        }
    }
    
    private async Task OnModalSubmitted(SocketModal modal)
    {
        // Check if this is a ticket creation modal (format: "ticket_create_{category}")
        if (!modal.Data.CustomId.StartsWith("ticket_create_")) return;
        
        try
        {
            await HandleTicketModalSubmissionAsync(modal);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error handling ticket modal submission");
        }
    }
    
    public async Task HandleTicketModalSubmissionAsync(SocketModal modal)
    {
        try
        {
            await modal.DeferAsync(ephemeral: true);
            
            using var scope = ServiceProvider.CreateScope();
            var formService = scope.ServiceProvider.GetRequiredService<TicketFormService>();
            var ticketService = scope.ServiceProvider.GetRequiredService<TicketService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<TicketNotificationService>();
            
            var dto = formService.ProcessModalSubmission(modal);
            var ticket = await ticketService.CreateTicketAsync(dto);
            
            await notificationService.NotifyTicketCreatedAsync(ticket);
            await PostTicketToLogChannelAsync(ticket);
            
            await modal.FollowupAsync(
                $"✅ Ticket created successfully! Your ticket ID is **{ticket.SubmissionId}**\n" +
                "You will be notified when staff assigns your ticket.",
                ephemeral: true);
            
            LogInfo("Created ticket {TicketId} for user {UserId}", ticket.SubmissionId, modal.User.Id);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to process ticket modal submission");
            await modal.FollowupAsync("❌ Failed to create ticket. Please try again.", ephemeral: true);
        }
    }
    
    public async Task HandleAssignTicketCommandAsync(SocketSlashCommand command)
    {
        try
        {
            await command.DeferAsync(ephemeral: true);
            
            var subcommandData = command.Data.Options.FirstOrDefault();
            var ticketId = subcommandData?.Options.FirstOrDefault(o => o.Name == "ticket_id")?.Value?.ToString();
            var staffUser = subcommandData?.Options.FirstOrDefault(o => o.Name == "staff")?.Value as IUser;
            
            if (string.IsNullOrEmpty(ticketId) || staffUser == null)
            {
                await command.FollowupAsync("❌ Invalid parameters", ephemeral: true);
                return;
            }
            
            using var scope = ServiceProvider.CreateScope();
            var ticketService = scope.ServiceProvider.GetRequiredService<TicketService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<TicketNotificationService>();
            
            var ticket = await ticketService.GetSubmissionByIdAsync(ticketId);
            if (ticket == null)
            {
                await command.FollowupAsync($"❌ Ticket not found: {ticketId}", ephemeral: true);
                return;
            }
            
            var success = await ticketService.AssignSubmissionAsync(ticketId, staffUser.Id, staffUser.Username);
            
            if (!success)
            {
                await command.FollowupAsync("❌ Failed to assign ticket.", ephemeral: true);
                return;
            }
            
            await notificationService.NotifyTicketAssignedToStaffAsync(ticket, staffUser.Id);
            await notificationService.NotifyTicketAssignedToSubmitterAsync(ticket);
            
            await command.FollowupAsync($"✅ Ticket **{ticketId}** assigned to {staffUser.Mention}", ephemeral: true);
            
            LogInfo("Assigned ticket {TicketId} to staff {UserId}", ticketId, staffUser.Id);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to assign ticket");
            await command.FollowupAsync("❌ Failed to assign ticket.", ephemeral: true);
        }
    }
    
    public async Task HandleResolveTicketCommandAsync(SocketSlashCommand command)
    {
        try
        {
            await command.DeferAsync(ephemeral: true);
            
            var subcommandData = command.Data.Options.FirstOrDefault();
            var ticketId = subcommandData?.Options.FirstOrDefault(o => o.Name == "ticket_id")?.Value?.ToString();
            var resolution = subcommandData?.Options.FirstOrDefault(o => o.Name == "resolution")?.Value?.ToString();
            
            if (string.IsNullOrEmpty(ticketId) || string.IsNullOrEmpty(resolution))
            {
                await command.FollowupAsync("❌ Invalid parameters", ephemeral: true);
                return;
            }
            
            using var scope = ServiceProvider.CreateScope();
            var ticketService = scope.ServiceProvider.GetRequiredService<TicketService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<TicketNotificationService>();
            
            var success = await ticketService.ResolveTicketAsync(ticketId, command.User.Id, command.User.Username, resolution);
            
            if (!success)
            {
                await command.FollowupAsync($"❌ Failed to resolve ticket: {ticketId}", ephemeral: true);
                return;
            }
            
            var ticket = await ticketService.GetSubmissionByIdAsync(ticketId);
            if (ticket != null)
            {
                await notificationService.NotifyTicketResolvedAsync(ticket);
            }
            
            await command.FollowupAsync($"✅ Ticket **{ticketId}** marked as resolved.", ephemeral: true);
            
            LogInfo("Resolved ticket {TicketId} by user {UserId}", ticketId, command.User.Id);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to resolve ticket");
            await command.FollowupAsync("❌ Failed to resolve ticket.", ephemeral: true);
        }
    }
    
    public async Task HandleCloseTicketCommandAsync(SocketSlashCommand command)
    {
        try
        {
            await command.DeferAsync(ephemeral: true);
            
            var subcommandData = command.Data.Options.FirstOrDefault();
            var ticketId = subcommandData?.Options.FirstOrDefault(o => o.Name == "ticket_id")?.Value?.ToString();
            
            if (string.IsNullOrEmpty(ticketId))
            {
                await command.FollowupAsync("❌ Invalid parameters", ephemeral: true);
                return;
            }
            
            using var scope = ServiceProvider.CreateScope();
            var ticketService = scope.ServiceProvider.GetRequiredService<TicketService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<TicketNotificationService>();
            
            var success = await ticketService.CloseSubmissionAsync(ticketId, command.User.Id, $"Closed by {command.User.Username}");
            
            if (!success)
            {
                await command.FollowupAsync($"❌ Failed to close ticket: {ticketId}", ephemeral: true);
                return;
            }
            
            var ticket = await ticketService.GetSubmissionByIdAsync(ticketId);
            if (ticket != null)
            {
                await notificationService.NotifyTicketClosedAsync(ticket);
            }
            
            await command.FollowupAsync($"✅ Ticket **{ticketId}** closed.", ephemeral: true);
            
            LogInfo("Closed ticket {TicketId} by user {UserId}", ticketId, command.User.Id);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to close ticket");
            await command.FollowupAsync("❌ Failed to close ticket.", ephemeral: true);
        }
    }
    
    public async Task HandleViewTicketsCommandAsync(SocketSlashCommand command)
    {
        try
        {
            await command.DeferAsync(ephemeral: true);
            
            var subcommandData = command.Data.Options.FirstOrDefault();
            var status = subcommandData?.Options.FirstOrDefault(o => o.Name == "status")?.Value?.ToString();
            
            using var scope = ServiceProvider.CreateScope();
            var ticketService = scope.ServiceProvider.GetRequiredService<TicketService>();
            
            var tickets = await ticketService.GetAllSubmissionsAsync(status: status, submittedBy: null, assignedTo: null);
            
            if (tickets.Count == 0)
            {
                var message = status != null ? $"No tickets found with status: {status}" : "No tickets found";
                await command.FollowupAsync($"ℹ️ {message}", ephemeral: true);
                return;
            }
            
            var embed = BuildTicketListEmbed(tickets, status);
            await command.FollowupAsync(embed: embed, ephemeral: true);
            
            LogInfo("Displayed {Count} tickets to user {UserId}", tickets.Count, command.User.Id);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to view tickets");
            await command.FollowupAsync("❌ Failed to retrieve tickets.", ephemeral: true);
        }
    }
    
    public async Task HandleExportTicketsCommandAsync(SocketSlashCommand command)
    {
        try
        {
            await command.DeferAsync(ephemeral: true);
            
            using var scope = ServiceProvider.CreateScope();
            var ticketService = scope.ServiceProvider.GetRequiredService<TicketService>();
            var exportService = scope.ServiceProvider.GetRequiredService<TicketExportService>();
            
            var tickets = await ticketService.GetAllSubmissionsAsync();
            
            if (tickets.Count == 0)
            {
                await command.FollowupAsync("ℹ️ No tickets to export.", ephemeral: true);
                return;
            }
            
            var csvBytes = await exportService.ExportTicketsToCsvBytesAsync(tickets);
            var fileName = $"tickets_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            
            using var stream = new MemoryStream(csvBytes);
            await command.FollowupWithFileAsync(stream, fileName, $"✅ Exported {tickets.Count} tickets to CSV.", ephemeral: true);
            
            LogInfo("Exported {Count} tickets to CSV for user {UserId}", tickets.Count, command.User.Id);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to export tickets");
            await command.FollowupAsync("❌ Failed to export tickets.", ephemeral: true);
        }
    }
    
    public async Task HandleTicketStatsCommandAsync(SocketSlashCommand command)
    {
        try
        {
            await command.DeferAsync(ephemeral: true);
            
            using var scope = ServiceProvider.CreateScope();
            var ticketService = scope.ServiceProvider.GetRequiredService<TicketService>();
            
            var stats = await ticketService.GetStatisticsAsync();
            var embed = BuildStatsEmbed(stats);
            
            await command.FollowupAsync(embed: embed, ephemeral: true);
            
            LogInfo("Displayed ticket statistics to user {UserId}", command.User.Id);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get ticket statistics");
            await command.FollowupAsync("❌ Failed to retrieve statistics.", ephemeral: true);
        }
    }
    
    private async Task ValidateConfigurationAsync()
    {
        using var scope = ServiceProvider.CreateScope();
        var getConfigService = scope.ServiceProvider.GetRequiredService<GetConfigValueService>();
        
        var logChannelId = await getConfigService.GetChannelIdAsync(ConfigKey.TicketLogChannelId);
        var embedChannelId = await getConfigService.GetChannelIdAsync(ConfigKey.TicketEmbedChannelId);
        
        if (!logChannelId.HasValue)
        {
            LogWarning("⚠️ TicketLogChannelId not configured. Tickets will not be posted to log channel.");
            LogWarning("   Run: /config set-ticket-log-channel");
        }
        
        if (!embedChannelId.HasValue)
        {
            LogWarning("⚠️ TicketEmbedChannelId not configured. Ticket creation embed will not be posted.");
            LogWarning("   Run: /config set-ticket-embed-channel");
        }
    }
    
    private async Task<bool> AreTicketsEnabledAsync()
    {
        await Task.CompletedTask;
        return true;
    }
    
    private async Task PostTicketToLogChannelAsync(TicketEntity ticket)
    {
        try
        {
            using var scope = ServiceProvider.CreateScope();
            var getConfigService = scope.ServiceProvider.GetRequiredService<GetConfigValueService>();
            
            var channelId = await getConfigService.GetChannelIdAsync(ConfigKey.TicketLogChannelId);
            
            if (!channelId.HasValue)
            {
                LogWarning("Ticket log channel not configured, skipping log post");
                return;
            }
            
            var embed = BuildTicketLogEmbed(ticket);
            await _discord.SendEmbedAsync(channelId.Value, embed);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to post ticket to log channel");
        }
    }
    
    private Embed BuildTicketLogEmbed(TicketEntity ticket)
    {
        return new EmbedBuilder()
            .WithTitle($"🎫 New Ticket: {ticket.SubmissionId}")
            .WithColor(Color.Blue)
            .AddField("Title", ticket.Title, inline: false)
            .AddField("Category", ticket.GetCategoryDisplay(), inline: true)
            .AddField("Priority", ticket.GetPriorityDisplay(), inline: true)
            .AddField("Status", ticket.Status, inline: true)
            .AddField("Submitted By", $"<@{ticket.SubmittedByUserId}>", inline: true)
            .AddField("Description", ticket.Description.Length > 1000 
                ? ticket.Description.Substring(0, 1000) + "..." 
                : ticket.Description, inline: false)
            .WithFooter($"Ticket ID: {ticket.SubmissionId}")
            .WithCurrentTimestamp()
            .Build();
    }
    
    private Embed BuildTicketListEmbed(List<TicketEntity> tickets, string? status)
    {
        var title = status != null ? $"Tickets - Status: {status}" : "All Tickets";
        var builder = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(Color.Blue)
            .WithDescription($"Found {tickets.Count} ticket(s)");
        
        var displayTickets = tickets.Take(10).ToList();
        
        foreach (var ticket in displayTickets)
        {
            var fieldValue = $"**Status:** {ticket.Status}\n" +
                           $"**Priority:** {ticket.GetPriorityDisplay()}\n" +
                           $"**Category:** {ticket.GetCategoryDisplay()}";
            
            builder.AddField($"{ticket.SubmissionId}: {ticket.Title}", fieldValue, inline: false);
        }
        
        if (tickets.Count > 10)
        {
            builder.WithFooter($"Showing 10 of {tickets.Count} tickets. Use /ticket export for full list.");
        }
        
        return builder.Build();
    }
    
    private Embed BuildStatsEmbed(TicketStatistics stats)
    {
        return new EmbedBuilder()
            .WithTitle("📊 Ticket Statistics")
            .WithColor(Color.Gold)
            .AddField("Total Tickets", stats.TotalTickets, inline: true)
            .AddField("Pending", stats.PendingTickets, inline: true)
            .AddField("Assigned", stats.AssignedTickets, inline: true)
            .AddField("In Progress", stats.InProgressTickets, inline: true)
            .AddField("Resolved", stats.ResolvedTickets, inline: true)
            .AddField("Closed", stats.ClosedTickets, inline: true)
            .WithCurrentTimestamp()
            .Build();
    }
}