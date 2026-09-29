using Jellyfin.Plugin.QBittorrent.QBittorrent;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.QBittorrent.Integration;

/// <summary>
/// Polls qBittorrent for torrents that go from incomplete to complete and, if
/// enabled, scans the Jellyfin library mapped to the torrent's profile (by
/// category), or all libraries when no profile maps it. Each poll scans every
/// affected library at most once.
/// </summary>
public sealed class TorrentCompletionMonitor : BackgroundService
{
    private readonly IQBittorrentClient _client;
    private readonly IJellyfinLibraryService _libraries;
    private readonly Func<PluginConfiguration> _configuration;
    private readonly ILogger<TorrentCompletionMonitor> _logger;

    // Completion state per torrent hash from the previous poll; null until the first poll
    // seeds it, so torrents that were already complete at startup never trigger a scan.
    private Dictionary<string, bool>? _wasComplete;

    /// <summary>
    /// Initializes a new instance of the <see cref="TorrentCompletionMonitor"/> class.
    /// </summary>
    public TorrentCompletionMonitor(
        IQBittorrentClient client,
        IJellyfinLibraryService libraries,
        Func<PluginConfiguration> configuration,
        ILogger<TorrentCompletionMonitor> logger)
    {
        _client = client;
        _libraries = libraries;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var config = _configuration();
            if (config.EnableCompletionMonitoring && !string.IsNullOrEmpty(config.QBittorrentUrl))
            {
                try
                {
                    await PollAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (QBittorrentApiException ex)
                {
                    _logger.LogWarning("Completion monitor could not reach qBittorrent: {Message}", ex.Message);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Completion monitor poll failed");
                }
            }
            else
            {
                _wasComplete = null;
            }

            var interval = TimeSpan.FromSeconds(Math.Max(5, config.CompletionPollIntervalSeconds));
            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Runs one poll: detects newly completed torrents and triggers the scans.
    /// </summary>
    internal async Task PollAsync(CancellationToken cancellationToken)
    {
        var torrents = await _client.GetTorrentsAsync(filter: null, cancellationToken).ConfigureAwait(false);
        var previous = _wasComplete;
        _wasComplete = torrents.ToDictionary(t => t.Hash, IsComplete, StringComparer.OrdinalIgnoreCase);

        if (previous is null)
        {
            return;
        }

        // A torrent not seen last poll counts as previously incomplete, so one that is
        // added and finishes between two polls still triggers a scan.
        var finished = torrents
            .Where(t => IsComplete(t) && !(previous.TryGetValue(t.Hash, out var was) && was))
            .ToList();

        if (finished.Count == 0)
        {
            return;
        }

        foreach (var torrent in finished)
        {
            _logger.LogInformation("Torrent completed: {Name}", torrent.Name);
        }

        var config = _configuration();
        if (!config.ScanLibraryOnCompletion)
        {
            return;
        }

        var libraryIds = finished.Select(t => MappedLibrary(config, t.Category)).ToList();
        if (libraryIds.Any(id => id is null))
        {
            _libraries.QueueFullScan();
            return;
        }

        foreach (var libraryId in libraryIds.Distinct())
        {
            await _libraries.ScanLibraryAsync(libraryId!.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    // Complete once fully downloaded and no longer moving/checking (moving normalizes to Downloading).
    private static bool IsComplete(TorrentInfo torrent)
        => torrent.Progress >= 1 && torrent.State is not ("Downloading" or "Checking");

    private static Guid? MappedLibrary(PluginConfiguration config, string category)
    {
        var profile = config.Profiles.FirstOrDefault(p =>
            !string.IsNullOrEmpty(p.Category)
            && string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(p.JellyfinLibraryId, out _));

        return profile is null ? null : Guid.Parse(profile.JellyfinLibraryId);
    }
}
