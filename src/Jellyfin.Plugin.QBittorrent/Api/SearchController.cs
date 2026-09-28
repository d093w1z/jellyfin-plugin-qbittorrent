using Jellyfin.Plugin.QBittorrent.QBittorrent;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Models;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.QBittorrent.Api;

/// <summary>
/// Internet torrent search, delegated entirely to qBittorrent's own search engine
/// and whichever search plugins the administrator has installed in qBittorrent.
/// Results are added through the regular add-torrent endpoint.
/// </summary>
[Route("api/qbittorrent/search")]
public sealed class SearchController : QBittorrentControllerBase
{
    private const int MaxPatternLength = 200;

    private readonly IQBittorrentClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchController"/> class.
    /// </summary>
    public SearchController(IQBittorrentClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Gets the search plugins installed in qBittorrent.
    /// </summary>
    [HttpGet("plugins")]
    public Task<ActionResult<IReadOnlyList<SearchPlugin>>> GetPlugins(CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.GetSearchPluginsAsync(cancellationToken));

    /// <summary>
    /// Starts a search across all enabled search plugins.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SearchJob>> Start([FromBody] SearchStartBody body, CancellationToken cancellationToken)
    {
        var pattern = body?.Pattern?.Trim();
        if (string.IsNullOrEmpty(pattern) || pattern.Length > MaxPatternLength)
        {
            return BadRequest($"pattern is required and must be at most {MaxPatternLength} characters.");
        }

        return await ExecuteAsync(async () => new SearchJob { Id = await _client.StartSearchAsync(pattern, cancellationToken).ConfigureAwait(false) }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a search job's results so far and whether it is still running.
    /// </summary>
    [HttpGet("{id:int}")]
    public Task<ActionResult<SearchResultPage>> GetResults(int id, CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.GetSearchResultsAsync(id, cancellationToken));

    /// <summary>
    /// Stops and discards a search job.
    /// </summary>
    [HttpDelete("{id:int}")]
    public Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.DeleteSearchAsync(id, cancellationToken));

    /// <summary>
    /// Request body for <see cref="Start"/>.
    /// </summary>
    public sealed class SearchStartBody
    {
        /// <summary>
        /// Gets or sets the search text.
        /// </summary>
        public string? Pattern { get; set; }
    }

    /// <summary>
    /// A started search job.
    /// </summary>
    public sealed class SearchJob
    {
        /// <summary>
        /// Gets the qBittorrent search job id.
        /// </summary>
        public int Id { get; init; }
    }
}
