using System.Net;
using Jellyfin.Plugin.QBittorrent.Models;
using Jellyfin.Plugin.QBittorrent.QBittorrent;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Authentication;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.QBittorrent.Tests;

public class QBittorrentClientTests
{
    private static (QBittorrentClient Client, FakeQBittorrentHandler Handler) CreateClient(AuthenticationMode mode = AuthenticationMode.ApiKey, string baseUrl = "http://qbittorrent:8080")
    {
        var handler = new FakeQBittorrentHandler();
        var httpClient = new HttpClient(handler);

        var options = new QBittorrentConnectionOptions
        {
            BaseUrl = baseUrl,
            AuthenticationMode = mode,
            ApiKey = "test-key",
            Username = "admin",
            Password = "adminadmin"
        };

        var connection = new QBittorrentConnection(
            httpClient,
            () => options,
            new ApiKeyAuthenticator(),
            new CookieAuthenticator(httpClient),
            NullLogger<QBittorrentConnection>.Instance);

        return (new QBittorrentClient(connection), handler);
    }

    [Fact]
    public async Task GetTorrentsAsync_MapsAndNormalizesState()
    {
        var (client, handler) = CreateClient();
        handler.OnJson("GET", "/api/v2/sync/maindata", """
            { "rid": 1, "full_update": true, "torrents": {
              "abc123": { "name": "Ubuntu.iso", "size": 1000, "completed": 1000, "progress": 1.0,
                "dlspeed": 0, "upspeed": 500, "downloaded": 1000, "uploaded": 200, "eta": 8640000,
                "num_seeds": 3, "num_leechs": 1, "state": "stalledUP", "category": "linux", "tags": "hd, linux",
                "save_path": "/downloads/linux", "added_on": 1700000000 } } }
            """);

        var torrents = await client.GetTorrentsAsync(filter: null, CancellationToken.None);

        var torrent = Assert.Single(torrents);
        Assert.Equal("abc123", torrent.Hash);
        Assert.Equal("Seeding", torrent.State);
        Assert.Equal("stalledUP", torrent.RawState);
        Assert.Equal("linux", torrent.Category);
        Assert.Equal(["hd", "linux"], torrent.Tags);
        Assert.NotNull(torrent.AddedOn);
    }

    [Fact]
    public async Task GetTorrentsAsync_FiltersBySearchAndNormalizedState()
    {
        var (client, handler) = CreateClient();
        handler.OnJson("GET", "/api/v2/sync/maindata", """
            { "rid": 1, "full_update": true, "torrents": {
              "a": { "name": "Ubuntu.iso", "state": "downloading", "added_on": 2, "tags": "" },
              "b": { "name": "Debian.iso", "state": "pausedUP", "added_on": 1, "tags": "linux" } } }
            """);

        var torrents = await client.GetTorrentsAsync(new TorrentFilter { Search = "ubuntu" }, CancellationToken.None);
        Assert.Equal(["a"], torrents.Select(t => t.Hash));

        var completed = await client.GetTorrentsAsync(new TorrentFilter { State = "Completed" }, CancellationToken.None);
        Assert.Equal(["b"], completed.Select(t => t.Hash));

        var tagged = await client.GetTorrentsAsync(new TorrentFilter { Tag = "LINUX" }, CancellationToken.None);
        Assert.Equal(["b"], tagged.Select(t => t.Hash));

        var oldestFirst = await client.GetTorrentsAsync(new TorrentFilter { Sort = "added_on" }, CancellationToken.None);
        Assert.Equal(["b", "a"], oldestFirst.Select(t => t.Hash));
        var newestFirst = await client.GetTorrentsAsync(new TorrentFilter { Sort = "added_on", Reverse = true }, CancellationToken.None);
        Assert.Equal(["a", "b"], newestFirst.Select(t => t.Hash));
    }

    [Fact]
    public async Task PauseAsync_SendsPipeJoinedHashesToStopEndpoint()
    {
        var (client, handler) = CreateClient();
        string? capturedBody = null;
        handler.OnRequest("POST", "/api/v2/torrents/stop", request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await client.PauseAsync(["hash1", "hash2"], CancellationToken.None);

        Assert.Equal("hashes=hash1%7Chash2", capturedBody);
    }

    [Fact]
    public async Task TestConnectionAsync_ThrowsSafeExceptionOnFailure()
    {
        var (client, handler) = CreateClient();
        handler.OnRequest("GET", "/api/v2/app/version", _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<QBittorrentApiException>(() => client.TestConnectionAsync(CancellationToken.None));

        Assert.Equal("Authentication failed", ex.Message);
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.DoesNotContain("test-key", ex.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_ReportsSafeErrorWhenUrlNotConfigured()
    {
        var (client, _) = CreateClient(baseUrl: string.Empty);

        var ex = await Assert.ThrowsAsync<QBittorrentApiException>(() => client.TestConnectionAsync(CancellationToken.None));

        Assert.Equal("qBittorrent is not configured", ex.Message);
    }

    [Fact]
    public async Task GetTransferInfoAsync_MapsFields()
    {
        var (client, handler) = CreateClient();
        handler.OnJson("GET", "/api/v2/sync/maindata", """
            { "rid": 1, "full_update": true, "server_state": { "dl_info_speed": 100, "up_info_speed": 50,
              "dl_info_data": 1000, "up_info_data": 500, "dht_nodes": 12, "connection_status": "connected",
              "dl_rate_limit": 0, "up_rate_limit": 2048, "use_alt_speed_limits": true } }
            """);

        var transfer = await client.GetTransferInfoAsync(CancellationToken.None);

        Assert.Equal(100, transfer.DownloadSpeed);
        Assert.Equal("connected", transfer.ConnectionStatus);
        Assert.Equal(2048, transfer.UploadSpeedLimit);
        Assert.True(transfer.AlternativeSpeedLimitsEnabled);
    }

    [Fact]
    public async Task SearchAsync_MapsResultsAndPlugins()
    {
        var (client, handler) = CreateClient();
        handler.OnJson("GET", "/api/v2/search/results", """
            { "status": "Running", "total": 1, "results": [
              { "fileName": "Ubuntu 24.04", "fileSize": 6000, "fileUrl": "magnet:?xt=urn:btih:abc", "nbSeeders": 50,
                "nbLeechers": 2, "siteUrl": "https://example.org", "descrLink": "https://example.org/t/1" } ] }
            """);
        handler.OnJson("GET", "/api/v2/search/plugins", """
            [ { "name": "example", "fullName": "Example", "enabled": true, "version": "1.0", "url": "https://example.org", "supportedCategories": [] } ]
            """);

        var page = await client.GetSearchResultsAsync(7, CancellationToken.None);
        var plugin = Assert.Single(await client.GetSearchPluginsAsync(CancellationToken.None));

        var result = Assert.Single(page.Results);
        Assert.Equal("Running", page.Status);
        Assert.Equal("magnet:?xt=urn:btih:abc", result.DownloadLink);
        Assert.Equal(50, result.Seeders);
        Assert.Equal("https://example.org/t/1", result.DescriptionUrl);
        Assert.Equal("Example", plugin.FullName);
        Assert.True(plugin.Enabled);
    }

    [Fact]
    public async Task MainData_MergesIncrementalUpdates()
    {
        var (client, handler) = CreateClient();
        client.MainData.MaxAge = TimeSpan.Zero;
        var rids = new List<string>();
        handler.OnRequest("GET", "/api/v2/sync/maindata", request =>
        {
            var rid = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["rid"]!;
            rids.Add(rid);
            var json = rid == "0"
                ? """
                  { "rid": 5, "full_update": true,
                    "torrents": { "a": { "name": "A", "progress": 0.5, "state": "downloading" }, "b": { "name": "B", "state": "pausedUP" } },
                    "server_state": { "dl_info_speed": 10, "connection_status": "connected" } }
                  """
                : """
                  { "rid": 6, "torrents": { "a": { "progress": 1.0, "state": "uploading" }, "c": { "name": "C", "state": "queuedDL" } },
                    "torrents_removed": ["b"], "server_state": { "dl_info_speed": 20 } }
                  """;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        });

        await client.GetTorrentsAsync(filter: null, CancellationToken.None);
        var torrents = await client.GetTorrentsAsync(filter: null, CancellationToken.None);
        var transfer = await client.GetTransferInfoAsync(CancellationToken.None);

        Assert.Equal(["0", "5", "6"], rids);
        Assert.Equal(["a", "c"], torrents.Select(t => t.Hash).Order());
        var a = torrents.Single(t => t.Hash == "a");
        Assert.Equal("A", a.Name);
        Assert.Equal(1.0, a.Progress);
        Assert.Equal("Seeding", a.State);
        Assert.Equal(20, transfer.DownloadSpeed);
        Assert.Equal("connected", transfer.ConnectionStatus);
    }

    [Fact]
    public async Task MainData_ReusesFreshSync_ButChangesInvalidateIt()
    {
        var (client, handler) = CreateClient();
        handler.OnJson("GET", "/api/v2/sync/maindata", """{ "rid": 1, "full_update": true, "torrents": {} }""");
        handler.OnJson("POST", "/api/v2/torrents/stop", "");

        await client.GetTorrentsAsync(filter: null, CancellationToken.None);
        await client.GetTransferInfoAsync(CancellationToken.None);
        await client.PauseAsync(["a"], CancellationToken.None);
        await client.GetTorrentsAsync(filter: null, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count(r => r.RequestUri!.AbsolutePath == "/api/v2/sync/maindata"));
    }

    [Fact]
    public async Task SetTagsAsync_ClearsThenAddsCommaJoinedTags()
    {
        var (client, handler) = CreateClient();
        var bodies = new List<string>();
        foreach (var path in new[] { "/api/v2/torrents/removeTags", "/api/v2/torrents/addTags" })
        {
            handler.OnRequest("POST", path, request =>
            {
                bodies.Add(path + "?" + request.Content!.ReadAsStringAsync().Result);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        }

        await client.SetTagsAsync("abc", ["hd", "movies"], CancellationToken.None);

        Assert.Equal(["/api/v2/torrents/removeTags?hashes=abc", "/api/v2/torrents/addTags?hashes=abc&tags=hd%2Cmovies"], bodies);
    }
}
