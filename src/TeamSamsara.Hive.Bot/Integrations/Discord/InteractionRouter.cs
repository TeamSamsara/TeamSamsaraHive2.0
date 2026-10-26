using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Integrations.Discord.Constants;

namespace TeamSamsara.Hive.Bot.Integrations.Discord;

// One place that receives every Discord interaction (slash commands, buttons,
// select menus, modals, autocomplete) and hands it to the system that owns it.
//
// Why it exists:
//   1. Discord.Net raises these events on its gateway (connection) task. A
//      handler that takes a few seconds there delays heartbeats and can get
//      the bot disconnected ("A SlashCommandExecuted handler is blocking the
//      gateway task"). The router acknowledges the event immediately and runs
//      the handler in the background.
//   2. Before, every system listened to every interaction and filtered it
//      itself. Now each system registers only what it owns:
//        - slash commands and autocomplete by command name ("config", "ticket")
//        - buttons, select menus and modals by custom-id prefix ("ticket_", "proto:")
//   3. If a handler throws before answering, the user gets an error message
//      instead of Discord's "The application did not respond".
public class InteractionRouter
{
    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<string, Func<SocketSlashCommand, Task>> _slashCommands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Func<SocketAutocompleteInteraction, Task>> _autocomplete = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Func<SocketMessageComponent, Task>> _components = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Func<SocketModal, Task>> _modals = new(StringComparer.Ordinal);

    public InteractionRouter(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Registration

    // Handle every use of the slash command with this name
    public void OnSlashCommand(string commandName, Func<SocketSlashCommand, Task> handler) =>
        Register(_slashCommands, commandName, handler, "slash command");

    // Answer autocomplete requests for options of this slash command
    public void OnAutocomplete(string commandName, Func<SocketAutocompleteInteraction, Task> handler) =>
        Register(_autocomplete, commandName, handler, "autocomplete");

    // Handle buttons and select menus whose custom id starts with this prefix
    public void OnComponent(string customIdPrefix, Func<SocketMessageComponent, Task> handler) =>
        Register(_components, customIdPrefix, handler, "component prefix");

    // Handle modal submissions whose custom id starts with this prefix
    public void OnModal(string customIdPrefix, Func<SocketModal, Task> handler) =>
        Register(_modals, customIdPrefix, handler, "modal prefix");

    private void Register<THandler>(ConcurrentDictionary<string, THandler> map, string key, THandler handler, string kind)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required", nameof(key));
        ArgumentNullException.ThrowIfNull(handler);

        if (!map.TryAdd(key, handler))
        {
            map[key] = handler;
            _logger.LogWarning("{Prefix} {Kind} '{Key}' was registered twice; the later handler replaces the earlier one",
                DiscordConstants.LogPrefix, kind, key);
        }
    }

    #endregion

    #region Wiring to the Discord client

    // Called once by DiscordIntegration when the client is created
    public void Attach(DiscordSocketClient client)
    {
        client.SlashCommandExecuted += cmd =>
            Dispatch(cmd, Find(_slashCommands, cmd.Data.Name, exact: true), $"/{cmd.Data.Name}");

        client.AutocompleteExecuted += ac =>
            Dispatch(ac, Find(_autocomplete, ac.Data.CommandName, exact: true), $"autocomplete /{ac.Data.CommandName}", replyOnError: false);

        client.ButtonExecuted += component =>
            Dispatch(component, Find(_components, component.Data.CustomId, exact: false), $"button {component.Data.CustomId}");

        client.SelectMenuExecuted += component =>
            Dispatch(component, Find(_components, component.Data.CustomId, exact: false), $"select {component.Data.CustomId}");

        client.ModalSubmitted += modal =>
            Dispatch(modal, Find(_modals, modal.Data.CustomId, exact: false), $"modal {modal.Data.CustomId}");
    }

    // Exact match for command names; longest matching prefix for custom ids
    private static Func<T, Task>? Find<T>(ConcurrentDictionary<string, Func<T, Task>> map, string key, bool exact)
    {
        if (exact)
        {
            return map.TryGetValue(key, out var handler) ? handler : null;
        }

        Func<T, Task>? best = null;
        var bestLength = -1;
        foreach (var (prefix, handler) in map)
        {
            if (prefix.Length > bestLength && key.StartsWith(prefix, StringComparison.Ordinal))
            {
                best = handler;
                bestLength = prefix.Length;
            }
        }

        return best;
    }

    // Returns to Discord.Net immediately; the handler runs in the background
    private Task Dispatch<T>(T interaction, Func<T, Task>? handler, string description, bool replyOnError = true)
        where T : SocketInteraction
    {
        if (handler == null)
        {
            _logger.LogDebug("{Prefix} No handler registered for {Interaction}", DiscordConstants.LogPrefix, description);
            return Task.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await handler(interaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Handler for {Interaction} failed", DiscordConstants.LogPrefix, description);
                if (replyOnError)
                {
                    await TryReportErrorAsync(interaction);
                }
            }
        });

        return Task.CompletedTask;
    }

    private async Task TryReportErrorAsync(SocketInteraction interaction)
    {
        const string message = "Something went wrong while handling that. The error was logged; please try again.";
        try
        {
            if (interaction.HasResponded)
            {
                await interaction.FollowupAsync(message, ephemeral: true);
            }
            else
            {
                await interaction.RespondAsync(message, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            // The interaction may have expired; nothing more we can do
            _logger.LogDebug(ex, "{Prefix} Could not report the error to the user", DiscordConstants.LogPrefix);
        }
    }

    #endregion
}
