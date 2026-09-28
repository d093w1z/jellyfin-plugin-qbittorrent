namespace Jellyfin.Plugin.QBittorrent.QBittorrent.Authentication;

/// <summary>
/// Attaches qBittorrent authentication to outgoing requests, and reacts to
/// authentication failures (e.g. an expired cookie).
/// </summary>
public interface IQBittorrentAuthenticator
{
    /// <summary>
    /// Attaches credentials to the given request. Must not log the credential value.
    /// </summary>
    /// <param name="request">The request to authenticate.</param>
    /// <param name="options">Current connection options.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    Task AuthenticateRequestAsync(HttpRequestMessage request, QBittorrentConnectionOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Called after a request comes back unauthorized. Returns <c>true</c> if the
    /// authenticator refreshed its credentials and the request should be retried once.
    /// </summary>
    /// <param name="options">Current connection options.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    Task<bool> HandleUnauthorizedAsync(QBittorrentConnectionOptions options, CancellationToken cancellationToken);
}
