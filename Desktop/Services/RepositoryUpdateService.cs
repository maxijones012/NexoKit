using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitHerramientas.Desktop.Services;

public sealed class RepositoryWatch
{
    public string Repository { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool AutoDownload { get; set; } = true;
    public int IntervalHours { get; set; } = 6;
    public string LastDownloadedId { get; set; } = "";
    public string LatestId { get; set; } = "";
    public string LatestName { get; set; } = "";
    public DateTimeOffset? LastChecked { get; set; }
    public string LastDownloadPath { get; set; } = "";
    public string Status { get; set; } = "Pendiente";

    [JsonIgnore]
    public string DisplayLastChecked => LastChecked is null ? "—" : LastChecked.Value.LocalDateTime.ToString("dd/MM HH:mm");
}

public sealed record RepositoryRemoteVersion(
    string Id,
    string Name,
    string DownloadUrl,
    string FileName,
    DateTimeOffset? PublishedAt,
    bool IsRelease);

public static class RepositoryUpdateService
{
    private static readonly HttpClient Http = CreateClient();

    private static readonly string StateDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NexoKit",
        "RepositoryUpdates");

    private static readonly string StateFile = Path.Combine(StateDirectory, "repositories.json");
    public static string DownloadsDirectory => Path.Combine(StateDirectory, "Downloads");

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NexoKit-Updater/0.9");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    public static List<RepositoryWatch> Load()
    {
        Directory.CreateDirectory(StateDirectory);
        var metaSeedFlag = Path.Combine(StateDirectory, "seed_meta_scan_v1.done");
        if (File.Exists(StateFile))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<RepositoryWatch>>(File.ReadAllText(StateFile));
                if (parsed is { Count: > 0 })
                {
                    // Migración de una sola vez: agrega Meta Scan a instalaciones R9 existentes,
                    // pero respeta una eliminación manual posterior.
                    if (!File.Exists(metaSeedFlag))
                    {
                        if (!parsed.Any(x => x.Repository.Equals("HackUnderway/meta_scan", StringComparison.OrdinalIgnoreCase)))
                            parsed.Add(new RepositoryWatch { Repository = "HackUnderway/meta_scan", IntervalHours = 12, AutoDownload = true });
                        Save(parsed);
                        File.WriteAllText(metaSeedFlag, DateTimeOffset.Now.ToString("O"));
                    }
                    return parsed;
                }
            }
            catch { }
        }

        var defaults = new List<RepositoryWatch>
        {
            new() { Repository = "maxijones012/PruebaRepositorio", IntervalHours = 6, AutoDownload = true },
            new() { Repository = "maxijones012/FACELY-Releases", IntervalHours = 6, AutoDownload = true },
            new() { Repository = "maxijones012/IrisTrack_AI", IntervalHours = 6, AutoDownload = true },
            new() { Repository = "HackUnderway/meta_scan", IntervalHours = 12, AutoDownload = true },
        };
        Save(defaults);
        File.WriteAllText(metaSeedFlag, DateTimeOffset.Now.ToString("O"));
        return defaults;
    }

    public static void Save(IEnumerable<RepositoryWatch> repositories)
    {
        Directory.CreateDirectory(StateDirectory);
        var json = JsonSerializer.Serialize(repositories, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StateFile, json);
    }

    public static bool IsDue(RepositoryWatch watch, DateTimeOffset now)
    {
        if (!watch.Enabled) return false;
        if (watch.LastChecked is null) return true;
        var hours = Math.Clamp(watch.IntervalHours, 1, 168);
        return now - watch.LastChecked.Value >= TimeSpan.FromHours(hours);
    }

    public static string? NormalizeRepository(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var value = input.Trim().TrimEnd('/');
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
        {
            var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0]}/{parts[1].Replace(".git", "", StringComparison.OrdinalIgnoreCase)}";
        }

        var raw = value.Replace(".git", "", StringComparison.OrdinalIgnoreCase);
        var split = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return split.Length == 2 ? $"{split[0]}/{split[1]}" : null;
    }

    public static async Task<RepositoryRemoteVersion> GetLatestAsync(string repository, bool android = false, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRepository(repository) ?? throw new ArgumentException("Repositorio inválido. Usá owner/repo o una URL de GitHub.");
        var releaseUrl = $"https://api.github.com/repos/{normalized}/releases/latest";
        using (var releaseResponse = await Http.GetAsync(releaseUrl, cancellationToken))
        {
            if (releaseResponse.IsSuccessStatusCode)
            {
                await using var stream = await releaseResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = doc.RootElement;
                var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "release" : "release";
                var name = root.TryGetProperty("name", out var nameEl) && !string.IsNullOrWhiteSpace(nameEl.GetString()) ? nameEl.GetString()! : tag;
                DateTimeOffset? published = null;
                if (root.TryGetProperty("published_at", out var pubEl) && DateTimeOffset.TryParse(pubEl.GetString(), out var parsed)) published = parsed;

                var assets = new List<(string Name, string Url)>();
                if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsEl.EnumerateArray())
                    {
                        var assetName = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var assetUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                        if (!string.IsNullOrWhiteSpace(assetName) && !string.IsNullOrWhiteSpace(assetUrl)) assets.Add((assetName, assetUrl));
                    }
                }

                var chosen = ChooseAsset(assets, android);
                if (chosen is not null)
                    return new(tag, name, chosen.Value.Url, chosen.Value.Name, published, true);

                var zipball = root.TryGetProperty("zipball_url", out var zipEl) ? zipEl.GetString() : null;
                if (!string.IsNullOrWhiteSpace(zipball))
                    return new(tag, name, zipball!, $"{SafeName(normalized.Replace('/', '_'))}_{SafeName(tag)}_source.zip", published, true);
            }
            else if (releaseResponse.StatusCode is not HttpStatusCode.NotFound)
            {
                var body = await releaseResponse.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"GitHub respondió {(int)releaseResponse.StatusCode}: {Short(body)}");
            }
        }

        // Sin releases: se sigue la rama por defecto y se descarga el ZIP de código cuando cambia el commit.
        using var repoResponse = await Http.GetAsync($"https://api.github.com/repos/{normalized}", cancellationToken);
        if (!repoResponse.IsSuccessStatusCode)
        {
            if (repoResponse.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidOperationException("Repositorio no encontrado o privado. Los repositorios privados requieren autenticación, que R9 no guarda ni embebe.");
            var body = await repoResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"GitHub respondió {(int)repoResponse.StatusCode}: {Short(body)}");
        }

        string branch;
        await using (var repoStream = await repoResponse.Content.ReadAsStreamAsync(cancellationToken))
        using (var repoDoc = await JsonDocument.ParseAsync(repoStream, cancellationToken: cancellationToken))
            branch = repoDoc.RootElement.TryGetProperty("default_branch", out var b) ? b.GetString() ?? "main" : "main";

        using var commitResponse = await Http.GetAsync($"https://api.github.com/repos/{normalized}/commits/{Uri.EscapeDataString(branch)}", cancellationToken);
        commitResponse.EnsureSuccessStatusCode();
        await using var commitStream = await commitResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var commitDoc = await JsonDocument.ParseAsync(commitStream, cancellationToken: cancellationToken);
        var sha = commitDoc.RootElement.GetProperty("sha").GetString() ?? throw new InvalidOperationException("GitHub no devolvió SHA.");
        DateTimeOffset? commitDate = null;
        try
        {
            var dateText = commitDoc.RootElement.GetProperty("commit").GetProperty("committer").GetProperty("date").GetString();
            if (DateTimeOffset.TryParse(dateText, out var parsed)) commitDate = parsed;
        }
        catch { }

        return new(
            sha,
            $"{branch} · {sha[..Math.Min(8, sha.Length)]}",
            $"https://github.com/{normalized}/archive/refs/heads/{Uri.EscapeDataString(branch)}.zip",
            $"{SafeName(normalized.Replace('/', '_'))}_{SafeName(branch)}_{sha[..Math.Min(8, sha.Length)]}.zip",
            commitDate,
            false);
    }

    public static async Task<string> DownloadAsync(string repository, RepositoryRemoteVersion version, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRepository(repository) ?? repository;
        var repoDir = Path.Combine(DownloadsDirectory, SafeName(normalized.Replace('/', '_')));
        Directory.CreateDirectory(repoDir);
        var filename = string.IsNullOrWhiteSpace(version.FileName) ? $"update_{DateTime.Now:yyyyMMdd_HHmmss}.bin" : SafeFileName(version.FileName);
        var finalPath = Path.Combine(repoDir, filename);
        var tempPath = finalPath + ".part";

        using var response = await Http.GetAsync(version.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var target = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 64, useAsync: true))
            await source.CopyToAsync(target, cancellationToken);

        File.Move(tempPath, finalPath, true);
        return finalPath;
    }

    private static (string Name, string Url)? ChooseAsset(List<(string Name, string Url)> assets, bool android)
    {
        if (assets.Count == 0) return null;
        IEnumerable<(string Name, string Url)> ordered;
        if (android)
        {
            ordered = assets.OrderBy(a => AssetRankAndroid(a.Name));
        }
        else
        {
            ordered = assets.OrderBy(a => AssetRankWindows(a.Name));
        }
        var chosen = ordered.First();
        var rank = android ? AssetRankAndroid(chosen.Name) : AssetRankWindows(chosen.Name);
        return rank >= 100 ? null : chosen;
    }

    private static int AssetRankWindows(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.EndsWith(".exe") && (n.Contains("win") || n.Contains("windows") || n.Contains("x64"))) return 0;
        if (n.EndsWith(".msi")) return 1;
        if (n.EndsWith(".zip") && (n.Contains("win") || n.Contains("windows") || n.Contains("x64"))) return 2;
        if (n.EndsWith(".exe")) return 3;
        if (n.EndsWith(".zip")) return 4;
        return 100;
    }

    private static int AssetRankAndroid(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.EndsWith(".apk")) return 0;
        if (n.EndsWith(".aab")) return 1;
        if (n.EndsWith(".zip") && n.Contains("android")) return 2;
        if (n.EndsWith(".zip")) return 3;
        return 100;
    }

    private static string SafeName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value.Replace(' ', '_');
    }

    private static string SafeFileName(string value)
    {
        var result = value;
        foreach (var c in Path.GetInvalidFileNameChars()) result = result.Replace(c, '_');
        return string.IsNullOrWhiteSpace(result) ? "update.bin" : result;
    }

    private static string Short(string value)
    {
        var text = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 180 ? text : text[..180] + "…";
    }
}
