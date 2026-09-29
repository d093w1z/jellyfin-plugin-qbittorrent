using Jellyfin.Plugin.QBittorrent.QBittorrent;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Models;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.QBittorrent.Api;

/// <summary>
/// Global transfer statistics.
/// </summary>
[Route("api/qbittorrent/transfer")]
public sealed class TransferController : QBittorrentControllerBase
{
    private readonly IQBittorrentClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferController"/> class.
    /// </summary>
    public TransferController(IQBittorrentClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Gets global transfer statistics.
    /// </summary>
    [HttpGet]
    public Task<ActionResult<TransferInfo>> Get(CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.GetTransferInfoAsync(cancellationToken));

    /// <summary>
    /// Sets the global download/upload limits in bytes per second; 0 means unlimited.
    /// </summary>
    [HttpPut("limits")]
    public async Task<ActionResult> SetLimits([FromBody] SpeedLimitsBody body, CancellationToken cancellationToken)
    {
        if (body is null || body.DownloadLimit < 0 || body.UploadLimit < 0)
        {
            return BadRequest("Limits must be 0 (unlimited) or a positive number of bytes per second.");
        }

        return await ExecuteAsync(() => _client.SetSpeedLimitsAsync(body.DownloadLimit, body.UploadLimit, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Switches qBittorrent's alternative speed limits on or off.
    /// </summary>
    [HttpPost("alternative-speed-limits/toggle")]
    public Task<ActionResult> ToggleAlternativeSpeedLimits(CancellationToken cancellationToken)
        => ExecuteAsync(() => _client.ToggleAlternativeSpeedLimitsAsync(cancellationToken));

    /// <summary>
    /// Request body for <see cref="SetLimits"/>.
    /// </summary>
    public sealed class SpeedLimitsBody
    {
        /// <summary>
        /// Gets or sets the download limit in bytes per second; 0 means unlimited.
        /// </summary>
        public long DownloadLimit { get; set; }

        /// <summary>
        /// Gets or sets the upload limit in bytes per second; 0 means unlimited.
        /// </summary>
        public long UploadLimit { get; set; }
    }
}
