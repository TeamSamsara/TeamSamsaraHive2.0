using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Tickets.Models.DTOs;
using TeamSamsara.Hive.Bot.Tickets.Models.Enums;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Tickets.Services;

// Service for building and handling Discord ticket forms
// Creates modals for ticket submission and processes responses
public class TicketFormService : BaseService
{
    public override string ServiceName => "TicketFormService";
    
    private const string TitleInputId = "ticket_title";
    private const string DescriptionInputId = "ticket_description";
    
    #region Constructor
    
    public TicketFormService(
        ILogger<TicketFormService> logger,
        IConfiguration configuration)
        : base(logger, configuration)
    {
    }
    
    #endregion
    
    #region Modal Building
    
    // Build the ticket creation modal with embedded category
    public Modal BuildCreateTicketModal(string category)
    {
        try
        {
            // Embed category in CustomId so we can extract it later
            var customId = $"ticket_create_{category}";
            
            // Get display name for category
            var categoryDisplay = GetCategoryDisplayName(category);
            
            var modal = new ModalBuilder()
                .WithTitle($"Create Ticket - {categoryDisplay}")
                .WithCustomId(customId)
                .AddTextInput(BuildTitleInput())
                .AddTextInput(BuildDescriptionInput())
                .Build();
            
            LogInfo("Created ticket modal for category {Category}", category);
            return modal;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to build ticket creation modal");
            throw;
        }
    }
    
    #endregion
    
    #region Modal Submission Processing
    
    // Process modal submission and create DTO
    public CreateTicketDto ProcessModalSubmission(SocketModal modal)
    {
        try
        {
            ValidateModalCustomId(modal);
            
            // Extract category from CustomId (e.g., "ticket_create_bug" -> "bug")
            var category = ExtractCategoryFromCustomId(modal.Data.CustomId);
            
            // Auto-assign priority based on category
            var priority = GetPriorityFromCategory(category);
            
            var components = modal.Data.Components.ToList();
            
            var title = ExtractTextValue(components, TitleInputId);
            var description = ExtractTextValue(components, DescriptionInputId);
            
            var categoryEnum = ParseCategory(category);
            var priorityEnum = ParsePriority(priority);
            
            var dto = BuildCreateTicketDto(modal.User, title, description, categoryEnum, priorityEnum);
            
            LogInfo("Processed ticket modal submission from user {UserId} - Category: {Category}, Priority: {Priority}", 
                modal.User.Id, category, priority);
            
            return dto;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to process modal submission");
            throw;
        }
    }
    
    #endregion
    
    #region Private Helper Methods - Modal Components
    
    // Build title input component
    private TextInputBuilder BuildTitleInput()
    {
        return new TextInputBuilder()
            .WithLabel("Ticket Title")
            .WithCustomId(TitleInputId)
            .WithStyle(TextInputStyle.Short)
            .WithPlaceholder("Brief summary of your issue")
            .WithRequired(true)
            .WithMaxLength(100);
    }
    
    // Build description input component
    private TextInputBuilder BuildDescriptionInput()
    {
        return new TextInputBuilder()
            .WithLabel("Description")
            .WithCustomId(DescriptionInputId)
            .WithStyle(TextInputStyle.Paragraph)
            .WithPlaceholder("Detailed description of your issue")
            .WithRequired(true)
            .WithMaxLength(2000);
    }
    
    #endregion
    
    #region Private Helper Methods - Category & Priority Mapping
    
    // Get display name for category
    private string GetCategoryDisplayName(string category)
    {
        return category switch
        {
            "bug" => "Bug Report",
            "feature" => "Feature Request",
            "general" => "General Question",
            "technical" => "Technical Support",
            "account" => "Account Issue",
            _ => "Support"
        };
    }
    
    // Extract category from CustomId
    private string ExtractCategoryFromCustomId(string customId)
    {
        // CustomId format: "ticket_create_{category}"
        if (customId.StartsWith("ticket_create_"))
        {
            return customId.Replace("ticket_create_", "");
        }
        
        LogWarning("Invalid CustomId format: {CustomId}, defaulting to general", customId);
        return "general";
    }
    
    // Map category to priority
    private string GetPriorityFromCategory(string category)
    {
        return category switch
        {
            "bug" => "high",
            "technical" => "high",
            "feature" => "medium",
            "account" => "medium",
            "general" => "low",
            _ => "medium"
        };
    }
    
    #endregion
    
    #region Private Helper Methods - Validation
    
    // Validate modal custom ID matches expected pattern
    private void ValidateModalCustomId(SocketModal modal)
    {
        if (!modal.Data.CustomId.StartsWith("ticket_create_"))
        {
            throw new InvalidOperationException($"Invalid modal custom ID: {modal.Data.CustomId}");
        }
    }
    
    #endregion
    
    #region Private Helper Methods - Extraction
    
    // Extract text value from modal components
    private string ExtractTextValue(List<SocketMessageComponentData> components, string customId)
    {
        var component = components.FirstOrDefault(c => c.CustomId == customId);
        
        if (component == null)
        {
            throw new InvalidOperationException($"Component not found: {customId}");
        }
        
        return component.Value.Trim();
    }
    
    #endregion
    
    #region Private Helper Methods - Parsing
    
    // Parse category from text input
    private TicketCategory ParseCategory(string categoryText)
    {
        if (string.IsNullOrWhiteSpace(categoryText))
        {
            return TicketCategory.General;
        }
        
        return categoryText.ToLower() switch
        {
            "bug" => TicketCategory.Bug,
            "feature" => TicketCategory.Feature,
            "question" or "general" => TicketCategory.General,
            "account" => TicketCategory.Account,
            "technical" => TicketCategory.Technical,
            "permissions" => TicketCategory.Permissions,
            "config" or "configuration" => TicketCategory.Configuration,
            "other" => TicketCategory.Other,
            _ => TicketCategory.General
        };
    }
    
    // Parse priority from text input
    private TicketPriority ParsePriority(string priorityText)
    {
        if (string.IsNullOrWhiteSpace(priorityText))
        {
            return TicketPriority.Medium;
        }
        
        return priorityText.ToLower() switch
        {
            "low" => TicketPriority.Low,
            "medium" => TicketPriority.Medium,
            "high" => TicketPriority.High,
            "critical" => TicketPriority.Critical,
            "emergency" => TicketPriority.Emergency,
            _ => TicketPriority.Medium
        };
    }
    
    #endregion
    
    #region Private Helper Methods - DTO Building
    
    // Build CreateTicketDto from modal data
    private CreateTicketDto BuildCreateTicketDto(
        SocketUser user,
        string title,
        string description,
        TicketCategory category,
        TicketPriority priority)
    {
        return new CreateTicketDto
        {
            SubmittedByUserId = user.Id,
            SubmittedByUsername = user.Username,
            Title = title,
            Description = description,
            Category = category,
            Priority = priority,
            Type = TicketType.Support // Default to Support
        };
    }
    
    #endregion
}