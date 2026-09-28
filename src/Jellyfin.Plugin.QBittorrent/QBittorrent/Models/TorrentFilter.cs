namespace Jellyfin.Plugin.QBittorrent.QBittorrent.Models;

/// <summary>
/// Filter/sort criteria for listing torrents. <see cref="Category"/> and
/// <see cref="Sort"/>/<see cref="Reverse"/> are forwarded to qBittorrent's own
/// <c>/torrents/info</c> filtering; <see cref="Search"/> and <see cref="State"/>
/// (a normalized state, not qBittorrent's raw one) are applied afterwards, since
/// qBittorrent has no equivalent native filter for them.
/// </summary>
public sealed class TorrentFilter
{
    /// <summary>
    /// Gets a case-insensitive substring to match against the torrent name.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Gets the normalized state to filter by (e.g. "Downloading", "Seeding").
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Gets the category to filter by.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the tag to filter by.
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// Gets the qBittorrent field to sort by, e.g. "added_on", "name", "size".
    /// </summary>
    public string? Sort { get; init; }

    /// <summary>
    /// Gets a value indicating whether the sort order is descending.
    /// </summary>
    public bool Reverse { get; init; }
}
