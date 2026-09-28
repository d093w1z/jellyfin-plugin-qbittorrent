namespace Jellyfin.Plugin.QBittorrent.Models;

/// <summary>
/// The result of checking connectivity to the configured qBittorrent instance.
/// Never includes credentials.
/// </summary>
public sealed class ConnectionStatus
{
    /// <summary>
    /// Gets a value indicating whether the plugin is currently connected to qBittorrent.
    /// </summary>
    public bool Connected { get; init; }

    /// <summary>
    /// Gets the qBittorrent version string, when connected.
    /// </summary>
    public string? QBittorrentVersion { get; init; }

    /// <summary>
    /// Gets the authentication mode currently configured.
    /// </summary>
    public required string AuthenticationMode { get; init; }

    /// <summary>
    /// Gets a safe, user-facing error message when not connected.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
