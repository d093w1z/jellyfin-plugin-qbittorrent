using System;
using System.Linq;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.QBittorrent.Tests;

public class PluginTests
{
    private static Plugin CreatePlugin()
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(p => p.PluginsPath).Returns(Environment.CurrentDirectory);
        applicationPaths.SetupGet(p => p.PluginConfigurationsPath).Returns(Environment.CurrentDirectory);

        var xmlSerializer = new Mock<IXmlSerializer>();

        return new Plugin(applicationPaths.Object, xmlSerializer.Object);
    }

    [Fact]
    public void Plugin_ExposesStableIdentity()
    {
        var plugin = CreatePlugin();

        Assert.Equal("qBittorrent Manager", plugin.Name);
        Assert.Equal(Guid.Parse("f6a0e4c2-3b3e-4b9a-9f4e-8a4a1d2c9b7d"), plugin.Id);
        Assert.Same(plugin, Plugin.Instance);
    }

    [Fact]
    public void GetPages_ReturnsConfigurationAndMainMenuPages()
    {
        var plugin = CreatePlugin();

        var pages = plugin.GetPages().ToList();

        var configPage = Assert.Single(pages, p => p.Name == "qBittorrent Manager");
        Assert.Equal("Jellyfin.Plugin.QBittorrent.Resources.PluginConfiguration.html", configPage.EmbeddedResourcePath);
        Assert.False(configPage.EnableInMainMenu);

        var mainPage = Assert.Single(pages, p => p.Name == "qbittorrent");
        Assert.Equal("Jellyfin.Plugin.QBittorrent.Web.index.html", mainPage.EmbeddedResourcePath);
        Assert.True(mainPage.EnableInMainMenu);
        Assert.Equal("server", mainPage.MenuSection);
    }
}
