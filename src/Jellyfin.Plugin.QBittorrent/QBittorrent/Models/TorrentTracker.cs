namespace Jellyfin.Plugin.QBittorrent.QBittorrent.Models;

/// <summary>
/// A tracker announced to for a given torrent.
/// </summary>
public sealed class TorrentTracker
{
    /// <summary>
    /// Gets the tracker's announce URL.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the tracker's status message.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of peers reported by this tracker.
    /// </summary>
    public int NumPeers { get; init; }

    /// <summary>
    /// Gets the tracker's last message (e.g. an error reason).
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
