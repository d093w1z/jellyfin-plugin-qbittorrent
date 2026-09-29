using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.QBittorrent.Integration;

/// <summary>
/// Triggers Jellyfin library scans after torrents complete.
/// </summary>
public interface IJellyfinLibraryService
{
    /// <summary>
    /// Queues Jellyfin's regular "Scan Media Library" task for all libraries.
    /// </summary>
    void QueueFullScan();

    /// <summary>
    /// Scans a single library (a Jellyfin virtual folder) for new files.
    /// Falls back to a full scan if the library no longer exists.
    /// </summary>
    Task ScanLibraryAsync(Guid libraryId, CancellationToken cancellationToken);
}

/// <summary>
/// <see cref="IJellyfinLibraryService"/> backed by Jellyfin's library manager.
/// </summary>
public sealed class JellyfinLibraryService : IJellyfinLibraryService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<JellyfinLibraryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinLibraryService"/> class.
    /// </summary>
    public JellyfinLibraryService(ILibraryManager libraryManager, IFileSystem fileSystem, ILogger<JellyfinLibraryService> logger)
    {
        _libraryManager = libraryManager;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <inheritdoc />
    public void QueueFullScan()
    {
        _logger.LogInformation("Queueing a full Jellyfin library scan");
        _libraryManager.QueueLibraryScan();
    }

    /// <inheritdoc />
    public Task ScanLibraryAsync(Guid libraryId, CancellationToken cancellationToken)
    {
        if (_libraryManager.GetItemById(libraryId) is not Folder library)
        {
            _logger.LogWarning("Mapped Jellyfin library {LibraryId} not found; falling back to a full scan", libraryId);
            QueueFullScan();
            return Task.CompletedTask;
        }

        _logger.LogInformation("Scanning Jellyfin library {LibraryName}", library.Name);
        return library.ValidateChildren(
            new Progress<double>(),
            new MetadataRefreshOptions(new DirectoryService(_fileSystem)),
            recursive: true,
            allowRemoveRoot: false,
            cancellationToken);
    }
}
