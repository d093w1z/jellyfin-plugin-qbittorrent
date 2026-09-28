using System.Net;
using Jellyfin.Plugin.QBittorrent.Api;
using Jellyfin.Plugin.QBittorrent.Models;
using Jellyfin.Plugin.QBittorrent.QBittorrent;
using Jellyfin.Plugin.QBittorrent.QBittorrent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.QBittorrent.Tests;

public class ControllerTests
{
    private static T CreateController<T>(IQBittorrentClient client)
        where T : QBittorrentControllerBase
    {
        var controller = (T)Activator.CreateInstance(typeof(T), client)!;
        AttachHttpContext(controller);
        return controller;
    }

    private static void AttachHttpContext(ControllerBase controller)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<ProblemDetailsFactory, DefaultProblemDetailsFactory>();
        var provider = services.BuildServiceProvider();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = provider }
        };
    }

    [Fact]
    public void EveryController_RequiresAdministratorElevation()
    {
        var attribute = typeof(QBittorrentControllerBase).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("RequiresElevation", attribute.Policy);
    }

    [Fact]
    public async Task GetTorrents_PaginatesOverTheFullFilteredList()
    {
        var mock = new Mock<IQBittorrentClient>();
        var all = Enumerable.Range(1, 5)
            .Select(i => new TorrentInfo { Hash = $"h{i}", Name = $"t{i}", State = "Downloading", RawState = "downloading" })
            .ToList();
        mock.Setup(c => c.GetTorrentsAsync(It.IsAny<TorrentFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(all);

        var controller = CreateController<TorrentController>(mock.Object);

        var result = await controller.GetTorrents(null, null, null, null, null, null, page: 2, pageSize: 2, CancellationToken.None);

        var page = Assert.IsType<OkObjectResult>(result.Result).Value as Models.PagedResult<TorrentInfo>;
        Assert.NotNull(page);
        Assert.Equal(5, page!.Total);
        Assert.Equal(["h3", "h4"], page.Items.Select(t => t.Hash));
    }

    [Fact]
    public async Task GetTorrent_ReturnsNotFound_WhenClientReturnsNull()
    {
        var mock = new Mock<IQBittorrentClient>();
        mock.Setup(c => c.GetTorrentAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((TorrentInfo?)null);

        var controller = CreateController<TorrentController>(mock.Object);

        var result = await controller.GetTorrent("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetFiles_MapsQBittorrentApiExceptionToSafeStatusCode()
    {
        var mock = new Mock<IQBittorrentClient>();
        mock.Setup(c => c.GetTorrentFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QBittorrentApiException("Torrent not found", HttpStatusCode.NotFound, "/api/v2/torrents/files"));

        var controller = CreateController<TorrentController>(mock.Object);

        var result = await controller.GetFiles("abc", CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task AddTorrent_WithJsonMagnetBody_ForwardsToClient()
    {
        var mock = new Mock<IQBittorrentClient>();
        AddTorrentRequest? captured = null;
        mock.Setup(c => c.AddTorrentAsync(It.IsAny<AddTorrentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AddTorrentRequest, CancellationToken>((r, _) => captured = r)
            .Returns(Task.CompletedTask);

        var controller = CreateController<TorrentController>(mock.Object);
        var json = "{\"magnetUri\":\"magnet:?xt=abc\",\"category\":\"Movies\",\"savePath\":\"/downloads/movies\",\"startImmediately\":true}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        controller.HttpContext.Request.Body = new MemoryStream(bytes);
        controller.HttpContext.Request.ContentType = "application/json";
        controller.HttpContext.Request.ContentLength = bytes.Length;

        var result = await controller.AddTorrent(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.NotNull(captured);
        Assert.Equal("magnet:?xt=abc", captured!.MagnetUri);
        Assert.Equal("Movies", captured.Category);
    }

    [Fact]
    public async Task AddTorrent_WithMultipartMagnetField_ForwardsToClient()
    {
        // Regression test: the browser's add-torrent form always posts FormData
        // (multipart), even for a magnet-only submission with no file attached.
        var mock = new Mock<IQBittorrentClient>();
        AddTorrentRequest? captured = null;
        mock.Setup(c => c.AddTorrentAsync(It.IsAny<AddTorrentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AddTorrentRequest, CancellationToken>((r, _) => captured = r)
            .Returns(Task.CompletedTask);

        var controller = CreateController<TorrentController>(mock.Object);
        controller.HttpContext.Request.ContentType = "application/x-www-form-urlencoded";
        controller.HttpContext.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["magnetUri"] = "magnet:?xt=abc",
            ["category"] = "Movies"
        });

        var result = await controller.AddTorrent(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("magnet:?xt=abc", captured!.MagnetUri);
    }

    [Fact]
    public async Task AddTorrent_WithNeitherMagnetNorFile_ReturnsBadRequest()
    {
        var mock = new Mock<IQBittorrentClient>();
        var controller = CreateController<TorrentController>(mock.Object);
        var bytes = System.Text.Encoding.UTF8.GetBytes("{}");
        controller.HttpContext.Request.Body = new MemoryStream(bytes);
        controller.HttpContext.Request.ContentType = "application/json";
        controller.HttpContext.Request.ContentLength = bytes.Length;

        var result = await controller.AddTorrent(CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        mock.Verify(c => c.AddTorrentAsync(It.IsAny<AddTorrentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pause_CallsClientWithSingleHash_AndReturnsNoContent()
    {
        var mock = new Mock<IQBittorrentClient>();
        mock.Setup(c => c.PauseAsync(new[] { "abc" }, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var controller = CreateController<TorrentController>(mock.Object);

        var result = await controller.Pause("abc", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        mock.Verify(c => c.PauseAsync(new[] { "abc" }, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_ForwardsDeleteFilesFlag()
    {
        var mock = new Mock<IQBittorrentClient>();
        mock.Setup(c => c.DeleteAsync(new[] { "abc" }, true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var controller = CreateController<TorrentController>(mock.Object);

        var result = await controller.Delete("abc", deleteFiles: true, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        mock.Verify(c => c.DeleteAsync(new[] { "abc" }, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Recheck_MapsQBittorrentApiExceptionToSafeStatusCode()
    {
        var mock = new Mock<IQBittorrentClient>();
        mock.Setup(c => c.RecheckAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QBittorrentApiException("Torrent not found", HttpStatusCode.NotFound, "/api/v2/torrents/recheck"));

        var controller = CreateController<TorrentController>(mock.Object);

        var result = await controller.Recheck("missing", CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetStatus_ReportsDisconnected_WithoutLeakingRawError()
    {
        var mock = new Mock<IQBittorrentClient>();
        mock.Setup(c => c.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QBittorrentApiException("qBittorrent unavailable", endpoint: "/api/v2/app/version"));

        var controller = new QBittorrentController(mock.Object, () => new PluginConfiguration());
        AttachHttpContext(controller);

        var result = await controller.GetStatus(CancellationToken.None);

        var status = Assert.IsType<OkObjectResult>(result.Result).Value as Models.ConnectionStatus;
        Assert.NotNull(status);
        Assert.False(status!.Connected);
        Assert.Equal("qBittorrent unavailable", status.ErrorMessage);
    }

    [Fact]
    public async Task StartSearch_RejectsBlankPattern_AndReturnsJobId()
    {
        var mock = new Mock<IQBittorrentClient>();
        mock.Setup(c => c.StartSearchAsync("ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(42);
        var controller = CreateController<SearchController>(mock.Object);

        var blank = await controller.Start(new SearchController.SearchStartBody { Pattern = "  " }, CancellationToken.None);
        var started = await controller.Start(new SearchController.SearchStartBody { Pattern = " ubuntu " }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(blank.Result);
        var job = Assert.IsType<OkObjectResult>(started.Result).Value as SearchController.SearchJob;
        Assert.Equal(42, job!.Id);
    }
}
