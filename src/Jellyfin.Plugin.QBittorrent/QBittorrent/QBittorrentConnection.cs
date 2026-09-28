using System.Net;
using System.Text.Json;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Authentication;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.QBittorrent.QBittorrent;

/// <summary>
/// Sends authenticated HTTP requests to qBittorrent's Web API and maps failures
/// to <see cref="QBittorrentApiException"/>. Never logs credentials.
/// </summary>
public sealed class QBittorrentConnection
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly Func<QBittorrentConnectionOptions> _optionsAccessor;
    private readonly IQBittorrentAuthenticator _apiKeyAuthenticator;
    private readonly IQBittorrentAuthenticator _cookieAuthenticator;
    private readonly ILogger<QBittorrentConnection> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QBittorrentConnection"/> class.
    /// </summary>
    public QBittorrentConnection(
        HttpClient httpClient,
        Func<QBittorrentConnectionOptions> optionsAccessor,
        IQBittorrentAuthenticator apiKeyAuthenticator,
        IQBittorrentAuthenticator cookieAuthenticator,
        ILogger<QBittorrentConnection> logger)
    {
        _httpClient = httpClient;
        _optionsAccessor = optionsAccessor;
        _apiKeyAuthenticator = apiKeyAuthenticator;
        _cookieAuthenticator = cookieAuthenticator;
        _logger = logger;
    }

    /// <summary>
    /// Sends a GET request and deserializes the JSON response.
    /// </summary>
    public async Task<T> GetJsonAsync<T>(string endpoint, IReadOnlyDictionary<string, string?>? query, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, endpoint, query, content: null, cancellationToken).ConfigureAwait(false);
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return result ?? throw new QBittorrentApiException("qBittorrent returned an empty response", endpoint: endpoint);
    }

    /// <summary>
    /// Sends a GET request and returns the raw text response body.
    /// </summary>
    public async Task<string> GetStringAsync(string endpoint, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, endpoint, query: null, content: null, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a POST request with form-url-encoded content and discards the response body.
    /// </summary>
    public async Task PostFormAsync(string endpoint, IReadOnlyDictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await SendAsync(HttpMethod.Post, endpoint, query: null, content, cancellationToken).ConfigureAwait(false);
        response.Dispose();
    }

    /// <summary>
    /// Sends a POST request with form-url-encoded content and deserializes the JSON response.
    /// </summary>
    public async Task<T> PostFormJsonAsync<T>(string endpoint, IReadOnlyDictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await SendAsync(HttpMethod.Post, endpoint, query: null, content, cancellationToken).ConfigureAwait(false);
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return result ?? throw new QBittorrentApiException("qBittorrent returned an empty response", endpoint: endpoint);
    }

    /// <summary>
    /// Sends a POST request with arbitrary content (e.g. multipart form data) and discards the response body.
    /// </summary>
    public async Task PostAsync(string endpoint, HttpContent content, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, endpoint, query: null, content, cancellationToken).ConfigureAwait(false);
        response.Dispose();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string endpoint, IReadOnlyDictionary<string, string?>? query, HttpContent? content, CancellationToken cancellationToken)
    {
        var options = _optionsAccessor();
        var authenticator = options.AuthenticationMode == AuthenticationMode.ApiKey ? _apiKeyAuthenticator : _cookieAuthenticator;

        var response = await SendOnceAsync(method, endpoint, query, content, options, authenticator, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            response.Dispose();
            var refreshed = await authenticator.HandleUnauthorizedAsync(options, cancellationToken).ConfigureAwait(false);
            if (!refreshed)
            {
                throw new QBittorrentApiException("Authentication failed", HttpStatusCode.Unauthorized, endpoint);
            }

            response = await SendOnceAsync(method, endpoint, query, content, options, authenticator, cancellationToken).ConfigureAwait(false);
        }

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            response.Dispose();
            _logger.LogWarning("qBittorrent request to {Endpoint} failed with status {StatusCode}", endpoint, statusCode);
            throw MapError(statusCode, endpoint);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpMethod method,
        string endpoint,
        IReadOnlyDictionary<string, string?>? query,
        HttpContent? content,
        QBittorrentConnectionOptions options,
        IQBittorrentAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        Uri uri;
        try
        {
            uri = BuildUri(options.BaseUrl, endpoint, query);
        }
        catch (UriFormatException ex)
        {
            throw new QBittorrentApiException(
                string.IsNullOrWhiteSpace(options.BaseUrl) ? "qBittorrent is not configured" : "Invalid qBittorrent URL configured",
                endpoint: endpoint,
                innerException: ex);
        }

        using var request = new HttpRequestMessage(method, uri) { Content = content };

        await authenticator.AuthenticateRequestAsync(request, options, cancellationToken).ConfigureAwait(false);

        try
        {
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "qBittorrent is unreachable at {Endpoint}", endpoint);
            throw new QBittorrentApiException("qBittorrent unavailable", endpoint: endpoint, innerException: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "qBittorrent request to {Endpoint} timed out", endpoint);
            throw new QBittorrentApiException("qBittorrent unavailable", endpoint: endpoint, innerException: ex);
        }
    }

    private static Uri BuildUri(string baseUrl, string endpoint, IReadOnlyDictionary<string, string?>? query)
    {
        var builder = new UriBuilder(baseUrl.TrimEnd('/') + endpoint);
        if (query is { Count: > 0 })
        {
            var pairs = query
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
            builder.Query = string.Join('&', pairs);
        }

        return builder.Uri;
    }

    private static QBittorrentApiException MapError(HttpStatusCode statusCode, string endpoint)
    {
        var message = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Authentication failed",
            HttpStatusCode.NotFound => "Torrent not found",
            HttpStatusCode.UnsupportedMediaType => "Invalid torrent",
            HttpStatusCode.Conflict => "Operation rejected",
            _ => "qBittorrent unavailable"
        };

        return new QBittorrentApiException(message, statusCode, endpoint);
    }
}
