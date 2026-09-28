namespace Jellyfin.Plugin.QBittorrent.Models;

/// <summary>
/// A request to add a torrent to qBittorrent, either by magnet/URL or by
/// uploading a <c>.torrent</c> file's raw bytes. Exactly one of
/// <see cref="MagnetUri"/> or <see cref="TorrentFile"/> must be set.
/// </summary>
public sealed class AddTorrentRequest
{
    /// <summary>
    /// Gets the magnet URI or HTTP(S) torrent URL to add.
    /// </summary>
    public string? MagnetUri { get; init; }

    /// <summary>
    /// Gets the raw bytes of an uploaded <c>.torrent</c> file.
    /// </summary>
    public byte[]? TorrentFile { get; init; }

    /// <summary>
    /// Gets the file name of the uploaded <c>.torrent</c> file, for the multipart request.
    /// </summary>
    public string? TorrentFileName { get; init; }

    /// <summary>
    /// Gets the save path for the downloaded content. qBittorrent, not the plugin,
    /// interprets this path — the plugin does not resolve or validate it against
    /// the local filesystem.
    /// </summary>
    public string? SavePath { get; init; }

    /// <summary>
    /// Gets the category to assign.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets a value indicating whether the torrent should start downloading immediately.
    /// </summary>
    public bool StartImmediately { get; init; } = true;
}
