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

Copy the contents of `./artifacts` into your Jellyfin server's
`plugins/qBittorrent Manager_<version>/` directory and restart Jellyfin.

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
