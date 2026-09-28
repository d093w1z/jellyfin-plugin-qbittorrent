namespace Jellyfin.Plugin.QBittorrent.QBittorrent;

/// <summary>
/// Maps qBittorrent's raw torrent state strings onto a small, stable set the UI
/// can render without knowing every qBittorrent-specific state.
/// </summary>
public static class TorrentStateNormalizer
{
    /// <summary>
    /// Normalizes a raw qBittorrent state string.
    /// </summary>
    /// <param name="rawState">The raw <c>state</c> field from qBittorrent's torrent list.</param>
    /// <returns>One of Downloading, Seeding, Paused, Completed, Queued, Checking, Error, Unknown.</returns>
    public static string Normalize(string rawState)
    {
        return rawState switch
        {
            "error" or "missingFiles" => "Error",
            "uploading" or "stalledUP" or "forcedUP" => "Seeding",
            "checkingUP" or "checkingDL" or "checkingResumeData" => "Checking",
            "pausedUP" => "Completed",
            "pausedDL" => "Paused",
            "queuedUP" or "queuedDL" => "Queued",
            "allocating" or "downloading" or "metaDL" or "forcedMetaDL" or "stalledDL" or "forcedDL" or "moving" => "Downloading",
            _ => "Unknown"
        };
    }
}
