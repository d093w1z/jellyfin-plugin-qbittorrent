namespace Jellyfin.Plugin.QBittorrent.QBittorrent.Models;

/// <summary>
/// A qBittorrent category.
/// </summary>
public sealed class Category
{
    /// <summary>
    /// Gets the category name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the category's default save path.
    /// </summary>
    public string SavePath { get; init; } = string.Empty;
}
