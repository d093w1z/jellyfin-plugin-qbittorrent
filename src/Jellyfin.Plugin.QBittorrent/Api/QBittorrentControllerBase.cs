using System.Net;
using Jellyfin.Plugin.QBittorrent.QBittorrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.QBittorrent.Api;

/// <summary>
/// Base class for qBittorrent API controllers. Every route requires an
/// authenticated, administrator Jellyfin user, and every
/// <see cref="QBittorrentApiException"/> is mapped to a safe HTTP response —
/// no raw qBittorrent error bodies or credentials ever reach the client.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
public abstract class QBittorrentControllerBase : ControllerBase
{
    /// <summary>
    /// Runs a qBittorrent operation, mapping <see cref="QBittorrentApiException"/> to a safe response.
    /// </summary>
    protected async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return Ok(await operation().ConfigureAwait(false));
        }
        catch (QBittorrentApiException ex)
        {
            return MapError(ex);
        }
    }

    /// <summary>
    /// Runs a qBittorrent operation with no return value, mapping
    /// <see cref="QBittorrentApiException"/> to a safe response.
    /// </summary>
    protected async Task<ActionResult> ExecuteAsync(Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
            return NoContent();
        }
        catch (QBittorrentApiException ex)
        {
            return MapError(ex);
        }
    }

    /// <summary>
    /// Maps a <see cref="QBittorrentApiException"/> to a safe <see cref="ActionResult"/>.
    /// </summary>
    protected ActionResult MapError(QBittorrentApiException ex)
    {
        var statusCode = ex.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => StatusCodes.Status401Unauthorized,
            HttpStatusCode.NotFound => StatusCodes.Status404NotFound,
            HttpStatusCode.UnsupportedMediaType => StatusCodes.Status422UnprocessableEntity,
            HttpStatusCode.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status502BadGateway
        };

        return Problem(detail: ex.Message, statusCode: statusCode);
    }
}
