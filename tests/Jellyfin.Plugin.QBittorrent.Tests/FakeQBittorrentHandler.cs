using System.Net;
using System.Text;

namespace Jellyfin.Plugin.QBittorrent.Tests;

/// <summary>
/// A minimal in-memory stand-in for qBittorrent's Web API, so client tests don't
/// depend on a real qBittorrent installation.
/// </summary>
internal sealed class FakeQBittorrentHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public void OnRequest(string method, string path, Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _routes[$"{method} {path}"] = handler;
    }

    public void OnJson(string method, string path, string json)
    {
        OnRequest(method, path, _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        var key = $"{request.Method.Method} {request.RequestUri!.AbsolutePath}";
        if (_routes.TryGetValue(key, out var handler))
        {
            return Task.FromResult(handler(request));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
