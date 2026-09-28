namespace Jellyfin.Plugin.QBittorrent.QBittorrent.Models;

/// <summary>
/// Global transfer statistics for the qBittorrent instance.
/// </summary>
public sealed class TransferInfo
{
    /// <summary>
    /// Gets the current global download speed, in bytes/second.
    /// </summary>
    public long DownloadSpeed { get; init; }

    /// <summary>
    /// Gets the current global upload speed, in bytes/second.
    /// </summary>
    public long UploadSpeed { get; init; }

    /// <summary>
    /// Gets the total bytes downloaded since qBittorrent started.
    /// </summary>
    public long DownloadedTotal { get; init; }

    /// <summary>
    /// Gets the total bytes uploaded since qBittorrent started.
    /// </summary>
    public long UploadedTotal { get; init; }

    /// <summary>
    /// Gets the number of DHT nodes.
    /// </summary>
    public int DhtNodes { get; init; }

    /// <summary>
    /// Gets the connection status (e.g. connected, firewalled, disconnected).
    /// </summary>
    public string ConnectionStatus { get; init; } = string.Empty;

    /// <summary>
    /// Gets the global download speed limit, in bytes/second (0 = unlimited).
    /// </summary>
    public long DownloadSpeedLimit { get; init; }

    /// <summary>
    /// Gets the global upload speed limit, in bytes/second (0 = unlimited).
    /// </summary>
    public long UploadSpeedLimit { get; init; }
}
