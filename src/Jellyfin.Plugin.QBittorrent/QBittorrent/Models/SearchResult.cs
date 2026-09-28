namespace Jellyfin.Plugin.QBittorrent.QBittorrent.Models;

/// <summary>
/// A single result from a qBittorrent search-plugin query.
/// </summary>
public sealed class SearchResult
{
    /// <summary>
    /// Gets the result's file/torrent name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the size in bytes, or -1 if the search plugin didn't report one.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Gets the torrent download link (a magnet URI or an http(s) .torrent URL) —
    /// pass this straight to <see cref="IQBittorrentClient.AddTorrentAsync"/>.
    /// </summary>
    public required string DownloadLink { get; init; }

    /// <summary>
    /// Gets the number of seeders.
    /// </summary>
    public int Seeders { get; init; }

    /// <summary>
    /// Gets the number of leechers.
    /// </summary>
    public int Leechers { get; init; }

    /// <summary>
    /// Gets the site the result came from.
    /// </summary>
    public string SiteUrl { get; init; } = string.Empty;
}

/// <summary>
/// A page of results from an in-progress or finished search job.
/// </summary>
public sealed class SearchResultPage
{
    /// <summary>
    /// Gets the results returned so far.
    /// </summary>
    public required IReadOnlyList<SearchResult> Results { get; init; }

    /// <summary>
    /// Gets the job's status: <c>Running</c> or <c>Stopped</c>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the total number of results found so far. Can still grow while <see cref="Status"/> is Running.
    /// </summary>
    public int Total { get; init; }
}
