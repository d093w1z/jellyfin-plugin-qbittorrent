namespace Jellyfin.Plugin.QBittorrent.QBittorrent;

/// <summary>
/// The subset of plugin configuration the qBittorrent protocol layer needs.
/// Kept independent of <see cref="PluginConfiguration"/> being read from a live
/// Jellyfin plugin instance, so the client can be constructed and tested without Jellyfin.
/// </summary>
public sealed class QBittorrentConnectionOptions
{
    /// <summary>
    /// Gets the base URL of the qBittorrent Web API, e.g. <c>http://qbittorrent:8080</c>.
    /// </summary>
    public required string BaseUrl { get; init; }

    /// <summary>
    /// Gets the authentication strategy to use.
    /// </summary>
    public AuthenticationMode AuthenticationMode { get; init; }

    /// <summary>
    /// Gets the qBittorrent API key.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the qBittorrent username (fallback authentication).
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Gets the qBittorrent password.
    /// </summary>
    public string Password { get; init; } = string.Empty;
}
