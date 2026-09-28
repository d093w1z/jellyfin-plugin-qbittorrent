namespace Jellyfin.Plugin.QBittorrent.QBittorrent.Models;

/// <summary>
/// A peer connected for a given torrent.
/// </summary>
public sealed class TorrentPeer
{
    /// <summary>
    /// Gets the peer's IP address.
    /// </summary>
    public required string Ip { get; init; }

    /// <summary>
    /// Gets the peer's port.
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Gets the peer's reported client/user agent.
    /// </summary>
    public string Client { get; init; } = string.Empty;

    /// <summary>
    /// Gets the peer's two-letter country code, if known.
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// Gets the peer's progress, from 0.0 to 1.0.
    /// </summary>
    public double Progress { get; init; }

    /// <summary>
    /// Gets the current download speed from this peer, in bytes/second.
    /// </summary>
    public long DownloadSpeed { get; init; }

    /// <summary>
    /// Gets the current upload speed to this peer, in bytes/second.
    /// </summary>
    public long UploadSpeed { get; init; }
}
