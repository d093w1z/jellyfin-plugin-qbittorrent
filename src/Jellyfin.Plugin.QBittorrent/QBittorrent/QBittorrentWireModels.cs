using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.QBittorrent.QBittorrent;

// Raw shapes of qBittorrent's WebAPI v2 JSON responses. Internal: never exposed
// outside the client — callers only ever see the plugin-owned DTOs in Models/.

internal sealed class WireTorrent
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("completed")]
    public long Completed { get; set; }

    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    [JsonPropertyName("dlspeed")]
    public long DlSpeed { get; set; }

    [JsonPropertyName("upspeed")]
    public long UpSpeed { get; set; }

    [JsonPropertyName("downloaded")]
    public long Downloaded { get; set; }

    [JsonPropertyName("uploaded")]
    public long Uploaded { get; set; }

    [JsonPropertyName("eta")]
    public long Eta { get; set; }

    [JsonPropertyName("num_seeds")]
    public int NumSeeds { get; set; }

    [JsonPropertyName("num_leechs")]
    public int NumLeechs { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public string Tags { get; set; } = string.Empty;

    [JsonPropertyName("save_path")]
    public string SavePath { get; set; } = string.Empty;

    [JsonPropertyName("added_on")]
    public long AddedOn { get; set; }
}

internal sealed class WireTorrentFile
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}

internal sealed class WirePeer
{
    [JsonPropertyName("ip")]
    public string Ip { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("client")]
    public string Client { get; set; } = string.Empty;

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    [JsonPropertyName("dl_speed")]
    public long DlSpeed { get; set; }

    [JsonPropertyName("up_speed")]
    public long UpSpeed { get; set; }
}

internal sealed class WirePeersResponse
{
    [JsonPropertyName("peers")]
    public Dictionary<string, WirePeer>? Peers { get; set; }
}

internal sealed class WireTracker
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("num_peers")]
    public int NumPeers { get; set; }

    [JsonPropertyName("msg")]
    public string Msg { get; set; } = string.Empty;
}

internal sealed class WireTransferInfo
{
    [JsonPropertyName("dl_info_speed")]
    public long DlInfoSpeed { get; set; }

    [JsonPropertyName("up_info_speed")]
    public long UpInfoSpeed { get; set; }

    [JsonPropertyName("dl_info_data")]
    public long DlInfoData { get; set; }

    [JsonPropertyName("up_info_data")]
    public long UpInfoData { get; set; }

    [JsonPropertyName("dht_nodes")]
    public int DhtNodes { get; set; }

    [JsonPropertyName("connection_status")]
    public string ConnectionStatus { get; set; } = string.Empty;

    [JsonPropertyName("dl_rate_limit")]
    public long DlRateLimit { get; set; }

    [JsonPropertyName("up_rate_limit")]
    public long UpRateLimit { get; set; }

    [JsonPropertyName("use_alt_speed_limits")]
    public bool UseAltSpeedLimits { get; set; }
}

internal sealed class WireCategoryEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("savePath")]
    public string SavePath { get; set; } = string.Empty;
}

internal sealed class WireSearchStart
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

internal sealed class WireSearchResultsResponse
{
    [JsonPropertyName("results")]
    public List<WireSearchResult> Results { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

internal sealed class WireSearchResult
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("fileUrl")]
    public string FileUrl { get; set; } = string.Empty;

    [JsonPropertyName("nbSeeders")]
    public int NbSeeders { get; set; }

    [JsonPropertyName("nbLeechers")]
    public int NbLeechers { get; set; }

    [JsonPropertyName("siteUrl")]
    public string SiteUrl { get; set; } = string.Empty;

    [JsonPropertyName("descrLink")]
    public string DescrLink { get; set; } = string.Empty;
}

internal sealed class WireSearchPlugin
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}
