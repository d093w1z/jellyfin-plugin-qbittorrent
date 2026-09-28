using System.Net.Http.Headers;

namespace Jellyfin.Plugin.QBittorrent.QBittorrent.Authentication;

/// <summary>
/// Bearer API-key authentication for qBittorrent &gt;= 5.2.0 (WebAPI &gt;= 2.14.1).
/// Stateless: qBittorrent supports only a single active key, so there is nothing to
/// refresh on failure — an unauthorized response means the configured key is wrong.
/// </summary>
public sealed class ApiKeyAuthenticator : IQBittorrentAuthenticator
{
    /// <inheritdoc />
    public Task AuthenticateRequestAsync(HttpRequestMessage request, QBittorrentConnectionOptions options, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> HandleUnauthorizedAsync(QBittorrentConnectionOptions options, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }
}
