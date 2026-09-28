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
        handler.OnJson("GET", "/api/v2/torrents/info", """
            [
              { "hash": "abc123", "name": "Ubuntu.iso", "size": 1000, "completed": 1000, "progress": 1.0,
                "dlspeed": 0, "upspeed": 500, "downloaded": 1000, "uploaded": 200, "eta": 8640000,
                "num_seeds": 3, "num_leechs": 1, "state": "stalledUP", "category": "linux",
                "save_path": "/downloads/linux", "added_on": 1700000000 }
            ]
            """);

        var torrents = await client.GetTorrentsAsync(filter: null, CancellationToken.None);

        var torrent = Assert.Single(torrents);
        Assert.Equal("abc123", torrent.Hash);
        Assert.Equal("Seeding", torrent.State);
        Assert.Equal("stalledUP", torrent.RawState);
        Assert.Equal("linux", torrent.Category);
        Assert.NotNull(torrent.AddedOn);
    }

    [Fact]
    public async Task GetTorrentsAsync_FiltersBySearchAndNormalizedState()
    {
        var (client, handler) = CreateClient();
        handler.OnJson("GET", "/api/v2/torrents/info", """
            [
              { "hash": "a", "name": "Ubuntu.iso", "state": "downloading" },
              { "hash": "b", "name": "Debian.iso", "state": "pausedUP" }
            ]
            """);

        var torrents = await client.GetTorrentsAsync(new TorrentFilter { Search = "ubuntu" }, CancellationToken.None);
        Assert.Equal(["a"], torrents.Select(t => t.Hash));

        var completed = await client.GetTorrentsAsync(new TorrentFilter { State = "Completed" }, CancellationToken.None);
        Assert.Equal(["b"], completed.Select(t => t.Hash));
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
        handler.OnJson("GET", "/api/v2/transfer/info", """
            { "dl_info_speed": 100, "up_info_speed": 50, "dl_info_data": 1000, "up_info_data": 500,
              "dht_nodes": 12, "connection_status": "connected", "dl_rate_limit": 0, "up_rate_limit": 0 }
            """);

        var transfer = await client.GetTransferInfoAsync(CancellationToken.None);

        Assert.Equal(100, transfer.DownloadSpeed);
        Assert.Equal("connected", transfer.ConnectionStatus);
    }
}
