using System.Net;
using Jellyfin.Plugin.QBittorrent.QBittorrent;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Authentication;
using Xunit;

namespace Jellyfin.Plugin.QBittorrent.Tests;

public class AuthenticationTests
{
    private static QBittorrentConnectionOptions ApiKeyOptions() => new()
    {
        BaseUrl = "http://qbittorrent:8080",
        AuthenticationMode = AuthenticationMode.ApiKey,
        ApiKey = "secret-key"
    };

    [Fact]
    public async Task ApiKeyAuthenticator_SetsBearerHeader()
    {
        var authenticator = new ApiKeyAuthenticator();
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://qbittorrent:8080/api/v2/app/version");

        await authenticator.AuthenticateRequestAsync(request, ApiKeyOptions(), CancellationToken.None);

        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("secret-key", request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task ApiKeyAuthenticator_NeverRefreshesOnUnauthorized()
    {
        var authenticator = new ApiKeyAuthenticator();
        var refreshed = await authenticator.HandleUnauthorizedAsync(ApiKeyOptions(), CancellationToken.None);
        Assert.False(refreshed);
    }

    [Fact]
    public async Task CookieAuthenticator_LogsInOnceAndReusesCookie()
    {
        var handler = new FakeQBittorrentHandler();
        var loginCalls = 0;
        handler.OnRequest("POST", "/api/v2/auth/login", _ =>
        {
            loginCalls++;
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("Set-Cookie", "SID=abc123; HttpOnly; Path=/");
            return response;
        });

        using var httpClient = new HttpClient(handler);
        var authenticator = new CookieAuthenticator(httpClient);
        var options = new QBittorrentConnectionOptions
        {
            BaseUrl = "http://qbittorrent:8080",
            AuthenticationMode = AuthenticationMode.UsernamePassword,
            Username = "admin",
            Password = "adminadmin"
        };

        using var request1 = new HttpRequestMessage(HttpMethod.Get, "http://qbittorrent:8080/api/v2/torrents/info");
        await authenticator.AuthenticateRequestAsync(request1, options, CancellationToken.None);

        using var request2 = new HttpRequestMessage(HttpMethod.Get, "http://qbittorrent:8080/api/v2/torrents/info");
        await authenticator.AuthenticateRequestAsync(request2, options, CancellationToken.None);

        Assert.Equal(1, loginCalls);
        Assert.Equal("SID=abc123", request1.Headers.GetValues("Cookie").Single());
        Assert.Equal("SID=abc123", request2.Headers.GetValues("Cookie").Single());
    }

    private sealed class UnreachableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Connection refused");
    }

    [Fact]
    public async Task CookieAuthenticator_MapsConnectionFailureToSafeException()
    {
        using var httpClient = new HttpClient(new UnreachableHandler());
        var authenticator = new CookieAuthenticator(httpClient);
        var options = new QBittorrentConnectionOptions
        {
            BaseUrl = "http://qbittorrent:8080",
            AuthenticationMode = AuthenticationMode.UsernamePassword,
            Username = "admin",
            Password = "adminadmin"
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, "http://qbittorrent:8080/api/v2/torrents/info");
        var ex = await Assert.ThrowsAsync<QBittorrentApiException>(
            () => authenticator.AuthenticateRequestAsync(request, options, CancellationToken.None));

        Assert.Equal("qBittorrent unavailable", ex.Message);
    }

    [Fact]
    public async Task CookieAuthenticator_ReLoginsOnUnauthorized()
    {
        var handler = new FakeQBittorrentHandler();
        var loginCalls = 0;
        handler.OnRequest("POST", "/api/v2/auth/login", _ =>
        {
            loginCalls++;
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("Set-Cookie", $"SID=session{loginCalls}; HttpOnly; Path=/");
            return response;
        });

        using var httpClient = new HttpClient(handler);
        var authenticator = new CookieAuthenticator(httpClient);
        var options = new QBittorrentConnectionOptions
        {
            BaseUrl = "http://qbittorrent:8080",
            AuthenticationMode = AuthenticationMode.UsernamePassword,
            Username = "admin",
            Password = "adminadmin"
        };

        var refreshed = await authenticator.HandleUnauthorizedAsync(options, CancellationToken.None);

        Assert.True(refreshed);
        Assert.Equal(1, loginCalls);
    }
}
