using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using KitHerramientas.Desktop.Services;

namespace KitHerramientas.Desktop.Controls;

public sealed class CsiSceneView : FrameworkElement
{
    private readonly DispatcherTimer _timer;
    private IReadOnlyList<CsiNodeSnapshot> _nodes = Array.Empty<CsiNodeSnapshot>();
    private double _phase;
    private WifiSensingReading? _wifiReading;
    private bool _wifiCalibrated;
    private bool _wifiSensingRunning;
    private string _wifiLabel = "ROUTER ↔ EQUIPO";

    public bool DemoMode { get; private set; } = true;
    public bool WifiDirectMode { get; private set; } = true;
    public bool ShowTracking { get; set; } = true;
    public bool ShowHeatmap { get; set; } = true;

    public CsiSceneView()
    {
        SnapsToDevicePixels = true;
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(45)
        };
        _timer.Tick += (_, _) =>
        {
            _phase += 0.055;
            if (_phase > Math.PI * 2) _phase -= Math.PI * 2;
            InvalidateVisual();
        };
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    public void SetDemoMode(bool enabled)
    {
        DemoMode = enabled;
        InvalidateVisual();
    }

    public void SetWifiDirectMode(bool enabled)
    {
        WifiDirectMode = enabled;
        InvalidateVisual();
    }

    public void UpdateWifiSensing(WifiSensingReading? reading, bool calibrated, bool running, string? linkLabel = null)
    {
        _wifiReading = reading;
        _wifiCalibrated = calibrated;
        _wifiSensingRunning = running;
        if (!string.IsNullOrWhiteSpace(linkLabel)) _wifiLabel = linkLabel!;
        InvalidateVisual();
    }

    public void UpdateNodes(IReadOnlyList<CsiNodeSnapshot> nodes)
    {
        _nodes = nodes ?? Array.Empty<CsiNodeSnapshot>();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var w = Math.Max(1, ActualWidth);
        var h = Math.Max(1, ActualHeight);

        var bg = new LinearGradientBrush(
            Color.FromRgb(4, 8, 14),
            Color.FromRgb(2, 5, 10),
            new Point(0, 0), new Point(1, 1));
        dc.DrawRoundedRectangle(bg, new Pen(new SolidColorBrush(Color.FromRgb(22, 49, 63)), 1), new Rect(0, 0, w, h), 14, 14);

        DrawAmbientGlow(dc, w, h);
        DrawPerspectiveGrid(dc, w, h);
        DrawRadarRings(dc, w, h);
        if (DemoMode || !WifiDirectMode) DrawSensors(dc, w, h);

        if (DemoMode)
            DrawDemo(dc, w, h);
        else if (WifiDirectMode)
            DrawWifiDirect(dc, w, h);
        else
            DrawReal(dc, w, h);

        DrawHud(dc, w, h);
    }

    private void DrawAmbientGlow(DrawingContext dc, double w, double h)
    {
        var cyan = new SolidColorBrush(Color.FromArgb(18, 0, 230, 255));
        var blue = new SolidColorBrush(Color.FromArgb(18, 35, 75, 255));
        dc.DrawEllipse(cyan, null, new Point(w * .72, h * .47), w * .25, h * .32);
        dc.DrawEllipse(blue, null, new Point(w * .42, h * .35), w * .34, h * .40);
    }

    private void DrawPerspectiveGrid(DrawingContext dc, double w, double h)
    {
        var horizon = h * .58;
        var floorBottom = h * .94;
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 32, 202, 226)), 1);
        var majorPen = new Pen(new SolidColorBrush(Color.FromArgb(65, 40, 235, 255)), 1.2);

        for (var i = -9; i <= 9; i++)
        {
            var xBottom = w * .5 + i * (w / 14.0);
            dc.DrawLine(i % 3 == 0 ? majorPen : gridPen, new Point(w * .5, horizon), new Point(xBottom, floorBottom));
        }

        for (var i = 0; i < 9; i++)
        {
            var t = i / 8.0;
            var eased = t * t;
            var y = horizon + eased * (floorBottom - horizon);
            var half = (w * .45) * eased;
            dc.DrawLine(i % 2 == 0 ? majorPen : gridPen, new Point(w * .5 - half, y), new Point(w * .5 + half, y));
        }
    }

    private void DrawRadarRings(DrawingContext dc, double w, double h)
    {
        var centers = new[]
        {
            new Point(w * .48, h * .48),
            new Point(w * .73, h * .50)
        };

        foreach (var center in centers)
        {
            for (var i = 0; i < 4; i++)
            {
                var pulse = (Math.Sin(_phase + i * .8) + 1) * .5;
                var rx = 50 + i * 35 + pulse * 8;
                var ry = rx * .70;
                var a = (byte)Math.Clamp(70 - i * 12, 14, 70);
                dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(a, 36, 112, 255)), 1.4), center, rx, ry);
            }
        }
    }

    private void DrawSensors(DrawingContext dc, double w, double h)
    {
        var sensorPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 226, 255)), 1.2);
        var fill = new SolidColorBrush(Color.FromArgb(95, 0, 226, 255));
        var points = new[]
        {
            new Point(w * .16, h * .70),
            new Point(w * .50, h * .77),
            new Point(w * .84, h * .70)
        };

        for (var i = 0; i < points.Length; i++)
        {
            var p = points[i];
            dc.DrawEllipse(fill, sensorPen, p, 7, 7);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 226, 255)), 1), p, 17 + Math.Sin(_phase + i) * 3, 17 + Math.Sin(_phase + i) * 3);
            DrawText(dc, $"S{i + 1}", p.X - 10, p.Y + 12, 10, Color.FromRgb(90, 221, 238), FontWeights.SemiBold);
        }
    }

    private void DrawDemo(DrawingContext dc, double w, double h)
    {
        var bob1 = Math.Sin(_phase) * 3;
        var bob2 = Math.Sin(_phase + 1.8) * 2;
        DrawHeatBlob(dc, new Point(w * .47, h * .56 + bob1), 64, 95, .95);
        DrawPerson(dc, new Point(w * .47, h * .58 + bob1), 1.00, "P1 · DEMO", 0.93);

        DrawHeatBlob(dc, new Point(w * .73, h * .58 + bob2), 54, 88, .75);
        DrawPerson(dc, new Point(w * .73, h * .60 + bob2), .88, "P2 · DEMO", 0.92);

        if (ShowTracking)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(130, 0, 255, 183)), 1.6) { DashStyle = DashStyles.Dash };
            var geo = new StreamGeometry();
            using var ctx = geo.Open();
            ctx.BeginFigure(new Point(w * .36, h * .76), false, false);
            ctx.BezierTo(new Point(w * .40, h * .70), new Point(w * .44, h * .68), new Point(w * .47, h * .66), true, false);
            dc.DrawGeometry(null, pen, geo);
        }
    }

    private void DrawWifiDirect(DrawingContext dc, double w, double h)
    {
        var center = new Point(w * .59, h * .58);

        if (!_wifiCalibrated)
        {
            DrawText(dc, "CALIBRÁ EL AMBIENTE", w * .5 - 92, h * .43, 17, Color.FromRgb(120, 145, 158), FontWeights.Bold);
            DrawText(dc, "Router + Wi‑Fi + este equipo · sin hardware externo", w * .5 - 165, h * .49, 11, Color.FromRgb(84, 107, 120), FontWeights.Normal);
            return;
        }

        if (!_wifiSensingRunning && _wifiReading is null)
        {
            DrawText(dc, "CALIBRACIÓN LISTA", w * .5 - 78, h * .43, 17, Color.FromRgb(83, 255, 196), FontWeights.Bold);
            DrawText(dc, "Iniciá sensing para observar variaciones RF", w * .5 - 130, h * .49, 11, Color.FromRgb(92, 132, 143), FontWeights.Normal);
            return;
        }

        var r = _wifiReading;
        if (r is null)
        {
            DrawText(dc, "ESPERANDO RSSI", w * .5 - 70, h * .45, 16, Color.FromRgb(116, 137, 151), FontWeights.Bold);
            return;
        }

        var intensity = Math.Clamp(r.Confidence, .08, 1.0);
        var pulse = (Math.Sin(_phase * 1.5) + 1) * .5;
        var rx = 48 + intensity * 85 + pulse * 9;
        var ry = 65 + intensity * 120 + pulse * 12;

        if (ShowHeatmap)
        {
            for (var i = 5; i >= 1; i--)
            {
                var scale = i / 5.0;
                var alpha = (byte)Math.Clamp(10 + intensity * (56 - i * 5), 8, 72);
                var color = r.State == "AMBIENTE ESTABLE"
                    ? Color.FromArgb(alpha, 0, 170, 255)
                    : Color.FromArgb(alpha, 0, 255, 167);
                dc.DrawEllipse(new SolidColorBrush(color), null, center, rx * scale, ry * scale);
            }
        }

        var ringColor = r.State == "AMBIENTE ESTABLE"
            ? Color.FromArgb(130, 35, 150, 255)
            : Color.FromArgb(150, 0, 255, 183);
        for (var i = 0; i < 4; i++)
        {
            var extra = i * 22 + pulse * 7;
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(ringColor), 1.2),
                center, rx + extra, (ry + extra) * .72);
        }

        DrawText(dc, r.State, center.X - 86, center.Y + ry * .72 + 18, 12,
            r.State == "MOVIMIENTO PROBABLE" ? Color.FromRgb(255, 211, 96) : Color.FromRgb(83, 255, 196), FontWeights.Bold);
        DrawText(dc, $"RF {r.Confidence * 100:0}% · Δ {r.Delta:0.0} dB · score {r.Score:0.00}",
            center.X - 105, center.Y + ry * .72 + 38, 10, Color.FromRgb(111, 167, 179), FontWeights.Normal);

        var router = new Point(w * .18, h * .69);
        var device = new Point(w * .88, h * .69);
        var linkPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 0, 226, 255)), 1.2) { DashStyle = DashStyles.Dash };
        dc.DrawLine(linkPen, router, device);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(150, 0, 226, 255)), null, router, 7, 7);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(150, 0, 255, 183)), null, device, 7, 7);
        DrawText(dc, "ROUTER / AP", router.X - 35, router.Y + 16, 9, Color.FromRgb(90, 221, 238), FontWeights.SemiBold);
        DrawText(dc, "TELÉFONO / PC", device.X - 42, device.Y + 16, 9, Color.FromRgb(83, 255, 196), FontWeights.SemiBold);

        if (ShowTracking && r.State != "AMBIENTE ESTABLE")
        {
            var trail = new Pen(new SolidColorBrush(Color.FromArgb((byte)(70 + intensity * 90), 0, 255, 183)), 1.5)
            {
                DashStyle = DashStyles.Dash
            };
            var geo = new StreamGeometry();
            using var ctx = geo.Open();
            ctx.BeginFigure(new Point(center.X - 95, center.Y + 70), false, false);
            ctx.BezierTo(new Point(center.X - 45, center.Y + 42), new Point(center.X + 10, center.Y + 80), new Point(center.X + 75, center.Y + 48), true, false);
            dc.DrawGeometry(null, trail, geo);
        }

        DrawText(dc, _wifiLabel, 18, h - 26, 9, Color.FromRgb(74, 116, 132), FontWeights.SemiBold);
    }

    private void DrawReal(DrawingContext dc, double w, double h)
    {
        var live = _nodes.Where(n => DateTimeOffset.Now - n.LastSeen < TimeSpan.FromSeconds(5)).ToList();
        if (live.Count == 0)
        {
            DrawText(dc, "ESPERANDO CSI", w * .5 - 70, h * .44, 17, Color.FromRgb(116, 137, 151), FontWeights.Bold);
            DrawText(dc, "Sin paquetes de sensores compatibles", w * .5 - 125, h * .49, 12, Color.FromRgb(90, 109, 122), FontWeights.Normal);
            return;
        }

        var personIndex = 0;
        foreach (var node in live.Take(6))
        {
            if (node.VitalsFrames > 0 && node.Presence == true)
            {
                var count = Math.Clamp(node.Persons <= 0 ? 1 : node.Persons, 1, 3);
                for (var p = 0; p < count; p++)
                {
                    var x = w * (.34 + ((personIndex * .19) % .53));
                    var y = h * (.58 + ((personIndex % 2) * .06));
                    var confidence = Math.Clamp(node.PresenceScore, 0, 1);
                    DrawHeatBlob(dc, new Point(x, y), 52, 82, Math.Max(.25, confidence));
                    DrawPerson(dc, new Point(x, y + 5), .86, $"N{node.NodeId} · EST.", confidence);
                    personIndex++;
                }
            }
            else if (node.RawFrames > 0)
            {
                var x = w * (.32 + ((node.NodeId * .137) % .55));
                var y = h * (.60 + ((node.NodeId % 2) * .08));
                var a = Math.Clamp(node.RawActivity / 35.0, .15, 1.0);
                DrawRawActivity(dc, new Point(x, y), a, node.NodeId);
            }
        }
    }

    private void DrawRawActivity(DrawingContext dc, Point center, double activity, byte nodeId)
    {
        var radius = 28 + activity * 35 + Math.Sin(_phase * 1.4 + nodeId) * 4;
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(30 + 70 * activity), 0, 210, 255)), null, center, radius, radius * .72);
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 230, 255)), 1.2), center, radius + 8, (radius + 8) * .72);
        DrawText(dc, $"N{nodeId} · CSI RAW", center.X - 35, center.Y + radius * .8, 10, Color.FromRgb(83, 220, 238), FontWeights.SemiBold);
    }

    private void DrawHeatBlob(DrawingContext dc, Point center, double rx, double ry, double intensity)
    {
        if (!ShowHeatmap) return;
        for (var i = 4; i >= 1; i--)
        {
            var scale = i / 4.0;
            var alpha = (byte)Math.Clamp(18 + intensity * 18 * (5 - i), 10, 70);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, 0, 255, 167));
            dc.DrawEllipse(brush, null, center, rx * scale, ry * scale);
        }
    }

    private void DrawPerson(DrawingContext dc, Point center, double scale, string label, double confidence)
    {
        var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(55, 0, 255, 170)), 8 * scale);
        var bodyPen = new Pen(new SolidColorBrush(Color.FromRgb(73, 255, 190)), 2.2 * scale);
        var joint = new SolidColorBrush(Color.FromRgb(0, 255, 183));

        var head = new Point(center.X, center.Y - 72 * scale);
        var neck = new Point(center.X, center.Y - 50 * scale);
        var hip = new Point(center.X, center.Y - 8 * scale);
        var lShoulder = new Point(center.X - 18 * scale, center.Y - 46 * scale);
        var rShoulder = new Point(center.X + 18 * scale, center.Y - 46 * scale);
        var lHand = new Point(center.X - 30 * scale, center.Y - 13 * scale);
        var rHand = new Point(center.X + 30 * scale, center.Y - 13 * scale);
        var lFoot = new Point(center.X - 16 * scale, center.Y + 50 * scale);
        var rFoot = new Point(center.X + 16 * scale, center.Y + 50 * scale);

        foreach (var pair in new[]
        {
            (neck, hip), (lShoulder, rShoulder), (lShoulder, lHand), (rShoulder, rHand),
            (hip, lFoot), (hip, rFoot)
        })
            dc.DrawLine(glowPen, pair.Item1, pair.Item2);

        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(45, 0, 255, 183)), null, head, 11 * scale, 13 * scale);
        dc.DrawEllipse(null, bodyPen, head, 9 * scale, 11 * scale);
        foreach (var pair in new[]
        {
            (neck, hip), (lShoulder, rShoulder), (lShoulder, lHand), (rShoulder, rHand),
            (hip, lFoot), (hip, rFoot)
        })
            dc.DrawLine(bodyPen, pair.Item1, pair.Item2);

        foreach (var p in new[] { neck, hip, lShoulder, rShoulder, lHand, rHand, lFoot, rFoot })
            dc.DrawEllipse(joint, null, p, 2.7 * scale, 2.7 * scale);

        var box = new Rect(center.X - 38 * scale, center.Y - 92 * scale, 76 * scale, 150 * scale);
        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(95, 33, 255, 196)), 1), box);
        DrawText(dc, label, center.X - 34 * scale, center.Y + 60 * scale, 10, Color.FromRgb(79, 255, 197), FontWeights.SemiBold);
        DrawText(dc, $"{confidence * 100:0}%", center.X - 12 * scale, center.Y + 74 * scale, 9, Color.FromRgb(104, 179, 164), FontWeights.Normal);
    }

    private void DrawHud(DrawingContext dc, double w, double h)
    {
        var mode = DemoMode ? "DEMO VISUAL" : WifiDirectMode ? "WI‑FI DIRECT" : "CSI REAL";
        DrawText(dc, mode, 18, 16, 11,
            DemoMode ? Color.FromRgb(245, 181, 60) : Color.FromRgb(0, 255, 183), FontWeights.Bold);
        DrawText(dc, "RF SENSING SCENE", 18, 35, 10, Color.FromRgb(84, 116, 131), FontWeights.SemiBold);

        var status = DemoMode
            ? "SIMULACIÓN"
            : WifiDirectMode
                ? (!_wifiCalibrated ? "CALIBRATE" : _wifiSensingRunning ? "RSSI LIVE" : "READY")
                : (_nodes.Any() ? "DATA LINK" : "WAITING");
        DrawText(dc, status, w - 94, 17, 10,
            DemoMode ? Color.FromRgb(245, 181, 60) : Color.FromRgb(0, 255, 183), FontWeights.Bold);
    }

    private static void DrawText(DrawingContext dc, string text, double x, double y, double size, Color color, FontWeight weight)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size,
            new SolidColorBrush(color),
            1.0);
        dc.DrawText(ft, new Point(x, y));
    }
}
