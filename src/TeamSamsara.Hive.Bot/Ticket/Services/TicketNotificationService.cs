using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Integrations.Discord;
using TeamSamsara.Hive.Bot.Tickets.Models.Entities;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Tickets.Services;

// Service for sending ticket-related notifications via Discord DMs
// Handles all user and staff notifications for ticket lifecycle events
public class TicketNotificationService : BaseService
{
    private readonly DiscordIntegration _discord;
    
    public override string ServiceName => "TicketNotificationService";
    
    #region Constructor
    
    public TicketNotificationService(
        ILogger<TicketNotificationService> logger,
        IConfiguration configuration,
        DiscordIntegration discord)
        : base(logger, configuration)
    {
        _discord = discord ?? throw new ArgumentNullException(nameof(discord));
    }
    
    #endregion
    
    #region Ticket Created Notifications
    
    // Notify user that their ticket was created
    public async Task NotifyTicketCreatedAsync(TicketEntity ticket)
    {
        try
        {
            var user = await GetUserAsync(ticket.SubmittedByUserId);
            if (user == null) return;
            
            var embed = BuildTicketCreatedEmbed(ticket);
            
            await SendDirectMessageAsync(user, embed);
            
            LogInfo("Sent ticket created notification to user {UserId} for ticket {TicketId}", 
                ticket.SubmittedByUserId, ticket.SubmissionId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to send ticket created notification for {TicketId}", ticket.SubmissionId);
        }
    }
    
    #endregion
    
    #region Assignment Notifications
    
    // Notify staff member that a ticket was assigned to them
    public async Task NotifyTicketAssignedToStaffAsync(TicketEntity ticket, ulong staffUserId)
    {
        try
        {
            var user = await GetUserAsync(staffUserId);
            if (user == null) return;
            
            var embed = BuildTicketAssignedToStaffEmbed(ticket);
            
            await SendDirectMessageAsync(user, embed);
            
            LogInfo("Sent ticket assigned notification to staff {UserId} for ticket {TicketId}", 
                staffUserId, ticket.SubmissionId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to send ticket assigned notification for {TicketId}", ticket.SubmissionId);
        }
    }
    
    // Notify ticket submitter that their ticket was assigned
    public async Task NotifyTicketAssignedToSubmitterAsync(TicketEntity ticket)
    {
        try
        {
            var user = await GetUserAsync(ticket.SubmittedByUserId);
            if (user == null) return;
            
            var embed = BuildTicketAssignedToSubmitterEmbed(ticket);
            
            await SendDirectMessageAsync(user, embed);
            
            LogInfo("Sent ticket assigned notification to submitter {UserId} for ticket {TicketId}", 
                ticket.SubmittedByUserId, ticket.SubmissionId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to send ticket assigned notification to submitter for {TicketId}", 
                ticket.SubmissionId);
        }
    }
    
    #endregion
    
    #region Status Change Notifications
    
    // Notify user of ticket status change
    public async Task NotifyStatusChangedAsync(TicketEntity ticket, string oldStatus, string newStatus)
    {
        try
        {
            var user = await GetUserAsync(ticket.SubmittedByUserId);
            if (user == null) return;
            
            var embed = BuildStatusChangedEmbed(ticket, oldStatus, newStatus);
            
            await SendDirectMessageAsync(user, embed);
            
            LogInfo("Sent status change notification to user {UserId} for ticket {TicketId}: {OldStatus} → {NewStatus}", 
                ticket.SubmittedByUserId, ticket.SubmissionId, oldStatus, newStatus);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to send status change notification for {TicketId}", ticket.SubmissionId);
        }
    }
    
    #endregion
    
    #region Resolution Notifications
    
    // Notify user that their ticket was resolved
    public async Task NotifyTicketResolvedAsync(TicketEntity ticket)
    {
        try
        {
            var user = await GetUserAsync(ticket.SubmittedByUserId);
            if (user == null) return;
            
            var embed = BuildTicketResolvedEmbed(ticket);
            
            await SendDirectMessageAsync(user, embed);
            
            LogInfo("Sent ticket resolved notification to user {UserId} for ticket {TicketId}", 
                ticket.SubmittedByUserId, ticket.SubmissionId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to send ticket resolved notification for {TicketId}", ticket.SubmissionId);
        }
    }
    
    #endregion
    
    #region Closure Notifications
    
    // Notify user that their ticket was closed
    public async Task NotifyTicketClosedAsync(TicketEntity ticket)
    {
        try
        {
            var user = await GetUserAsync(ticket.SubmittedByUserId);
            if (user == null) return;
            
            var embed = BuildTicketClosedEmbed(ticket);
            
            await SendDirectMessageAsync(user, embed);
            
            LogInfo("Sent ticket closed notification to user {UserId} for ticket {TicketId}", 
                ticket.SubmittedByUserId, ticket.SubmissionId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to send ticket closed notification for {TicketId}", ticket.SubmissionId);
        }
    }
    
    #endregion
    
    #region Private Helper Methods - User Retrieval
    
    // Get Discord user by ID
    private async Task<IUser?> GetUserAsync(ulong userId)
    {
        try
        {
            var user = await _discord.GetUserAsync(userId);
            
            if (user == null)
            {
                LogWarning("User not found: {UserId}", userId);
            }
            
            return user;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get user: {UserId}", userId);
            return null;
        }
    }
    
    #endregion
    
    #region Private Helper Methods - Message Sending
    
    // Send direct message to user
    private async Task SendDirectMessageAsync(IUser user, Embed embed)
    {
        try
        {
            var dmChannel = await user.CreateDMChannelAsync();
            await dmChannel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to send DM to user {UserId}", user.Id);
            throw;
        }
    }
    
    #endregion
    
    #region Private Helper Methods - Embed Building
    
    // Build embed for ticket created notification
    private Embed BuildTicketCreatedEmbed(TicketEntity ticket)
    {
        return new EmbedBuilder()
            .WithTitle("✅ Ticket Created")
            .WithDescription($"Your support ticket has been created successfully.")
            .WithColor(Color.Green)
            .AddField("Ticket ID", ticket.SubmissionId, inline: true)
            .AddField("Status", ticket.Status, inline: true)
            .AddField("Priority", ticket.GetPriorityDisplay(), inline: true)
            .AddField("Title", ticket.Title, inline: false)
            .AddField("Description", TruncateDescription(ticket.Description), inline: false)
            .WithFooter("You will be notified when staff assigns your ticket")
            .WithCurrentTimestamp()
            .Build();
    }
    
    // Build embed for staff assignment notification
    private Embed BuildTicketAssignedToStaffEmbed(TicketEntity ticket)
    {
        return new EmbedBuilder()
            .WithTitle("📌 Ticket Assigned to You")
            .WithDescription($"A support ticket has been assigned to you.")
            .WithColor(Color.Blue)
            .AddField("Ticket ID", ticket.SubmissionId, inline: true)
            .AddField("Priority", ticket.GetPriorityDisplay(), inline: true)
            .AddField("Category", ticket.GetCategoryDisplay(), inline: true)
            .AddField("Title", ticket.Title, inline: false)
            .AddField("Description", TruncateDescription(ticket.Description), inline: false)
            .AddField("Submitted By", ticket.SubmittedByUsername, inline: true)
            .WithFooter("Use /ticket view to see full details")
            .WithCurrentTimestamp()
            .Build();
    }
    
    // Build embed for submitter assignment notification
    private Embed BuildTicketAssignedToSubmitterEmbed(TicketEntity ticket)
    {
        var assignedTo = ticket.AssignedToUsername ?? "Staff";
        
        return new EmbedBuilder()
            .WithTitle("👤 Ticket Assigned")
            .WithDescription($"Your ticket has been assigned to **{assignedTo}**.")
            .WithColor(Color.Blue)
            .AddField("Ticket ID", ticket.SubmissionId, inline: true)
            .AddField("Status", ticket.Status, inline: true)
            .AddField("Title", ticket.Title, inline: false)
            .WithFooter("You will be notified of any updates")
            .WithCurrentTimestamp()
            .Build();
    }
    
    // Build embed for status change notification
    private Embed BuildStatusChangedEmbed(TicketEntity ticket, string oldStatus, string newStatus)
    {
        return new EmbedBuilder()
            .WithTitle("🔄 Ticket Status Updated")
            .WithDescription($"Your ticket status has been updated.")
            .WithColor(Color.Orange)
            .AddField("Ticket ID", ticket.SubmissionId, inline: true)
            .AddField("Status Change", $"{oldStatus} → {newStatus}", inline: true)
            .AddField("Title", ticket.Title, inline: false)
            .WithCurrentTimestamp()
            .Build();
    }
    
    // Build embed for ticket resolved notification
    private Embed BuildTicketResolvedEmbed(TicketEntity ticket)
    {
        var resolvedBy = ticket.ResolvedByUsername ?? "Staff";
        
        return new EmbedBuilder()
            .WithTitle("✅ Ticket Resolved")
            .WithDescription($"Your ticket has been resolved by **{resolvedBy}**.")
            .WithColor(Color.Green)
            .AddField("Ticket ID", ticket.SubmissionId, inline: true)
            .AddField("Status", ticket.Status, inline: true)
            .AddField("Title", ticket.Title, inline: false)
            .AddField("Resolution", TruncateDescription(ticket.Resolution ?? "No resolution notes provided"), inline: false)
            .WithFooter("If you're satisfied, the ticket will be closed soon")
            .WithCurrentTimestamp()
            .Build();
    }
    
    // Build embed for ticket closed notification
    private Embed BuildTicketClosedEmbed(TicketEntity ticket)
    {
        return new EmbedBuilder()
            .WithTitle("🔒 Ticket Closed")
            .WithDescription($"Your ticket has been closed.")
            .WithColor(Color.DarkGrey)
            .AddField("Ticket ID", ticket.SubmissionId, inline: true)
            .AddField("Status", ticket.Status, inline: true)
            .AddField("Title", ticket.Title, inline: false)
            .WithFooter("Thank you for using our support system")
            .WithCurrentTimestamp()
            .Build();
    }
    
    #endregion
    
    #region Private Helper Methods - Text Formatting
    
    // Truncate description to fit in embed (max 1024 chars per field)
    private string TruncateDescription(string description)
    {
        const int maxLength = 1000;
        
        if (string.IsNullOrWhiteSpace(description))
        {
            return "No description provided";
        }
        
        if (description.Length <= maxLength)
        {
            return description;
        }
        
        return description.Substring(0, maxLength) + "...";
    }
    
    #endregion
}