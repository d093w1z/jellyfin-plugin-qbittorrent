# Jellyfin qBittorrent Manager

A Jellyfin server plugin for remotely managing a qBittorrent instance from
within Jellyfin. The browser never talks to qBittorrent directly; every
request is proxied through the Jellyfin server, which is the only place
qBittorrent credentials are ever held.

```text
Browser -> Jellyfin -> Plugin -> qBittorrent
```

See [docs/spec.md](docs/spec.md) for the full engineering specification.

## Status

Through Phase 4: the plugin loads in Jellyfin, connects to qBittorrent
(API-key or username/password), exposes a read-only, paginated REST API
under `/api/qbittorrent`, and has a read-only web UI — reachable from the
server's Dashboard sidebar as "qBittorrent" — showing live transfer stats,
a searchable/filterable torrent list, and per-torrent files/peers/trackers.
All endpoints require an authenticated Jellyfin administrator. Add-torrent
and management actions (pause/resume/delete/etc.) land in subsequent phases
— see the spec's version roadmap.

## Torrent search

The **Search** tab searches torrent sites through qBittorrent's own search
engine, using whichever search plugins are installed and enabled in
qBittorrent (View → Search Engine → Search plugins; qBittorrent needs Python
for these). The plugin doesn't ship or manage search plugins itself. **Add**
on a result opens the usual add dialog with its link filled in, so profiles,
category and save path work the same way as for any other torrent.

## Tags, file priority and speed limits

* **Tags** show on each torrent; edit them from the torrent's detail panel
  (comma-separated; new tags are created in qBittorrent automatically).
* **File priority** (Skip / Normal / High / Maximum) is set per file in the
  Files tab.
* **Limits** (header button) sets the global download/upload limits and
  switches qBittorrent's alternative speed limits on or off; the active limits
  show under the speeds.

The server keeps one mirror of qBittorrent's state via its sync API, so
polling costs a small diff rather than the full torrent list.

## Library scans on completion

With **Monitor torrents for completion** enabled, the server checks
qBittorrent every *Completion check interval* seconds (default 30) and notices
when a torrent finishes (fully downloaded and no longer moving or checking).
Torrents already complete when monitoring starts are ignored.

With **Scan Jellyfin library when a torrent completes** also enabled, each
download profile can name a Jellyfin library: a finished torrent whose
category matches the profile scans just that library. If a finished torrent
matches no profile with a library, Jellyfin's regular *Scan Media Library*
task is queued instead. Several torrents finishing together trigger each
scan once.

## Build

Requires the .NET 10 SDK on Linux.

```bash
dotnet restore
dotnet build -c Release
dotnet test
```

Produce a plugin artifact:

```bash
dotnet publish src/Jellyfin.Plugin.QBittorrent -c Release -o ./artifacts
```

## Install

Every push to `main` publishes a GitHub release with the plugin zip and adds
it to the Jellyfin repository manifest on the `manifest` branch.

**Plugin repository (auto-updates):** in Jellyfin go to Dashboard → Plugins →
Repositories, add

```text
https://raw.githubusercontent.com/d093w1z/jellyfin-plugin-qbittorrent/manifest/manifest.json
```

then install *qBittorrent Manager* from the Catalog and restart. Jellyfin's
"Update Plugins" scheduled task picks up new releases; restart Jellyfin to
load them.

**Manual, from the Jellyfin machine's terminal:**

```bash
PLUGINS=/var/lib/jellyfin/plugins   # adjust for your install / Docker volume
rm -rf "$PLUGINS"/"qBittorrent Manager"*
mkdir -p "$PLUGINS/qBittorrent Manager"
curl -fsSL -o /tmp/qbt.zip https://github.com/d093w1z/jellyfin-plugin-qbittorrent/releases/latest/download/qbittorrent-manager.zip
unzip -o /tmp/qbt.zip -d "$PLUGINS/qBittorrent Manager" && rm /tmp/qbt.zip
chown -R jellyfin:jellyfin "$PLUGINS/qBittorrent Manager"
systemctl restart jellyfin          # or: docker restart jellyfin
```

Use one method or the other; don't mix a manual copy with a repository install.

## Compatibility

Targets Jellyfin 12.x (`Jellyfin.Controller`/`Jellyfin.Model` 12.1.0, .NET 10).
Requires qBittorrent with the WebUI API enabled; API-key authentication
requires qBittorrent >= 5.2.0 (WebAPI >= 2.14.1), with username/password
login as a fallback for older versions.

## Security

* qBittorrent credentials (API key, password, auth cookie) are never sent
  to the browser, never logged, and never accepted from the frontend.
* The qBittorrent URL is an administrator-only setting; ordinary users
  cannot see or change it, and no endpoint accepts an arbitrary target URL.
* All management endpoints require an authenticated Jellyfin user; by
  default only administrators can use them.

Recommended deployment keeps qBittorrent off the public Internet entirely,
reachable only from Jellyfin over a private/Docker network or LAN.
