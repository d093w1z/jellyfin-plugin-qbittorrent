using System.Globalization;
using Jellyfin.Plugin.QBittorrent.Models;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Models;

namespace Jellyfin.Plugin.QBittorrent.QBittorrent;

/// <inheritdoc />
public sealed class QBittorrentClient : IQBittorrentClient
{
    private readonly QBittorrentConnection _connection;
    private readonly MainDataCache _mainData = new();

    /// <summary>
    /// Gets the sync cache; exposed for tests.
    /// </summary>
    internal MainDataCache MainData => _mainData;

    /// <summary>
    /// Initializes a new instance of the <see cref="QBittorrentClient"/> class.
    /// </summary>
    public QBittorrentClient(QBittorrentConnection connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public async Task<TransferInfo> GetTransferInfoAsync(CancellationToken cancellationToken)
    {
        var wire = await _mainData.GetServerStateAsync(_connection, cancellationToken).ConfigureAwait(false);
        return new TransferInfo
        {
            DownloadSpeed = wire.DlInfoSpeed,
            UploadSpeed = wire.UpInfoSpeed,
            DownloadedTotal = wire.DlInfoData,
            UploadedTotal = wire.UpInfoData,
            DhtNodes = wire.DhtNodes,
            ConnectionStatus = wire.ConnectionStatus,
            DownloadSpeedLimit = wire.DlRateLimit,
            UploadSpeedLimit = wire.UpRateLimit,
            AlternativeSpeedLimitsEnabled = wire.UseAltSpeedLimits
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TorrentInfo>> GetTorrentsAsync(TorrentFilter? filter, CancellationToken cancellationToken)
    {
        var wireTorrents = await _mainData.GetTorrentsAsync(_connection, filter?.Sort, filter?.Reverse is true, cancellationToken).ConfigureAwait(false);
        var torrents = wireTorrents.Select(MapTorrent);

        if (filter?.Category is not null)
        {
            torrents = torrents.Where(t => string.Equals(t.Category, filter.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(filter?.Tag))
        {
            torrents = torrents.Where(t => t.Tags.Contains(filter.Tag, StringComparer.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(filter?.State))
        {
            torrents = torrents.Where(t => string.Equals(t.State, filter.State, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(filter?.Search))
        {
            torrents = torrents.Where(t => t.Name.Contains(filter.Search, StringComparison.OrdinalIgnoreCase));
        }

        return torrents.ToList();
    }

    /// <inheritdoc />
    public async Task<TorrentInfo?> GetTorrentAsync(string hash, CancellationToken cancellationToken)
    {
        var wireTorrents = await _mainData.GetTorrentsAsync(_connection, sortField: null, reverse: false, cancellationToken).ConfigureAwait(false);
        var wire = wireTorrents.Find(t => string.Equals(t.Hash, hash, StringComparison.OrdinalIgnoreCase));
        return wire is null ? null : MapTorrent(wire);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TorrentFile>> GetTorrentFilesAsync(string hash, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?> { ["hash"] = hash };
        var wireFiles = await _connection.GetJsonAsync<List<WireTorrentFile>>("/api/v2/torrents/files", query, cancellationToken).ConfigureAwait(false);
        return wireFiles.Select(f => new TorrentFile
        {
            Index = f.Index,
            Name = f.Name,
            Size = f.Size,
            Progress = f.Progress,
            Priority = f.Priority
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TorrentPeer>> GetTorrentPeersAsync(string hash, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?> { ["hash"] = hash, ["rid"] = "0" };
        var response = await _connection.GetJsonAsync<WirePeersResponse>("/api/v2/sync/torrentPeers", query, cancellationToken).ConfigureAwait(false);
        if (response.Peers is null)
        {
            return [];
        }

        return response.Peers.Values.Select(p => new TorrentPeer
        {
            Ip = p.Ip,
            Port = p.Port,
            Client = p.Client,
            Country = p.CountryCode,
            Progress = p.Progress,
            DownloadSpeed = p.DlSpeed,
            UploadSpeed = p.UpSpeed
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TorrentTracker>> GetTorrentTrackersAsync(string hash, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?> { ["hash"] = hash };
        var wireTrackers = await _connection.GetJsonAsync<List<WireTracker>>("/api/v2/torrents/trackers", query, cancellationToken).ConfigureAwait(false);
        return wireTrackers.Select(t => new TorrentTracker
        {
            Url = t.Url,
            Status = TrackerStatusText(t.Status),
            NumPeers = t.NumPeers,
            Message = t.Msg
        }).ToList();
    }

    /// <inheritdoc />
    public async Task AddTorrentAsync(AddTorrentRequest request, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();

        if (!string.IsNullOrEmpty(request.MagnetUri))
        {
            content.Add(new StringContent(request.MagnetUri), "urls");
        }
        else if (request.TorrentFile is { Length: > 0 })
        {
            var fileContent = new ByteArrayContent(request.TorrentFile);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-bittorrent");
            content.Add(fileContent, "torrents", request.TorrentFileName ?? "upload.torrent");
        }
        else
        {
            throw new ArgumentException("Either MagnetUri or TorrentFile must be set.", nameof(request));
        }

        if (!string.IsNullOrEmpty(request.SavePath))
        {
            content.Add(new StringContent(request.SavePath), "savepath");
        }

        if (!string.IsNullOrEmpty(request.Category))
        {
            content.Add(new StringContent(request.Category), "category");
        }

        content.Add(new StringContent(request.StartImmediately ? "false" : "true"), "paused");

        await _connection.PostAsync("/api/v2/torrents/add", content, cancellationToken).ConfigureAwait(false);
        _mainData.Invalidate();
    }

    /// <inheritdoc />
    public Task PauseAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
        => PostFormAsync("/api/v2/torrents/stop", HashesForm(hashes), cancellationToken);

    /// <inheritdoc />
    public Task ResumeAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
        => PostFormAsync("/api/v2/torrents/start", HashesForm(hashes), cancellationToken);

    /// <inheritdoc />
    public Task ForceResumeAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
    {
        var form = HashesForm(hashes);
        form["value"] = "true";
        return PostFormAsync("/api/v2/torrents/setForceStart", form, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(IEnumerable<string> hashes, bool deleteFiles, CancellationToken cancellationToken)
    {
        var form = HashesForm(hashes);
        form["deleteFiles"] = deleteFiles ? "true" : "false";
        return PostFormAsync("/api/v2/torrents/delete", form, cancellationToken);
    }

    /// <inheritdoc />
    public Task RecheckAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
        => PostFormAsync("/api/v2/torrents/recheck", HashesForm(hashes), cancellationToken);

    /// <inheritdoc />
    public Task ReannounceAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
        => PostFormAsync("/api/v2/torrents/reannounce", HashesForm(hashes), cancellationToken);

    /// <inheritdoc />
    public async Task<int> StartSearchAsync(string pattern, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string> { ["pattern"] = pattern, ["plugins"] = "enabled", ["category"] = "all" };
        var wire = await _connection.PostFormJsonAsync<WireSearchStart>("/api/v2/search/start", form, cancellationToken).ConfigureAwait(false);
        return wire.Id;
    }

    /// <inheritdoc />
    public async Task<SearchResultPage> GetSearchResultsAsync(int searchId, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?> { ["id"] = searchId.ToString(CultureInfo.InvariantCulture) };
        var wire = await _connection.GetJsonAsync<WireSearchResultsResponse>("/api/v2/search/results", query, cancellationToken).ConfigureAwait(false);
        return new SearchResultPage
        {
            Status = wire.Status,
            Total = wire.Total,
            Results = wire.Results.Select(r => new SearchResult
            {
                Name = r.FileName,
                Size = r.FileSize,
                DownloadLink = r.FileUrl,
                Seeders = r.NbSeeders,
                Leechers = r.NbLeechers,
                SiteUrl = r.SiteUrl,
                DescriptionUrl = r.DescrLink
            }).ToList()
        };
    }

    /// <inheritdoc />
    public Task DeleteSearchAsync(int searchId, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string> { ["id"] = searchId.ToString(CultureInfo.InvariantCulture) };
        return PostFormAsync("/api/v2/search/delete", form, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchPlugin>> GetSearchPluginsAsync(CancellationToken cancellationToken)
    {
        var wire = await _connection.GetJsonAsync<List<WireSearchPlugin>>("/api/v2/search/plugins", query: null, cancellationToken).ConfigureAwait(false);
        return wire.Select(p => new SearchPlugin { Name = p.Name, FullName = p.FullName, Enabled = p.Enabled }).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        var wireCategories = await _connection.GetJsonAsync<Dictionary<string, WireCategoryEntry>>("/api/v2/torrents/categories", query: null, cancellationToken).ConfigureAwait(false);
        return wireCategories.Values.Select(c => new Category { Name = c.Name, SavePath = c.SavePath }).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken)
    {
        var wireTags = await _connection.GetJsonAsync<List<string>>("/api/v2/torrents/tags", query: null, cancellationToken).ConfigureAwait(false);
        return wireTags.Select(t => new Tag { Name = t }).ToList();
    }

    /// <inheritdoc />
    public Task SetCategoryAsync(IEnumerable<string> hashes, string category, CancellationToken cancellationToken)
    {
        var form = HashesForm(hashes);
        form["category"] = category;
        return PostFormAsync("/api/v2/torrents/setCategory", form, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetTagsAsync(string hash, IEnumerable<string> tags, CancellationToken cancellationToken)
    {
        // removeTags without a tag list clears them all; addTags creates tags that don't exist yet.
        await PostFormAsync("/api/v2/torrents/removeTags", HashesForm([hash]), cancellationToken).ConfigureAwait(false);
        var tagList = string.Join(',', tags);
        if (tagList.Length > 0)
        {
            var form = HashesForm([hash]);
            form["tags"] = tagList;
            await PostFormAsync("/api/v2/torrents/addTags", form, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task SetFilePriorityAsync(string hash, IEnumerable<int> fileIndexes, int priority, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["hash"] = hash,
            ["id"] = string.Join('|', fileIndexes.Select(i => i.ToString(CultureInfo.InvariantCulture))),
            ["priority"] = priority.ToString(CultureInfo.InvariantCulture)
        };
        return PostFormAsync("/api/v2/torrents/filePrio", form, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetSpeedLimitsAsync(long downloadLimit, long uploadLimit, CancellationToken cancellationToken)
    {
        await PostFormAsync("/api/v2/transfer/setDownloadLimit", new() { ["limit"] = downloadLimit.ToString(CultureInfo.InvariantCulture) }, cancellationToken).ConfigureAwait(false);
        await PostFormAsync("/api/v2/transfer/setUploadLimit", new() { ["limit"] = uploadLimit.ToString(CultureInfo.InvariantCulture) }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ToggleAlternativeSpeedLimitsAsync(CancellationToken cancellationToken)
        => PostFormAsync("/api/v2/transfer/toggleSpeedLimitsMode", new(), cancellationToken);

    /// <inheritdoc />
    public async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        await GetVersionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<string> GetVersionAsync(CancellationToken cancellationToken)
        => _connection.GetStringAsync("/api/v2/app/version", cancellationToken);

    // Every change goes through here so the next read re-syncs and shows it immediately.
    private async Task PostFormAsync(string endpoint, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        await _connection.PostFormAsync(endpoint, form, cancellationToken).ConfigureAwait(false);
        _mainData.Invalidate();
    }

    private static Dictionary<string, string> HashesForm(IEnumerable<string> hashes)
        => new() { ["hashes"] = string.Join('|', hashes) };

    private static TorrentInfo MapTorrent(WireTorrent wire)
    {
        return new TorrentInfo
        {
            Hash = wire.Hash,
            Name = wire.Name,
            Size = wire.Size,
            Completed = wire.Completed,
            Progress = wire.Progress,
            DownloadSpeed = wire.DlSpeed,
            UploadSpeed = wire.UpSpeed,
            Downloaded = wire.Downloaded,
            Uploaded = wire.Uploaded,
            Eta = wire.Eta,
            Seeds = wire.NumSeeds,
            Peers = wire.NumLeechs,
            State = TorrentStateNormalizer.Normalize(wire.State),
            RawState = wire.State,
            Category = wire.Category,
            Tags = wire.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            SavePath = wire.SavePath,
            AddedOn = wire.AddedOn > 0 ? DateTimeOffset.FromUnixTimeSeconds(wire.AddedOn) : null
        };
    }

    private static string TrackerStatusText(int status) => status switch
    {
        0 => "Disabled",
        1 => "Not contacted",
        2 => "Working",
        3 => "Updating",
        4 => "Error",
        _ => "Unknown"
    };
}
