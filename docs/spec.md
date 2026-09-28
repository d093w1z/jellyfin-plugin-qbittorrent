# Jellyfin qBittorrent Manager — Engineering Specification

## 1. Project summary

Build a **Jellyfin Server Plugin** that provides a web-based interface for remotely managing a qBittorrent instance from within Jellyfin.

The plugin must:

* Run entirely on the Jellyfin server.
* Communicate with qBittorrent through its Web API.
* Provide a Jellyfin-native web interface.
* Authenticate users through Jellyfin.
* Never expose qBittorrent credentials to the browser.
* Support qBittorrent API-key authentication.
* Provide username/password authentication as a compatibility fallback.
* Allow torrent browsing, searching, filtering, adding, controlling, and deleting.
* Display qBittorrent transfer statistics.
* Optionally trigger Jellyfin library scans after torrent completion.
* Be developed and built on Linux.
* Be suitable for installation into a standard Jellyfin Linux/Docker deployment.

The project should be designed so that **qBittorrent does not need to be exposed to the Internet**.

---

# 2. Primary architecture

```text
                    REMOTE CLIENT
                 Phone / Desktop Browser
                          │
                          │ HTTPS
                          ▼
                 ┌───────────────────┐
                 │     Jellyfin      │
                 │                   │
                 │  Authentication   │
                 │        │          │
                 │        ▼          │
                 │ ┌────────────────┐│
                 │ │ qBit Web UI     ││
                 │ └───────┬────────┘│
                 │         │         │
                 │         ▼         │
                 │ ┌────────────────┐│
                 │ │ Plugin API      ││
                 │ └───────┬────────┘│
                 │         │         │
                 │         ▼         │
                 │ ┌────────────────┐│
                 │ │ qBit Service    ││
                 │ └───────┬────────┘│
                 └─────────┼─────────┘
                           │
                     HTTP / local LAN
                           │
                           ▼
                 ┌───────────────────┐
                 │    qBittorrent    │
                 │    Web API        │
                 └─────────┬─────────┘
                           │
                           ▼
                      Download FS
                           │
                           ▼
                      Jellyfin FS
```

### Critical architectural rule

The browser must **never communicate directly with qBittorrent**.

All communication must be:

```text
Browser
  ↓
Jellyfin
  ↓
Plugin
  ↓
qBittorrent
```

---

# 3. Technology stack

## Backend

Use:

* C#
* .NET version compatible with the target Jellyfin version
* Jellyfin plugin SDK/API
* `HttpClient`
* `System.Text.Json`
* standard .NET dependency injection

Avoid unnecessary third-party dependencies.

## Frontend

Use the technologies already used by the target Jellyfin web client/plugin architecture.

Do **not** create a separate React/Vue/Next.js application unless Jellyfin's plugin architecture requires it.

The UI should integrate into Jellyfin rather than becoming an independently hosted web application.

## Development environment

Target:

```text
Linux
├── Git
├── .NET SDK
├── Node.js/npm (only if required by Jellyfin frontend tooling)
├── Docker (optional)
└── Jellyfin development/runtime environment
```

The project must be buildable entirely from Linux CLI.

---

# 4. Repository structure

Use this structure:

```text
jellyfin-plugin-qbittorrent/
│
├── README.md
├── LICENSE
├── .gitignore
├── .editorconfig
│
├── Jellyfin.Plugin.QBittorrent.sln
│
├── Directory.Build.props
├── Directory.Build.targets
├── Directory.Packages.props
│
├── src/
│   └── Jellyfin.Plugin.QBittorrent/
│       │
│       ├── Plugin.cs
│       ├── PluginConfiguration.cs
│       ├── PluginServiceRegistrator.cs
│       │
│       ├── Api/
│       │   ├── QBittorrentController.cs
│       │   ├── TorrentController.cs
│       │   ├── TransferController.cs
│       │   ├── CategoryController.cs
│       │   └── SettingsController.cs
│       │
│       ├── Services/
│       │   ├── QBittorrentService.cs
│       │   ├── TorrentMonitorService.cs
│       │   ├── TorrentStateCache.cs
│       │   ├── ConnectionMonitorService.cs
│       │   └── JellyfinLibraryService.cs
│       │
│       ├── QBittorrent/
│       │   ├── IQBittorrentClient.cs
│       │   ├── QBittorrentClient.cs
│       │   ├── QBittorrentConnection.cs
│       │   ├── QBittorrentApiException.cs
│       │   │
│       │   ├── Authentication/
│       │   │   ├── IQBittorrentAuthenticator.cs
│       │   │   ├── ApiKeyAuthenticator.cs
│       │   │   └── CookieAuthenticator.cs
│       │   │
│       │   └── Models/
│       │       ├── TorrentInfo.cs
│       │       ├── TorrentFile.cs
│       │       ├── TorrentPeer.cs
│       │       ├── TorrentTracker.cs
│       │       ├── TransferInfo.cs
│       │       ├── TorrentProperties.cs
│       │       ├── Category.cs
│       │       ├── Tag.cs
│       │       └── TorrentFilter.cs
│       │
│       ├── Models/
│       │   ├── AddTorrentRequest.cs
│       │   ├── PluginStatus.cs
│       │   └── ConnectionStatus.cs
│       │
│       ├── Web/
│       │   ├── index.html
│       │   ├── qbit.js
│       │   └── qbit.css
│       │
│       └── Resources/
│           └── PluginConfiguration.html
│
├── tests/
│   └── Jellyfin.Plugin.QBittorrent.Tests/
│       ├── QBittorrentClientTests.cs
│       ├── AuthenticationTests.cs
│       ├── TorrentServiceTests.cs
│       ├── TorrentMonitorTests.cs
│       └── ControllerTests.cs
│
├── docs/
│   ├── architecture.md
│   ├── api.md
│   ├── development.md
│   └── installation.md
│
└── .github/
    └── workflows/
        ├── build.yml
        └── release.yml
```

---

# 5. Plugin responsibilities

`Plugin.cs` should:

* identify the plugin
* expose plugin metadata
* expose configuration
* register plugin services
* expose plugin assembly information
* provide the plugin configuration page

It should **not** contain qBittorrent business logic.

---

# 6. Configuration

Create a configuration object similar to:

```csharp
public sealed class PluginConfiguration
{
    public string QBittorrentUrl { get; set; } = string.Empty;

    public AuthenticationMode AuthenticationMode { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool EnableDashboardWidget { get; set; } = true;

    public int RefreshIntervalSeconds { get; set; } = 3;

    public bool EnableCompletionMonitoring { get; set; }

    public bool ScanLibraryOnCompletion { get; set; }
}
```

Enum:

```csharp
public enum AuthenticationMode
{
    ApiKey,
    UsernamePassword
}
```

Do not return credentials through normal API responses.

---

# 7. qBittorrent client abstraction

Everything interacting with qBittorrent must go through:

```csharp
public interface IQBittorrentClient
{
    Task<TransferInfo> GetTransferInfoAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TorrentInfo>> GetTorrentsAsync(
        TorrentFilter? filter,
        CancellationToken cancellationToken);

    Task<TorrentInfo?> GetTorrentAsync(
        string hash,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TorrentFile>> GetTorrentFilesAsync(
        string hash,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TorrentPeer>> GetTorrentPeersAsync(
        string hash,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TorrentTracker>> GetTorrentTrackersAsync(
        string hash,
        CancellationToken cancellationToken);

    Task AddTorrentAsync(
        AddTorrentRequest request,
        CancellationToken cancellationToken);

    Task PauseAsync(
        IEnumerable<string> hashes,
        CancellationToken cancellationToken);

    Task ResumeAsync(
        IEnumerable<string> hashes,
        CancellationToken cancellationToken);

    Task ForceResumeAsync(
        IEnumerable<string> hashes,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        IEnumerable<string> hashes,
        bool deleteFiles,
        CancellationToken cancellationToken);

    Task RecheckAsync(
        IEnumerable<string> hashes,
        CancellationToken cancellationToken);

    Task ReannounceAsync(
        IEnumerable<string> hashes,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Category>> GetCategoriesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Tag>> GetTagsAsync(
        CancellationToken cancellationToken);

    Task SetCategoryAsync(
        IEnumerable<string> hashes,
        string category,
        CancellationToken cancellationToken);

    Task TestConnectionAsync(
        CancellationToken cancellationToken);
}
```

This abstraction is important because it allows the qBittorrent implementation to be tested without Jellyfin.

---

# 8. Authentication architecture

Implement two authentication strategies.

```text
IQBittorrentAuthenticator
          │
     ┌────┴─────┐
     ▼          ▼
 API Key      Cookie
```

## API key

For supported qBittorrent versions:

```http
Authorization: Bearer <api-key>
```

The API key should never be sent to the browser.

## Username/password fallback

For older versions:

```text
POST /api/v2/auth/login
```

Store the returned authentication cookie in the server-side client.

Handle:

* login
* cookie expiration
* reauthentication
* failed authentication

---

# 9. HTTP client requirements

Use a dedicated `HttpClient`.

Requirements:

* connection timeout
* cancellation token support
* reasonable request timeout
* automatic authentication
* status-code validation
* structured exceptions
* logging
* no credential logging

Never log:

```text
API key
password
authentication cookie
Authorization header
```

---

# 10. qBittorrent API errors

Create:

```csharp
public sealed class QBittorrentApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public string? Endpoint { get; }

    ...
}
```

Map errors into safe Jellyfin responses.

Example:

```text
qBittorrent unavailable
Authentication failed
Torrent not found
Invalid torrent hash
Operation rejected
```

Do not expose raw credentials or internal HTTP details to the UI.

---

# 11. Torrent model

Represent qBittorrent's torrent information internally.

Example:

```csharp
public sealed class TorrentInfo
{
    public string Hash { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    public long Size { get; init; }
    public long Completed { get; init; }

    public double Progress { get; init; }

    public long DownloadSpeed { get; init; }
    public long UploadSpeed { get; init; }

    public long Downloaded { get; init; }
    public long Uploaded { get; init; }

    public long Eta { get; init; }

    public int Seeds { get; init; }
    public int Peers { get; init; }

    public string State { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string SavePath { get; init; } = string.Empty;

    public DateTimeOffset? AddedOn { get; init; }
}
```

Do not blindly expose the qBittorrent API's JSON objects to the frontend.

Use plugin-owned DTOs.

---

# 12. Torrent state normalization

qBittorrent exposes several states.

Normalize them for the UI.

For example:

```text
Downloading
Uploading
Paused
Queued
Checking
Error
Completed
Unknown
```

Internally retain the original qBittorrent state.

Example:

```text
downloading
      ↓
Downloading

uploading
      ↓
Seeding

pausedDL
pausedUP
      ↓
Paused
```

The UI should not need to understand every qBittorrent-specific state.

---

# 13. REST API exposed by the plugin

Use a consistent namespace.

```text
/api/qbittorrent/status

/api/qbittorrent/transfer

/api/qbittorrent/torrents

/api/qbittorrent/torrents/{hash}

/api/qbittorrent/torrents/{hash}/pause
/api/qbittorrent/torrents/{hash}/resume
/api/qbittorrent/torrents/{hash}/force-resume
/api/qbittorrent/torrents/{hash}/recheck
/api/qbittorrent/torrents/{hash}/reannounce

/api/qbittorrent/torrents/{hash}/files
/api/qbittorrent/torrents/{hash}/peers
/api/qbittorrent/torrents/{hash}/trackers

/api/qbittorrent/categories
/api/qbittorrent/tags

/api/qbittorrent/test-connection
```

For destructive operations:

```text
DELETE /api/qbittorrent/torrents/{hash}
```

Support:

```text
?deleteFiles=true
```

---

# 14. Add torrent API

Endpoint:

```text
POST /api/qbittorrent/torrents
```

Request:

```json
{
  "magnetUri": "magnet:?xt=...",
  "savePath": "/downloads/movies",
  "category": "Movies",
  "startImmediately": true
}
```

Also support multipart upload:

```text
POST /api/qbittorrent/torrents
Content-Type: multipart/form-data
```

with:

```text
torrentFile
savePath
category
startImmediately
```

Do not allow arbitrary filesystem paths to be interpreted by the plugin unless qBittorrent itself is configured to accept them.

---

# 15. Torrent list API

Support:

```text
GET /api/qbittorrent/torrents
```

Query parameters:

```text
search=
state=
category=
tag=
sort=
order=
page=
pageSize=
```

Example:

```text
/api/qbittorrent/torrents?search=ubuntu&state=downloading
```

The UI should support:

```text
All
Downloading
Seeding
Paused
Completed
Errored
```

---

# 16. Pagination

Do not load thousands of torrents into the browser at once.

The API should support pagination.

```json
{
  "items": [],
  "total": 153,
  "page": 1,
  "pageSize": 50
}
```

If qBittorrent can efficiently perform the filtering/sorting, leverage it.

Otherwise implement server-side filtering.

---

# 17. Frontend

The UI should feel like a Jellyfin screen.

Primary navigation:

```text
Jellyfin
├── Home
├── Movies
├── Shows
├── ...
└── qBittorrent
```

If modifying the main navigation isn't reliable across Jellyfin versions, expose the plugin through the plugin/dashboard interface instead.

Do not make navigation changes that depend on private Jellyfin internals unless unavoidable.

---

# 18. Main qBittorrent page

Structure:

```text
┌──────────────────────────────────────────────┐
│ qBittorrent                                  │
│                                              │
│  ↓ 42.8 MB/s       ↑ 8.2 MB/s              │
│  12 Downloading     35 Seeding              │
│                                              │
│ ┌──────────────────────────────────────────┐ │
│ │ Search torrents...                       │ │
│ └──────────────────────────────────────────┘ │
│                                              │
│ [All] [Downloading] [Seeding] [Paused]      │
│                                              │
│ torrent cards                                │
│                                              │
│                              [ + Add ]       │
└──────────────────────────────────────────────┘
```

---

# 19. Torrent card

Display:

```text
Name
Progress
Progress bar
Download speed
Upload speed
ETA
Seeds
Peers
Category
State
```

Example:

```text
┌────────────────────────────────────────────┐
│ Movie.Name.2026                            │
│                                            │
│ ███████████████████░░ 92.4%                │
│                                            │
│ ↓ 18.2 MB/s     ↑ 2.4 MB/s     ETA 3m      │
│ Seeds 42       Peers 14                    │
│ Movies                         ⋮           │
└────────────────────────────────────────────┘
```

---

# 20. Torrent actions

Context menu:

```text
Resume
Pause
Force Resume
Recheck
Reannounce
Set Category
Set Tags
Delete
Delete + Files
```

For:

**Delete + Files**

show a confirmation dialog.

```text
Delete torrent and downloaded files?

Movie.Name.2026

This will permanently delete the torrent
and its downloaded data.

[Cancel] [Delete Files]
```

---

# 21. Torrent detail page

Tabs:

```text
Overview
Files
Peers
Trackers
```

Overview:

```text
Name
Hash
State
Progress
Size
Downloaded
Uploaded
Download speed
Upload speed
ETA
Seeds
Peers
Category
Tags
Save path
Added date
```

---

# 22. Files tab

Display:

```text
Filename
Size
Progress
Priority
```

Example:

```text
Movie.mkv             7.8 GB    100%
poster.jpg             250 KB   100%
subtitles.srt           50 KB   100%
```

Phase 1 does not need file-priority editing.

---

# 23. Transfer statistics

Top-level statistics:

```text
Download speed
Upload speed
Download total
Upload total
DHT nodes
Connection status
Global download limit
Global upload limit
```

Display live speed.

Refresh interval:

```text
default = 3 seconds
```

Make it configurable.

Do not create a new HTTP connection for every UI component.

Use a centralized polling mechanism.

---

# 24. Dashboard widget

Optional dashboard card:

```text
qBittorrent

↓ 42.8 MB/s
↑ 8.2 MB/s

3 downloading
9 seeding

Movie.Name.2026
████████████████░░ 82%

Another.Movie
████████░░░░░░░░░░ 43%

[Open]
```

The widget must not cause significant qBittorrent traffic when the dashboard isn't visible.

---

# 25. Add Torrent UI

```text
┌──────────────────────────────────────────┐
│ Add Torrent                               │
│                                          │
│ Magnet / URL                              │
│ ┌──────────────────────────────────────┐ │
│ │ magnet:?xt=...                       │ │
│ └──────────────────────────────────────┘ │
│                                          │
│ OR                                       │
│                                          │
│ [ Choose .torrent ]                      │
│                                          │
│ Profile                                  │
│ [ Movies ▼ ]                             │
│                                          │
│ Save path                                │
│ [ /downloads/movies ]                    │
│                                          │
│ ☑ Start immediately                      │
│                                          │
│              [Cancel] [Add Torrent]      │
└──────────────────────────────────────────┘
```

---

# 26. Download profiles

Implement profiles as a plugin configuration feature.

Example:

```text
Movies
    category = Movies
    savePath = /downloads/movies

TV Shows
    category = TV
    savePath = /downloads/tv

Anime
    category = Anime
    savePath = /downloads/anime
```

The UI can then present:

```text
Profile:
[ Movies ▼ ]
```

The user shouldn't need to repeatedly enter filesystem paths.

---

# 27. Completion monitoring

Implement an optional background service:

```text
TorrentMonitorService
```

Its responsibilities:

1. Obtain torrent state.
2. Maintain previous state.
3. Detect transitions.
4. Detect:

```text
incomplete → complete
```

5. If enabled:

   * trigger Jellyfin library refresh.

Avoid constantly refreshing the entire Jellyfin library.

The service should have a configurable polling interval.

---

# 28. Jellyfin library integration

Create:

```csharp
public interface IJellyfinLibraryService
{
    Task RefreshLibrariesAsync(
        CancellationToken cancellationToken);
}
```

Initially implement a safe general library refresh.

Later enhancement:

```text
Torrent category
       ↓
Profile
       ↓
Jellyfin library
```

Example:

```text
Movies
   → Movies library

TV
   → TV Shows library

Anime
   → Anime library
```

This mapping should be optional.

---

# 29. Security

This is a major requirement.

### Authentication

Every management endpoint must require an authenticated Jellyfin user.

### Authorization

At minimum:

```text
Administrator → full access
Other users → disabled by default
```

Prefer checking Jellyfin user policy/administrator status rather than implementing a completely independent authentication system.

### Credentials

Never:

* send qBittorrent API key to browser
* return password from configuration API
* log API key
* log password
* log auth cookies
* put credentials into frontend JavaScript
* put credentials into URLs

---

# 30. Network security

Document recommended deployment:

```text
Internet
   │
   ▼
Cloudflare/Tailscale
   │
   ▼
Jellyfin
   │
   │ private network
   ▼
qBittorrent
```

The plugin must work with:

```text
http://qbittorrent:8080
```

inside Docker networks.

It should also work with:

```text
http://192.168.x.x:8080
```

for traditional LAN installations.

---

# 31. Docker development environment

Provide a development `docker-compose.yml` optionally:

```yaml
services:

  jellyfin:
    image: jellyfin/jellyfin
    container_name: jellyfin-dev
    ports:
      - "8096:8096"
    volumes:
      - ./dev/jellyfin:/config
      - ./dev/cache:/cache
    networks:
      - media

  qbittorrent:
    image: lscr.io/linuxserver/qbittorrent
    container_name: qbittorrent-dev
    environment:
      - PUID=1000
      - PGID=1000
      - TZ=Asia/Kolkata
      - WEBUI_PORT=8080
    volumes:
      - ./dev/qbittorrent:/config
      - ./dev/downloads:/downloads
    networks:
      - media

networks:
  media:
```

The actual image/version should be pinned appropriately by the coding agent after checking current compatibility.

---

# 32. Linux build commands

The repository should ultimately support:

```bash
git clone <repo>
cd jellyfin-plugin-qbittorrent

dotnet restore
dotnet build -c Release
dotnet test
```

And:

```bash
dotnet publish \
    src/Jellyfin.Plugin.QBittorrent \
    -c Release \
    -o ./artifacts
```

The exact Jellyfin plugin packaging process should be determined from the target Jellyfin release's plugin-development documentation rather than invented.

---

# 33. CI

GitHub Actions:

```text
push
  ↓
restore
  ↓
build
  ↓
test
  ↓
package
  ↓
artifact
```

Release:

```text
git tag v0.1.0
       ↓
GitHub Actions
       ↓
build
       ↓
test
       ↓
package plugin
       ↓
GitHub Release
```

Later:

```text
GitHub Release
      ↓
Plugin Manifest
      ↓
Jellyfin Plugin Repository
```

---

# 34. Testing strategy

## Unit tests

Test:

* authentication
* API requests
* JSON deserialization
* torrent-state normalization
* filtering
* pagination
* error handling
* completion detection
* configuration validation

## Mock qBittorrent server

Create a lightweight fake HTTP server for tests.

Example:

```text
GET /api/v2/torrents/info
        ↓
fixture JSON
        ↓
QBittorrentClient
```

Don't make unit tests depend on an actual qBittorrent installation.

## Integration test

Optional Docker-based test:

```text
Test runner
     ↓
qBittorrent container
     ↓
plugin
     ↓
API
```

---

# 35. Logging

Use Jellyfin's logging infrastructure.

Useful events:

```text
qBittorrent connection established
qBittorrent connection failed
authentication failed
torrent added
torrent paused
torrent resumed
torrent deleted
torrent completed
library scan triggered
```

Avoid excessive polling logs.

Do not log every:

```text
GET /torrents/info
```

request.

---

# 36. Configuration validation

When the user clicks:

```text
[Test Connection]
```

the backend should:

1. validate URL
2. validate authentication configuration
3. authenticate
4. query qBittorrent version/status
5. return a safe result

Success:

```text
✓ Connected

qBittorrent: 5.x.x
API: available
Authentication: API key
```

Failure:

```text
✗ Connection failed

Unable to authenticate with qBittorrent.
```

---

# 37. URL validation

Prevent obviously unsafe/malformed configuration.

Accept:

```text
http://qbittorrent:8080
http://192.168.1.50:8080
https://qb.example.com
```

Reject malformed URLs.

Do not silently append paths incorrectly.

Normalize trailing `/`.

---

# 38. SSRF considerations

Because the plugin makes server-side HTTP requests to a user-configurable URL, explicitly consider SSRF.

At minimum:

* document that qBittorrent URL is administrator-only configuration
* do not expose connection configuration to ordinary users
* do not allow arbitrary URLs through the public API
* connection-test endpoint must only use configured qBittorrent URL
* never accept a URL from the frontend as a qBittorrent target

The public API should contain:

```text
POST /test-connection
```

not:

```text
POST /test-connection
{
    "url": "http://anything/"
}
```

---

# 39. Performance requirements

The plugin should remain lightweight.

Target:

```text
UI refresh: ~3 seconds
```

But:

* reuse `HttpClient`
* don't create a client per request
* don't poll unnecessarily
* don't fetch files/peers unless their tabs are opened
* don't request full torrent details when displaying the dashboard
* use qBittorrent sync endpoints where appropriate

---

# 40. API efficiency

Main page should ideally require:

```text
GET /api/qbittorrent/status
GET /api/qbittorrent/transfer
GET /api/qbittorrent/torrents
```

Not:

```text
GET torrent 1
GET torrent 2
GET torrent 3
...
GET torrent 500
```

Torrent details are lazy-loaded.

---

# 41. Responsive design

Must work on:

### Desktop

```text
1920×1080
```

### Laptop

```text
1366×768
```

### Mobile

```text
390×844
```

The mobile experience is particularly important because the purpose is **remote management**.

Don't simply shrink the desktop UI.

---

# 42. UX principles

Prioritize:

1. Quickly seeing what is downloading.
2. Quickly seeing download speed.
3. Quickly pausing/resuming.
4. Quickly adding a magnet.
5. Quickly deleting a torrent.
6. Seeing completion progress.

Advanced information should be one interaction away.

---

# 43. Explicitly out of scope for v1

Do **not** initially implement:

* torrent search engines
* RSS management
* RSS auto-downloading
* proxy configuration
* VPN management
* tracker editing
* peer banning
* WebSocket infrastructure
* custom notification server
* torrent creation
* custom torrent tracker
* download scheduling
* automatic torrent categorization using AI
* media renaming
* automatic file moving
* Sonarr/Radarr integration

Those can become later extensions.

---

# 44. Version roadmap

## v0.1 — Foundation

```text
Plugin loads
Configuration page
qBittorrent connection
API key authentication
Connection test
```

Acceptance:

```text
Jellyfin starts successfully
Plugin appears
Configuration saves
Plugin connects to qBittorrent
```

---

## v0.2 — Read-only

```text
Torrent list
Search
Filters
Transfer statistics
Torrent details
Categories
Dashboard widget
```

Acceptance:

```text
Can remotely see torrent state without opening qBittorrent WebUI.
```

---

## v0.3 — Controls

```text
Pause
Resume
Force resume
Recheck
Reannounce
Delete
Delete + files
```

Acceptance:

```text
Every operation is reflected correctly in qBittorrent.
```

---

## v0.4 — Adding torrents

```text
Magnet
URL
Torrent upload
Profiles
Category
Save path
Start immediately
```

---

## v0.5 — Jellyfin integration

```text
Completion detection
Library scan
Profile → Jellyfin library mapping
```

---

## v0.6 — Advanced

```text
Files
Peers
Trackers
Tags
Speed limits
Sync API
```

---

# 45. Definition of Done

The project is considered complete for v1 when all of the following are true:

### Installation

* [ ] Builds successfully on Linux.
* [ ] Produces a Jellyfin-compatible plugin artifact.
* [ ] Installs into a Jellyfin server.
* [ ] Plugin survives Jellyfin restart.

### Connection

* [ ] Connects to qBittorrent.
* [ ] Supports API-key authentication.
* [ ] Supports username/password fallback.
* [ ] Handles qBittorrent being offline.
* [ ] Handles authentication failure.

### UI

* [ ] Torrent list works.
* [ ] Search works.
* [ ] Filters work.
* [ ] Mobile UI works.
* [ ] Dashboard widget works.
* [ ] Torrent details work.

### Management

* [ ] Add magnet.
* [ ] Add `.torrent`.
* [ ] Pause.
* [ ] Resume.
* [ ] Force resume.
* [ ] Recheck.
* [ ] Reannounce.
* [ ] Delete.
* [ ] Delete + files.

### Security

* [ ] Jellyfin authentication required.
* [ ] Admin-only configuration.
* [ ] Credentials never sent to browser.
* [ ] Credentials never appear in logs.
* [ ] No arbitrary qBittorrent URL supplied by frontend.

### Integration

* [ ] Completion detection works.
* [ ] Optional Jellyfin library scan works.

### Quality

* [ ] Unit tests pass.
* [ ] No compiler warnings that indicate actual defects.
* [ ] README contains installation instructions.
* [ ] README contains Docker deployment instructions.
* [ ] README documents qBittorrent compatibility.
* [ ] README documents security/network recommendations.

---

# 46. Coding-agent instructions

I would give the coding agent the following **rules in addition to the specification above**:

> You are implementing a production-quality Jellyfin Server Plugin for remotely managing qBittorrent.
>
> Before writing code, inspect the current Jellyfin plugin SDK, current Jellyfin plugin examples, current Jellyfin server APIs, and current qBittorrent Web API documentation. Do not rely on outdated plugin examples if current APIs differ.
>
> The target environment is Linux.
>
> Do not invent Jellyfin APIs. Verify the APIs and interfaces against the actual Jellyfin version/SDK being targeted.
>
> Keep qBittorrent communication isolated behind `IQBittorrentClient`.
>
> Keep Jellyfin-specific functionality isolated from qBittorrent protocol implementation.
>
> Do not expose qBittorrent credentials to the frontend.
>
> Do not implement a separate authentication system. Use Jellyfin authentication and authorization.
>
> Do not introduce unnecessary dependencies.
>
> Prefer standard .NET libraries where practical.
>
> Implement one phase at a time and keep the repository buildable after every phase.
>
> Write unit tests alongside implementation.
>
> Do not mark a feature complete until its acceptance criteria pass.
>
> If the current Jellyfin architecture differs from this specification, adapt the implementation to the current official architecture while preserving the externally visible requirements.
>
> Before finalizing each phase, run:
>
> ```bash
> dotnet restore
> dotnet build -c Release
> dotnet test
> ```
>
> Report:
>
> 1. files created/modified
> 2. architecture decisions
> 3. commands executed
> 4. test results
> 5. known limitations
> 6. next phase

---

## One architectural change I'd make from the earlier design

I would **not have the coding agent start by building the entire UI**.

Have it proceed:

```text
                    PHASE 0
             Jellyfin SDK research
                       │
                       ▼
                    PHASE 1
             Plugin loads in Jellyfin
                       │
                       ▼
                    PHASE 2
              qBittorrent client
                       │
                       ▼
                    PHASE 3
              Read-only REST API
                       │
                       ▼
                    PHASE 4
                 Web UI
                       │
                       ▼
                    PHASE 5
              Management actions
                       │
                       ▼
                    PHASE 6
              Add torrent/profiles
                       │
                       ▼
                    PHASE 7
             Jellyfin integration
                       │
                       ▼
                    PHASE 8
                Packaging/CI
```

**Phase 0 is important.** Jellyfin's plugin APIs and frontend integration points have changed over time, so the agent should first inspect the **current Jellyfin plugin-development documentation/source and the exact Jellyfin version you intend to run**, then lock the project to that compatibility target. That prevents us from giving the agent a beautiful architecture built around a deprecated Jellyfin API.
