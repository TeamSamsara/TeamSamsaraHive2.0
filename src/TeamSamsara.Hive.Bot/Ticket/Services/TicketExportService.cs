using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Tickets.Models.Entities;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Tickets.Services;

// Service for exporting tickets to CSV format
// Handles ticket data serialization for reporting and backup
public class TicketExportService : BaseService
{
    public override string ServiceName => "TicketExportService";
    
    private const string CsvDelimiter = ",";
    private const string CsvNewLine = "\n";
    
    #region Constructor
    
    public TicketExportService(
        ILogger<TicketExportService> logger,
        IConfiguration configuration)
        : base(logger, configuration)
    {
    }
    
    #endregion
    
    #region CSV Export
    
    // Export tickets to CSV format
    public async Task<string> ExportTicketsToCsvAsync(List<TicketEntity> tickets)
    {
        try
        {
            ValidateTicketList(tickets);
            
            var csv = new StringBuilder();
            
            AppendCsvHeader(csv);
            AppendCsvRows(csv, tickets);
            
            var result = csv.ToString();
            
            LogInfo("Exported {Count} tickets to CSV", tickets.Count);
            
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to export tickets to CSV");
            throw;
        }
    }
    
    // Export tickets to CSV file and return byte array
    public async Task<byte[]> ExportTicketsToCsvBytesAsync(List<TicketEntity> tickets)
    {
        try
        {
            var csv = await ExportTicketsToCsvAsync(tickets);
            var bytes = Encoding.UTF8.GetBytes(csv);
            
            LogInfo("Converted CSV to bytes: {Size} bytes", bytes.Length);
            
            return bytes;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to export tickets to CSV bytes");
            throw;
        }
    }
    
    // Export single ticket to CSV format
    public async Task<string> ExportTicketToCsvAsync(TicketEntity ticket)
    {
        try
        {
            if (ticket == null)
            {
                throw new ArgumentNullException(nameof(ticket));
            }
            
            var tickets = new List<TicketEntity> { ticket };
            return await ExportTicketsToCsvAsync(tickets);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to export single ticket to CSV");
            throw;
        }
    }
    
    #endregion
    
    #region Private Helper Methods - Validation
    
    // Validate ticket list
    private void ValidateTicketList(List<TicketEntity> tickets)
    {
        if (tickets == null)
        {
            throw new ArgumentNullException(nameof(tickets));
        }
        
        if (tickets.Count == 0)
        {
            LogWarning("Exporting empty ticket list");
        }
    }
    
    #endregion
    
    #region Private Helper Methods - CSV Building
    
    // Append CSV header row
    private void AppendCsvHeader(StringBuilder csv)
    {
        var headers = new[]
        {
            "TicketId",
            "Status",
            "Title",
            "Description",
            "Category",
            "Priority",
            "Type",
            "SubmittedBy",
            "SubmittedByUsername",
            "SubmittedAt",
            "AssignedTo",
            "AssignedToUsername",
            "AssignedAt",
            "Resolution",
            "ResolvedBy",
            "ResolvedByUsername",
            "ResolvedAt",
            "ClosedAt",
            "StaffNotes"
        };
        
        csv.AppendLine(string.Join(CsvDelimiter, headers));
    }
    
    // Append CSV data rows
    private void AppendCsvRows(StringBuilder csv, List<TicketEntity> tickets)
    {
        foreach (var ticket in tickets)
        {
            AppendCsvRow(csv, ticket);
        }
    }
    
    // Append single CSV row
    private void AppendCsvRow(StringBuilder csv, TicketEntity ticket)
    {
        var values = new[]
        {
            EscapeCsvValue(ticket.SubmissionId),
            EscapeCsvValue(ticket.Status),
            EscapeCsvValue(ticket.Title),
            EscapeCsvValue(ticket.Description),
            EscapeCsvValue(ticket.Category.ToString()),
            EscapeCsvValue(ticket.Priority.ToString()),
            EscapeCsvValue(ticket.Type.ToString()),
            EscapeCsvValue(ticket.SubmittedByUserId.ToString()),
            EscapeCsvValue(ticket.SubmittedByUsername),
            EscapeCsvValue(FormatDateTime(ticket.SubmittedAtUtc)),
            EscapeCsvValue(ticket.AssignedToUserId?.ToString() ?? ""),
            EscapeCsvValue(ticket.AssignedToUsername ?? ""),
            EscapeCsvValue(FormatDateTime(ticket.AssignedAtUtc)),
            EscapeCsvValue(ticket.Resolution ?? ""),
            EscapeCsvValue(ticket.ResolvedByUserId?.ToString() ?? ""),
            EscapeCsvValue(ticket.ResolvedByUsername ?? ""),
            EscapeCsvValue(FormatDateTime(ticket.ResolvedAtUtc)),
            EscapeCsvValue(FormatDateTime(ticket.ClosedAtUtc)),
            EscapeCsvValue(ticket.StaffNotes ?? "")
        };
        
        csv.AppendLine(string.Join(CsvDelimiter, values));
    }
    
    #endregion
    
    #region Private Helper Methods - Formatting
    
    // Escape CSV value (handle quotes, commas, newlines)
    private string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        
        // If value contains comma, quote, or newline, wrap in quotes and escape quotes
        if (value.Contains(CsvDelimiter) || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        
        return value;
    }
    
    // Format DateTime to ISO 8601 string
    private string FormatDateTime(DateTime? dateTime)
    {
        if (!dateTime.HasValue)
        {
            return string.Empty;
        }
        
        return dateTime.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }
    
    #endregion
    
    #region Statistics Export
    
    // Generate summary statistics CSV
    public async Task<string> ExportTicketStatisticsCsvAsync(
        int totalTickets,
        int pendingTickets,
        int assignedTickets,
        int inProgressTickets,
        int resolvedTickets,
        int closedTickets)
    {
        try
        {
            var csv = new StringBuilder();
            
            csv.AppendLine("Statistic,Count");
            csv.AppendLine($"Total,{totalTickets}");
            csv.AppendLine($"Pending,{pendingTickets}");
            csv.AppendLine($"Assigned,{assignedTickets}");
            csv.AppendLine($"In Progress,{inProgressTickets}");
            csv.AppendLine($"Resolved,{resolvedTickets}");
            csv.AppendLine($"Closed,{closedTickets}");
            
            LogInfo("Generated ticket statistics CSV");
            
            return await Task.FromResult(csv.ToString());
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to export ticket statistics");
            throw;
        }
    }
    
    #endregion
}