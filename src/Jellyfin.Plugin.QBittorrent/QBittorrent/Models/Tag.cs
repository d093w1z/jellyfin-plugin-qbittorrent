namespace Jellyfin.Plugin.QBittorrent.QBittorrent.Models;

/// <summary>
/// A qBittorrent tag.
/// </summary>
public sealed class Tag
{
    /// <summary>
    /// Gets the tag name.
    /// </summary>
    public required string Name { get; init; }
}
