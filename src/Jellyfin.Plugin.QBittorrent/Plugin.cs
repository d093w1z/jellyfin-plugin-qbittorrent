using System;
using System.Collections.Generic;
using System.Globalization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.QBittorrent;

/// <summary>
/// The main plugin entry point. Identifies the plugin and exposes its configuration
/// page. Contains no qBittorrent business logic; service registration is added in a
/// later phase via <c>IPluginServiceRegistrator</c>.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "qBittorrent Manager";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("f6a0e4c2-3b3e-4b9a-9f4e-8a4a1d2c9b7d");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = GetType().Namespace;
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Resources.PluginConfiguration.html", ns)
            },
            new PluginPageInfo
            {
                Name = "qbittorrent",
                DisplayName = "qBittorrent",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Web.index.html", ns),
                EnableInMainMenu = true,
                MenuSection = "server",
                MenuIcon = "download"
            }
        ];
    }
}
