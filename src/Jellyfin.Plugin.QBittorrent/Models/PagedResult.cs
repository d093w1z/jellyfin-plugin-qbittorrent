namespace Jellyfin.Plugin.QBittorrent.Models;

/// <summary>
/// A page of results, so the browser never has to load an entire torrent list at once.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>
    /// Gets the items on this page.
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Gets the total number of items across all pages.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Gets the page size.
    /// </summary>
    public int PageSize { get; init; }
}
