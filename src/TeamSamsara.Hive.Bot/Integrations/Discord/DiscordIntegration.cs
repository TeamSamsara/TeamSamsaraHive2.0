using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Integrations.Discord.Constants;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Integrations.Discord;

// Integration for Discord Bot using Discord.Net
// Wraps Discord WebSocket client with reconnection logic
public class DiscordIntegration : BaseIntegration
{
    private readonly IConfiguration _configuration;
    private DiscordSocketClient _client = null!;
    private TaskCompletionSource<bool> _readyTaskSource = null!;
    private int _reconnectAttempts;
    private bool _isReconnecting;
    private string _botToken = string.Empty;

    public override string IntegrationName => "Discord";

    #region Constructor

    public DiscordIntegration(HttpClient httpClient, ILogger<DiscordIntegration> logger, IConfiguration configuration)
        : base(httpClient, logger)
    {
        _configuration = configuration;
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

    // Send an embed to a specific channel
    public async Task SendEmbedAsync(ulong channelId, Embed embed)
    {
        Logger.LogDebug($"{DiscordConstants.LogPrefix} Sending embed to channel: {channelId}");

        var channel = await GetChannelAsync(channelId);

        if (channel is not IMessageChannel messageChannel)
        {
            throw new InvalidOperationException($"Channel {channelId} is not a message channel");
        }

        await messageChannel.SendMessageAsync(embed: embed);
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

    #region Reconnection Logic

    // Handle client connected event
    private Task OnConnected()
    {
        Logger.LogInformation($"{DiscordConstants.LogPrefix} Connected to Discord");

        // Reset reconnection state on successful connection
        _reconnectAttempts = 0;
        _isReconnecting = false;

        return Task.CompletedTask;
    }

    // Handle client disconnected event
    private Task OnDisconnected(Exception exception)
    {
        Logger.LogWarning(exception, $"{DiscordConstants.LogPrefix} {DiscordConstants.DisconnectedMessage}");

        // Start reconnection process if not already reconnecting
        if (!_isReconnecting)
        {
            _isReconnecting = true;
            _ = Task.Run(async () => await AttemptReconnectAsync());
        }

        return Task.CompletedTask;
    }

    // Attempt to reconnect to Discord with exponential backoff
    private async Task AttemptReconnectAsync()
    {
        while (_reconnectAttempts < DiscordConstants.MaxReconnectAttempts)
        {
            _reconnectAttempts++;

            Logger.LogInformation(
                DiscordConstants.ReconnectingMessage,
                _reconnectAttempts,
                DiscordConstants.MaxReconnectAttempts
            );

            try
            {
                // Calculate delay with exponential backoff
                var delay = CalculateReconnectDelay(_reconnectAttempts);
                await Task.Delay(delay);

                // Attempt reconnection
                await _client.StartAsync();

                // Wait a moment to see if connection succeeds
                await Task.Delay(2000);

                if (_client.ConnectionState == ConnectionState.Connected)
                {
                    Logger.LogInformation($"{DiscordConstants.LogPrefix} {DiscordConstants.ReconnectSuccessMessage}");
                    _reconnectAttempts = 0;
                    _isReconnecting = false;
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"{DiscordConstants.LogPrefix} Reconnection attempt failed");
            }
        }

        // Max attempts reached
        Logger.LogCritical(
            DiscordConstants.ReconnectFailedMessage,
            DiscordConstants.MaxReconnectAttempts
        );

        IsInitialized = false;
        _isReconnecting = false;
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

        // Message events (stubbed for Phase 2)
        _client.MessageReceived += OnMessageReceived;
        _client.ReactionAdded += OnReactionAdded;
        _client.ThreadCreated += OnThreadCreated;

        // Discord.Net internal logging
        _client.Log += LogDiscordMessage;
    }

    // Calculate reconnection delay with exponential backoff
    private int CalculateReconnectDelay(int attemptNumber)
    {
        var delay = (int)(DiscordConstants.InitialReconnectDelayMs *
        Math.Pow(DiscordConstants.ReconnectBackoffMultiplier, attemptNumber - 1));

        return Math.Min(delay, DiscordConstants.MaxReconnectDelayMs);
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