using System.Globalization;
using Jellyfin.Plugin.QBittorrent.Models;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Models;

namespace Jellyfin.Plugin.QBittorrent.QBittorrent;

/// <inheritdoc />
public sealed class QBittorrentClient : IQBittorrentClient
{
    private readonly QBittorrentConnection _connection;

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
        var wire = await _connection.GetJsonAsync<WireTransferInfo>("/api/v2/transfer/info", query: null, cancellationToken).ConfigureAwait(false);
        return new TransferInfo
        {
            DownloadSpeed = wire.DlInfoSpeed,
            UploadSpeed = wire.UpInfoSpeed,
            DownloadedTotal = wire.DlInfoData,
            UploadedTotal = wire.UpInfoData,
            DhtNodes = wire.DhtNodes,
            ConnectionStatus = wire.ConnectionStatus,
            DownloadSpeedLimit = wire.DlRateLimit,
            UploadSpeedLimit = wire.UpRateLimit
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TorrentInfo>> GetTorrentsAsync(TorrentFilter? filter, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["category"] = filter?.Category,
            ["tag"] = filter?.Tag,
            ["sort"] = filter?.Sort,
            ["reverse"] = filter?.Reverse is true ? "true" : null
        };

        var wireTorrents = await _connection.GetJsonAsync<List<WireTorrent>>("/api/v2/torrents/info", query, cancellationToken).ConfigureAwait(false);
        var torrents = wireTorrents.Select(MapTorrent).AsEnumerable();

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
        var query = new Dictionary<string, string?> { ["hashes"] = hash };
        var wireTorrents = await _connection.GetJsonAsync<List<WireTorrent>>("/api/v2/torrents/info", query, cancellationToken).ConfigureAwait(false);
        return wireTorrents.Count > 0 ? MapTorrent(wireTorrents[0]) : null;
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
    }

    /// <inheritdoc />
    public Task PauseAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
        => _connection.PostFormAsync("/api/v2/torrents/stop", HashesForm(hashes), cancellationToken);

    /// <inheritdoc />
    public Task ResumeAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
        => _connection.PostFormAsync("/api/v2/torrents/start", HashesForm(hashes), cancellationToken);

    /// <inheritdoc />
    public Task ForceResumeAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
    {
        var form = HashesForm(hashes);
        form["value"] = "true";
        return _connection.PostFormAsync("/api/v2/torrents/setForceStart", form, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(IEnumerable<string> hashes, bool deleteFiles, CancellationToken cancellationToken)
    {
        var form = HashesForm(hashes);
        form["deleteFiles"] = deleteFiles ? "true" : "false";
        return _connection.PostFormAsync("/api/v2/torrents/delete", form, cancellationToken);
    }

    /// <inheritdoc />
    public Task RecheckAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
        => _connection.PostFormAsync("/api/v2/torrents/recheck", HashesForm(hashes), cancellationToken);

    /// <inheritdoc />
    public Task ReannounceAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
        => _connection.PostFormAsync("/api/v2/torrents/reannounce", HashesForm(hashes), cancellationToken);

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
        return _connection.PostFormAsync("/api/v2/search/delete", form, cancellationToken);
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
        return _connection.PostFormAsync("/api/v2/torrents/setCategory", form, cancellationToken);
    }

    /// <inheritdoc />
    public async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        await GetVersionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<string> GetVersionAsync(CancellationToken cancellationToken)
        => _connection.GetStringAsync("/api/v2/app/version", cancellationToken);

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
