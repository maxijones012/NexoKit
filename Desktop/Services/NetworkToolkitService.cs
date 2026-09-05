using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace KitHerramientas.Desktop.Services;

public sealed record PingWindowResult(
    string Target,
    int Sent,
    int Received,
    double LossPercent,
    long? MinMs,
    double? AvgMs,
    long? MaxMs,
    IReadOnlyList<string> Samples);

public sealed record LanDevice(
    string Ip,
    string HostName,
    string Mac,
    string Oui,
    string Vendor,
    string Note);

public sealed record CidrInfo(
    string Address,
    int Prefix,
    string Mask,
    string Wildcard,
    string Network,
    string Broadcast,
    string FirstHost,
    string LastHost,
    ulong AddressCount,
    ulong UsableHosts);

public static class NetworkToolkitService
{
    public static async Task<string> DnsLookupAsync(string query)
    {
        query = CleanTarget(query);
        if (query.Length == 0) return "Ingresá un dominio o una dirección IP.";

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Consulta: {query}");

            if (IPAddress.TryParse(query, out var ip))
            {
                var host = await Dns.GetHostEntryAsync(ip).WaitAsync(TimeSpan.FromSeconds(4));
                sb.AppendLine($"Nombre: {host.HostName}");
                foreach (var addr in host.AddressList.Distinct()) sb.AppendLine($"Dirección: {addr}");
            }
            else
            {
                var addresses = await Dns.GetHostAddressesAsync(query).WaitAsync(TimeSpan.FromSeconds(5));
                if (addresses.Length == 0) return $"DNS: no se encontraron direcciones para {query}.";
                foreach (var addr in addresses.Distinct())
                    sb.AppendLine($"{(addr.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6")}: {addr}");

                try
                {
                    var entry = await Dns.GetHostEntryAsync(query).WaitAsync(TimeSpan.FromSeconds(3));
                    if (!string.IsNullOrWhiteSpace(entry.HostName)) sb.AppendLine($"Canónico: {entry.HostName}");
                }
                catch { /* El lookup directo ya es suficiente. */ }
            }

            return sb.ToString().TrimEnd();
        }
        catch (TimeoutException)
        {
            return "DNS: tiempo de espera agotado.";
        }
        catch (Exception ex)
        {
            return $"DNS: {ex.Message}";
        }
    }

    public static async Task<PingWindowResult> PingWindowAsync(string target, int count = 10, int timeoutMs = 1200)
    {
        target = CleanTarget(target);
        count = Math.Clamp(count, 1, 30);
        timeoutMs = Math.Clamp(timeoutMs, 250, 5000);
        var times = new List<long>();
        var samples = new List<string>();

        if (target.Length == 0)
            return new("—", count, 0, 100, null, null, null, new[] { "Ingresá un destino." });

        using var ping = new Ping();
        for (var i = 1; i <= count; i++)
        {
            try
            {
                var reply = await ping.SendPingAsync(target, timeoutMs);
                if (reply.Status == IPStatus.Success)
                {
                    times.Add(reply.RoundtripTime);
                    samples.Add($"{i:00}: {reply.Address} · {reply.RoundtripTime} ms");
                }
                else
                {
                    samples.Add($"{i:00}: {reply.Status}");
                }
            }
            catch (Exception ex)
            {
                samples.Add($"{i:00}: error · {ex.Message}");
            }

            if (i < count) await Task.Delay(120);
        }

        var received = times.Count;
        return new(
            target,
            count,
            received,
            (count - received) * 100.0 / count,
            received == 0 ? null : times.Min(),
            received == 0 ? null : times.Average(),
            received == 0 ? null : times.Max(),
            samples);
    }

    public static string FormatPing(PingWindowResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"PING · {result.Target}");
        foreach (var line in result.Samples) sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine($"Enviados: {result.Sent} · Recibidos: {result.Received} · Pérdida: {result.LossPercent:0.#}%");
        if (result.Received > 0)
            sb.AppendLine($"Mín: {result.MinMs} ms · Prom: {result.AvgMs:0.0} ms · Máx: {result.MaxMs} ms");
        return sb.ToString().TrimEnd();
    }

    public static async Task<string> TraceRouteAsync(string target)
    {
        target = CleanTarget(target);
        if (target.Length == 0) return "Ingresá un destino.";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "tracert.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add("-h");
            psi.ArgumentList.Add("20");
            psi.ArgumentList.Add("-w");
            psi.ArgumentList.Add("700");
            psi.ArgumentList.Add(target);

            using var process = Process.Start(psi);
            if (process is null) return "No se pudo iniciar tracert.";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            var output = await outputTask;
            var error = await errorTask;

            var text = string.IsNullOrWhiteSpace(output) ? error : output;
            return string.IsNullOrWhiteSpace(text) ? "Traceroute sin salida." : text.Trim();
        }
        catch (OperationCanceledException)
        {
            return "Traceroute detenido por tiempo máximo (25 s).";
        }
        catch (Exception ex)
        {
            return $"Traceroute: {ex.Message}";
        }
    }

    public static CidrInfo CalculateCidr(string ipText, int prefix)
    {
        if (!IPAddress.TryParse(ipText.Trim(), out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Ingresá una IPv4 válida.");
        if (prefix is < 0 or > 32) throw new ArgumentOutOfRangeException(nameof(prefix), "El prefijo debe estar entre 0 y 32.");

        var value = ToUInt(ip);
        var mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        var wildcard = ~mask;
        var network = value & mask;
        var broadcast = network | wildcard;
        var count = 1UL << (32 - prefix);

        uint first;
        uint last;
        ulong usable;
        if (prefix <= 30)
        {
            first = network + 1;
            last = broadcast - 1;
            usable = Math.Max(0UL, count - 2);
        }
        else if (prefix == 31)
        {
            first = network;
            last = broadcast;
            usable = 2;
        }
        else
        {
            first = value;
            last = value;
            usable = 1;
        }

        return new(
            ip.ToString(), prefix, FromUInt(mask).ToString(), FromUInt(wildcard).ToString(),
            FromUInt(network).ToString(), FromUInt(broadcast).ToString(),
            FromUInt(first).ToString(), FromUInt(last).ToString(), count, usable);
    }

    public static string FormatCidr(CidrInfo c) =>
        $"IP: {c.Address}/{c.Prefix}\n" +
        $"Máscara: {c.Mask}\n" +
        $"Wildcard: {c.Wildcard}\n" +
        $"Red: {c.Network}\n" +
        $"Broadcast: {c.Broadcast}\n" +
        $"Hosts: {c.FirstHost} — {c.LastHost}\n" +
        $"Direcciones: {c.AddressCount:N0} · Utilizables: {c.UsableHosts:N0}";

    public static async Task<IReadOnlyList<LanDevice>> DiscoverLocal24Async(IProgress<string>? progress = null)
    {
        var snapshot = NetworkService.GetSnapshot();
        if (!IPAddress.TryParse(snapshot.LocalIp, out var local) || local.AddressFamily != AddressFamily.InterNetwork)
            return Array.Empty<LanDevice>();

        var bytes = local.GetAddressBytes();
        var prefix = $"{bytes[0]}.{bytes[1]}.{bytes[2]}";
        progress?.Report($"Explorando {prefix}.0/24 por ICMP…");

        var live = new System.Collections.Concurrent.ConcurrentBag<string>();
        using var gate = new SemaphoreSlim(48);
        var tasks = Enumerable.Range(1, 254).Select(async host =>
        {
            await gate.WaitAsync();
            try
            {
                var ip = $"{prefix}.{host}";
                if (ip == snapshot.LocalIp)
                {
                    live.Add(ip);
                    return;
                }
                using var ping = new Ping();
                try
                {
                    var reply = await ping.SendPingAsync(ip, 260);
                    if (reply.Status == IPStatus.Success) live.Add(ip);
                }
                catch { }
            }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);

        progress?.Report($"{live.Count} respuestas · leyendo tabla ARP y nombres…");
        var arp = await ReadArpTableAsync();
        var arpLocal = arp.Keys.Where(ip => ip.StartsWith(prefix + ".", StringComparison.Ordinal));
        var sortedIps = live
            .Concat(arpLocal)
            .Append(snapshot.LocalIp)
            .Distinct()
            .OrderBy(ip => IPAddress.Parse(ip).GetAddressBytes()[3])
            .ToList();

        var nameTasks = sortedIps.Select(async ip =>
        {
            try
            {
                var entry = await Dns.GetHostEntryAsync(ip).WaitAsync(TimeSpan.FromMilliseconds(650));
                return (Ip: ip, Host: entry.HostName);
            }
            catch { return (Ip: ip, Host: "—"); }
        }).ToArray();
        var names = (await Task.WhenAll(nameTasks)).ToDictionary(x => x.Ip, x => x.Host, StringComparer.OrdinalIgnoreCase);

        var result = new List<LanDevice>();
        foreach (var ip in sortedIps)
        {
            arp.TryGetValue(ip, out var mac);
            mac ??= ip == snapshot.LocalIp ? GetLocalMacForIp(ip) : "—";
            var oui = ExtractOui(mac);
            var note = ip == snapshot.LocalIp ? "Este equipo" : live.Contains(ip) ? "Respuesta ICMP" : "Vecino ARP";
            result.Add(new(ip, names.GetValueOrDefault(ip, "—"), mac, oui, "Base OUI local no cargada", note));
        }

        progress?.Report($"Listo · {result.Count} equipos/vecinos visibles en el segmento local /24.");
        return result;
    }

    public static (string Normalized, string Oui, string Status) InspectMac(string input)
    {
        var hex = Regex.Replace(input ?? "", "[^0-9A-Fa-f]", "").ToUpperInvariant();
        if (hex.Length != 12) return ("—", "—", "Ingresá una MAC de 12 dígitos hexadecimales.");
        var normalized = string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
        var oui = $"{hex[..2]}:{hex.Substring(2, 2)}:{hex.Substring(4, 2)}";
        var first = Convert.ToByte(hex[..2], 16);
        var locallyAdministered = (first & 0x02) != 0;
        var multicast = (first & 0x01) != 0;
        var status = locallyAdministered
            ? "MAC local/aleatorizada: el OUI puede no representar al fabricante real."
            : multicast
                ? "Dirección multicast/grupal."
                : "OUI extraído. Para fabricante se puede agregar una base IEEE local.";
        return (normalized, oui, status);
    }

    private static string CleanTarget(string value)
    {
        var target = (value ?? "").Trim();
        if (target.Length > 253) target = target[..253];
        return target;
    }

    private static async Task<Dictionary<string, string>> ReadArpTableAsync()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "arp.exe",
                Arguments = "-a",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null) return dict;
            var text = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            foreach (Match m in Regex.Matches(text, @"(?m)^\s*(?<ip>\d{1,3}(?:\.\d{1,3}){3})\s+(?<mac>[0-9a-f]{2}(?:-[0-9a-f]{2}){5})\s+", RegexOptions.IgnoreCase))
                dict[m.Groups["ip"].Value] = m.Groups["mac"].Value.Replace('-', ':').ToUpperInvariant();
        }
        catch { }
        return dict;
    }

    private static string GetLocalMacForIp(string ip)
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.GetIPProperties().UnicastAddresses.Any(a => a.Address.ToString() == ip))
                {
                    var hex = nic.GetPhysicalAddress().ToString();
                    if (hex.Length == 12) return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
                }
            }
        }
        catch { }
        return "—";
    }

    private static string ExtractOui(string mac)
    {
        var hex = Regex.Replace(mac ?? "", "[^0-9A-Fa-f]", "").ToUpperInvariant();
        return hex.Length >= 6 ? $"{hex[..2]}:{hex.Substring(2, 2)}:{hex.Substring(4, 2)}" : "—";
    }

    private static uint ToUInt(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    }

    private static IPAddress FromUInt(uint value) => new(new[]
    {
        (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value
    });
}
