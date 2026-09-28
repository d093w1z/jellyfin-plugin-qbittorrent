using Jellyfin.Plugin.QBittorrent.Models;
using Jellyfin.Plugin.QBittorrent.QBittorrent;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.QBittorrent.Api;

/// <summary>
/// Connection status for the configured qBittorrent instance. Never accepts a
/// URL from the caller — always checks the administrator-configured qBittorrent
/// URL, never one supplied by the frontend.
/// </summary>
[Route("api/qbittorrent")]
public sealed class QBittorrentController : QBittorrentControllerBase
{
    private readonly IQBittorrentClient _client;
    private readonly Func<PluginConfiguration> _configurationAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="QBittorrentController"/> class.
    /// </summary>
    public QBittorrentController(IQBittorrentClient client, Func<PluginConfiguration> configurationAccessor)
    {
        _client = client;
        _configurationAccessor = configurationAccessor;
    }

    /// <summary>
    /// Gets the current connection status, for periodic/dashboard polling.
    /// </summary>
    [HttpGet("status")]
    public Task<ActionResult<ConnectionStatus>> GetStatus(CancellationToken cancellationToken) => CheckConnectionAsync(cancellationToken);

    /// <summary>
    /// Explicitly tests the connection, e.g. from the configuration page's "Test Connection" button.
    /// </summary>
    [HttpPost("test-connection")]
    public Task<ActionResult<ConnectionStatus>> TestConnection(CancellationToken cancellationToken) => CheckConnectionAsync(cancellationToken);

    /// <summary>
    /// Gets the administrator-configured download profiles offered by the add-torrent UI.
    /// </summary>
    [HttpGet("profiles")]
    public ActionResult<IReadOnlyList<DownloadProfile>> GetProfiles() => Ok(_configurationAccessor().Profiles);

    private async Task<ActionResult<ConnectionStatus>> CheckConnectionAsync(CancellationToken cancellationToken)
    {
        var authenticationMode = _configurationAccessor().AuthenticationMode.ToString();

        try
        {
            var version = await _client.GetVersionAsync(cancellationToken).ConfigureAwait(false);
            return Ok(new ConnectionStatus { Connected = true, QBittorrentVersion = version, AuthenticationMode = authenticationMode });
        }
        catch (QBittorrentApiException ex)
        {
            return Ok(new ConnectionStatus { Connected = false, AuthenticationMode = authenticationMode, ErrorMessage = ex.Message });
        }
    }
}
