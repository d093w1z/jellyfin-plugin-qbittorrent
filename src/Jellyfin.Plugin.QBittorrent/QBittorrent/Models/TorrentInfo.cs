namespace Jellyfin.Plugin.QBittorrent.QBittorrent.Models;

/// <summary>
/// Plugin-owned representation of a qBittorrent torrent. Never a passthrough of
/// qBittorrent's raw JSON.
/// </summary>
public sealed class TorrentInfo
{
    /// <summary>
    /// Gets the torrent hash.
    /// </summary>
    public required string Hash { get; init; }

    /// <summary>
    /// Gets the torrent name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the total size in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Gets the number of bytes completed.
    /// </summary>
    public long Completed { get; init; }

    /// <summary>
    /// Gets the progress, from 0.0 to 1.0.
    /// </summary>
    public double Progress { get; init; }

    /// <summary>
    /// Gets the current download speed, in bytes/second.
    /// </summary>
    public long DownloadSpeed { get; init; }

    /// <summary>
    /// Gets the current upload speed, in bytes/second.
    /// </summary>
    public long UploadSpeed { get; init; }

    /// <summary>
    /// Gets the total bytes downloaded.
    /// </summary>
    public long Downloaded { get; init; }

    /// <summary>
    /// Gets the total bytes uploaded.
    /// </summary>
    public long Uploaded { get; init; }

    /// <summary>
    /// Gets the estimated time remaining, in seconds. qBittorrent reports a very
    /// large sentinel value (8640000) when the ETA is effectively infinite.
    /// </summary>
    public long Eta { get; init; }

    /// <summary>
    /// Gets the number of connected seeds.
    /// </summary>
    public int Seeds { get; init; }

    /// <summary>
    /// Gets the number of connected peers.
    /// </summary>
    public int Peers { get; init; }

    /// <summary>
    /// Gets the normalized state, e.g. Downloading, Seeding, Paused, Queued,
    /// Checking, Error, Completed, Unknown. See the state normalization table.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets the original, qBittorrent-specific state string (e.g. <c>pausedUP</c>),
    /// retained for diagnostics.
    /// </summary>
    public required string RawState { get; init; }

    /// <summary>
    /// Gets the torrent's category.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the save path on the qBittorrent host's filesystem.
    /// </summary>
    public string SavePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the date the torrent was added, if known.
    /// </summary>
    public DateTimeOffset? AddedOn { get; init; }

    /// <summary>
    /// Gets the torrent's tags.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
