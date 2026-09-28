using Jellyfin.Plugin.QBittorrent.QBittorrent;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Models;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.QBittorrent.Api;

/// <summary>
/// Categories and tags configured in qBittorrent.
/// </summary>
[Route("api/qbittorrent")]
public sealed class CategoryController : QBittorrentControllerBase
{
    private readonly IQBittorrentClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="CategoryController"/> class.
    /// </summary>
    public CategoryController(IQBittorrentClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Gets all configured categories.
    /// </summary>
    [HttpGet("categories")]
    public Task<ActionResult<IReadOnlyList<Category>>> GetCategories(CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.GetCategoriesAsync(cancellationToken));

    /// <summary>
    /// Gets all configured tags.
    /// </summary>
    [HttpGet("tags")]
    public Task<ActionResult<IReadOnlyList<Tag>>> GetTags(CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.GetTagsAsync(cancellationToken));
}
