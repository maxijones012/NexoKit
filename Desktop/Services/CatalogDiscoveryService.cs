using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KitHerramientas.Desktop.Services;

public sealed class CatalogSourceWatch
{
    public string Repository { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 12;
    public string LastCommitId { get; set; } = "";
    public DateTimeOffset? LastChecked { get; set; }
    public int TotalCount { get; set; }
    public int NewCount { get; set; }
    public string Status { get; set; } = "Pendiente";

    [JsonIgnore]
    public string DisplayLastChecked => LastChecked is null ? "—" : LastChecked.Value.LocalDateTime.ToString("dd/MM HH:mm");
}

public sealed class DiscoveredTool
{
    public string Repository { get; set; } = "";
    public string Category { get; set; } = "General";
    public string Source { get; set; } = "";
    public DateTimeOffset FirstSeen { get; set; } = DateTimeOffset.Now;
    public bool IsNew { get; set; }
    [JsonIgnore] public string State => IsNew ? "NUEVO" : "CATÁLOGO";
    [JsonIgnore] public string Url => $"https://github.com/{Repository}";
}

public sealed record CatalogSnapshot(string CommitId, IReadOnlyList<DiscoveredTool> Tools);

public static class CatalogDiscoveryService
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly Regex GithubUrl = new(@"https?://github\.com/([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string StateDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexoKit", "Discovery");
    private static readonly string SourcesFile = Path.Combine(StateDirectory, "catalog_sources.json");
    private static readonly string ToolsFile = Path.Combine(StateDirectory, "discovered_tools.json");

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NexoKit-Discovery/0.9");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.Timeout = TimeSpan.FromSeconds(35);
        return client;
    }

    public static List<CatalogSourceWatch> LoadSources()
    {
        Directory.CreateDirectory(StateDirectory);
        if (File.Exists(SourcesFile))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<CatalogSourceWatch>>(File.ReadAllText(SourcesFile));
                if (parsed is { Count: > 0 }) return parsed;
            }
            catch { }
        }
        var defaults = new List<CatalogSourceWatch>
        {
            new() { Repository = "Astrosp/Awesome-OSINT-List", IntervalHours = 12, Enabled = true }
        };
        SaveSources(defaults);
        return defaults;
    }

    public static List<DiscoveredTool> LoadTools()
    {
        Directory.CreateDirectory(StateDirectory);
        if (!File.Exists(ToolsFile)) return new();
        try { return JsonSerializer.Deserialize<List<DiscoveredTool>>(File.ReadAllText(ToolsFile)) ?? new(); }
        catch { return new(); }
    }

    public static void SaveSources(IEnumerable<CatalogSourceWatch> sources)
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(SourcesFile, JsonSerializer.Serialize(sources, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void SaveTools(IEnumerable<DiscoveredTool> tools)
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(ToolsFile, JsonSerializer.Serialize(tools, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static bool IsDue(CatalogSourceWatch source, DateTimeOffset now)
    {
        if (!source.Enabled) return false;
        if (source.LastChecked is null) return true;
        return now - source.LastChecked.Value >= TimeSpan.FromHours(Math.Clamp(source.IntervalHours, 1, 168));
    }

    public static async Task<CatalogSnapshot> FetchAsync(string repository, CancellationToken cancellationToken = default)
    {
        var repo = RepositoryUpdateService.NormalizeRepository(repository) ?? throw new ArgumentException("Fuente inválida. Usá owner/repo o URL de GitHub.");
        var infoJson = await GetJsonAsync($"https://api.github.com/repos/{repo}", cancellationToken);
        var branch = infoJson.RootElement.TryGetProperty("default_branch", out var branchEl) ? branchEl.GetString() ?? "main" : "main";
        var commitJson = await GetJsonAsync($"https://api.github.com/repos/{repo}/commits/{Uri.EscapeDataString(branch)}", cancellationToken);
        var commit = commitJson.RootElement.TryGetProperty("sha", out var shaEl) ? shaEl.GetString() ?? "" : "";

        using var readmeResponse = await Http.GetAsync($"https://api.github.com/repos/{repo}/readme", cancellationToken);
        if (!readmeResponse.IsSuccessStatusCode) throw new InvalidOperationException($"No pude leer README de {repo} (HTTP {(int)readmeResponse.StatusCode}).");
        await using var readmeStream = await readmeResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var readmeDoc = await JsonDocument.ParseAsync(readmeStream, cancellationToken: cancellationToken);
        var encoded = readmeDoc.RootElement.TryGetProperty("content", out var contentEl) ? contentEl.GetString() ?? "" : "";
        var markdown = Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Replace("\n", "")));
        return new CatalogSnapshot(commit, ParseMarkdown(repo, markdown));
    }

    private static IReadOnlyList<DiscoveredTool> ParseMarkdown(string sourceRepo, string markdown)
    {
        var found = new Dictionary<string, DiscoveredTool>(StringComparer.OrdinalIgnoreCase);
        var category = "General";
        foreach (var rawLine in markdown.Replace("\r", "").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#'))
            {
                var heading = line.TrimStart('#').Trim();
                if (!string.IsNullOrWhiteSpace(heading)) category = CleanHeading(heading);
            }
            foreach (Match match in GithubUrl.Matches(line))
            {
                var owner = match.Groups[1].Value.Trim();
                var name = match.Groups[2].Value.Trim().TrimEnd('.', ',', ')', ']', ';', ':');
                if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
                if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(name)) continue;
                var repo = $"{owner}/{name}";
                if (repo.Equals(sourceRepo, StringComparison.OrdinalIgnoreCase)) continue;
                if (!found.ContainsKey(repo)) found[repo] = new DiscoveredTool { Repository = repo, Category = category, Source = sourceRepo };
            }
        }
        return found.Values.OrderBy(x => x.Category).ThenBy(x => x.Repository).ToList();
    }

    public static int MergeSnapshot(List<DiscoveredTool> current, CatalogSourceWatch source, CatalogSnapshot snapshot)
    {
        var firstRun = string.IsNullOrWhiteSpace(source.LastCommitId);
        var existing = current.ToDictionary(x => $"{x.Source}|{x.Repository}", StringComparer.OrdinalIgnoreCase);
        var seenNow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var incoming in snapshot.Tools)
        {
            var key = $"{source.Repository}|{incoming.Repository}";
            seenNow.Add(key);
            if (existing.TryGetValue(key, out var item))
            {
                item.Category = incoming.Category;
                item.IsNew = false;
            }
            else
            {
                incoming.Source = source.Repository;
                incoming.FirstSeen = DateTimeOffset.Now;
                incoming.IsNew = !firstRun;
                current.Add(incoming);
                if (!firstRun) added++;
            }
        }
        source.LastCommitId = snapshot.CommitId;
        source.LastChecked = DateTimeOffset.Now;
        source.TotalCount = snapshot.Tools.Count;
        source.NewCount = added;
        source.Status = firstRun ? $"BASE CREADA · {snapshot.Tools.Count} recursos" : added > 0 ? $"{added} NUEVOS" : $"SIN NOVEDADES · {snapshot.Tools.Count}";
        return added;
    }

    public static void MarkAllSeen(List<DiscoveredTool> tools)
    {
        foreach (var tool in tools) tool.IsNew = false;
        SaveTools(tools);
    }

    private static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"GitHub HTTP {(int)response.StatusCode} en {url}");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string CleanHeading(string value)
    {
        var text = Regex.Replace(value, @"\[[^\]]+\]\([^\)]+\)", "");
        text = Regex.Replace(text, @"[`*_#]", "").Trim();
        return text.Length > 70 ? text[..70] : string.IsNullOrWhiteSpace(text) ? "General" : text;
    }
}
