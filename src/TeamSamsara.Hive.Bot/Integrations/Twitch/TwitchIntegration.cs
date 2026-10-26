using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TeamSamsara.Hive.Bot.Integrations.Twitch.Constants;
using TeamSamsara.Hive.Bot.Integrations.Twitch.DTOs;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Integrations.Twitch;

// Integration for Twitch Helix API
// Wraps all external Twitch API calls
public class TwitchIntegration : BaseIntegration
{
    private readonly IConfiguration _configuration;
    private string _accessToken = string.Empty;
    private string _clientId = string.Empty;
    private string _clientSecret = string.Empty;

    // App access tokens expire (Twitch says how long in expires_in). The token
    // used to be fetched once at startup and never renewed, so after it
    // expired every request failed with 401 and stream alerts silently
    // stopped. Now it is renewed shortly before expiry, and again if Twitch
    // ever answers 401.
    private DateTime _tokenExpiresAtUtc = DateTime.MinValue;
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private static readonly TimeSpan RenewBeforeExpiry = TimeSpan.FromHours(1);

    public override string IntegrationName => "Twitch";

    #region Constructor
    public TwitchIntegration(HttpClient httpClient, ILogger<TwitchIntegration> logger, IConfiguration configuration)
        : base(httpClient, logger)
    {
        _configuration = configuration;
    }
    #endregion

    #region Initialize Integration

    // Initialize the integration - authenticate with Twitch OAuth
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[TwitchIntegration] Initializing...");

        // Load credentials from configuration
        _clientId = _configuration[TwitchConstants.ClientIdConfigKey]
            ?? throw new InvalidOperationException("Twitch Client ID not configured");
        _clientSecret = _configuration[TwitchConstants.ClientSecretConfigKey]
            ?? throw new InvalidOperationException("Twitch Client Secret not configured");

        // Get OAuth access token (headers are added per request, see SendGetAsync)
        await AuthenticateAsync(cancellationToken);

        IsInitialized = true;
        Logger.LogInformation("[TwitchIntegration] Initialized successfully");
    }
    #endregion

    #region Public API

    // Check if integration can reach Twitch API
    public override async Task<bool> HealthCheckAsync()
    {
        try
        {
            var url = $"{TwitchConstants.ApiBaseUrl}{TwitchConstants.UsersEndpoint}";
            using var response = await SendGetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[TwitchIntegration] Health check failed");
            return false;
        }
    }

    // Get stream status for a specific user
    public async Task<TwitchStreamDto?> GetStreamStatusAsync(string username)
    {
        Logger.LogDebug("[TwitchIntegration] Getting stream status for: {Username}", username);

        // /helix/streams identifies users via "user_login"
        var url = BuildApiUrl(TwitchConstants.StreamsEndpoint, (TwitchConstants.UserLoginParam, username));
        var response = await GetApiResponseAsync<TwitchStreamResponseDto>(url);

        if (response?.data == null || response.data.Count == 0)
        {
            Logger.LogDebug("[TwitchIntegration] Stream not live for: {Username}", username);
            return null;
        }

        return response.data[0];
    }

    // Get user information
    public async Task<TwitchUserDto?> GetUserInfoAsync(string username)
    {
        Logger.LogDebug("[TwitchIntegration] Getting user info for: {Username}", username);

        // /helix/users identifies users via "login" (NOT "user_login" - that param
        // is only valid on /helix/streams and causes a 400 Bad Request here)
        var url = BuildApiUrl(TwitchConstants.UsersEndpoint, (TwitchConstants.LoginParam, username));
        var response = await GetApiResponseAsync<TwitchUserResponseDto>(url);

        if (response?.data == null || response.data.Count == 0)
        {
            Logger.LogWarning("[TwitchIntegration] User not found: {Username}", username);
            return null;
        }

        return response.data[0];
    }

    // Get follower count for a broadcaster
    public async Task<int> GetFollowerCountAsync(string username)
    {
        Logger.LogDebug("[TwitchIntegration] Getting follower count for: {Username}", username);

        var userId = await GetUserIdAsync(username);
        if (userId == null)
        {
            return 0;
        }

        var url = BuildApiUrl(
            TwitchConstants.FollowsEndpoint,
            (TwitchConstants.BroadcasterIdParam, userId),
            (TwitchConstants.FirstParam, "1")
        );

        var response = await GetApiResponseAsync<TwitchFollowerResponseDto>(url);
        return response?.total ?? 0;
    }

    // Get recent VODs for a user
    public async Task<List<TwitchVODDto>> GetRecentVODsAsync(string username, int count = 5)
    {
        Logger.LogDebug("[TwitchIntegration] Getting recent VODs for: {Username}", username);

        var userId = await GetUserIdAsync(username);
        if (userId == null)
        {
            return new List<TwitchVODDto>();
        }

        var pageSize = Math.Min(count, TwitchConstants.MaxPageSize);
        var url = BuildApiUrl(
            TwitchConstants.VideosEndpoint,
            (TwitchConstants.UserIdParam, userId),
            (TwitchConstants.FirstParam, pageSize.ToString())
        );

        var response = await GetApiResponseAsync<TwitchVODResponseDto>(url);
        return response?.data ?? new List<TwitchVODDto>();
    }

    #endregion

    #region Private Helper Methods

    // Authenticate with Twitch OAuth using Client Credentials flow
    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("[TwitchIntegration] Authenticating with Twitch OAuth...");

        var tokenUrl = $"{TwitchConstants.OAuthBaseUrl}{TwitchConstants.TokenEndpoint}";

        var requestBody = new Dictionary<string, string>
        {
            { TwitchConstants.ClientIdParam, _clientId },
            { TwitchConstants.ClientSecretParam, _clientSecret },
            { TwitchConstants.GrantTypeParam, TwitchConstants.ClientCredentialsGrantType }
        };

        var response = await HttpClient.PostAsync(
            tokenUrl,
            new FormUrlEncodedContent(requestBody),
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<TwitchOAuthResponseDto>(cancellationToken);

        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token))
        {
            throw new InvalidOperationException("Failed to obtain Twitch access token");
        }

        _accessToken = tokenResponse.access_token;
        _tokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(tokenResponse.expires_in, 0));
        Logger.LogInformation("[TwitchIntegration] Authentication successful (token valid until {ExpiresAt:u})", _tokenExpiresAtUtc);
    }

    // Get user ID by username
    private async Task<string?> GetUserIdAsync(string username)
    {
        var user = await GetUserInfoAsync(username);
        if (user == null)
        {
            Logger.LogWarning("[TwitchIntegration] User not found: {Username}", username);
            return null;
        }
        return user.id;
    }

    // Build API URL with query parameters
    private string BuildApiUrl(string endpoint, params (string key, string value)[] queryParams)
    {
        var url = $"{TwitchConstants.ApiBaseUrl}{endpoint}";

        if (queryParams.Length > 0)
        {
            var queryString = string.Join("&", queryParams.Select(p =>
                $"{Uri.EscapeDataString(p.key)}={Uri.EscapeDataString(p.value)}"));
            url = $"{url}?{queryString}";
        }

        return url;
    }

    // Make GET request and deserialize response
    private async Task<T?> GetApiResponseAsync<T>(string url) where T : class
    {
        using var response = await SendGetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    // Sends an authenticated GET. Renews the token first when it is close to
    // expiring, and once more (then retries) if Twitch still answers 401.
    private async Task<HttpResponseMessage> SendGetAsync(string url)
    {
        if (DateTime.UtcNow >= _tokenExpiresAtUtc - RenewBeforeExpiry)
        {
            await RenewTokenAsync(_accessToken, "token is about to expire");
        }

        var tokenUsed = _accessToken;
        var response = await HttpClient.SendAsync(BuildGetRequest(url, tokenUsed));
        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        await RenewTokenAsync(tokenUsed, "Twitch answered 401 Unauthorized");
        return await HttpClient.SendAsync(BuildGetRequest(url, _accessToken));
    }

    private HttpRequestMessage BuildGetRequest(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TwitchConstants.ClientIdHeader, _clientId);
        request.Headers.Add(TwitchConstants.AuthorizationHeader, $"{TwitchConstants.AuthorizationPrefix} {token}");
        return request;
    }

    // Only one renewal at a time; callers that waited for it reuse the new token
    private async Task RenewTokenAsync(string staleToken, string reason)
    {
        await _authLock.WaitAsync();
        try
        {
            if (_accessToken != staleToken && DateTime.UtcNow < _tokenExpiresAtUtc - RenewBeforeExpiry)
            {
                return; // someone else already renewed it
            }

            Logger.LogInformation("[TwitchIntegration] Renewing access token: {Reason}", reason);
            await AuthenticateAsync(CancellationToken.None);
        }
        finally
        {
            _authLock.Release();
        }
    }

    #endregion
}