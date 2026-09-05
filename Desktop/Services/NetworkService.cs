using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitHerramientas.Desktop.Services;

public sealed record NetworkSnapshot(
    string HostName,
    string LocalIp,
    int PrefixLength,
    string Gateway,
    string Adapter,
    string AdapterDescription,
    string InterfaceType,
    string MacAddress,
    string LinkSpeed,
    string DnsServers,
    string DnsSuffix,
    string Status);

public sealed record NetworkProfileInfo(
    string Name,
    string InterfaceAlias,
    string Category,
    string Ipv4Connectivity);

public static class NetworkService
{
    public static NetworkSnapshot GetSnapshot()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .Select(n => new
            {
                Interface = n,
                Properties = SafeProperties(n),
                Score = Score(n)
            })
            .Where(x => x.Properties is not null)
            .Where(x => x.Properties!.GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Any.Equals(g.Address)))
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Interface.Speed)
            .ToList();

        var selected = candidates.FirstOrDefault();
        if (selected is null)
        {
            return new(
                Environment.MachineName, "-", 0, "-", "Sin interfaz activa", "-", "-", "-", "-", "-", "-",
                "Sin conexión de red activa");
        }

        var active = selected.Interface;
        var props = selected.Properties!;
        var unicast = props.UnicastAddresses.FirstOrDefault(a =>
            a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address));
        var ip = unicast?.Address.ToString() ?? "-";
        var prefix = unicast?.PrefixLength ?? 0;
        var gateway = props.GatewayAddresses
            .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Any.Equals(g.Address))
            ?.Address.ToString() ?? "-";
        var mbps = active.Speed > 0 ? $"{active.Speed / 1_000_000.0:0.#} Mbps" : "-";
        var mac = FormatMac(active.GetPhysicalAddress());
        var dns = string.Join(", ", props.DnsAddresses
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork || a.AddressFamily == AddressFamily.InterNetworkV6)
            .Select(a => a.ToString()));
        if (string.IsNullOrWhiteSpace(dns)) dns = "-";

        var type = active.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => "Wi‑Fi",
            NetworkInterfaceType.Ethernet => "Ethernet",
            NetworkInterfaceType.Ppp => "PPP",
            _ => active.NetworkInterfaceType.ToString()
        };

        return new(
            Environment.MachineName,
            ip,
            prefix,
            gateway,
            active.Name,
            string.IsNullOrWhiteSpace(active.Description) ? "-" : active.Description,
            type,
            mac,
            mbps,
            dns,
            string.IsNullOrWhiteSpace(props.DnsSuffix) ? "-" : props.DnsSuffix,
            active.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Conectado por Wi‑Fi" : $"Conectado por {type}");
    }

    public static async Task<NetworkProfileInfo?> GetProfileAsync(string interfaceAlias)
    {
        if (string.IsNullOrWhiteSpace(interfaceAlias) || interfaceAlias == "-") return null;
        try
        {
            var safeAlias = interfaceAlias.Replace("'", "''");
            var command = "$p=Get-NetConnectionProfile -InterfaceAlias '" + safeAlias + "' -ErrorAction SilentlyContinue | Select-Object -First 1 Name,InterfaceAlias,NetworkCategory,IPv4Connectivity; if($p){$p | ConvertTo-Json -Compress}";
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
            var output = await RunProcessAsync("powershell.exe", $"-NoProfile -NonInteractive -EncodedCommand {encoded}", 5000);
            if (string.IsNullOrWhiteSpace(output)) return null;
            using var doc = JsonDocument.Parse(output.Trim());
            var root = doc.RootElement;
            return new NetworkProfileInfo(
                GetJson(root, "Name"),
                GetJson(root, "InterfaceAlias"),
                GetJson(root, "NetworkCategory"),
                GetJson(root, "IPv4Connectivity"));
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string> GetGatewayMacAsync(string gateway)
    {
        if (!IPAddress.TryParse(gateway, out var address) || address.AddressFamily != AddressFamily.InterNetwork) return "-";
        try
        {
            // Hacer un ping corto ayuda a poblar la caché ARP antes de consultarla.
            using (var ping = new Ping())
            {
                try { await ping.SendPingAsync(gateway, 700); } catch { }
            }
            var output = await RunProcessAsync("arp.exe", $"-a {gateway}", 3500);
            if (string.IsNullOrWhiteSpace(output)) return "-";
            var match = Regex.Match(output, @"(?im)^\s*" + Regex.Escape(gateway) + @"\s+([0-9a-f]{2}(?:[-:][0-9a-f]{2}){5})\s+");
            return match.Success ? match.Groups[1].Value.Replace('-', ':').ToUpperInvariant() : "-";
        }
        catch
        {
            return "-";
        }
    }

    public static async Task<string> PingAsync(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host == "-") return "Gateway no disponible";
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 1800);
            return reply.Status == IPStatus.Success
                ? $"{reply.RoundtripTime} ms"
                : reply.Status.ToString();
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static IPInterfaceProperties? SafeProperties(NetworkInterface n)
    {
        try { return n.GetIPProperties(); }
        catch { return null; }
    }

    private static int Score(NetworkInterface n)
    {
        // El objetivo principal es describir el router/red Wi‑Fi a la que está conectado el equipo.
        // Priorizamos Wi‑Fi real; Ethernet queda como fallback si no hay Wi‑Fi con gateway.
        var score = n.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => 10_000,
            NetworkInterfaceType.Ethernet => 5_000,
            _ => 1_000
        };
        var name = (n.Name + " " + n.Description).ToLowerInvariant();
        if (name.Contains("virtual") || name.Contains("vpn") || name.Contains("hyper-v") || name.Contains("wsl") || name.Contains("bluetooth"))
            score -= 4_000;
        return score;
    }

    private static string FormatMac(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 0 ? "-" : string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    private static string GetJson(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value)) return "-";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "-" : value.ToString();
    }

    private static async Task<string> RunProcessAsync(string file, string arguments, int timeoutMs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p is null) return "";
        var outputTask = p.StandardOutput.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
        try { await p.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch { }
            return "";
        }
        return (await outputTask).Trim();
    }
}
