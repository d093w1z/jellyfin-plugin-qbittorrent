using Jellyfin.Plugin.QBittorrent.Integration;
using Jellyfin.Plugin.QBittorrent.QBittorrent;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.QBittorrent.Tests;

public class CompletionMonitorTests
{
    private static readonly Guid MoviesLibrary = Guid.NewGuid();

    private readonly Mock<IQBittorrentClient> _client = new();
    private readonly Mock<IJellyfinLibraryService> _libraries = new();
    private readonly PluginConfiguration _config = new()
    {
        EnableCompletionMonitoring = true,
        ScanLibraryOnCompletion = true,
        Profiles = [new DownloadProfile { Name = "Movies", Category = "movies", JellyfinLibraryId = MoviesLibrary.ToString() }]
    };

    private List<TorrentInfo> _torrents = [];

    public CompletionMonitorTests()
    {
        _client.Setup(c => c.GetTorrentsAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(() => _torrents);
    }

    private TorrentCompletionMonitor CreateMonitor()
        => new(_client.Object, _libraries.Object, () => _config, NullLogger<TorrentCompletionMonitor>.Instance);

    private static TorrentInfo Torrent(string hash, double progress, string state, string category = "movies")
        => new() { Hash = hash, Name = hash, Progress = progress, State = state, RawState = state, Category = category };

    [Fact]
    public async Task FirstPoll_OnlySeeds_AlreadyCompleteTorrentsNeverScan()
    {
        var monitor = CreateMonitor();
        _torrents = [Torrent("a", 1, "Seeding")];

        await monitor.PollAsync(CancellationToken.None);
        await monitor.PollAsync(CancellationToken.None);

        _libraries.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Completion_ScansMappedLibraryOnce_PerPoll()
    {
        var monitor = CreateMonitor();
        _torrents = [Torrent("a", 0.5, "Downloading"), Torrent("b", 0.9, "Downloading")];
        await monitor.PollAsync(CancellationToken.None);

        _torrents = [Torrent("a", 1, "Seeding"), Torrent("b", 1, "Completed")];
        await monitor.PollAsync(CancellationToken.None);
        await monitor.PollAsync(CancellationToken.None);

        _libraries.Verify(l => l.ScanLibraryAsync(MoviesLibrary, It.IsAny<CancellationToken>()), Times.Once);
        _libraries.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StillMovingOrChecking_IsNotComplete()
    {
        var monitor = CreateMonitor();
        _torrents = [Torrent("a", 0.5, "Downloading")];
        await monitor.PollAsync(CancellationToken.None);

        _torrents = [Torrent("a", 1, "Downloading")];
        await monitor.PollAsync(CancellationToken.None);
        _torrents = [Torrent("a", 1, "Checking")];
        await monitor.PollAsync(CancellationToken.None);

        _libraries.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnmappedCategory_OrTorrentFinishedBetweenPolls_QueuesFullScan()
    {
        var monitor = CreateMonitor();
        await monitor.PollAsync(CancellationToken.None);

        _torrents = [Torrent("new", 1, "Seeding", category: "tv")];
        await monitor.PollAsync(CancellationToken.None);

        _libraries.Verify(l => l.QueueFullScan(), Times.Once);
        _libraries.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ScanDisabled_DetectsButDoesNotScan()
    {
        _config.ScanLibraryOnCompletion = false;
        var monitor = CreateMonitor();
        _torrents = [Torrent("a", 0.5, "Downloading")];
        await monitor.PollAsync(CancellationToken.None);

        _torrents = [Torrent("a", 1, "Seeding")];
        await monitor.PollAsync(CancellationToken.None);

        _libraries.VerifyNoOtherCalls();
    }
}
