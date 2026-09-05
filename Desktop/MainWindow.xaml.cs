using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using KitHerramientas.Desktop.Services;
using Microsoft.Win32;

namespace KitHerramientas.Desktop;

public partial class MainWindow : Window
{
    private NetworkSnapshot _snapshot = NetworkService.GetSnapshot();
    private WifiConnection _wifi = new(false, "—", "—", null, null, null, "—", "—", "—", "—", "—", "—");
    private readonly List<int> _signalHistory = new();
    private readonly DispatcherTimer _signalTimer;
    private bool _monitoring;
    private bool _captureBusy;

    private readonly RssiLabEngine _labEngine = new();
    private readonly List<int> _calibrationSamples = new();
    private readonly List<RssiLabReading> _labSession = new();
    private bool _labCalibrating;
    private bool _labRunning;
    private const int CalibrationSamples = 15;
    private const int MaxLabRows = 21600;
    private string _lastNetworkResult = "";
    private IReadOnlyList<LanDevice> _lanDevices = Array.Empty<LanDevice>();
    private readonly CsiUdpService _csi = new();
    private bool _csiDemoMode = false;
    private bool _csiTracking = true;
    private bool _csiHeatmap = true;
    private IReadOnlyList<CsiNodeSnapshot> _lastCsiNodes = Array.Empty<CsiNodeSnapshot>();

    private readonly WifiSensingEngine _wifiSensingEngine = new();
    private readonly List<int> _wifiSensingCalibration = new();
    private bool _wifiSensingCalibrating;
    private bool _wifiSensingRunning;
    private WifiSensingReading? _lastWifiSensingReading;
    private int _wifiSensingSamples;
    private const int WifiSensingCalibrationSamples = 25;

    private readonly ObservableCollection<RepositoryWatch> _repositoryWatches;
    private readonly DispatcherTimer _repositoryTimer;
    private bool _repositoryCheckBusy;
    private readonly List<CatalogSourceWatch> _catalogSources;
    private readonly List<DiscoveredTool> _discoveredTools;
    private bool _catalogCheckBusy;

    public MainWindow()
    {
        InitializeComponent();
        _signalTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _signalTimer.Tick += async (_, _) => await CaptureSignalAsync();
        _repositoryWatches = new ObservableCollection<RepositoryWatch>(RepositoryUpdateService.Load());
        _catalogSources = CatalogDiscoveryService.LoadSources();
        _discoveredTools = CatalogDiscoveryService.LoadTools();
        _repositoryTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _repositoryTimer.Tick += async (_, _) =>
        {
            await CheckDueRepositoriesAsync();
            await CheckDueCatalogSourcesAsync();
        };
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _csi.Dispose();
        _csi.Updated += nodes => Dispatcher.Invoke(() => UpdateCsiUi(nodes));
        _csi.StatusChanged += text => Dispatcher.Invoke(() =>
        {
            if (!_csiDemoMode && !CsiScene.WifiDirectMode) CsiStatusText.Text = text;
        });
    }


    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            DiagnosticText.Text = "Diagnóstico: leyendo red local…";
            await RefreshAllAsync();
        }
        catch (Exception ex)
        {
            DiagnosticText.Text = $"Inicio parcial: {ex.Message}";
        }

        // El receptor CSI queda escuchando desde el inicio. Sin sensor externo no habrá paquetes,
        // pero la interfaz debe indicar claramente que está esperando hardware CSI.
        try
        {
            if (!_csi.IsRunning) _csi.Start();
            CsiStatusText.Text = "WI‑FI DIRECTO · listo para calibrar con el router conectado";
        }
        catch (Exception ex)
        {
            CsiStatusText.Text = $"Wi‑Fi directo listo · CSI opcional no pudo abrir UDP 5005: {ex.Message}";
        }

        CsiScene.SetWifiDirectMode(true);
        ApplyCsiModeUi();

        RepositoryGrid.ItemsSource = _repositoryWatches;
        RepositoryStatusText.Text = $"{_repositoryWatches.Count} repositorios · comprobación automática independiente";
        CatalogSourceGrid.ItemsSource = _catalogSources;
        RefreshDiscoveryGrid();
        CatalogStatusText.Text = $"{_catalogSources.Count} fuente(s) · {_discoveredTools.Count} recursos guardados";
        _repositoryTimer.Start();
        _ = CheckDueRepositoriesAsync();
        _ = CheckDueCatalogSourcesAsync();
    }

    private async Task RefreshAllAsync()
    {
        try
        {
            _snapshot = NetworkService.GetSnapshot();
            StatusText.Text = _snapshot.Status;
            AdapterText.Text = $"Adaptador: {_snapshot.Adapter} · {_snapshot.InterfaceType}";
            AdapterDescriptionText.Text = $"Hardware: {_snapshot.AdapterDescription}";
            AdapterMacText.Text = $"MAC adaptador: {_snapshot.MacAddress}";
            SpeedText.Text = $"Enlace: {_snapshot.LinkSpeed}";
            IpText.Text = $"IP local: {_snapshot.LocalIp}/{_snapshot.PrefixLength}";
            GatewayText.Text = $"Router / gateway: {_snapshot.Gateway}";
            DnsText.Text = $"DNS: {_snapshot.DnsServers}";
            PingText.Text = "Ping router: midiendo…";
            GatewayMacText.Text = "MAC router: leyendo…";

            var basicOk = _snapshot.LocalIp != "-";
            DiagnosticText.Text = basicOk
                ? $"RED OK · {_snapshot.InterfaceType} · leyendo perfil y Wi‑Fi…"
                : "SIN IPv4 ACTIVA · revisá el adaptador o la conexión";

            NetworkProfileInfo? profile = null;
            try { profile = await NetworkService.GetProfileAsync(_snapshot.Adapter); } catch { }
            ProfileText.Text = profile is null
                ? "Perfil Windows: —"
                : $"Perfil Windows: {profile.Name} · {profile.Category} · IPv4 {profile.Ipv4Connectivity}";

            if (_snapshot.Gateway != "-")
            {
                var pingTask = NetworkService.PingAsync(_snapshot.Gateway);
                var macTask = NetworkService.GetGatewayMacAsync(_snapshot.Gateway);
                PingText.Text = $"Ping router: {await pingTask}";
                GatewayMacText.Text = $"MAC router: {await macTask}";
            }
            else
            {
                PingText.Text = "Ping router: —";
                GatewayMacText.Text = "MAC router: —";
            }

            try
            {
                _wifi = await WifiService.GetCurrentAsync();
                var displaySsid = _wifi.Ssid != "—" ? _wifi.Ssid : (_wifi.Profile != "—" ? _wifi.Profile : profile?.Name ?? "—");
                WifiSsidText.Text = $"Red Wi‑Fi: {displaySsid}";
                WifiBssidText.Text = $"BSSID/AP: {_wifi.Bssid}";
                WifiSignalText.Text = _wifi.SignalPercent is null
                    ? "Señal: no disponible"
                    : $"Señal: {_wifi.SignalPercent}% · ~{_wifi.ApproxRssi} dBm";
                WifiChannelText.Text = $"Canal: {_wifi.Channel?.ToString() ?? "—"} · {_wifi.RadioType}";
                WifiRatesText.Text = $"RX/TX: {_wifi.ReceiveRate}/{_wifi.TransmitRate} Mbps · {_wifi.Authentication}";
                LiveSsidText.Text = displaySsid;

                if (_wifi.Connected && _wifi.SignalPercent is not null)
                {
                    DiagnosticText.Text = $"RED OK · WI‑FI OK · router {_snapshot.Gateway} · RSSI disponible";
                    WifiWarningText.Text = "";
                    if (!_monitoring) ToggleMonitoring(true);
                    _ = AutoScanWifiAsync();
                }
                else if (basicOk)
                {
                    DiagnosticText.Text = $"RED OK · {_snapshot.InterfaceType} · router {_snapshot.Gateway} · SSID/RSSI limitado";
                    WifiWarningText.Text = string.IsNullOrWhiteSpace(WifiService.LastDiagnostic)
                        ? "Windows no entregó SSID/RSSI. IP, gateway, DNS, MAC y perfil de red sí están disponibles."
                        : WifiService.LastDiagnostic;
                }
            }
            catch (Exception ex)
            {
                WifiSsidText.Text = $"Red Wi‑Fi: {profile?.Name ?? "no disponible"}";
                WifiBssidText.Text = "BSSID/AP: —";
                WifiSignalText.Text = "Señal: no disponible";
                WifiChannelText.Text = "Canal: —";
                WifiRatesText.Text = "RX/TX: —";
                WifiWarningText.Text = $"Wi‑Fi: {ex.Message}";
                DiagnosticText.Text = basicOk ? "RED OK · WI‑FI NO DISPONIBLE" : "RED NO DISPONIBLE";
            }

            if (string.IsNullOrWhiteSpace(NetTargetBox.Text))
                NetTargetBox.Text = _snapshot.Gateway is "-" ? "1.1.1.1" : _snapshot.Gateway;
            if (IPAddress.TryParse(_snapshot.LocalIp, out _)) CidrIpBox.Text = _snapshot.LocalIp;
            CsiTargetText.Text = _wifi.Connected
                ? $"Wi‑Fi directo · {_wifi.Ssid} · AP {_wifi.Bssid} · gateway {_snapshot.Gateway}"
                : $"Wi‑Fi directo · gateway {_snapshot.Gateway} · esperando RSSI";
        }
        catch (Exception ex)
        {
            DiagnosticText.Text = $"ERROR DE DIAGNÓSTICO: {ex.Message}";
            StatusText.Text = "La interfaz abrió, pero falló la lectura de red.";
        }
    }

    private async Task AutoScanWifiAsync()
    {
        try
        {
            var networks = await WifiService.ScanAsync();
            NetworksGrid.ItemsSource = networks;
            ChannelsList.ItemsSource = WifiService.ChannelSummary(networks)
                .Select(c => $"Canal {c.Channel}: {c.Count} AP · mejor señal {c.Strongest}%")
                .ToList();
            ScanStatusText.Text = networks.Count == 0
                ? "Sin resultados automáticos. Tocá ESCANEAR para reintentar."
                : $"{networks.Count} puntos de acceso · actualización automática {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            ScanStatusText.Text = $"Escaneo automático no disponible: {ex.Message}";
        }
    }

    private void OpenLocationSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:privacy-location") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            WifiWarningText.Text = $"No pude abrir Configuración: {ex.Message}";
        }
    }

    private async void RefreshAll_Click(object sender, RoutedEventArgs e) => await RefreshAllAsync();

    private async void Ping_Click(object sender, RoutedEventArgs e)
    {
        PingText.Text = "Ping: midiendo…";
        var result = await NetworkService.PingAsync(_snapshot.Gateway);
        PingText.Text = $"Ping: {result}";
    }

    private void StartMonitor_Click(object sender, RoutedEventArgs e)
    {
        SignalTab.IsSelected = true;
        if (!_monitoring) ToggleMonitoring(true);
    }

    private void ToggleMonitor_Click(object sender, RoutedEventArgs e) => ToggleMonitoring(!_monitoring);

    private void ToggleMonitoring(bool enabled)
    {
        _monitoring = enabled;
        MonitorButton.Content = enabled ? "DETENER" : "INICIAR";
        UpdateCaptureTimer();
        if (enabled) _ = CaptureSignalAsync();
    }

    private async Task CaptureSignalAsync()
    {
        if (_captureBusy) return;
        _captureBusy = true;
        try
        {
            var current = await WifiService.GetCurrentAsync();
            _wifi = current;
            LiveSsidText.Text = current.Ssid;
            if (current.SignalPercent is null || current.ApproxRssi is null)
            {
                LiveSignalText.Text = "Sin señal Wi‑Fi medible";
                if (_labCalibrating || _labRunning)
                    LabStatusText.Text = "Sin señal Wi‑Fi medible. Verificá la conexión.";
                return;
            }

            var dbm = current.ApproxRssi.Value;
            if (_monitoring)
            {
                LiveSignalText.Text = $"~{dbm} dBm · {current.SignalPercent}%";
                _signalHistory.Add(dbm);
                if (_signalHistory.Count > 120) _signalHistory.RemoveAt(0);
                UpdateChart();
            }

            if (_labCalibrating)
            {
                _calibrationSamples.Add(dbm);
                LabCurrentText.Text = $"~{dbm} dBm";
                LabStatusText.Text = $"CALIBRANDO · {_calibrationSamples.Count}/{CalibrationSamples} · mantené el ambiente de referencia estable";
                if (_calibrationSamples.Count >= CalibrationSamples)
                {
                    var baseline = _labEngine.Calibrate(_calibrationSamples);
                    _labCalibrating = false;
                    if (baseline is not null)
                    {
                        LabBaselineText.Text = $"Línea base: ~{baseline.Mean:0.00} dBm · ruido σ {baseline.StdDev:0.00} · {baseline.Samples} muestras";
                        LabStatusText.Text = "CALIBRACIÓN LISTA · ya podés iniciar la sesión";
                        LabDetailsText.Text = "La línea base queda fija hasta que vuelvas a calibrar.";
                    }
                    UpdateCaptureTimer();
                }
            }
            else if (_labRunning)
            {
                var reading = _labEngine.Evaluate(dbm);
                if (reading is not null)
                {
                    AppendLabReading(reading);
                    UpdateLabReadingUi(reading);
                }
            }

            if (_wifiSensingCalibrating)
            {
                _wifiSensingCalibration.Add(dbm);
                _wifiSensingSamples++;
                CsiHeartText.Text = $"{dbm} dBm";
                CsiRespText.Text = $"{_wifiSensingCalibration.Average():0.0} dBm";
                CsiConfidenceText.Text = "CAL";
                CsiMotionText.Text = $"CALIBRANDO {_wifiSensingCalibration.Count}/{WifiSensingCalibrationSamples}";
                CsiPacketsText.Text = _wifiSensingSamples.ToString();
                CsiPresenceText.Text = "—";
                CsiPersonsText.Text = "—";
                CsiStatusText.Text = $"CALIBRANDO AMBIENTE · {_wifiSensingCalibration.Count}/{WifiSensingCalibrationSamples} · dejá router y equipo quietos";
                CsiScene.UpdateWifiSensing(null, false, false, BuildWifiSenseLabel());

                if (_wifiSensingCalibration.Count >= WifiSensingCalibrationSamples)
                {
                    var baseline = _wifiSensingEngine.Calibrate(_wifiSensingCalibration);
                    _wifiSensingCalibrating = false;
                    CsiRunButton.Content = "RECALIBRAR";
                    if (baseline is not null)
                    {
                        CsiRespText.Text = $"{baseline.Mean:0.0} dBm";
                        CsiConfidenceText.Text = "LISTO";
                        CsiMotionText.Text = "AMBIENTE ESTABLE";
                        CsiStatusText.Text = $"CALIBRACIÓN LISTA · base {baseline.Mean:0.0} dBm · ruido σ {baseline.StdDev:0.00}";
                        CsiScene.UpdateWifiSensing(null, true, false, BuildWifiSenseLabel());
                    }
                    UpdateCaptureTimer();
                }
            }
            else if (_wifiSensingRunning)
            {
                var sense = _wifiSensingEngine.Evaluate(dbm);
                if (sense is not null)
                {
                    _lastWifiSensingReading = sense;
                    _wifiSensingSamples++;
                    UpdateWifiSensingUi(sense);
                }
            }
        }
        finally
        {
            _captureBusy = false;
        }
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _signalHistory.Clear();
        UpdateChart();
    }

    private void SignalCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateChart();

    private void UpdateChart()
    {
        SignalPolyline.Points.Clear();
        var w = Math.Max(10, SignalCanvas.ActualWidth - 16);
        var h = Math.Max(10, SignalCanvas.ActualHeight - 16);
        if (_signalHistory.Count > 1)
        {
            for (int i = 0; i < _signalHistory.Count; i++)
            {
                var x = 8 + (i * w / Math.Max(1, _signalHistory.Count - 1));
                var normalized = Math.Clamp((_signalHistory[i] + 100) / 70.0, 0, 1);
                var y = 8 + (1 - normalized) * h;
                SignalPolyline.Points.Add(new Point(x, y));
            }
        }
        HistoryCountText.Text = $"Muestras: {_signalHistory.Count}";
        HistoryAverageText.Text = _signalHistory.Count == 0 ? "Promedio: —" : $"Promedio: {_signalHistory.Average():0.0} dBm";
    }

    private async void ScanWifi_Click(object sender, RoutedEventArgs e)
    {
        ScanStatusText.Text = "Escaneando…";
        var networks = await WifiService.ScanAsync();
        NetworksGrid.ItemsSource = networks;
        ChannelsList.ItemsSource = WifiService.ChannelSummary(networks)
            .Select(c => $"Canal {c.Channel}: {c.Count} AP · mejor señal {c.Strongest}%")
            .ToList();
        ScanStatusText.Text = networks.Count == 0
            ? "No aparecieron redes. Verificá que Windows tenga Wi‑Fi activo."
            : $"{networks.Count} puntos de acceso detectados · {DateTime.Now:HH:mm:ss}";
    }

    private void LabCalibrate_Click(object sender, RoutedEventArgs e)
    {
        LabTab.IsSelected = true;
        _labRunning = false;
        _labCalibrating = true;
        _labEngine.ClearCalibration();
        _calibrationSamples.Clear();
        _labSession.Clear();
        RefreshLabGrid();
        LabRunButton.Content = "INICIAR SESIÓN";
        LabStatusText.Text = $"CALIBRANDO · 0/{CalibrationSamples}";
        LabBaselineText.Text = "Línea base: midiendo…";
        LabCurrentText.Text = "—";
        LabDetailsText.Text = $"No cambies el ambiente durante las próximas {CalibrationSamples} muestras.";
        UpdateCaptureTimer();
        _ = CaptureSignalAsync();
    }

    private void LabRun_Click(object sender, RoutedEventArgs e)
    {
        if (_labEngine.Baseline is null)
        {
            LabStatusText.Text = "Primero calibrá el ambiente.";
            return;
        }

        _labCalibrating = false;
        _labRunning = !_labRunning;
        LabRunButton.Content = _labRunning ? "DETENER SESIÓN" : "INICIAR SESIÓN";
        LabStatusText.Text = _labRunning ? "SESIÓN ACTIVA · registrando una muestra por segundo" : "SESIÓN DETENIDA";
        if (_labRunning) _labEngine.ResetTracking();
        UpdateCaptureTimer();
        if (_labRunning) _ = CaptureSignalAsync();
    }

    private void LabSensitivity_Click(object sender, RoutedEventArgs e)
    {
        _labEngine.Sensitivity = _labEngine.Sensitivity switch
        {
            LabSensitivity.High => LabSensitivity.Normal,
            LabSensitivity.Normal => LabSensitivity.Low,
            _ => LabSensitivity.High
        };
        LabSensitivityButton.Content = $"SENSIBILIDAD: {_labEngine.SensitivityLabel}";
        LabDetailsText.Text = $"Sensibilidad {_labEngine.SensitivityLabel.ToLowerInvariant()}: modifica el umbral relativo de variación.";
    }

    private async void MarkerEmpty_Click(object sender, RoutedEventArgs e) => await AddManualMarkerAsync("HABITACIÓN VACÍA");
    private async void MarkerEntry_Click(object sender, RoutedEventArgs e) => await AddManualMarkerAsync("INGRESO");
    private async void MarkerMove_Click(object sender, RoutedEventArgs e) => await AddManualMarkerAsync("MOVIMIENTO");
    private async void MarkerExit_Click(object sender, RoutedEventArgs e) => await AddManualMarkerAsync("SALIDA");

    private async Task AddManualMarkerAsync(string marker)
    {
        if (_labEngine.Baseline is null)
        {
            LabStatusText.Text = "Calibrá primero antes de agregar marcadores.";
            return;
        }
        var current = await WifiService.GetCurrentAsync();
        if (current.ApproxRssi is null)
        {
            LabStatusText.Text = "No pude leer la señal para registrar el marcador.";
            return;
        }
        var reading = _labEngine.Evaluate(current.ApproxRssi.Value, marker);
        if (reading is null) return;
        AppendLabReading(reading);
        UpdateLabReadingUi(reading);
        LabStatusText.Text = $"MARCADOR REGISTRADO · {marker}";
    }

    private void AppendLabReading(RssiLabReading reading)
    {
        _labSession.Add(reading);
        if (_labSession.Count > MaxLabRows) _labSession.RemoveAt(0);
        RefreshLabGrid();
    }

    private void RefreshLabGrid()
    {
        LabRecentGrid.ItemsSource = _labSession.TakeLast(120).Reverse().ToList();
        LabCountText.Text = $"Registros: {_labSession.Count}";
    }

    private void UpdateLabReadingUi(RssiLabReading reading)
    {
        LabCurrentText.Text = $"~{reading.Rssi} dBm · {reading.State}";
        LabDetailsText.Text = $"Δ línea base {reading.Delta:0.00} dB · puntaje {reading.Score:0.00} · sensibilidad {_labEngine.SensitivityLabel}";
        if (_labRunning) LabStatusText.Text = $"SESIÓN ACTIVA · {_labSession.Count} registros";
    }

    private void ClearLab_Click(object sender, RoutedEventArgs e)
    {
        _labSession.Clear();
        RefreshLabGrid();
        LabStatusText.Text = "Sesión borrada. La calibración se conserva.";
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_labSession.Count == 0)
        {
            LabStatusText.Text = "No hay registros para exportar.";
            return;
        }
        var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"rssi_lab_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (dialog.ShowDialog(this) != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("fecha_hora;rssi_dbm;baseline_dbm;delta_db;score;estado;marcador");
        foreach (var row in _labSession)
        {
            sb.Append(row.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(';')
              .Append(row.Rssi).Append(';')
              .Append(row.Baseline.ToString("0.000", CultureInfo.InvariantCulture)).Append(';')
              .Append(row.Delta.ToString("0.000", CultureInfo.InvariantCulture)).Append(';')
              .Append(row.Score.ToString("0.000", CultureInfo.InvariantCulture)).Append(';')
              .Append(row.State.Replace(';', ',')).Append(';')
              .AppendLine(row.Marker?.Replace(';', ',') ?? "");
        }
        File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
        LabStatusText.Text = $"CSV exportado · {Path.GetFileName(dialog.FileName)}";
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (_labSession.Count == 0)
        {
            LabStatusText.Text = "No hay registros para exportar.";
            return;
        }
        var dialog = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = $"rssi_lab_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };
        if (dialog.ShowDialog(this) != true) return;

        var payload = new
        {
            revision = "R9",
            sensitivity = _labEngine.SensitivityLabel,
            baseline = _labEngine.Baseline,
            readings = _labSession.Select(row => new
            {
                timestamp = row.Timestamp,
                rssi_dbm = row.Rssi,
                baseline_dbm = row.Baseline,
                delta_db = row.Delta,
                score = row.Score,
                state = row.State,
                marker = row.Marker
            })
        };
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(true));
        LabStatusText.Text = $"JSON exportado · {Path.GetFileName(dialog.FileName)}";
    }



    private void CsiDemo_Click(object sender, RoutedEventArgs e)
    {
        _csiDemoMode = true;
        _wifiSensingRunning = false;
        _wifiSensingCalibrating = false;
        CsiScene.SetWifiDirectMode(true);
        ApplyCsiModeUi();
        UpdateCaptureTimer();
    }

    private void CsiReal_Click(object sender, RoutedEventArgs e)
    {
        _csiDemoMode = false;
        CsiScene.SetWifiDirectMode(true);
        ApplyCsiModeUi();

        RepositoryGrid.ItemsSource = _repositoryWatches;
        RepositoryStatusText.Text = $"{_repositoryWatches.Count} repositorios · comprobación automática independiente";
        _repositoryTimer.Start();
        _ = CheckDueRepositoriesAsync();
    }

    private void CsiTracking_Click(object sender, RoutedEventArgs e)
    {
        if (_csiDemoMode)
        {
            _csiDemoMode = false;
            CsiScene.SetWifiDirectMode(true);
        }

        if (_wifiSensingEngine.Baseline is null)
        {
            CsiStatusText.Text = "Primero calibrá 25 segundos con el ambiente quieto.";
            ApplyCsiModeUi();
            return;
        }

        _wifiSensingCalibrating = false;
        _wifiSensingRunning = !_wifiSensingRunning;
        CsiTrackingButton.Content = _wifiSensingRunning ? "DETENER" : "INICIAR";
        CsiTrackingButton.Opacity = 1.0;
        CsiStatusText.Text = _wifiSensingRunning
            ? "WI‑FI SENSING ACTIVO · analizando variaciones RSSI una vez por segundo"
            : "WI‑FI SENSING DETENIDO · calibración conservada";
        CsiScene.UpdateWifiSensing(_lastWifiSensingReading, true, _wifiSensingRunning, BuildWifiSenseLabel());
        UpdateCaptureTimer();
        if (_wifiSensingRunning) _ = CaptureSignalAsync();
    }

    private void CsiHeat_Click(object sender, RoutedEventArgs e)
    {
        _csiHeatmap = !_csiHeatmap;
        CsiScene.ShowHeatmap = _csiHeatmap;
        CsiHeatButton.Opacity = _csiHeatmap ? 1.0 : 0.45;
        CsiScene.InvalidateVisual();
    }

    private void ApplyCsiModeUi()
    {
        CsiScene.SetDemoMode(_csiDemoMode);
        CsiScene.SetWifiDirectMode(true);
        CsiModeBadge.Text = _csiDemoMode ? "DEMO" : "WI‑FI";
        CsiModeBadge.Foreground = _csiDemoMode
            ? new SolidColorBrush(Color.FromRgb(245, 181, 60))
            : new SolidColorBrush(Color.FromRgb(83, 255, 208));
        CsiDemoButton.Opacity = _csiDemoMode ? 1.0 : 0.50;
        CsiRealButton.Opacity = _csiDemoMode ? 0.50 : 1.0;

        if (_csiDemoMode)
        {
            CsiHeartText.Text = "-55 dBm";
            CsiRespText.Text = "-56.2 dBm";
            CsiConfidenceText.Text = "82 %";
            CsiMotionText.Text = "MOVIMIENTO PROBABLE";
            CsiNodesText.Text = "1";
            CsiPacketsText.Text = "128";
            CsiPresenceText.Text = "3.8 dB";
            CsiPersonsText.Text = "3.7";
            CsiStatusText.Text = "DEMO VISUAL · datos simulados para mostrar la interfaz";
            CsiTargetText.Text = "DEMO · en WI‑FI se usa el router/AP al que está conectado este equipo";
            CsiScene.UpdateWifiSensing(null, false, false, "DEMO");
            return;
        }

        CsiRunButton.Content = _wifiSensingEngine.Baseline is null ? "CALIBRAR 25s" : "RECALIBRAR";
        CsiTrackingButton.Content = _wifiSensingRunning ? "DETENER" : "INICIAR";
        CsiNodesText.Text = _wifi.Connected ? "1" : "0";
        CsiPacketsText.Text = _wifiSensingSamples.ToString();
        CsiTargetText.Text = BuildWifiSenseLabel();

        var baseline = _wifiSensingEngine.Baseline;
        if (baseline is null)
        {
            CsiHeartText.Text = _wifi.ApproxRssi is null ? "— dBm" : $"{_wifi.ApproxRssi} dBm";
            CsiRespText.Text = "— dBm";
            CsiConfidenceText.Text = "— %";
            CsiMotionText.Text = "SIN CALIBRAR";
            CsiPresenceText.Text = "—";
            CsiPersonsText.Text = "—";
            CsiStatusText.Text = "WI‑FI DIRECTO · calibrá 25 segundos con el ambiente quieto";
            CsiScene.UpdateWifiSensing(null, false, false, BuildWifiSenseLabel());
        }
        else
        {
            CsiRespText.Text = $"{baseline.Mean:0.0} dBm";
            if (_lastWifiSensingReading is not null) UpdateWifiSensingUi(_lastWifiSensingReading);
            else
            {
                CsiConfidenceText.Text = "LISTO";
                CsiMotionText.Text = "CALIBRADO";
                CsiStatusText.Text = "CALIBRACIÓN LISTA · presioná INICIAR";
                CsiScene.UpdateWifiSensing(null, true, false, BuildWifiSenseLabel());
            }
        }
    }

    private void CsiRun_Click(object sender, RoutedEventArgs e)
    {
        _csiDemoMode = false;
        _wifiSensingRunning = false;
        _wifiSensingCalibrating = true;
        _wifiSensingCalibration.Clear();
        _wifiSensingEngine.Reset();
        _lastWifiSensingReading = null;
        _wifiSensingSamples = 0;
        CsiScene.SetDemoMode(false);
        CsiScene.SetWifiDirectMode(true);
        CsiRunButton.Content = "CALIBRANDO…";
        CsiTrackingButton.Content = "INICIAR";
        CsiModeBadge.Text = "WI‑FI";
        CsiStatusText.Text = $"CALIBRANDO AMBIENTE · 0/{WifiSensingCalibrationSamples} · no muevas router ni equipo";
        CsiHeartText.Text = "— dBm";
        CsiRespText.Text = "— dBm";
        CsiConfidenceText.Text = "CAL";
        CsiMotionText.Text = "CALIBRANDO";
        CsiNodesText.Text = _wifi.Connected ? "1" : "0";
        CsiPacketsText.Text = "0";
        CsiPresenceText.Text = "—";
        CsiPersonsText.Text = "—";
        CsiScene.UpdateWifiSensing(null, false, false, BuildWifiSenseLabel());
        UpdateCaptureTimer();
        _ = CaptureSignalAsync();
    }

    private void CsiClear_Click(object sender, RoutedEventArgs e)
    {
        _wifiSensingRunning = false;
        _wifiSensingCalibrating = false;
        _wifiSensingCalibration.Clear();
        _wifiSensingEngine.Reset();
        _lastWifiSensingReading = null;
        _wifiSensingSamples = 0;
        _csi.Clear();
        ApplyCsiModeUi();
        UpdateCaptureTimer();
    }

    private string BuildWifiSenseLabel()
    {
        var ssid = _wifi.Ssid == "—" ? "Wi‑Fi actual" : _wifi.Ssid;
        var ap = _wifi.Bssid == "—" ? "AP —" : $"AP {_wifi.Bssid}";
        return $"{ssid} · {ap} · gateway {_snapshot.Gateway}";
    }

    private void UpdateWifiSensingUi(WifiSensingReading reading)
    {
        var baseline = _wifiSensingEngine.Baseline;
        CsiHeartText.Text = $"{reading.Rssi} dBm";
        CsiRespText.Text = $"{reading.Baseline:0.0} dBm";
        CsiConfidenceText.Text = $"{reading.Confidence * 100:0} %";
        CsiMotionText.Text = reading.State;
        CsiNodesText.Text = _wifi.Connected ? "1" : "0";
        CsiPacketsText.Text = _wifiSensingSamples.ToString();
        CsiPresenceText.Text = $"{reading.Delta:0.0} dB";
        CsiPersonsText.Text = $"{reading.Score:0.00}";
        CsiStatusText.Text = $"WI‑FI SENSING · {reading.State} · RSSI {reading.Rssi} dBm · Δ {reading.Delta:0.0} dB";
        CsiTargetText.Text = BuildWifiSenseLabel();
        CsiScene.UpdateWifiSensing(reading, baseline is not null, _wifiSensingRunning, BuildWifiSenseLabel());
    }

    private void UpdateCsiUi(IReadOnlyList<CsiNodeSnapshot> nodes)
    {
        _lastCsiNodes = nodes;
        CsiNodesGrid.ItemsSource = nodes;
        CsiScene.UpdateNodes(nodes);

        // CSI queda como hardware avanzado opcional. No pisa la interfaz Wi‑Fi directa.
        if (_csiDemoMode || CsiScene.WifiDirectMode) return;

        CsiPacketsText.Text = _csi.TotalPackets.ToString();
        var live = nodes.Where(n => DateTimeOffset.Now - n.LastSeen < TimeSpan.FromSeconds(5)).ToList();
        CsiNodesText.Text = live.Count.ToString();
    }

    private async void NetDns_Click(object sender, RoutedEventArgs e)
    {
        await RunNetworkToolAsync("Resolviendo DNS…", () => NetworkToolkitService.DnsLookupAsync(NetTargetBox.Text));
    }

    private async void NetPing_Click(object sender, RoutedEventArgs e)
    {
        NetResultBox.Text = "Midiendo latencia y pérdida…";
        try
        {
            var result = await NetworkToolkitService.PingWindowAsync(NetTargetBox.Text, 10, 1200);
            _lastNetworkResult = NetworkToolkitService.FormatPing(result);
            NetResultBox.Text = _lastNetworkResult;
        }
        catch (Exception ex)
        {
            _lastNetworkResult = $"Ping: {ex.Message}";
            NetResultBox.Text = _lastNetworkResult;
        }
    }

    private async void NetTrace_Click(object sender, RoutedEventArgs e)
    {
        await RunNetworkToolAsync("Ejecutando traceroute…", () => NetworkToolkitService.TraceRouteAsync(NetTargetBox.Text));
    }

    private async Task RunNetworkToolAsync(string status, Func<Task<string>> action)
    {
        NetResultBox.Text = status;
        try
        {
            _lastNetworkResult = await action();
        }
        catch (Exception ex)
        {
            _lastNetworkResult = ex.Message;
        }
        NetResultBox.Text = _lastNetworkResult;
    }

    private void CalculateCidr_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(CidrPrefixBox.Text.Trim(), out var prefix)) throw new ArgumentException("Prefijo inválido.");
            var cidr = NetworkToolkitService.CalculateCidr(CidrIpBox.Text, prefix);
            CidrResultText.Text = NetworkToolkitService.FormatCidr(cidr);
        }
        catch (Exception ex)
        {
            CidrResultText.Text = $"CIDR: {ex.Message}";
        }
    }

    private async void ScanLan_Click(object sender, RoutedEventArgs e)
    {
        LanScanButton.IsEnabled = false;
        LanStatusText.Text = "Iniciando exploración del segmento local…";
        try
        {
            var progress = new Progress<string>(text => LanStatusText.Text = text);
            _lanDevices = await NetworkToolkitService.DiscoverLocal24Async(progress);
            LanGrid.ItemsSource = _lanDevices;
            if (_lanDevices.Count == 0)
                LanStatusText.Text = "No hubo respuestas. La red puede bloquear ICMP o no haber IPv4 local disponible.";
        }
        catch (Exception ex)
        {
            LanStatusText.Text = $"LAN: {ex.Message}";
        }
        finally
        {
            LanScanButton.IsEnabled = true;
        }
    }

    private void InspectMac_Click(object sender, RoutedEventArgs e)
    {
        var result = NetworkToolkitService.InspectMac(MacInputBox.Text);
        MacResultText.Text = result.Normalized == "—"
            ? result.Status
            : $"MAC: {result.Normalized} · OUI: {result.Oui}\n{result.Status}";
    }

    private void ExportNetwork_Click(object sender, RoutedEventArgs e)
    {
        var body = BuildNetworkDiagnosticText();
        if (string.IsNullOrWhiteSpace(body))
        {
            NetResultBox.Text = "Todavía no hay diagnóstico para guardar.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Texto (*.txt)|*.txt",
            FileName = $"diagnostico_red_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };
        if (dialog.ShowDialog(this) != true) return;
        File.WriteAllText(dialog.FileName, body, new UTF8Encoding(true));
        NetResultBox.Text = $"{_lastNetworkResult}\n\nGuardado: {Path.GetFileName(dialog.FileName)}".Trim();
    }

    private string BuildNetworkDiagnosticText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("KIT HERRAMIENTAS · DIAGNÓSTICO DE RED · R9");
        sb.AppendLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Equipo: {_snapshot.HostName}");
        sb.AppendLine($"Adaptador: {_snapshot.Adapter}");
        sb.AppendLine($"IP local: {_snapshot.LocalIp}");
        sb.AppendLine($"Gateway: {_snapshot.Gateway}");
        sb.AppendLine($"Wi-Fi: {_wifi.Ssid} · {_wifi.Bssid}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(_lastNetworkResult))
        {
            sb.AppendLine("ÚLTIMA PRUEBA");
            sb.AppendLine(_lastNetworkResult);
            sb.AppendLine();
        }
        if (_lanDevices.Count > 0)
        {
            sb.AppendLine("EQUIPOS LAN");
            foreach (var d in _lanDevices)
                sb.AppendLine($"{d.Ip}\t{d.HostName}\t{d.Mac}\t{d.Oui}\t{d.Note}");
        }
        return sb.ToString().TrimEnd();
    }

    private void AddRepository_Click(object sender, RoutedEventArgs e)
    {
        var normalized = RepositoryUpdateService.NormalizeRepository(RepositoryInputBox.Text);
        if (normalized is null)
        {
            RepositoryStatusText.Text = "Repositorio inválido. Pegá una URL de GitHub o owner/repo.";
            return;
        }
        if (_repositoryWatches.Any(r => r.Repository.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            RepositoryStatusText.Text = $"{normalized} ya está en la lista.";
            return;
        }
        var hours = int.TryParse(RepositoryHoursBox.Text, out var parsed) ? Math.Clamp(parsed, 1, 168) : 6;
        _repositoryWatches.Add(new RepositoryWatch
        {
            Repository = normalized,
            IntervalHours = hours,
            Enabled = true,
            AutoDownload = true,
            Status = "Pendiente"
        });
        SaveRepositorySettings();
        RepositoryInputBox.Clear();
        RepositoryStatusText.Text = $"Agregado {normalized} · cada {hours} h · descarga automática activa.";
    }

    private void RemoveRepository_Click(object sender, RoutedEventArgs e)
    {
        if (RepositoryGrid.SelectedItem is not RepositoryWatch watch)
        {
            RepositoryStatusText.Text = "Seleccioná un repositorio para quitarlo.";
            return;
        }
        _repositoryWatches.Remove(watch);
        SaveRepositorySettings();
        RepositoryStatusText.Text = $"Quitado {watch.Repository}.";
    }

    private void SaveRepositories_Click(object sender, RoutedEventArgs e)
    {
        foreach (var watch in _repositoryWatches)
            watch.IntervalHours = Math.Clamp(watch.IntervalHours, 1, 168);
        SaveRepositorySettings();
        RepositoryGrid.Items.Refresh();
        RepositoryStatusText.Text = "Configuración guardada. Cada repositorio conserva su propio intervalo.";
    }

    private async void CheckSelectedRepository_Click(object sender, RoutedEventArgs e)
    {
        if (RepositoryGrid.SelectedItem is not RepositoryWatch watch)
        {
            RepositoryStatusText.Text = "Seleccioná un repositorio.";
            return;
        }
        await CheckRepositoryAsync(watch, force: true);
    }

    private async void CheckAllRepositories_Click(object sender, RoutedEventArgs e)
    {
        await CheckRepositoriesAsync(_repositoryWatches.Where(r => r.Enabled).ToList(), force: true);
    }

    private void OpenRepositoryDownloads_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(RepositoryUpdateService.DownloadsDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", RepositoryUpdateService.DownloadsDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            RepositoryStatusText.Text = $"No pude abrir la carpeta: {ex.Message}";
        }
    }

    private async Task CheckDueRepositoriesAsync()
    {
        var due = _repositoryWatches.Where(r => RepositoryUpdateService.IsDue(r, DateTimeOffset.Now)).ToList();
        if (due.Count == 0) return;
        await CheckRepositoriesAsync(due, force: false);
    }

    private async Task CheckRepositoriesAsync(IReadOnlyList<RepositoryWatch> repositories, bool force)
    {
        if (_repositoryCheckBusy) return;
        _repositoryCheckBusy = true;
        try
        {
            foreach (var watch in repositories)
            {
                if (!watch.Enabled) continue;
                if (!force && !RepositoryUpdateService.IsDue(watch, DateTimeOffset.Now)) continue;
                await CheckRepositoryAsync(watch, force);
            }
        }
        finally
        {
            _repositoryCheckBusy = false;
            SaveRepositorySettings();
            RepositoryGrid.Items.Refresh();
        }
    }

    private async Task CheckRepositoryAsync(RepositoryWatch watch, bool force)
    {
        watch.Status = "Buscando actualización…";
        RepositoryStatusText.Text = $"Revisando {watch.Repository}…";
        RepositoryGrid.Items.Refresh();
        try
        {
            var latest = await RepositoryUpdateService.GetLatestAsync(watch.Repository, android: false);
            watch.LatestId = latest.Id;
            watch.LatestName = latest.Name;
            watch.LastChecked = DateTimeOffset.Now;

            var changed = !string.Equals(watch.LastDownloadedId, latest.Id, StringComparison.OrdinalIgnoreCase);
            if (!changed)
            {
                watch.Status = "AL DÍA";
                RepositoryStatusText.Text = $"{watch.Repository}: al día ({latest.Name}).";
            }
            else if (!watch.AutoDownload)
            {
                watch.Status = "ACTUALIZACIÓN DISPONIBLE";
                RepositoryStatusText.Text = $"{watch.Repository}: actualización disponible · {latest.Name}.";
            }
            else
            {
                watch.Status = "Descargando…";
                RepositoryGrid.Items.Refresh();
                var path = await RepositoryUpdateService.DownloadAsync(watch.Repository, latest);
                watch.LastDownloadedId = latest.Id;
                watch.LastDownloadPath = path;
                watch.Status = $"DESCARGADA · {Path.GetFileName(path)}";
                RepositoryStatusText.Text = $"{watch.Repository}: descargada {latest.Name}. No se ejecuta ni instala automáticamente.";
            }
        }
        catch (Exception ex)
        {
            watch.LastChecked = DateTimeOffset.Now;
            watch.Status = $"ERROR · {ex.Message}";
            RepositoryStatusText.Text = $"{watch.Repository}: {ex.Message}";
        }
        finally
        {
            SaveRepositorySettings();
            RepositoryGrid.Items.Refresh();
        }
    }

    private void SaveRepositorySettings()
    {
        RepositoryUpdateService.Save(_repositoryWatches);
    }

    private void UpdateCaptureTimer()
    {
        if (_monitoring || _labCalibrating || _labRunning || _wifiSensingCalibrating || _wifiSensingRunning)
        {
            if (!_signalTimer.IsEnabled) _signalTimer.Start();
        }
        else if (_signalTimer.IsEnabled)
        {
            _signalTimer.Stop();
        }
    }

    private void RefreshDiscoveryGrid()
    {
        DiscoveryGrid.ItemsSource = _discoveredTools
            .OrderByDescending(x => x.IsNew)
            .ThenBy(x => x.Category)
            .ThenBy(x => x.Repository)
            .ToList();
        DiscoveryGrid.Items.Refresh();
        CatalogSourceGrid.Items.Refresh();
    }

    private void AddCatalogSource_Click(object sender, RoutedEventArgs e)
    {
        var normalized = RepositoryUpdateService.NormalizeRepository(CatalogSourceInputBox.Text);
        if (normalized is null)
        {
            CatalogStatusText.Text = "Fuente inválida. Pegá owner/repo o una URL de GitHub.";
            return;
        }
        if (_catalogSources.Any(x => x.Repository.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            CatalogStatusText.Text = $"{normalized} ya está agregado.";
            return;
        }
        var hours = int.TryParse(CatalogHoursBox.Text, out var parsed) ? Math.Clamp(parsed, 1, 168) : 12;
        _catalogSources.Add(new CatalogSourceWatch { Repository = normalized, IntervalHours = hours, Enabled = true });
        CatalogDiscoveryService.SaveSources(_catalogSources);
        CatalogSourceGrid.Items.Refresh();
        CatalogSourceInputBox.Clear();
        CatalogStatusText.Text = $"Fuente agregada: {normalized} · cada {hours} h.";
    }

    private void RemoveCatalogSource_Click(object sender, RoutedEventArgs e)
    {
        if (CatalogSourceGrid.SelectedItem is not CatalogSourceWatch source)
        {
            CatalogStatusText.Text = "Seleccioná una fuente.";
            return;
        }
        _catalogSources.Remove(source);
        CatalogDiscoveryService.SaveSources(_catalogSources);
        CatalogSourceGrid.Items.Refresh();
        CatalogStatusText.Text = $"Fuente quitada: {source.Repository}. Los descubrimientos históricos se conservan.";
    }

    private async void CheckCatalogSources_Click(object sender, RoutedEventArgs e) => await CheckCatalogSourcesAsync(_catalogSources.Where(x => x.Enabled).ToList(), true);

    private async Task CheckDueCatalogSourcesAsync()
    {
        var due = _catalogSources.Where(x => CatalogDiscoveryService.IsDue(x, DateTimeOffset.Now)).ToList();
        if (due.Count > 0) await CheckCatalogSourcesAsync(due, false);
    }

    private async Task CheckCatalogSourcesAsync(IReadOnlyList<CatalogSourceWatch> sources, bool force)
    {
        if (_catalogCheckBusy) return;
        _catalogCheckBusy = true;
        var totalNew = 0;
        try
        {
            foreach (var source in sources)
            {
                if (!source.Enabled) continue;
                if (!force && !CatalogDiscoveryService.IsDue(source, DateTimeOffset.Now)) continue;
                CatalogStatusText.Text = $"Revisando catálogo {source.Repository}…";
                source.Status = "REVISANDO…";
                CatalogSourceGrid.Items.Refresh();
                try
                {
                    var snapshot = await CatalogDiscoveryService.FetchAsync(source.Repository);
                    totalNew += CatalogDiscoveryService.MergeSnapshot(_discoveredTools, source, snapshot);
                }
                catch (Exception ex)
                {
                    source.LastChecked = DateTimeOffset.Now;
                    source.Status = $"ERROR · {ex.Message}";
                }
                CatalogDiscoveryService.SaveSources(_catalogSources);
                CatalogDiscoveryService.SaveTools(_discoveredTools);
                RefreshDiscoveryGrid();
            }
            CatalogStatusText.Text = totalNew > 0
                ? $"Descubrimiento terminado · {totalNew} recurso(s) NUEVO(S)."
                : $"Descubrimiento terminado · sin novedades · {_discoveredTools.Count} recursos en catálogo.";
        }
        finally { _catalogCheckBusy = false; }
    }

    private void MarkDiscoveriesSeen_Click(object sender, RoutedEventArgs e)
    {
        CatalogDiscoveryService.MarkAllSeen(_discoveredTools);
        foreach (var source in _catalogSources) source.NewCount = 0;
        CatalogDiscoveryService.SaveSources(_catalogSources);
        RefreshDiscoveryGrid();
        CatalogStatusText.Text = "Novedades marcadas como vistas.";
    }

    private void OpenDiscovery_Click(object sender, RoutedEventArgs e)
    {
        if (DiscoveryGrid.SelectedItem is not DiscoveredTool tool)
        {
            CatalogStatusText.Text = "Seleccioná un recurso descubierto.";
            return;
        }
        try { Process.Start(new ProcessStartInfo(tool.Url) { UseShellExecute = true }); }
        catch (Exception ex) { CatalogStatusText.Text = $"No pude abrir GitHub: {ex.Message}"; }
    }

    private void AddDiscoveryToUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (DiscoveryGrid.SelectedItem is not DiscoveredTool tool)
        {
            CatalogStatusText.Text = "Seleccioná un recurso descubierto.";
            return;
        }
        if (_repositoryWatches.Any(x => x.Repository.Equals(tool.Repository, StringComparison.OrdinalIgnoreCase)))
        {
            CatalogStatusText.Text = $"{tool.Repository} ya está en Actualizaciones.";
            return;
        }
        _repositoryWatches.Add(new RepositoryWatch
        {
            Repository = tool.Repository,
            IntervalHours = 12,
            Enabled = true,
            AutoDownload = false,
            Status = "AGREGADO DESDE DESCUBRIR · SOLO AVISA"
        });
        SaveRepositorySettings();
        RepositoryGrid.Items.Refresh();
        CatalogStatusText.Text = $"{tool.Repository} agregado a Actualizaciones en modo SOLO AVISA.";
    }

}
