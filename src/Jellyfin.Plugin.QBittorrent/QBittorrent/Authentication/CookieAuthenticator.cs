namespace Jellyfin.Plugin.QBittorrent.QBittorrent.Authentication;

/// <summary>
/// Username/password cookie-based authentication, for qBittorrent versions that
/// predate API-key support. Logs in once, reuses the returned session cookie,
/// and re-authenticates on demand when the server rejects it as expired.
/// </summary>
public sealed class CookieAuthenticator : IQBittorrentAuthenticator
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private string? _cookie;

    /// <summary>
    /// Initializes a new instance of the <see cref="CookieAuthenticator"/> class.
    /// </summary>
    /// <param name="httpClient">The shared HTTP client used to reach qBittorrent.</param>
    public CookieAuthenticator(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task AuthenticateRequestAsync(HttpRequestMessage request, QBittorrentConnectionOptions options, CancellationToken cancellationToken)
    {
        if (_cookie is null)
        {
            await LoginAsync(options, cancellationToken).ConfigureAwait(false);
        }

        if (_cookie is not null)
        {
            request.Headers.Add("Cookie", _cookie);
        }
    }

    /// <inheritdoc />
    public async Task<bool> HandleUnauthorizedAsync(QBittorrentConnectionOptions options, CancellationToken cancellationToken)
    {
        _cookie = null;
        await LoginAsync(options, cancellationToken).ConfigureAwait(false);
        return _cookie is not null;
    }

    private async Task LoginAsync(QBittorrentConnectionOptions options, CancellationToken cancellationToken)
    {
        await _loginLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cookie is not null)
            {
                // Another caller already logged in while we were waiting.
                return;
            }

            if (string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                throw new QBittorrentApiException("qBittorrent is not configured", endpoint: "/api/v2/auth/login");
            }

            HttpResponseMessage response;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl.TrimEnd('/')}/api/v2/auth/login")
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["username"] = options.Username,
                        ["password"] = options.Password
                    })
                };

                response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
            {
                throw new QBittorrentApiException("qBittorrent unavailable", endpoint: "/api/v2/auth/login", innerException: ex);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new QBittorrentApiException("Authentication failed", response.StatusCode, "/api/v2/auth/login");
                }

                if (response.Headers.TryGetValues("Set-Cookie", out var setCookieValues))
                {
                    var sid = setCookieValues.FirstOrDefault(v => v.StartsWith("SID=", StringComparison.OrdinalIgnoreCase));
                    if (sid is not null)
                    {
                        _cookie = sid.Split(';', 2)[0];
                    }
                }

                if (_cookie is null)
                {
                    throw new QBittorrentApiException("Authentication failed", response.StatusCode, "/api/v2/auth/login");
                }
            }
        }
        finally
        {
            _loginLock.Release();
        }
    }
}
