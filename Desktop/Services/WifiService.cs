using System.Diagnostics;
using System.Text.RegularExpressions;

namespace KitHerramientas.Desktop.Services;

public sealed record WifiConnection(
    bool Connected,
    string Ssid,
    string Bssid,
    int? SignalPercent,
    int? ApproxRssi,
    int? Channel,
    string RadioType,
    string InterfaceName,
    string Profile,
    string Authentication,
    string ReceiveRate,
    string TransmitRate);

public sealed record WifiNetwork(
    string Ssid,
    string Bssid,
    int? SignalPercent,
    int? ApproxRssi,
    int? Channel,
    string Authentication);

public static class WifiService
{
    public static string LastDiagnostic { get; private set; } = "";

    public static async Task<WifiConnection> GetCurrentAsync()
    {
        var text = await RunNetshAsync("wlan show interfaces");
        if (string.IsNullOrWhiteSpace(text))
            return Empty();

        string ssid = ValueAny(text, "SSID") ?? "—";
        string bssid = ValueAny(text, "BSSID") ?? "—";
        string signalText = ValueAny(text, "Signal", "Señal", "Senal") ?? "";
        string channelText = ValueAny(text, "Channel", "Canal") ?? "";
        string radio = ValueAny(text, "Radio type", "Tipo de radio") ?? "—";
        string iface = ValueAny(text, "Name", "Nombre") ?? "—";
        string state = ValueAny(text, "State", "Estado") ?? "";
        string profile = ValueAny(text, "Profile", "Perfil") ?? "—";
        string auth = ValueAny(text, "Authentication", "Autenticación", "Autenticacion") ?? "—";
        string rx = ValueAny(text, "Receive rate (Mbps)", "Velocidad de recepción (Mbps)", "Velocidad de recepcion (Mbps)") ?? "—";
        string tx = ValueAny(text, "Transmit rate (Mbps)", "Velocidad de transmisión (Mbps)", "Velocidad de transmision (Mbps)") ?? "—";

        var signal = ParsePercent(signalText);
        var channel = ParseInt(channelText);
        var connected = state.Contains("connected", StringComparison.OrdinalIgnoreCase)
                     || state.Contains("conectado", StringComparison.OrdinalIgnoreCase)
                     || (ssid != "—" && bssid != "—");

        if (!connected && string.IsNullOrWhiteSpace(LastDiagnostic))
            LastDiagnostic = "Windows no informó una conexión Wi‑Fi activa mediante netsh.";

        return new(
            connected,
            ssid,
            bssid,
            signal,
            signal is null ? null : PercentToApproxRssi(signal.Value),
            channel,
            radio,
            iface,
            profile,
            auth,
            rx,
            tx);
    }

    public static async Task<IReadOnlyList<WifiNetwork>> ScanAsync()
    {
        var text = await RunNetshAsync("wlan show networks mode=bssid");
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<WifiNetwork>();

        var lines = text.Replace("\r", "").Split('\n');
        var result = new List<WifiNetwork>();
        string currentSsid = "Red oculta";
        string currentAuth = "—";
        string? bssid = null;
        int? signal = null;
        int? channel = null;

        void FlushBssid()
        {
            if (bssid is null) return;
            result.Add(new WifiNetwork(
                string.IsNullOrWhiteSpace(currentSsid) ? "Red oculta" : currentSsid,
                bssid,
                signal,
                signal is null ? null : PercentToApproxRssi(signal.Value),
                channel,
                currentAuth));
            bssid = null;
            signal = null;
            channel = null;
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (Regex.IsMatch(line, @"^SSID\s+\d+\s*:", RegexOptions.IgnoreCase))
            {
                FlushBssid();
                currentSsid = line[(line.IndexOf(':') + 1)..].Trim();
                currentAuth = "—";
                continue;
            }
            if (StartsAny(line, "Authentication", "Autenticación", "Autenticacion"))
            {
                currentAuth = AfterColon(line);
                continue;
            }
            if (Regex.IsMatch(line, @"^BSSID\s+\d+\s*:", RegexOptions.IgnoreCase))
            {
                FlushBssid();
                bssid = AfterColon(line);
                continue;
            }
            if (StartsAny(line, "Signal", "Señal", "Senal"))
            {
                signal = ParsePercent(AfterColon(line));
                continue;
            }
            if (StartsAny(line, "Channel", "Canal"))
            {
                channel = ParseInt(AfterColon(line));
            }
        }
        FlushBssid();

        return result
            .OrderByDescending(x => x.SignalPercent ?? -1)
            .ThenBy(x => x.Ssid, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<(int Channel, int Count, int Strongest)> ChannelSummary(IEnumerable<WifiNetwork> networks) =>
        networks
            .Where(n => n.Channel is not null)
            .GroupBy(n => n.Channel!.Value)
            .Select(g => (Channel: g.Key, Count: g.Count(), Strongest: g.Max(x => x.SignalPercent ?? 0)))
            .OrderBy(x => x.Channel)
            .ToList();

    private static async Task<string> RunNetshAsync(string arguments)
    {
        try
        {
            LastDiagnostic = "";
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null)
            {
                LastDiagnostic = "Windows no pudo iniciar netsh.";
                return "";
            }

            var outputTask = p.StandardOutput.ReadToEndAsync();
            var errorTask = p.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            try
            {
                await p.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(true); } catch { }
                LastDiagnostic = "netsh tardó más de 6 segundos y fue cancelado.";
                return "";
            }

            var output = await outputTask;
            var error = await errorTask;
            var combined = (output + "\n" + error).Trim();
            var lower = combined.ToLowerInvariant();
            if (p.ExitCode != 0)
            {
                LastDiagnostic = $"netsh devolvió código {p.ExitCode}: {Compact(combined)}";
            }
            else if (lower.Contains("location") || lower.Contains("ubicaci") ||
                     lower.Contains("access is denied") || lower.Contains("acceso denegado") ||
                     lower.Contains("permission") || lower.Contains("permiso"))
            {
                LastDiagnostic = "Windows está ocultando información Wi‑Fi por privacidad. Activá Ubicación y 'Permitir que las aplicaciones de escritorio accedan a tu ubicación'.";
            }
            else if (string.IsNullOrWhiteSpace(output))
            {
                LastDiagnostic = "netsh no devolvió información Wi‑Fi.";
            }
            return output;
        }
        catch (Exception ex)
        {
            LastDiagnostic = $"netsh: {ex.Message}";
            return "";
        }
    }

    private static WifiConnection Empty() => new(false, "—", "—", null, null, null, "—", "—", "—", "—", "—", "—");

    private static string Compact(string text)
    {
        var oneLine = Regex.Replace(text ?? "", @"\s+", " ").Trim();
        return oneLine.Length > 220 ? oneLine[..220] + "…" : oneLine;
    }

    private static string? ValueAny(string text, params string[] keys)
    {
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            foreach (var key in keys)
            {
                if (line.StartsWith(key + " ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
                    return AfterColon(line);
            }
        }
        return null;
    }

    private static bool StartsAny(string line, params string[] keys) =>
        keys.Any(k => line.StartsWith(k + " ", StringComparison.OrdinalIgnoreCase) || line.StartsWith(k + ":", StringComparison.OrdinalIgnoreCase));

    private static string AfterColon(string line)
    {
        var i = line.IndexOf(':');
        return i < 0 ? line.Trim() : line[(i + 1)..].Trim();
    }

    private static int? ParsePercent(string text)
    {
        var m = Regex.Match(text ?? "", @"(\d{1,3})");
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? Math.Clamp(n, 0, 100) : null;
    }

    private static int? ParseInt(string text)
    {
        var m = Regex.Match(text ?? "", @"-?\d+");
        return m.Success && int.TryParse(m.Value, out var n) ? n : null;
    }

    public static int PercentToApproxRssi(int percent) =>
        percent <= 0 ? -100 : percent >= 100 ? -50 : (percent / 2) - 100;
}
