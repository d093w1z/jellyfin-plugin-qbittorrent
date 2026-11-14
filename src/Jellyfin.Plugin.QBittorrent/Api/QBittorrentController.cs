using Jellyfin.Plugin.QBittorrent.Models;
using Jellyfin.Plugin.QBittorrent.QBittorrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.QBittorrent.Api;

/// <summary>
/// Connection status for the configured qBittorrent instance. Never accepts a
/// URL from the caller — always checks the administrator-configured qBittorrent
/// URL, never one supplied by the frontend.
/// </summary>
[Route("api/qbittorrent")]
public sealed class QBittorrentController : QBittorrentControllerBase
{
    // Injected by SidebarScriptStartupFilter into Jellyfin's index.html.
    // Adds a qBittorrent Manager entry to Jellyfin 12's avatar/profile dropdown menu.
    // Selectors target Jellyfin 12's MUI-based user menu (same pattern as Jellyscribe plugin).
    private const string SidebarScript = """
        (function(){
          'use strict';
          var ATTR='data-qbt-nav',HASH='#/qbittorrent.html',LABEL='qBittorrent Manager';
          function inject(){
            if(document.querySelector('['+ATTR+']'))return;
            var settings=document.querySelector('#app-user-menu a[href="#/mypreferencesmenu"]');
            if(!settings)return;
            var el=settings.cloneNode(true);
            el.setAttribute(ATTR,'');
            el.href='#';
            var lbl=el.querySelector('.MuiListItemText-primary');
            if(lbl)lbl.textContent=LABEL;
            var svg=el.querySelector('svg');
            if(svg){var icon=document.createElement('span');icon.className='material-icons';icon.textContent='download';svg.parentNode.replaceChild(icon,svg);}
            el.addEventListener('click',function(e){
              e.preventDefault();e.stopPropagation();
              var bd=document.querySelector('.MuiBackdrop-root');if(bd)bd.click();
              location.hash=HASH;
            });
            settings.parentNode.insertBefore(el,settings.nextSibling);
          }
          new MutationObserver(inject).observe(document.body,{childList:true});
          setTimeout(inject,1000);
        })();
        """;

    private readonly IQBittorrentClient _client;
    private readonly Func<PluginConfiguration> _configurationAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="QBittorrentController"/> class.
    /// </summary>
    public QBittorrentController(IQBittorrentClient client, Func<PluginConfiguration> configurationAccessor)
    {
        _client = client;
        _configurationAccessor = configurationAccessor;
    }

    /// <summary>
    /// Returns the sidebar injection script served by <see cref="SidebarScriptStartupFilter"/>.
    /// Anonymous: loaded by the browser from index.html before any auth token is available.
    /// </summary>
    [HttpGet("sidebar.js")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    public ContentResult GetSidebarScript() => Content(SidebarScript, "application/javascript");

    /// <summary>
    /// Gets the current connection status, for periodic/dashboard polling.
    /// </summary>
    [HttpGet("status")]
    public Task<ActionResult<ConnectionStatus>> GetStatus(CancellationToken cancellationToken) => CheckConnectionAsync(cancellationToken);

    /// <summary>
    /// Explicitly tests the connection, e.g. from the configuration page's "Test Connection" button.
    /// </summary>
    [HttpPost("test-connection")]
    public Task<ActionResult<ConnectionStatus>> TestConnection(CancellationToken cancellationToken) => CheckConnectionAsync(cancellationToken);

    /// <summary>
    /// Gets the administrator-configured download profiles offered by the add-torrent UI.
    /// </summary>
    [HttpGet("profiles")]
    public ActionResult<IReadOnlyList<DownloadProfile>> GetProfiles() => Ok(_configurationAccessor().Profiles);

    private async Task<ActionResult<ConnectionStatus>> CheckConnectionAsync(CancellationToken cancellationToken)
    {
        var authenticationMode = _configurationAccessor().AuthenticationMode.ToString();

        var pluginVersion = Plugin.Instance?.Version?.ToString();

        try
        {
            var version = await _client.GetVersionAsync(cancellationToken).ConfigureAwait(false);
            return Ok(new ConnectionStatus { Connected = true, QBittorrentVersion = version, AuthenticationMode = authenticationMode, PluginVersion = pluginVersion });
        }
        catch (QBittorrentApiException ex)
        {
            return Ok(new ConnectionStatus { Connected = false, AuthenticationMode = authenticationMode, PluginVersion = pluginVersion, ErrorMessage = ex.Message });
        }
    }
}
