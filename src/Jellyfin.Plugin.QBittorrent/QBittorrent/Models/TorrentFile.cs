namespace Jellyfin.Plugin.QBittorrent.QBittorrent.Models;

/// <summary>
/// A single file within a torrent.
/// </summary>
public sealed class TorrentFile
{
    /// <summary>
    /// Gets the file's index within the torrent.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Gets the file name (path relative to the torrent's save path).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Gets the download progress, from 0.0 to 1.0.
    /// </summary>
    public double Progress { get; init; }

    /// <summary>
    /// Gets the download priority (0 = do not download).
    /// </summary>
    public int Priority { get; init; }
}
