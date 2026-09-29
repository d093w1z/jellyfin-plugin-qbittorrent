using Jellyfin.Plugin.QBittorrent.Integration;
using Jellyfin.Plugin.QBittorrent.QBittorrent;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Authentication;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.QBittorrent;

/// <summary>
/// Registers qBittorrent client services with Jellyfin's dependency injection container.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    private const string HttpClientName = "qbittorrent";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient(HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        serviceCollection.AddSingleton<ApiKeyAuthenticator>();
        serviceCollection.AddSingleton<CookieAuthenticator>(sp =>
            new CookieAuthenticator(sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));

        serviceCollection.AddSingleton(sp => new QBittorrentConnection(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
            GetConnectionOptions,
            sp.GetRequiredService<ApiKeyAuthenticator>(),
            sp.GetRequiredService<CookieAuthenticator>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<QBittorrentConnection>>()));

        serviceCollection.AddSingleton<IQBittorrentClient, QBittorrentClient>();

        serviceCollection.AddSingleton<Func<PluginConfiguration>>(_ => GetConfiguration);

        serviceCollection.AddSingleton<IJellyfinLibraryService, JellyfinLibraryService>();
        serviceCollection.AddHostedService<TorrentCompletionMonitor>();
    }

    private static PluginConfiguration GetConfiguration() => Plugin.Instance!.Configuration;

    private static QBittorrentConnectionOptions GetConnectionOptions()
    {
        var config = GetConfiguration();
        return new QBittorrentConnectionOptions
        {
            BaseUrl = config.QBittorrentUrl,
            AuthenticationMode = config.AuthenticationMode,
            ApiKey = config.ApiKey,
            Username = config.Username,
            Password = config.Password
        };
    }
}
