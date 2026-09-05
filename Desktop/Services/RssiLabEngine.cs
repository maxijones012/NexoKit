namespace KitHerramientas.Desktop.Services;

public enum LabSensitivity
{
    High,
    Normal,
    Low
}

public sealed record RssiBaseline(double Mean, double StdDev, int Samples);

public sealed record RssiLabReading(
    DateTimeOffset Timestamp,
    int Rssi,
    double Baseline,
    double Delta,
    double Score,
    string State,
    string? Marker = null)
{
    public string TimeText => Timestamp.LocalDateTime.ToString("HH:mm:ss");
    public string MarkerText => Marker ?? "";
}

public sealed class RssiLabEngine
{
    public RssiBaseline? Baseline { get; private set; }
    public LabSensitivity Sensitivity { get; set; } = LabSensitivity.Normal;
    private int? _previousRssi;

    public string SensitivityLabel => Sensitivity switch
    {
        LabSensitivity.High => "ALTA",
        LabSensitivity.Low => "BAJA",
        _ => "NORMAL"
    };

    public void ClearCalibration()
    {
        Baseline = null;
        _previousRssi = null;
    }

    public void ResetTracking() => _previousRssi = null;

    public RssiBaseline? Calibrate(IReadOnlyCollection<int> samples)
    {
        if (samples.Count < 5) return null;
        var mean = samples.Average();
        var variance = samples.Sum(v => Math.Pow(v - mean, 2)) / samples.Count;
        var std = Math.Max(1.0, Math.Sqrt(variance));
        Baseline = new RssiBaseline(mean, std, samples.Count);
        _previousRssi = samples.LastOrDefault();
        return Baseline;
    }

    public RssiLabReading? Evaluate(int rssi, string? marker = null, DateTimeOffset? timestamp = null)
    {
        var baseline = Baseline;
        if (baseline is null) return null;

        var delta = Math.Abs(rssi - baseline.Mean);
        var jump = _previousRssi is null ? 0.0 : Math.Abs(rssi - _previousRssi.Value);
        var noise = Math.Max(1.25, baseline.StdDev + 0.60);
        var rawScore = Math.Max(delta / noise, (jump / noise) * 0.90);
        var scale = Sensitivity switch
        {
            LabSensitivity.High => 0.78,
            LabSensitivity.Low => 1.30,
            _ => 1.0
        };
        var score = rawScore / scale;
        var state = score switch
        {
            < 1.40 => "ESTABLE",
            < 2.40 => "CAMBIO LEVE",
            < 3.60 => "CAMBIO FUERTE",
            _ => "VARIACIÓN COMPATIBLE CON MOVIMIENTO"
        };

        _previousRssi = rssi;
        return new RssiLabReading(timestamp ?? DateTimeOffset.Now, rssi, baseline.Mean, delta, score, state, marker);
    }
}
