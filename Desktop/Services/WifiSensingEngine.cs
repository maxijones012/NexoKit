namespace KitHerramientas.Desktop.Services;

public sealed record WifiSensingBaseline(double Mean, double StdDev, int Samples);

public sealed record WifiSensingReading(
    DateTimeOffset Timestamp,
    int Rssi,
    double SmoothedRssi,
    double Baseline,
    double Delta,
    double Score,
    double Confidence,
    string State);

public sealed class WifiSensingEngine
{
    private readonly Queue<double> _window = new();
    private double? _previousSmoothed;

    public WifiSensingBaseline? Baseline { get; private set; }
    public int WindowSize { get; set; } = 5;
    public double Sensitivity { get; set; } = 1.0;

    public WifiSensingBaseline? Calibrate(IReadOnlyCollection<int> samples)
    {
        if (samples.Count < 10) return null;
        var mean = samples.Average();
        var variance = samples.Sum(v => Math.Pow(v - mean, 2)) / samples.Count;
        var std = Math.Max(0.95, Math.Sqrt(variance));
        Baseline = new WifiSensingBaseline(mean, std, samples.Count);
        _window.Clear();
        _previousSmoothed = null;
        foreach (var sample in samples.TakeLast(Math.Max(1, WindowSize))) _window.Enqueue(sample);
        return Baseline;
    }

    public void Reset()
    {
        Baseline = null;
        _window.Clear();
        _previousSmoothed = null;
    }

    public WifiSensingReading? Evaluate(int rssi, DateTimeOffset? timestamp = null)
    {
        var baseline = Baseline;
        if (baseline is null) return null;

        _window.Enqueue(rssi);
        while (_window.Count > Math.Max(1, WindowSize)) _window.Dequeue();
        var smoothed = _window.Average();

        var delta = Math.Abs(smoothed - baseline.Mean);
        var jump = _previousSmoothed is null ? 0.0 : Math.Abs(smoothed - _previousSmoothed.Value);
        var noise = Math.Max(1.0, baseline.StdDev + 0.55);
        var rawScore = Math.Max(delta / noise, (jump / noise) * 0.72);
        var score = rawScore / Math.Clamp(Sensitivity, 0.65, 1.45);
        var confidence = Math.Clamp((score - 0.95) / 3.0, 0.0, 1.0);

        var state = score switch
        {
            < 1.25 => "AMBIENTE ESTABLE",
            < 2.00 => "VARIACIÓN LEVE",
            < 3.20 => "ACTIVIDAD RF ALTA",
            _ => "MOVIMIENTO PROBABLE"
        };

        _previousSmoothed = smoothed;
        return new WifiSensingReading(
            timestamp ?? DateTimeOffset.Now,
            rssi,
            smoothed,
            baseline.Mean,
            delta,
            score,
            confidence,
            state);
    }
}
