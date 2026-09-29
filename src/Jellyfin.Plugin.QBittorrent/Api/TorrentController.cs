using System.Text.Json;
using Jellyfin.Plugin.QBittorrent.Models;
using Jellyfin.Plugin.QBittorrent.QBittorrent;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Models;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.QBittorrent.Api;

/// <summary>
/// Torrent listing, detail, and management: list (paginated, filtered, searched),
/// per-torrent detail/files/peers/trackers, adding, and control actions. Detail
/// data is only fetched when explicitly requested — the list endpoint never
/// triggers N detail calls.
/// </summary>
[Route("api/qbittorrent/torrents")]
public sealed class TorrentController : QBittorrentControllerBase
{
    private const int MaxPageSize = 200;
    private const long MaxTorrentFileBytes = 10 * 1024 * 1024;

    private readonly IQBittorrentClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="TorrentController"/> class.
    /// </summary>
    public TorrentController(IQBittorrentClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Adds a torrent by magnet/URL (JSON body) or by uploading a <c>.torrent</c>
    /// file (multipart form: <c>torrentFile</c>, <c>savePath</c>, <c>category</c>,
    /// <c>startImmediately</c>).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> AddTorrent(CancellationToken cancellationToken)
    {
        AddTorrentRequest request;

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            var file = form.Files.GetFile("torrentFile");
            byte[]? fileBytes = null;

            if (file is not null)
            {
                if (file.Length > MaxTorrentFileBytes)
                {
                    return BadRequest("Torrent file is too large.");
                }

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
                fileBytes = stream.ToArray();
            }

            request = new AddTorrentRequest
            {
                MagnetUri = form["magnetUri"].ToString() is { Length: > 0 } magnetUri ? magnetUri : null,
                TorrentFile = fileBytes,
                TorrentFileName = file?.FileName,
                SavePath = form["savePath"].ToString(),
                Category = form["category"].ToString(),
                StartImmediately = !bool.TryParse(form["startImmediately"], out var startForm) || startForm
            };
        }
        else
        {
            var body = await JsonSerializer.DeserializeAsync<AddTorrentBody>(Request.Body, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (body is null || string.IsNullOrWhiteSpace(body.MagnetUri))
            {
                return BadRequest("magnetUri is required.");
            }

            request = new AddTorrentRequest
            {
                MagnetUri = body.MagnetUri,
                SavePath = body.SavePath,
                Category = body.Category,
                StartImmediately = body.StartImmediately
            };
        }

        if (string.IsNullOrEmpty(request.MagnetUri) && request.TorrentFile is null)
        {
            return BadRequest("Either a magnet/URL or a torrent file is required.");
        }

        return await ExecuteAsync(() => _client.AddTorrentAsync(request, cancellationToken));
    }

    /// <summary>
    /// Replaces a torrent's tags.
    /// </summary>
    [HttpPut("{hash}/tags")]
    public async Task<ActionResult> SetTags(string hash, [FromBody] SetTagsBody body, CancellationToken cancellationToken)
    {
        var tags = (body?.Tags ?? []).Select(t => t?.Trim() ?? string.Empty).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (tags.Any(t => t.Contains(',', StringComparison.Ordinal)))
        {
            return BadRequest("Tags cannot contain commas.");
        }

        return await ExecuteAsync(() => _client.SetTagsAsync(hash, tags, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the priority of files within a torrent: 0 = skip, 1 = normal, 6 = high, 7 = maximum.
    /// </summary>
    [HttpPost("{hash}/files/priority")]
    public async Task<ActionResult> SetFilePriority(string hash, [FromBody] FilePriorityBody body, CancellationToken cancellationToken)
    {
        if (body?.Files is not { Count: > 0 } files || files.Any(i => i < 0) || body.Priority is not (0 or 1 or 6 or 7))
        {
            return BadRequest("files must be a non-empty list of file indexes and priority one of 0, 1, 6, 7.");
        }

        return await ExecuteAsync(() => _client.SetFilePriorityAsync(hash, files, body.Priority, cancellationToken)).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private sealed class AddTorrentBody
    {
        public string? MagnetUri { get; set; }

        public string? SavePath { get; set; }

        public string? Category { get; set; }

        public bool StartImmediately { get; set; } = true;
    }

    /// <summary>
    /// Lists torrents, optionally filtered/sorted/searched and paginated.
    /// </summary>
    [HttpGet]
    public Task<ActionResult<PagedResult<TorrentInfo>>> GetTorrents(
        [FromQuery] string? search,
        [FromQuery] string? state,
        [FromQuery] string? category,
        [FromQuery] string? tag,
        [FromQuery] string? sort,
        [FromQuery] string? order,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var filter = new TorrentFilter
        {
            Search = search,
            State = state,
            Category = category,
            Tag = tag,
            Sort = sort,
            Reverse = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase)
        };

        return ExecuteAsync(async () =>
        {
            var all = await _client.GetTorrentsAsync(filter, cancellationToken).ConfigureAwait(false);
            var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return new PagedResult<TorrentInfo> { Items = items, Total = all.Count, Page = page, PageSize = pageSize };
        });
    }

    /// <summary>
    /// Gets a single torrent's detail.
    /// </summary>
    [HttpGet("{hash}")]
    public async Task<ActionResult<TorrentInfo>> GetTorrent(string hash, CancellationToken cancellationToken)
    {
        try
        {
            var torrent = await _client.GetTorrentAsync(hash, cancellationToken).ConfigureAwait(false);
            return torrent is null ? NotFound() : Ok(torrent);
        }
        catch (QBittorrentApiException ex)
        {
            return MapError(ex);
        }
    }

    /// <summary>
    /// Gets the files within a torrent. Lazy-loaded — only called when the Files tab is opened.
    /// </summary>
    [HttpGet("{hash}/files")]
    public Task<ActionResult<IReadOnlyList<TorrentFile>>> GetFiles(string hash, CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.GetTorrentFilesAsync(hash, cancellationToken));

    /// <summary>
    /// Gets the peers connected for a torrent. Lazy-loaded — only called when the Peers tab is opened.
    /// </summary>
    [HttpGet("{hash}/peers")]
    public Task<ActionResult<IReadOnlyList<TorrentPeer>>> GetPeers(string hash, CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.GetTorrentPeersAsync(hash, cancellationToken));

    /// <summary>
    /// Gets the trackers for a torrent. Lazy-loaded — only called when the Trackers tab is opened.
    /// </summary>
    [HttpGet("{hash}/trackers")]
    public Task<ActionResult<IReadOnlyList<TorrentTracker>>> GetTrackers(string hash, CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.GetTorrentTrackersAsync(hash, cancellationToken));

    /// <summary>
    /// Pauses a torrent.
    /// </summary>
    [HttpPost("{hash}/pause")]
    public Task<ActionResult> Pause(string hash, CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.PauseAsync([hash], cancellationToken));

    /// <summary>
    /// Resumes a torrent.
    /// </summary>
    [HttpPost("{hash}/resume")]
    public Task<ActionResult> Resume(string hash, CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.ResumeAsync([hash], cancellationToken));

    /// <summary>
    /// Force-resumes a torrent, bypassing queueing.
    /// </summary>
    [HttpPost("{hash}/force-resume")]
    public Task<ActionResult> ForceResume(string hash, CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.ForceResumeAsync([hash], cancellationToken));

    /// <summary>
    /// Forces a hash recheck on a torrent.
    /// </summary>
    [HttpPost("{hash}/recheck")]
    public Task<ActionResult> Recheck(string hash, CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.RecheckAsync([hash], cancellationToken));

    /// <summary>
    /// Forces a tracker reannounce on a torrent.
    /// </summary>
    [HttpPost("{hash}/reannounce")]
    public Task<ActionResult> Reannounce(string hash, CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.ReannounceAsync([hash], cancellationToken));

    /// <summary>
    /// Deletes a torrent, optionally also deleting its downloaded files.
    /// </summary>
    [HttpDelete("{hash}")]
    public Task<ActionResult> Delete(string hash, [FromQuery] bool deleteFiles, CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.DeleteAsync([hash], deleteFiles, cancellationToken));

    /// <summary>
    /// Request body for <see cref="SetTags"/>.
    /// </summary>
    public sealed class SetTagsBody
    {
        /// <summary>
        /// Gets or sets the complete list of tags the torrent should have.
        /// </summary>
        public List<string>? Tags { get; set; }
    }

    /// <summary>
    /// Request body for <see cref="SetFilePriority"/>.
    /// </summary>
    public sealed class FilePriorityBody
    {
        /// <summary>
        /// Gets or sets the indexes of the files to change.
        /// </summary>
        public List<int>? Files { get; set; }

        /// <summary>
        /// Gets or sets the priority: 0 = skip, 1 = normal, 6 = high, 7 = maximum.
        /// </summary>
        public int Priority { get; set; }
    }
}
