using Jellyfin.Plugin.QBittorrent.Models;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Models;

namespace Jellyfin.Plugin.QBittorrent.QBittorrent;

/// <summary>
/// Everything that talks to qBittorrent goes through this interface. It has no
/// dependency on Jellyfin, so it can be implemented and tested standalone.
/// </summary>
public interface IQBittorrentClient
{
    /// <summary>
    /// Gets global transfer statistics.
    /// </summary>
    Task<TransferInfo> GetTransferInfoAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the list of torrents, optionally filtered/sorted.
    /// </summary>
    Task<IReadOnlyList<TorrentInfo>> GetTorrentsAsync(TorrentFilter? filter, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a single torrent by hash, or <c>null</c> if it doesn't exist.
    /// </summary>
    Task<TorrentInfo?> GetTorrentAsync(string hash, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the files within a torrent.
    /// </summary>
    Task<IReadOnlyList<TorrentFile>> GetTorrentFilesAsync(string hash, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the peers currently connected for a torrent.
    /// </summary>
    Task<IReadOnlyList<TorrentPeer>> GetTorrentPeersAsync(string hash, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the trackers for a torrent.
    /// </summary>
    Task<IReadOnlyList<TorrentTracker>> GetTorrentTrackersAsync(string hash, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a torrent by magnet/URL or uploaded file.
    /// </summary>
    Task AddTorrentAsync(AddTorrentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Pauses the given torrents.
    /// </summary>
    Task PauseAsync(IEnumerable<string> hashes, CancellationToken cancellationToken);

    /// <summary>
    /// Resumes the given torrents.
    /// </summary>
    Task ResumeAsync(IEnumerable<string> hashes, CancellationToken cancellationToken);

    /// <summary>
    /// Force-resumes the given torrents, bypassing queueing.
    /// </summary>
    Task ForceResumeAsync(IEnumerable<string> hashes, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the given torrents, optionally deleting their downloaded files.
    /// </summary>
    Task DeleteAsync(IEnumerable<string> hashes, bool deleteFiles, CancellationToken cancellationToken);

    /// <summary>
    /// Forces a hash recheck on the given torrents.
    /// </summary>
    Task RecheckAsync(IEnumerable<string> hashes, CancellationToken cancellationToken);

    /// <summary>
    /// Forces a tracker reannounce on the given torrents.
    /// </summary>
    Task ReannounceAsync(IEnumerable<string> hashes, CancellationToken cancellationToken);

    /// <summary>
    /// Starts a search across all of qBittorrent's enabled search plugins.
    /// Plugin installation/management is out of scope — this assumes the
    /// administrator has already installed and enabled plugins directly in qBittorrent.
    /// </summary>
    /// <returns>The search job id, used with <see cref="GetSearchResultsAsync"/> and <see cref="DeleteSearchAsync"/>.</returns>
    Task<int> StartSearchAsync(string pattern, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the results of a search job so far, along with whether it's still running.
    /// </summary>
    Task<SearchResultPage> GetSearchResultsAsync(int searchId, CancellationToken cancellationToken);

    /// <summary>
    /// Stops and discards a search job.
    /// </summary>
    Task DeleteSearchAsync(int searchId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all configured categories.
    /// </summary>
    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets all configured tags.
    /// </summary>
    Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sets the category for the given torrents.
    /// </summary>
    Task SetCategoryAsync(IEnumerable<string> hashes, string category, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies connectivity and authentication against the configured qBittorrent
    /// instance. Throws <see cref="QBittorrentApiException"/> on failure.
    /// </summary>
    Task TestConnectionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the qBittorrent application version, e.g. <c>v5.0.3</c>. Used to confirm
    /// connectivity and to display the connected version to the administrator.
    /// </summary>
    Task<string> GetVersionAsync(CancellationToken cancellationToken);
}
