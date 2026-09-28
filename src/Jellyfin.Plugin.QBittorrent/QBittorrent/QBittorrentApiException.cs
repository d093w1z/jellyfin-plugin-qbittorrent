using System.Net;

namespace Jellyfin.Plugin.QBittorrent.QBittorrent;

/// <summary>
/// A safe, structured exception for qBittorrent API failures. Never carries
/// credentials or raw response bodies — only what's needed to map to a
/// user-facing message.
/// </summary>
public sealed class QBittorrentApiException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QBittorrentApiException"/> class.
    /// </summary>
    /// <param name="message">A safe, non-sensitive error message.</param>
    /// <param name="statusCode">The HTTP status code returned by qBittorrent, if any.</param>
    /// <param name="endpoint">The relative endpoint path that was called, e.g. <c>/api/v2/torrents/info</c>.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public QBittorrentApiException(string message, HttpStatusCode? statusCode = null, string? endpoint = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Endpoint = endpoint;
    }

    /// <summary>
    /// Gets the HTTP status code returned by qBittorrent, if any.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets the relative endpoint path that was called.
    /// </summary>
    public string? Endpoint { get; }
}
