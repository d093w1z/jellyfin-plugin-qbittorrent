using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.QBittorrent;

/// <summary>
/// Authentication strategy used to talk to qBittorrent.
/// </summary>
public enum AuthenticationMode
{
    /// <summary>
    /// Bearer API key (qBittorrent &gt;= 5.2.0 / WebAPI &gt;= 2.14.1).
    /// </summary>
    ApiKey,

    /// <summary>
    /// Username/password cookie-based login (compatibility fallback).
    /// </summary>
    UsernamePassword
}

/// <summary>
/// A named download profile — a category + save path pair the add-torrent UI can
/// offer as a preset, so the user isn't retyping filesystem paths every time.
/// </summary>
public sealed class DownloadProfile
{
    /// <summary>
    /// Gets or sets the profile's display name, e.g. "Movies".
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the qBittorrent category to assign.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the save path qBittorrent should use.
    /// </summary>
    public string SavePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the id of the Jellyfin library to scan when a torrent in this
    /// profile's category completes. Empty means scan all libraries.
    /// </summary>
    public string JellyfinLibraryId { get; set; } = string.Empty;
}

/// <summary>
/// Plugin configuration. Never return <see cref="ApiKey"/> or <see cref="Password"/>
/// through a normal API response.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the base URL of the qBittorrent Web API, e.g. <c>http://qbittorrent:8080</c>.
    /// </summary>
    public string QBittorrentUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authentication strategy to use.
    /// </summary>
    public AuthenticationMode AuthenticationMode { get; set; } = AuthenticationMode.ApiKey;

    /// <summary>
    /// Gets or sets the qBittorrent API key. Server-side only, never sent to the browser.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the qBittorrent username (fallback authentication).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the qBittorrent password. Server-side only, never sent to the browser.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the dashboard widget is enabled.
    /// </summary>
    public bool EnableDashboardWidget { get; set; } = true;

    /// <summary>
    /// Gets or sets the UI polling interval, in seconds.
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether completion monitoring is enabled.
    /// </summary>
    public bool EnableCompletionMonitoring { get; set; }

    /// <summary>
    /// Gets or sets how often completion monitoring polls qBittorrent, in seconds.
    /// </summary>
    public int CompletionPollIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether a library scan is triggered on torrent completion.
    /// </summary>
    public bool ScanLibraryOnCompletion { get; set; }

    /// <summary>
    /// Gets or sets the configured download profiles offered by the add-torrent UI.
    /// </summary>
    public List<DownloadProfile> Profiles { get; set; } = [];
}
