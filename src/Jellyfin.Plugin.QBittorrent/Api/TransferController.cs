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
}
