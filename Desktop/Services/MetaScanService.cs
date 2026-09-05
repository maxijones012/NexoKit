using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace KitHerramientas.Desktop.Services;

public sealed class MetaScanBundle
{
    public string Username { get; init; } = "";
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.Now;
    public JsonElement? Profile { get; init; }
    public JsonElement? BusinessHome { get; init; }
    public JsonElement? BusinessAbout { get; init; }
    public JsonElement? BusinessTransparency { get; init; }
    public Dictionary<string, string> Errors { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class MetaScanService
{
    private const string Host = "facebook-pages-scraper3.p.rapidapi.com";
    private const string ProfilePath = "/get-profile-home-page-details";
    private const string BusinessHomePath = "/get-business-home-page-details";
    private const string BusinessAboutPath = "/get-business-about-details-page";
    private const string BusinessTransparencyPath = "/get-business-about-profile-transparency-page-details";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NexoKit-MetaScan/1.0");
        client.Timeout = TimeSpan.FromSeconds(50);
        return client;
    }

    public static string NormalizeUsername(string input)
    {
        var value = (input ?? "").Trim();
        if (value.StartsWith("@")) value = value[1..];

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Host.Contains("facebook.com", StringComparison.OrdinalIgnoreCase))
        {
            var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) value = parts[0];
        }

        value = value.Trim().Trim('/');
        return value;
    }

    public static async Task<MetaScanBundle> ScanAsync(
        string target,
        string apiKey,
        bool includeBusiness = true,
        CancellationToken cancellationToken = default)
    {
        var username = NormalizeUsername(target);
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Ingresá un usuario o URL de Facebook.");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Ingresá tu API key de RapidAPI.");

        JsonElement? profile = null;
        JsonElement? businessHome = null;
        JsonElement? businessAbout = null;
        JsonElement? businessTransparency = null;
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            profile = await GetAsync(ProfilePath, username, apiKey, includeUrlParameter: true, cancellationToken);
        }
        catch (Exception ex)
        {
            errors["Perfil"] = ex.Message;
        }

        if (includeBusiness)
        {
            try { businessHome = await GetAsync(BusinessHomePath, username, apiKey, false, cancellationToken); }
            catch (Exception ex) { errors["Business Home"] = ex.Message; }

            try { businessAbout = await GetAsync(BusinessAboutPath, username, apiKey, false, cancellationToken); }
            catch (Exception ex) { errors["About"] = ex.Message; }

            try { businessTransparency = await GetAsync(BusinessTransparencyPath, username, apiKey, false, cancellationToken); }
            catch (Exception ex) { errors["Transparencia"] = ex.Message; }
        }

        return new MetaScanBundle
        {
            Username = username,
            Profile = profile,
            BusinessHome = businessHome,
            BusinessAbout = businessAbout,
            BusinessTransparency = businessTransparency,
            Errors = errors
        };
    }

    private static async Task<JsonElement> GetAsync(
        string path,
        string username,
        string apiKey,
        bool includeUrlParameter,
        CancellationToken cancellationToken)
    {
        var facebookUrl = $"https://www.facebook.com/{username}";
        var query = $"urlSupplier={Uri.EscapeDataString(facebookUrl)}";
        if (includeUrlParameter)
            query += $"&url={Uri.EscapeDataString(facebookUrl)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{Host}{path}?{query}");
        request.Headers.TryAddWithoutValidation("x-rapidapi-host", Host);
        request.Headers.TryAddWithoutValidation("x-rapidapi-key", apiKey.Trim());

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var shortBody = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (shortBody.Length > 280) shortBody = shortBody[..280] + "…";
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {shortBody}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    public static string FormatSummary(MetaScanBundle bundle)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"META SCAN · @{bundle.Username}");
        sb.AppendLine($"Consulta: {bundle.CheckedAt.LocalDateTime:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine(new string('─', 72));

        if (bundle.Profile is JsonElement profile)
        {
            AppendField(sb, profile, "ID", "id");
            AppendField(sb, profile, "Nombre", "name");
            AppendField(sb, profile, "Tipo", "type_name");
            AppendField(sb, profile, "Género", "gender");
            AppendField(sb, profile, "Email", "email");
            AppendField(sb, profile, "Teléfono", "phone");
            AppendField(sb, profile, "Sitio web", "website");
            AppendField(sb, profile, "Seguidores", "followers");
            AppendField(sb, profile, "Me gusta", "likes");
            AppendField(sb, profile, "Categorías", "categories");
            AppendField(sb, profile, "Descripción", "best_description");
            AppendField(sb, profile, "Perfil", "profile_url");
        }
        else
        {
            sb.AppendLine("Perfil: sin datos.");
        }

        if (bundle.BusinessAbout is JsonElement about && TryFindValue(about, "about_text", out var aboutValue))
        {
            sb.AppendLine();
            sb.AppendLine("ABOUT");
            sb.AppendLine(aboutValue);
        }

        if (bundle.BusinessTransparency is JsonElement transparency)
        {
            sb.AppendLine();
            sb.AppendLine("TRANSPARENCIA");
            AppendField(sb, transparency, "Estado anuncios", "ad_status");
            AppendField(sb, transparency, "Fecha creación", "creation_date");
        }

        if (bundle.Errors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("AVISOS / ERRORES PARCIALES");
            foreach (var item in bundle.Errors)
                sb.AppendLine($"• {item.Key}: {item.Value}");
        }

        sb.AppendLine();
        sb.AppendLine("JSON CRUDO");
        sb.AppendLine(JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true }));
        return sb.ToString();
    }

    private static void AppendField(StringBuilder sb, JsonElement root, string label, string key)
    {
        if (!TryFindValue(root, key, out var value) || string.IsNullOrWhiteSpace(value)) return;
        sb.AppendLine($"{label}: {value}");
    }

    private static bool TryFindValue(JsonElement element, string key, out string value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    value = ElementToText(property.Value);
                    return true;
                }
                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array &&
                    TryFindValue(property.Value, key, out value))
                    return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                if (TryFindValue(item, key, out value)) return true;
        }

        value = "";
        return false;
    }

    private static string ElementToText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "Sí",
        JsonValueKind.False => "No",
        JsonValueKind.Null => "",
        JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Take(20).Select(ElementToText).Where(x => !string.IsNullOrWhiteSpace(x))),
        _ => value.GetRawText()
    };
}
