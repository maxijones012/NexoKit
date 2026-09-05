using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace KitHerramientas.Desktop.Services;

public sealed record CsiNodeSnapshot(
    byte NodeId,
    string SourceIp,
    DateTimeOffset LastSeen,
    long Packets,
    long RawFrames,
    long VitalsFrames,
    int Rssi,
    int FrequencyMhz,
    int Subcarriers,
    bool? Presence,
    bool Motion,
    int Persons,
    double MotionEnergy,
    double PresenceScore,
    double BreathingBpm,
    double HeartBpm,
    double RawActivity,
    string State);

public sealed class CsiUdpService : IDisposable
{
    public const int DefaultPort = 5005;
    public const uint RawMagic = 0xC5110001;
    public const uint VitalsMagic = 0xC5110002;

    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private readonly object _gate = new();
    private readonly Dictionary<byte, MutableNode> _nodes = new();

    public bool IsRunning => _udp is not null;
    public long TotalPackets { get; private set; }
    public long InvalidPackets { get; private set; }
    public event Action<IReadOnlyList<CsiNodeSnapshot>>? Updated;
    public event Action<string>? StatusChanged;

    public void Start(int port = DefaultPort)
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        StatusChanged?.Invoke($"Escuchando CSI por UDP :{port}");
        _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _udp?.Close(); } catch { }
        _udp?.Dispose();
        _udp = null;
        _cts?.Dispose();
        _cts = null;
        StatusChanged?.Invoke("Receptor CSI detenido");
    }

    public void Clear()
    {
        lock (_gate)
        {
            _nodes.Clear();
            TotalPackets = 0;
            InvalidPackets = 0;
        }
        Publish();
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _udp is not null)
        {
            try
            {
                var result = await _udp.ReceiveAsync(token);
                Parse(result.Buffer, result.RemoteEndPoint.Address.ToString());
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"CSI: {ex.Message}");
                await Task.Delay(300, token).ContinueWith(_ => { }, CancellationToken.None);
            }
        }
    }

    private void Parse(byte[] data, string sourceIp)
    {
        if (data.Length < 5)
        {
            InvalidPackets++;
            return;
        }

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0, 4));
        if (magic == RawMagic) ParseRaw(data, sourceIp);
        else if (magic == VitalsMagic) ParseVitals(data, sourceIp);
        else
        {
            InvalidPackets++;
            return;
        }
        TotalPackets++;
        Publish();
    }

    private void ParseRaw(byte[] data, string sourceIp)
    {
        if (data.Length < 20) { InvalidPackets++; return; }
        var nodeId = data[4];
        var antennas = Math.Max(1, (int)data[5]);
        var subcarriers = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6, 2));
        var freq = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8, 4));
        var rssi = unchecked((sbyte)data[16]);
        var expectedPairs = antennas * subcarriers;
        var availablePairs = Math.Min(expectedPairs, Math.Max(0, (data.Length - 20) / 2));
        double amp = 0;
        for (var i = 0; i < availablePairs; i++)
        {
            var iqOffset = 20 + i * 2;
            var ii = unchecked((sbyte)data[iqOffset]);
            var qq = unchecked((sbyte)data[iqOffset + 1]);
            amp += Math.Sqrt(ii * ii + qq * qq);
        }
        var meanAmp = availablePairs > 0 ? amp / availablePairs : 0;

        lock (_gate)
        {
            var node = GetNode(nodeId, sourceIp);
            var previous = node.LastMeanAmplitude;
            var delta = previous > 0 ? Math.Abs(meanAmp - previous) / previous * 100.0 : 0;
            node.RawActivity = node.RawFrames == 0 ? delta : node.RawActivity * 0.78 + delta * 0.22;
            node.LastMeanAmplitude = meanAmp;
            node.RawFrames++;
            node.Packets++;
            node.Rssi = rssi;
            node.FrequencyMhz = freq;
            node.Subcarriers = subcarriers;
            node.LastSeen = DateTimeOffset.Now;
            node.SourceIp = sourceIp;
        }
    }

    private void ParseVitals(byte[] data, string sourceIp)
    {
        if (data.Length < 32) { InvalidPackets++; return; }
        var nodeId = data[4];
        var flags = data[5];
        var breathingRaw = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6, 2));
        var heartRaw = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8, 4));
        var rssi = unchecked((sbyte)data[12]);
        var persons = data[13];
        var motionEnergy = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(16, 4)));
        var presenceScore = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(20, 4)));

        lock (_gate)
        {
            var node = GetNode(nodeId, sourceIp);
            node.VitalsFrames++;
            node.Packets++;
            node.Rssi = rssi;
            node.Presence = (flags & 0x01) != 0;
            node.Fall = (flags & 0x02) != 0;
            node.Motion = (flags & 0x04) != 0;
            node.Persons = persons;
            node.BreathingBpm = breathingRaw / 100.0;
            node.HeartBpm = heartRaw / 10000.0;
            node.MotionEnergy = motionEnergy;
            node.PresenceScore = presenceScore;
            node.LastSeen = DateTimeOffset.Now;
            node.SourceIp = sourceIp;
        }
    }

    private MutableNode GetNode(byte id, string sourceIp)
    {
        if (!_nodes.TryGetValue(id, out var node))
        {
            node = new MutableNode { NodeId = id, SourceIp = sourceIp, LastSeen = DateTimeOffset.Now };
            _nodes[id] = node;
        }
        return node;
    }

    private void Publish()
    {
        IReadOnlyList<CsiNodeSnapshot> snapshot;
        lock (_gate)
        {
            snapshot = _nodes.Values
                .OrderBy(n => n.NodeId)
                .Select(n => n.ToSnapshot())
                .ToList();
        }
        Updated?.Invoke(snapshot);
    }

    public void Dispose() => Stop();

    private sealed class MutableNode
    {
        public byte NodeId;
        public string SourceIp = "—";
        public DateTimeOffset LastSeen;
        public long Packets;
        public long RawFrames;
        public long VitalsFrames;
        public int Rssi;
        public int FrequencyMhz;
        public int Subcarriers;
        public bool? Presence;
        public bool Motion;
        public bool Fall;
        public int Persons;
        public double MotionEnergy;
        public double PresenceScore;
        public double BreathingBpm;
        public double HeartBpm;
        public double RawActivity;
        public double LastMeanAmplitude;

        public CsiNodeSnapshot ToSnapshot()
        {
            var state = Fall ? "ALERTA CAÍDA" : Presence == true ? (Motion ? "PRESENCIA + MOVIMIENTO" : "PRESENCIA") : Presence == false ? "SIN PRESENCIA" : RawFrames > 0 ? "CSI RAW" : "SIN DATOS";
            return new(NodeId, SourceIp, LastSeen, Packets, RawFrames, VitalsFrames, Rssi, FrequencyMhz, Subcarriers,
                Presence, Motion, Persons, MotionEnergy, PresenceScore, BreathingBpm, HeartBpm, RawActivity, state);
        }
    }
}
