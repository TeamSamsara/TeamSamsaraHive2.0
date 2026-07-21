using TeamSamsara.Hive.Bot.Tickets.Models.Enums;
using TeamSamsara.Hive.Core.Workflows.Abstractions;

namespace TeamSamsara.Hive.Bot.Tickets.Models.Entities;

// Ticket entity extending the base submission workflow
// Represents a support ticket in the system
public class TicketEntity : BaseSubmissionEntity
{
    #region Ticket-Specific Fields
    
    // Short title summarizing the issue
    public string Title { get; set; } = string.Empty;
    
    // Detailed description of the issue
    public string Description { get; set; } = string.Empty;
    
    // Category for classification (Bug, Feature, Question, etc.)
    public TicketCategory Category { get; set; } = TicketCategory.General;
    
    // Priority level (Low, Medium, High, Critical, Emergency)
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    
    // Type for team routing (Support, Technical, Community, etc.)
    public TicketType Type { get; set; } = TicketType.Support;
    
    #endregion
    
    #region Resolution Details
    
    // Solution or resolution provided by staff
    public string? Resolution { get; set; }
    
    // User who resolved the ticket
    public ulong? ResolvedByUserId { get; set; }
    
    // Username of resolver (for audit trail)
    public string? ResolvedByUsername { get; set; }
    
    #endregion
    
    #region Helper Methods
    
    // Check if ticket is high priority or higher
    public bool IsHighPriority()
    {
        return Priority >= TicketPriority.High;
    }
    
    // Check if ticket is critical or emergency
    public bool IsCritical()
    {
        return Priority >= TicketPriority.Critical;
    }
    
    // Get formatted ticket display name
    public string GetDisplayName()
    {
        return $"[{SubmissionId}] {Title}";
    }
    
    // Get formatted priority with emoji
    public string GetPriorityDisplay()
    {
        return Priority switch
        {
            TicketPriority.Low => "🟢 Low",
            TicketPriority.Medium => "🟡 Medium",
            TicketPriority.High => "🟠 High",
            TicketPriority.Critical => "🔴 Critical",
            TicketPriority.Emergency => "🚨 Emergency",
            _ => "⚪ Unknown"
        };
    }
    
    // Get formatted category display
    public string GetCategoryDisplay()
    {
        return Category switch
        {
            TicketCategory.Bug => "🐛 Bug",
            TicketCategory.Feature => "✨ Feature",
            TicketCategory.Question => "❓ Question",
            TicketCategory.Account => "👤 Account",
            TicketCategory.Permissions => "🔒 Permissions",
            TicketCategory.Configuration => "⚙️ Configuration",
            TicketCategory.General => "📋 General",
            TicketCategory.Other => "📌 Other",
            _ => "❔ Unknown"
        };
    }
    
    #endregion
}