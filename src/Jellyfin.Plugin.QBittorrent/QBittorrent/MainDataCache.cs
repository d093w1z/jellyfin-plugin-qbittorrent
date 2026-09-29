using System.Text.Json;
using System.Text.Json.Nodes;

namespace Jellyfin.Plugin.QBittorrent.QBittorrent;

/// <summary>
/// Mirror of qBittorrent's torrent list and server state, kept current through
/// <c>/api/v2/sync/maindata</c>: after the first full response qBittorrent only
/// sends what changed since the previous response id, so polling stays cheap
/// however many torrents there are. Reads within <see cref="MaxAge"/> of the last
/// sync reuse it, so the UI's parallel list/transfer requests cost one call.
/// </summary>
internal sealed class MainDataCache
{
    private const string Endpoint = "/api/v2/sync/maindata";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, JsonObject> _torrents = new(StringComparer.OrdinalIgnoreCase);
    private JsonObject _serverState = [];
    private long _rid;
    private DateTimeOffset _lastSync = DateTimeOffset.MinValue;

    /// <summary>
    /// Gets or sets how long a sync stays fresh.
    /// </summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Forces the next read to sync, e.g. after a change was made through the API.
    /// </summary>
    public void Invalidate() => _lastSync = DateTimeOffset.MinValue;

    /// <summary>
    /// Gets all torrents, optionally sorted by a qBittorrent torrent field (e.g. <c>added_on</c>).
    /// </summary>
    public async Task<List<WireTorrent>> GetTorrentsAsync(QBittorrentConnection connection, string? sortField, bool reverse, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SyncAsync(connection, cancellationToken).ConfigureAwait(false);
            IEnumerable<JsonObject> torrents = _torrents.Values;
            if (!string.IsNullOrEmpty(sortField))
            {
                var comparer = Comparer<JsonNode?>.Create(CompareValues);
                torrents = reverse
                    ? torrents.OrderByDescending(t => t[sortField], comparer)
                    : torrents.OrderBy(t => t[sortField], comparer);
            }

            return torrents.Select(t => t.Deserialize<WireTorrent>(JsonOptions)!).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets the global transfer state.
    /// </summary>
    public async Task<WireTransferInfo> GetServerStateAsync(QBittorrentConnection connection, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SyncAsync(connection, cancellationToken).ConfigureAwait(false);
            return _serverState.Deserialize<WireTransferInfo>(JsonOptions)!;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SyncAsync(QBittorrentConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastSync < MaxAge)
        {
            return;
        }

        JsonObject data;
        try
        {
            var query = new Dictionary<string, string?> { ["rid"] = _rid.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            data = await connection.GetJsonAsync<JsonObject>(Endpoint, query, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Unknown what qBittorrent last saw; start over with a full update.
            _rid = 0;
            throw;
        }

        Apply(data);
        _lastSync = now;
    }

    private void Apply(JsonObject data)
    {
        if (data["full_update"] is JsonValue full && full.TryGetValue<bool>(out var isFull) && isFull)
        {
            _torrents.Clear();
            _serverState = [];
        }

        if (data["torrents"] is JsonObject changedTorrents)
        {
            foreach (var (hash, node) in changedTorrents)
            {
                if (node is not JsonObject changes)
                {
                    continue;
                }

                if (!_torrents.TryGetValue(hash, out var torrent))
                {
                    _torrents[hash] = torrent = new JsonObject { ["hash"] = hash };
                }

                Merge(torrent, changes);
            }
        }

        if (data["torrents_removed"] is JsonArray removed)
        {
            foreach (var hash in removed)
            {
                _torrents.Remove(hash?.GetValue<string>() ?? string.Empty);
            }
        }

        if (data["server_state"] is JsonObject serverState)
        {
            Merge(_serverState, serverState);
        }

        _rid = data["rid"] is JsonValue rid && rid.TryGetValue<long>(out var value) ? value : 0;
    }

    private static void Merge(JsonObject target, JsonObject changes)
    {
        foreach (var (key, value) in changes)
        {
            target[key] = value?.DeepClone();
        }
    }

    // Numbers compare numerically, everything else as case-insensitive text; missing values sort first.
    private static int CompareValues(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null)
        {
            return (a is null ? 0 : 1) - (b is null ? 0 : 1);
        }

        if (a is JsonValue va && b is JsonValue vb && va.TryGetValue<double>(out var da) && vb.TryGetValue<double>(out var db))
        {
            return da.CompareTo(db);
        }

        return string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
