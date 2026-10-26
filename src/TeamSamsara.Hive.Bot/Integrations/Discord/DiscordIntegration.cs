using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Integrations.Discord.Constants;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Integrations.Discord;

// Integration for Discord Bot using Discord.Net
// Wraps the Discord WebSocket client. Reconnecting is left entirely to
// Discord.Net; this class only watches for a connection that stays down.
public class DiscordIntegration : BaseIntegration
{
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _lifetime;
    private DiscordSocketClient _client = null!;
    private TaskCompletionSource<bool> _readyTaskSource = null!;
    private string _botToken = string.Empty;

    // UTC ticks of when the current outage started; 0 while connected.
    // Written from Discord.Net's event threads and read by the watchdog,
    // so it is only touched through Interlocked.
    private long _disconnectedSinceTicks;
    private readonly CancellationTokenSource _watchdogCts = new();
    public DiscordSocketClient Client => _client;

    // Every system registers its slash commands, buttons, menus and modals
    // here instead of subscribing to the client's events directly.
    public InteractionRouter Interactions { get; }
    public override string IntegrationName => "Discord";

    #region Constructor

    public DiscordIntegration(
        HttpClient httpClient,
        ILogger<DiscordIntegration> logger,
        IConfiguration configuration,
        IHostApplicationLifetime lifetime)
        : base(httpClient, logger)
    {
        _configuration = configuration;
        _lifetime = lifetime;
        Interactions = new InteractionRouter(logger);

        // Stop watching once the app is shutting down on purpose
        _lifetime.ApplicationStopping.Register(() => _watchdogCts.Cancel());
    }

    #endregion

    #region Initialize Integration

    // Initialize the integration - connect Discord bot
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation($"{DiscordConstants.LogPrefix} {DiscordConstants.InitializingMessage}");

        // Load bot token from configuration
        _botToken = _configuration[DiscordConstants.BotTokenConfigKey]
            ?? throw new InvalidOperationException("Discord bot token not configured");

        // Create Discord client with gateway intents
        _client = CreateDiscordClient();

        // Register event handlers
        RegisterEventHandlers();

        // Create TaskCompletionSource to wait for Ready event
        _readyTaskSource = new TaskCompletionSource<bool>();

        // Login and start client
        await _client.LoginAsync(TokenType.Bot, _botToken);
        await _client.StartAsync();

        // Wait for Ready event with timeout
        var readyTask = _readyTaskSource.Task;
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(DiscordConstants.ReadyTimeoutSeconds), cancellationToken);

        var completedTask = await Task.WhenAny(readyTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            throw new TimeoutException(DiscordConstants.ReadyTimeoutMessage);
        }

        IsInitialized = true;

        // Start watching for a connection that stays down
        _ = RunWatchdogAsync(_watchdogCts.Token);

        Logger.LogInformation($"{DiscordConstants.LogPrefix} Initialized successfully");
    }

    #endregion

    #region Public API

    // Check if integration is connected to Discord
    public override async Task<bool> HealthCheckAsync()
    {
        await Task.CompletedTask;

        if (_client == null || _client.ConnectionState != ConnectionState.Connected || !IsInitialized)
        {
            Logger.LogWarning($"{DiscordConstants.LogPrefix} {DiscordConstants.HealthCheckFailedMessage}");
            return false;
        }

        return true;
    }

    // Send an embed to a specific channel, optionally with plain-text content
    // above it (e.g. "<@&ROLEID> Hey, X is live!"). AllowedMentions.All is
    // passed explicitly so role/user mentions in `content` actually notify -
    // Discord.Net does not guarantee mentions ping by default.
    public async Task SendEmbedAsync(ulong channelId, Embed embed, string? content = null)
    {
        Logger.LogDebug($"{DiscordConstants.LogPrefix} Sending embed to channel: {channelId}");

        var channel = await GetChannelAsync(channelId);

        if (channel is not IMessageChannel messageChannel)
        {
            throw new InvalidOperationException($"Channel {channelId} is not a message channel");
        }

        await messageChannel.SendMessageAsync(text: content, embed: embed, allowedMentions: AllowedMentions.All);
    }

    // Send a direct message to a user
    public async Task SendDirectMessageAsync(ulong userId, string content)
    {
        Logger.LogDebug($"{DiscordConstants.LogPrefix} Sending DM to user: {userId}");

        var user = await GetUserAsync(userId);

        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found");
        }

        var dmChannel = await user.CreateDMChannelAsync();
        await dmChannel.SendMessageAsync(content);
    }

    // Get a channel by ID
    public async Task<IChannel?> GetChannelAsync(ulong channelId)
    {
        await Task.CompletedTask;

        var channel = _client.GetChannel(channelId);

        if (channel == null)
        {
            Logger.LogWarning($"{DiscordConstants.LogPrefix} Channel not found: {channelId}");
        }

        return channel;
    }

    // Get a user by ID
    public async Task<IUser?> GetUserAsync(ulong userId)
    {
        await Task.CompletedTask;

        var user = _client.GetUser(userId);

        if (user == null)
        {
            Logger.LogWarning($"{DiscordConstants.LogPrefix} User not found: {userId}");
        }

        return user;
    }

    #endregion

    #region Event Handlers (Stubbed for Phase 2)

    // Handle Discord Ready event
    private Task OnReady()
    {
        Logger.LogInformation($"{DiscordConstants.LogPrefix} {DiscordConstants.ConnectedMessage}");

        // Signal that initialization is complete
        _readyTaskSource?.TrySetResult(true);

        return Task.CompletedTask;
    }

    // Handle message received event
    private Task OnMessageReceived(SocketMessage message)
    {
        Logger.LogDebug($"{DiscordConstants.LogPrefix} Event: MessageReceived");
        // TODO Phase 2: Emit EventBus event
        return Task.CompletedTask;
    }

    // Handle reaction added event
    private Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        Logger.LogDebug($"{DiscordConstants.LogPrefix} Event: ReactionAdded");
        // TODO Phase 2: Emit EventBus event
        return Task.CompletedTask;
    }

    // Handle thread created event
    private Task OnThreadCreated(SocketThreadChannel thread)
    {
        Logger.LogDebug($"{DiscordConstants.LogPrefix} Event: ThreadCreated");
        // TODO Phase 2: Emit EventBus event
        return Task.CompletedTask;
    }

    #endregion

    #region Connection Watchdog

    // Discord.Net reconnects by itself whenever the gateway drops. Discord asks
    // every bot to reconnect every few hours, so disconnects are normal.
    //
    // We must NOT call _client.StartAsync() ourselves when that happens: it
    // races the library's own reconnect and can leave its connection manager
    // broken (ObjectDisposedException / "Cannot start an already running
    // client"), which is what silently took the bot offline on 2026-10-26.
    //
    // So these handlers only record the outage. If it lasts longer than
    // DisconnectedRestartMinutes, the watchdog stops the app with exit code 1
    // and the host (Render) starts a fresh process.

    // Handle client connected event
    private Task OnConnected()
    {
        Interlocked.Exchange(ref _disconnectedSinceTicks, 0);
        Logger.LogInformation($"{DiscordConstants.LogPrefix} Connected to Discord");
        return Task.CompletedTask;
    }

    // Handle client disconnected event
    private Task OnDisconnected(Exception exception)
    {
        // Keep the time of the FIRST disconnect if several fire in a row
        Interlocked.CompareExchange(ref _disconnectedSinceTicks, DateTime.UtcNow.Ticks, 0);
        Logger.LogWarning(exception, $"{DiscordConstants.LogPrefix} {DiscordConstants.DisconnectedMessage}");
        return Task.CompletedTask;
    }

    // Periodically check whether the bot has been disconnected for too long
    private async Task RunWatchdogAsync(CancellationToken cancellationToken)
    {
        var threshold = TimeSpan.FromMinutes(DiscordConstants.DisconnectedRestartMinutes);
        var interval = TimeSpan.FromSeconds(DiscordConstants.WatchdogCheckIntervalSeconds);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationToken);

                var sinceTicks = Interlocked.Read(ref _disconnectedSinceTicks);
                if (sinceTicks == 0)
                {
                    continue; // connected
                }

                var downFor = DateTime.UtcNow - new DateTime(sinceTicks, DateTimeKind.Utc);
                if (downFor < threshold)
                {
                    continue; // give Discord.Net time to reconnect
                }

                Logger.LogCritical($"{DiscordConstants.LogPrefix} {DiscordConstants.WatchdogRestartMessage}", downFor.TotalMinutes);

                IsInitialized = false;
                Environment.ExitCode = 1;   // non-zero so the host treats it as a crash and restarts
                _lifetime.StopApplication();
                return;
            }
        }
        catch (OperationCanceledException)
        {
            // App is shutting down
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"{DiscordConstants.LogPrefix} Connection watchdog stopped unexpectedly");
        }
    }

    #endregion

    #region Private Helper Methods

    // Create Discord client with configured gateway intents
    private DiscordSocketClient CreateDiscordClient()
    {
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds |
                           GatewayIntents.GuildMessages |
                           GatewayIntents.MessageContent |
                           GatewayIntents.DirectMessages |
                           GatewayIntents.GuildMessageReactions
        };

        return new DiscordSocketClient(config);
    }

    // Register all Discord event handlers
    private void RegisterEventHandlers()
    {
        // Lifecycle events
        _client.Ready += OnReady;
        _client.Connected += OnConnected;
        _client.Disconnected += OnDisconnected;

        // Interactions (slash commands, buttons, menus, modals, autocomplete)
        // are dispatched off the gateway task by the router
        Interactions.Attach(_client);

        // Message events (stubbed for Phase 2)
        _client.MessageReceived += OnMessageReceived;
        _client.ReactionAdded += OnReactionAdded;
        _client.ThreadCreated += OnThreadCreated;

        // Discord.Net internal logging
        _client.Log += LogDiscordMessage;
    }

    // Forward Discord.Net log messages to Microsoft logging framework
    private Task LogDiscordMessage(LogMessage message)
    {
        var logLevel = ConvertDiscordLogLevel(message.Severity);
        Logger.Log(logLevel, message.Exception, message.Message);
        return Task.CompletedTask;
    }

    // Convert Discord.Net log severity to Microsoft LogLevel
    private LogLevel ConvertDiscordLogLevel(LogSeverity severity)
    {
        return severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };
    }

    #endregion
}