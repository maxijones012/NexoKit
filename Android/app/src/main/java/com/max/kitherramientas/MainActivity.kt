package com.max.kitherramientas

import android.Manifest
import android.app.Activity
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Color
import android.location.LocationManager
import android.net.ConnectivityManager
import android.net.LinkProperties
import android.net.NetworkCapabilities
import android.net.wifi.ScanResult
import android.net.wifi.WifiInfo
import android.net.wifi.WifiManager
import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.provider.Settings
import android.widget.Button
import android.widget.EditText
import android.widget.TextView
import org.json.JSONArray
import org.json.JSONObject
import java.net.Inet4Address
import java.net.InetAddress
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import kotlin.concurrent.thread

class MainActivity : Activity() {
    private lateinit var networkStatus: TextView
    private lateinit var ssid: TextView
    private lateinit var bssid: TextView
    private lateinit var ip: TextView
    private lateinit var gateway: TextView
    private lateinit var dns: TextView
    private lateinit var rssi: TextView
    private lateinit var frequency: TextView
    private lateinit var link: TextView
    private lateinit var ping: TextView
    private lateinit var permission: TextView
    private lateinit var liveValue: TextView
    private lateinit var liveStats: TextView
    private lateinit var graph: SignalGraphView
    private lateinit var btnMonitor: Button
    private lateinit var scanStatus: TextView
    private lateinit var networks: TextView
    private lateinit var channels: TextView

    private lateinit var labStatus: TextView
    private lateinit var labBaseline: TextView
    private lateinit var labCurrent: TextView
    private lateinit var labDetails: TextView
    private lateinit var labRecent: TextView
    private lateinit var labExportStatus: TextView
    private lateinit var btnLabRun: Button
    private lateinit var btnSensitivity: Button

    private lateinit var netTarget: EditText
    private lateinit var netResult: TextView
    private lateinit var cidrIp: EditText
    private lateinit var cidrPrefix: EditText
    private lateinit var cidrResult: TextView
    private lateinit var lanStatus: TextView
    private lateinit var lanDevices: TextView
    private lateinit var macInput: EditText
    private lateinit var macResult: TextView

    private lateinit var repoInput: EditText
    private lateinit var repoHours: EditText
    private lateinit var repoStatus: TextView
    private lateinit var repoList: TextView
    private lateinit var catalogSourceInput: EditText
    private lateinit var catalogHours: EditText
    private lateinit var catalogStatus: TextView
    private lateinit var catalogSources: TextView
    private lateinit var catalogTools: TextView
    private var currentLocalIpv4: String? = null
    private var lastNetworkResult: String = ""
    private var lastLanText: String = ""

    private lateinit var csiStatus: TextView
    private lateinit var csiTarget: TextView
    private lateinit var csiSummary: TextView
    private lateinit var csiNodes: TextView
    private lateinit var csiModeBadge: TextView
    private lateinit var csiHeart: TextView
    private lateinit var csiResp: TextView
    private lateinit var csiConfidence: TextView
    private lateinit var csiMotion: TextView
    private lateinit var btnCsiRun: Button
    private lateinit var btnCsiDemo: Button
    private lateinit var btnCsiReal: Button
    private lateinit var btnCsiTracking: Button
    private lateinit var btnCsiHeat: Button
    private lateinit var csiScene: CsiSceneView
    private lateinit var csiReceiver: CsiUdpReceiver
    private var csiDemoMode = false
    private var csiTracking = true
    private var csiHeatmap = true
    private var lastCsiNodes: List<CsiNodeSnapshot> = emptyList()

    private val wifiSensingEngine = WifiSensingEngine()
    private val wifiSensingCalibration = mutableListOf<Int>()
    private var wifiSensingCalibrating = false
    private var wifiSensingRunning = false
    private var lastWifiSensingReading: WifiSensingReading? = null
    private var wifiSensingSamples = 0

    private lateinit var wifiManager: WifiManager
    private val handler = Handler(Looper.getMainLooper())
    private var gatewayAddress: String? = null
    private var monitoring = false

    private val labEngine = RssiLabEngine()
    private val calibrationSamples = mutableListOf<Int>()
    private val labSession = mutableListOf<RssiLabReading>()
    private var calibrating = false
    private var labRunning = false
    private var pendingExportFormat = "csv"

    private val monitorRunnable = object : Runnable {
        override fun run() {
            captureLiveSignal()
            if (monitoring || wifiSensingCalibrating || wifiSensingRunning) handler.postDelayed(this, 1000)
        }
    }

    private val labRunnable = object : Runnable {
        override fun run() {
            val value = currentWifiInfo()?.rssi?.takeIf { it in -126..-1 }
            if (value == null) {
                labStatus.text = "Sin RSSI disponible. Verificá que el teléfono siga conectado por Wi‑Fi."
            } else if (calibrating) {
                calibrationSamples += value
                labStatus.text = "CALIBRANDO · ${calibrationSamples.size}/$CALIBRATION_SAMPLES · mantené el ambiente de referencia estable"
                labCurrent.text = "$value dBm"
                if (calibrationSamples.size >= CALIBRATION_SAMPLES) {
                    val base = labEngine.calibrate(calibrationSamples)
                    calibrating = false
                    if (base != null) {
                        labBaseline.text = "Línea base: ${fmt(base.mean)} dBm · ruido σ ${fmt(base.stdDev)} · ${base.samples} muestras"
                        labStatus.text = "CALIBRACIÓN LISTA · ya podés iniciar la sesión"
                        labDetails.text = "La línea base queda fija hasta que vuelvas a calibrar."
                    }
                }
            } else if (labRunning) {
                val reading = labEngine.evaluate(value)
                if (reading != null) {
                    appendLabReading(reading)
                    updateLabReadingUi(reading)
                }
            }

            if (calibrating || labRunning) handler.postDelayed(this, 1000)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        wifiManager = applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
        networkStatus = findViewById(R.id.txtNetworkStatus)
        ssid = findViewById(R.id.txtSsid)
        bssid = findViewById(R.id.txtBssid)
        ip = findViewById(R.id.txtIp)
        gateway = findViewById(R.id.txtGateway)
        dns = findViewById(R.id.txtDns)
        rssi = findViewById(R.id.txtRssi)
        frequency = findViewById(R.id.txtFrequency)
        link = findViewById(R.id.txtLink)
        ping = findViewById(R.id.txtPing)
        permission = findViewById(R.id.txtPermission)
        liveValue = findViewById(R.id.txtLiveValue)
        liveStats = findViewById(R.id.txtLiveStats)
        graph = findViewById(R.id.signalGraph)
        btnMonitor = findViewById(R.id.btnMonitor)
        scanStatus = findViewById(R.id.txtScanStatus)
        networks = findViewById(R.id.txtNetworks)
        channels = findViewById(R.id.txtChannels)

        labStatus = findViewById(R.id.txtLabStatus)
        labBaseline = findViewById(R.id.txtLabBaseline)
        labCurrent = findViewById(R.id.txtLabCurrent)
        labDetails = findViewById(R.id.txtLabDetails)
        labRecent = findViewById(R.id.txtLabRecent)
        labExportStatus = findViewById(R.id.txtLabExportStatus)
        btnLabRun = findViewById(R.id.btnLabRun)
        btnSensitivity = findViewById(R.id.btnSensitivity)

        netTarget = findViewById(R.id.txtNetTarget)
        netResult = findViewById(R.id.txtNetResult)
        cidrIp = findViewById(R.id.txtCidrIp)
        cidrPrefix = findViewById(R.id.txtCidrPrefix)
        cidrResult = findViewById(R.id.txtCidrResult)
        lanStatus = findViewById(R.id.txtLanStatus)
        lanDevices = findViewById(R.id.txtLanDevices)
        macInput = findViewById(R.id.txtMacInput)
        macResult = findViewById(R.id.txtMacResult)

        repoInput = findViewById(R.id.txtRepoInput)
        repoHours = findViewById(R.id.txtRepoHours)
        repoStatus = findViewById(R.id.txtRepoStatus)
        repoList = findViewById(R.id.txtRepoList)
        catalogSourceInput = findViewById(R.id.txtCatalogSource)
        catalogHours = findViewById(R.id.txtCatalogHours)
        catalogStatus = findViewById(R.id.txtCatalogStatus)
        catalogSources = findViewById(R.id.txtCatalogSources)
        catalogTools = findViewById(R.id.txtCatalogTools)

        csiStatus = findViewById(R.id.txtCsiStatus)
        csiTarget = findViewById(R.id.txtCsiTarget)
        csiSummary = findViewById(R.id.txtCsiSummary)
        csiNodes = findViewById(R.id.txtCsiNodes)
        csiModeBadge = findViewById(R.id.txtCsiModeBadge)
        csiHeart = findViewById(R.id.txtCsiHeart)
        csiResp = findViewById(R.id.txtCsiResp)
        csiConfidence = findViewById(R.id.txtCsiConfidence)
        csiMotion = findViewById(R.id.txtCsiMotion)
        btnCsiRun = findViewById(R.id.btnCsiRun)
        btnCsiDemo = findViewById(R.id.btnCsiDemo)
        btnCsiReal = findViewById(R.id.btnCsiReal)
        btnCsiTracking = findViewById(R.id.btnCsiTracking)
        btnCsiHeat = findViewById(R.id.btnCsiHeat)
        csiScene = findViewById(R.id.csiScene)
        csiReceiver = CsiUdpReceiver(
            onUpdate = { nodes, total, invalid -> runOnUiThread { updateCsiUi(nodes, total, invalid) } },
            onStatus = { text -> runOnUiThread { if (!csiDemoMode && !csiScene.wifiDirectMode) csiStatus.text = text } }
        )

        findViewById<Button>(R.id.btnRefresh).setOnClickListener { ensureBasePermissionAndRefresh() }
        findViewById<Button>(R.id.btnPing).setOnClickListener { pingGateway() }
        btnMonitor.setOnClickListener { toggleMonitor() }
        findViewById<Button>(R.id.btnClearHistory).setOnClickListener {
            graph.clearHistory()
            liveStats.text = "Muestras: 0 · Promedio: —"
        }
        findViewById<Button>(R.id.btnScan).setOnClickListener { ensureScanPermissionAndScan() }
        findViewById<Button>(R.id.btnLocationSettings).setOnClickListener {
            startActivity(Intent(Settings.ACTION_LOCATION_SOURCE_SETTINGS))
        }

        findViewById<Button>(R.id.btnLabCalibrate).setOnClickListener { startLabCalibration() }
        btnLabRun.setOnClickListener { toggleLab() }
        btnSensitivity.setOnClickListener { cycleSensitivity() }
        findViewById<Button>(R.id.btnMarkerEmpty).setOnClickListener { recordManualMarker("HABITACIÓN VACÍA") }
        findViewById<Button>(R.id.btnMarkerEntry).setOnClickListener { recordManualMarker("INGRESO") }
        findViewById<Button>(R.id.btnMarkerMove).setOnClickListener { recordManualMarker("MOVIMIENTO") }
        findViewById<Button>(R.id.btnMarkerExit).setOnClickListener { recordManualMarker("SALIDA") }
        findViewById<Button>(R.id.btnLabClear).setOnClickListener { clearLabSession() }
        findViewById<Button>(R.id.btnExportCsv).setOnClickListener { exportLab("csv") }
        findViewById<Button>(R.id.btnExportJson).setOnClickListener { exportLab("json") }

        findViewById<Button>(R.id.btnNetDns).setOnClickListener { runDnsLookup() }
        findViewById<Button>(R.id.btnNetPing).setOnClickListener { runPingWindow() }
        findViewById<Button>(R.id.btnNetTrace).setOnClickListener { runTraceRoute() }
        findViewById<Button>(R.id.btnNetExport).setOnClickListener { exportNetworkDiagnostic() }
        findViewById<Button>(R.id.btnCidr).setOnClickListener { calculateCidr() }
        findViewById<Button>(R.id.btnLanScan).setOnClickListener { scanLocalLan() }
        findViewById<Button>(R.id.btnMacInspect).setOnClickListener { inspectMac() }
        findViewById<Button>(R.id.btnRepoAdd).setOnClickListener { addOrUpdateRepository() }
        findViewById<Button>(R.id.btnRepoCheckOne).setOnClickListener { checkOneRepositoryNow() }
        findViewById<Button>(R.id.btnRepoCheck).setOnClickListener { checkRepositoriesNow() }
        findViewById<Button>(R.id.btnRepoPause).setOnClickListener { toggleRepositoryEnabled() }
        findViewById<Button>(R.id.btnRepoToggle).setOnClickListener { toggleRepositoryAutoDownload() }
        findViewById<Button>(R.id.btnRepoRemove).setOnClickListener { removeRepository() }
        findViewById<Button>(R.id.btnCatalogAdd).setOnClickListener { addCatalogSource() }
        findViewById<Button>(R.id.btnCatalogCheck).setOnClickListener { checkCatalogsNow() }
        findViewById<Button>(R.id.btnCatalogSeen).setOnClickListener { markCatalogsSeen() }
        btnCsiRun.setOnClickListener { startWifiSensingCalibration() }
        btnCsiDemo.setOnClickListener {
            csiDemoMode = true
            wifiSensingCalibrating = false
            wifiSensingRunning = false
            applyCsiModeUi()
            updateSamplingLoop()
        }
        btnCsiReal.setOnClickListener {
            csiDemoMode = false
            csiScene.wifiDirectMode = true
            applyCsiModeUi()
        }
        btnCsiTracking.setOnClickListener { toggleWifiSensing() }
        btnCsiHeat.setOnClickListener {
            csiHeatmap = !csiHeatmap
            csiScene.showHeatmap = csiHeatmap
            btnCsiHeat.alpha = if (csiHeatmap) 1f else .45f
        }
        findViewById<Button>(R.id.btnCsiClear).setOnClickListener { resetWifiSensing() }

        btnSensitivity.text = "SENSIBILIDAD: ${labEngine.sensitivity.displayName}"
        RepositoryUpdater.schedule(applicationContext)
        refreshRepositoryUi()
        checkDueRepositoriesOnLaunch()
        refreshCatalogUi()
        checkDueCatalogsOnLaunch()
        ensureBasePermissionAndRefresh()
    }


    private fun refreshCatalogUi() {
        val sources = CatalogDiscovery.loadSources(this)
        val tools = CatalogDiscovery.loadTools(this)
        catalogSources.text = CatalogDiscovery.formatSources(sources)
        catalogTools.text = CatalogDiscovery.formatTools(tools)
        val newCount = tools.count { it.isNew }
        catalogStatus.text = "${sources.size} fuente(s) · ${tools.size} recursos · $newCount nuevos"
    }

    private fun addCatalogSource() {
        val normalized = CatalogDiscovery.normalizeRepository(catalogSourceInput.text.toString())
        if (normalized == null) {
            catalogStatus.text = "Fuente inválida. Pegá owner/repo o URL de GitHub."
            return
        }
        val sources = CatalogDiscovery.loadSources(this)
        if (sources.any { it.repository.equals(normalized, true) }) {
            catalogStatus.text = "$normalized ya está agregado."
            return
        }
        val hours = catalogHours.text.toString().toIntOrNull()?.coerceIn(1, 168) ?: 12
        sources += CatalogSource(normalized, intervalHours = hours)
        CatalogDiscovery.saveSources(this, sources)
        catalogSourceInput.text.clear()
        refreshCatalogUi()
        catalogStatus.text = "Fuente agregada: $normalized · cada $hours h"
    }

    private fun checkCatalogsNow() {
        catalogStatus.text = "Revisando catálogos…"
        thread(name = "catalog-check") {
            val pair = CatalogDiscovery.checkDue(applicationContext, force = true) { msg -> runOnUiThread { catalogStatus.text = msg } }
            runOnUiThread {
                catalogSources.text = CatalogDiscovery.formatSources(pair.first)
                catalogTools.text = CatalogDiscovery.formatTools(pair.second)
                val n = pair.second.count { it.isNew }
                catalogStatus.text = if (n > 0) "$n recurso(s) NUEVO(S)" else "Catálogos al día · sin novedades"
            }
        }
    }

    private fun checkDueCatalogsOnLaunch() {
        thread(name = "catalog-due-launch") {
            CatalogDiscovery.checkDue(applicationContext, force = false)
            runOnUiThread { refreshCatalogUi() }
        }
    }

    private fun markCatalogsSeen() {
        CatalogDiscovery.markSeen(this)
        refreshCatalogUi()
        catalogStatus.text = "Novedades marcadas como vistas."
    }

    override fun onDestroy() {
        monitoring = false
        calibrating = false
        labRunning = false
        wifiSensingCalibrating = false
        wifiSensingRunning = false
        handler.removeCallbacksAndMessages(null)
        if (::csiReceiver.isInitialized) csiReceiver.stop()
        super.onDestroy()
    }

    private fun hasNearbyPermission(): Boolean =
        Build.VERSION.SDK_INT < 33 || checkSelfPermission(Manifest.permission.NEARBY_WIFI_DEVICES) == PackageManager.PERMISSION_GRANTED

    private fun hasFineLocation(): Boolean =
        checkSelfPermission(Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED

    private fun ensureBasePermissionAndRefresh() {
        // IP, gateway y DNS no deben quedar en blanco por un permiso Wi-Fi pendiente.
        refreshNetwork()
        if (!hasNearbyPermission()) {
            permission.text = "La red básica ya está disponible. Para SSID/BSSID completos, permití Dispositivos cercanos."
            requestPermissions(arrayOf(Manifest.permission.NEARBY_WIFI_DEVICES), REQ_NEARBY)
        }
    }

    private fun ensureScanPermissionAndScan() {
        val missing = mutableListOf<String>()
        if (Build.VERSION.SDK_INT >= 33 && !hasNearbyPermission()) missing += Manifest.permission.NEARBY_WIFI_DEVICES
        if (!hasFineLocation()) missing += Manifest.permission.ACCESS_FINE_LOCATION
        if (missing.isNotEmpty()) {
            requestPermissions(missing.toTypedArray(), REQ_SCAN)
            return
        }
        if (!isLocationEnabled()) {
            scanStatus.text = "Para escanear redes Android exige que Ubicación esté activada. Tocá 'ABRIR UBICACIÓN'."
            return
        }
        scanNearbyNetworks()
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        val granted = grantResults.isNotEmpty() && grantResults.all { it == PackageManager.PERMISSION_GRANTED }
        when (requestCode) {
            REQ_NEARBY -> {
                permission.text = if (granted) "" else "Android restringió parte de la información Wi‑Fi. Podés habilitar Dispositivos cercanos desde Ajustes."
                refreshNetwork()
            }
            REQ_SCAN -> {
                if (granted) {
                    permission.text = ""
                    if (isLocationEnabled()) scanNearbyNetworks()
                    else scanStatus.text = "Permiso concedido. Falta activar Ubicación para el escaneo Wi‑Fi."
                } else {
                    scanStatus.text = "Sin permiso de ubicación precisa Android no entrega la lista de redes cercanas."
                }
            }
        }
    }

    @Suppress("DEPRECATION")
    private fun currentWifiInfo(): WifiInfo? {
        val cm = getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
        val network = cm.activeNetwork
        val caps = network?.let { cm.getNetworkCapabilities(it) }
        val transport = caps?.transportInfo as? WifiInfo
        return try {
            if (Build.VERSION.SDK_INT >= 31) transport ?: wifiManager.connectionInfo
            else wifiManager.connectionInfo
        } catch (_: SecurityException) {
            // Sin permiso, Android todavía puede dejar disponible información de red básica.
            transport
        }
    }

    @Suppress("DEPRECATION")
    private fun refreshNetwork() {
        val cm = getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
        val network = cm.activeNetwork
        val caps = network?.let { cm.getNetworkCapabilities(it) }
        val props = network?.let { cm.getLinkProperties(it) }
        val wifiInfo = currentWifiInfo()

        val isWifi = caps?.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) == true
        val hasInternet = caps?.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED) == true
        networkStatus.text = when {
            isWifi && hasInternet -> "Estado: Wi‑Fi conectado · Internet validado"
            isWifi -> "Estado: Wi‑Fi conectado · sin Internet validado"
            network != null -> "Estado: red activa, pero no es Wi‑Fi"
            else -> "Estado: sin red activa"
        }

        val rawSsid = wifiInfo?.ssid?.trim('"')
        val ssidRestricted = rawSsid.isNullOrBlank() || rawSsid == "<unknown ssid>"
        ssid.text = if (!isWifi) "SSID: sin Wi‑Fi activo" else
            "SSID: ${if (ssidRestricted) "restringido por Android" else rawSsid}"

        val rawBssid = wifiInfo?.bssid
        val bssidRestricted = rawBssid.isNullOrBlank() || rawBssid == "02:00:00:00:00:00"
        bssid.text = "BSSID/AP: ${if (bssidRestricted) "restringido" else rawBssid}"
        val currentRssi = wifiInfo?.rssi?.takeIf { it in -126..-1 }
        rssi.text = "RSSI: ${currentRssi?.let { "$it dBm · ${qualityLabel(it)}" } ?: "—"}"
        frequency.text = "Frecuencia: ${wifiInfo?.frequency?.takeIf { it > 0 }?.let { "$it MHz · canal ${channelFromFrequency(it) ?: "—"}" } ?: "—"}"
        link.text = "Enlace: ${wifiInfo?.linkSpeed?.takeIf { it > 0 }?.let { "$it Mbps" } ?: "—"}"

        val linkAddress = props?.linkAddresses?.firstOrNull { it.address is Inet4Address }
        val ipv4 = linkAddress?.address?.hostAddress
        currentLocalIpv4 = ipv4
        ip.text = "IP: ${ipv4?.let { "$it/${linkAddress.prefixLength}" } ?: "—"}"
        gatewayAddress = findGateway(props)
        gateway.text = "Router / gateway: ${gatewayAddress ?: "—"}"
        val dnsText = props?.dnsServers?.joinToString(", ") { it.hostAddress ?: it.toString() }.orEmpty()
        dns.text = "DNS: ${dnsText.ifBlank { "—" }}"
        ping.text = "Ping router: —"

        if (isWifi && ssidRestricted) {
            permission.text = if (!hasNearbyPermission())
                "Android permite IP/gateway/DNS, pero restringe SSID/BSSID hasta conceder Dispositivos cercanos."
            else
                "Android sigue restringiendo SSID/BSSID. Para escaneo de redes también requiere Ubicación precisa y Ubicación activa."
        } else if (isWifi && !ssidRestricted) {
            permission.text = "Conectado al router actual. La información mostrada corresponde a esta red Wi‑Fi."
        }

        if (netTarget.text.isBlank()) netTarget.setText(gatewayAddress ?: "1.1.1.1")
        if (cidrIp.text.isBlank() && ipv4 != null) cidrIp.setText(ipv4)
        if (cidrPrefix.text.isBlank()) cidrPrefix.setText(linkAddress?.prefixLength?.toString() ?: "24")
        csiTarget.text = buildWifiSenseLabel()

        // Mostramos actividad desde el arranque cuando Android entrega RSSI.
        if (isWifi && currentRssi != null && !monitoring) toggleMonitor()
        if (gatewayAddress != null) pingGateway()

        // CSI queda preparado en segundo plano como hardware avanzado opcional.
        if (!csiReceiver.isRunning()) csiReceiver.start()
        csiScene.wifiDirectMode = true
        applyCsiModeUi()
    }

    private fun toggleMonitor() {
        monitoring = !monitoring
        btnMonitor.text = if (monitoring) "DETENER" else "INICIAR"
        updateSamplingLoop()
    }

    private fun updateSamplingLoop() {
        handler.removeCallbacks(monitorRunnable)
        if (monitoring || wifiSensingCalibrating || wifiSensingRunning) handler.post(monitorRunnable)
    }

    private fun captureLiveSignal() {
        val value = currentWifiInfo()?.rssi?.takeIf { it in -126..-1 }
        if (value == null) {
            liveValue.text = "Sin señal medible"
            return
        }
        graph.addRssi(value)
        liveValue.text = "$value dBm · ${qualityLabel(value)}"
        val data = graph.history()
        liveStats.text = "Muestras: ${data.size} · Promedio: ${"%.1f".format(data.average())} dBm · Min: ${data.minOrNull()} · Max: ${data.maxOrNull()}"

        if (wifiSensingCalibrating) {
            wifiSensingCalibration += value
            wifiSensingSamples++
            csiHeart.text = "$value dBm"
            csiResp.text = "${String.format(Locale.US, "%.1f", wifiSensingCalibration.average())} dBm"
            csiConfidence.text = "CAL"
            csiMotion.text = "CALIBRANDO ${wifiSensingCalibration.size}/$WIFI_SENSING_CALIBRATION_SAMPLES"
            csiSummary.text = "Enlace: 1 · Muestras: $wifiSensingSamples · Δ RSSI: — · Score: —"
            csiStatus.text = "CALIBRANDO AMBIENTE · ${wifiSensingCalibration.size}/$WIFI_SENSING_CALIBRATION_SAMPLES · dejá router y teléfono quietos"
            csiScene.updateWifiSensing(null, false, false, buildWifiSenseLabel())

            if (wifiSensingCalibration.size >= WIFI_SENSING_CALIBRATION_SAMPLES) {
                val base = wifiSensingEngine.calibrate(wifiSensingCalibration)
                wifiSensingCalibrating = false
                btnCsiRun.text = "RECALIBRAR"
                if (base != null) {
                    csiResp.text = "${String.format(Locale.US, "%.1f", base.mean)} dBm"
                    csiConfidence.text = "LISTO"
                    csiMotion.text = "AMBIENTE ESTABLE"
                    csiStatus.text = "CALIBRACIÓN LISTA · base ${String.format(Locale.US, "%.1f", base.mean)} dBm · ruido σ ${String.format(Locale.US, "%.2f", base.stdDev)}"
                    csiScene.updateWifiSensing(null, true, false, buildWifiSenseLabel())
                }
                updateSamplingLoop()
            }
        } else if (wifiSensingRunning) {
            wifiSensingEngine.evaluate(value)?.let {
                lastWifiSensingReading = it
                wifiSensingSamples++
                updateWifiSensingUi(it)
            }
        }
    }

    private fun startLabCalibration() {
        val value = currentWifiInfo()?.rssi?.takeIf { it in -126..-1 }
        if (value == null) {
            labStatus.text = "No hay RSSI disponible para calibrar. Conectate a una red Wi‑Fi."
            return
        }
        labRunning = false
        calibrating = true
        btnLabRun.text = "INICIAR SESIÓN"
        labEngine.clearCalibration()
        calibrationSamples.clear()
        labSession.clear()
        labRecent.text = "—"
        labBaseline.text = "Línea base: midiendo…"
        labCurrent.text = "$value dBm"
        labDetails.text = "No te muevas ni cambies el ambiente durante las próximas $CALIBRATION_SAMPLES muestras."
        labExportStatus.text = ""
        handler.removeCallbacks(labRunnable)
        handler.post(labRunnable)
    }

    private fun toggleLab() {
        if (labEngine.baseline == null) {
            labStatus.text = "Primero tocá CALIBRAR para aprender el ruido normal del ambiente."
            return
        }
        labRunning = !labRunning
        calibrating = false
        btnLabRun.text = if (labRunning) "DETENER SESIÓN" else "INICIAR SESIÓN"
        labStatus.text = if (labRunning) "SESIÓN ACTIVA · registrando una muestra por segundo" else "SESIÓN DETENIDA"
        if (labRunning) {
            labEngine.resetTracking()
            handler.removeCallbacks(labRunnable)
            handler.post(labRunnable)
        } else handler.removeCallbacks(labRunnable)
    }

    private fun cycleSensitivity() {
        val values = LabSensitivity.values()
        val next = values[(labEngine.sensitivity.ordinal + 1) % values.size]
        labEngine.sensitivity = next
        btnSensitivity.text = "SENSIBILIDAD: ${next.displayName}"
        labDetails.text = "Sensibilidad ${next.displayName.lowercase()}: cambia cuánto se aparta la señal antes de marcar una variación."
    }

    private fun recordManualMarker(label: String) {
        if (labEngine.baseline == null) {
            labStatus.text = "Calibrá primero antes de agregar marcadores."
            return
        }
        val value = currentWifiInfo()?.rssi?.takeIf { it in -126..-1 }
        if (value == null) {
            labStatus.text = "No pude leer RSSI para registrar el marcador."
            return
        }
        val reading = labEngine.evaluate(value, marker = label) ?: return
        appendLabReading(reading)
        updateLabReadingUi(reading)
        labStatus.text = "MARCADOR REGISTRADO · $label"
    }

    private fun appendLabReading(reading: RssiLabReading) {
        labSession += reading
        if (labSession.size > MAX_LAB_ROWS) labSession.removeAt(0)
        val sdf = SimpleDateFormat("HH:mm:ss", Locale.getDefault())
        labRecent.text = labSession.takeLast(8).asReversed().joinToString("\n") { row ->
            val marker = row.marker?.let { " · [$it]" } ?: ""
            "${sdf.format(Date(row.timestampMs))} · ${row.rssi} dBm · ${row.state}$marker"
        }
    }

    private fun updateLabReadingUi(reading: RssiLabReading) {
        labCurrent.text = "${reading.rssi} dBm · ${reading.state}"
        labDetails.text = "Δ línea base ${fmt(reading.delta)} dB · puntaje ${fmt(reading.score)} · sensibilidad ${labEngine.sensitivity.displayName}"
        if (labRunning) labStatus.text = "SESIÓN ACTIVA · ${labSession.size} registros"
    }

    private fun clearLabSession() {
        labSession.clear()
        labRecent.text = "—"
        labExportStatus.text = "Sesión borrada. La calibración se conserva."
    }

    private fun exportLab(format: String) {
        if (labSession.isEmpty()) {
            labExportStatus.text = "No hay registros para exportar."
            return
        }
        pendingExportFormat = format
        val stamp = SimpleDateFormat("yyyyMMdd_HHmmss", Locale.US).format(Date())
        val intent = Intent(Intent.ACTION_CREATE_DOCUMENT).apply {
            addCategory(Intent.CATEGORY_OPENABLE)
            type = if (format == "json") "application/json" else "text/csv"
            putExtra(Intent.EXTRA_TITLE, "rssi_lab_${stamp}.$format")
        }
        @Suppress("DEPRECATION")
        startActivityForResult(intent, REQ_EXPORT)
    }

    @Deprecated("Deprecated in Java")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (resultCode != RESULT_OK) return
        val uri = data?.data ?: return
        try {
            when (requestCode) {
                REQ_EXPORT -> {
                    contentResolver.openOutputStream(uri)?.bufferedWriter()?.use { writer ->
                        writer.write(if (pendingExportFormat == "json") buildLabJson() else buildLabCsv())
                    }
                    labExportStatus.text = "Exportación ${pendingExportFormat.uppercase()} lista."
                }
                REQ_EXPORT_NET -> {
                    contentResolver.openOutputStream(uri)?.bufferedWriter()?.use { writer -> writer.write(buildNetworkDiagnostic()) }
                    netResult.text = "${lastNetworkResult}\n\nDiagnóstico TXT guardado.".trim()
                }
            }
        } catch (e: Exception) {
            if (requestCode == REQ_EXPORT_NET) netResult.text = "No se pudo guardar: ${e.message ?: "error"}"
            else labExportStatus.text = "No se pudo exportar: ${e.message ?: "error"}"
        }
    }

    private fun buildLabCsv(): String {
        val sdf = SimpleDateFormat("yyyy-MM-dd HH:mm:ss.SSS", Locale.US)
        return buildString {
            appendLine("fecha_hora;rssi_dbm;baseline_dbm;delta_db;score;estado;marcador")
            labSession.forEach { row ->
                append(sdf.format(Date(row.timestampMs))).append(';')
                append(row.rssi).append(';')
                append(fmtUs(row.baseline)).append(';')
                append(fmtUs(row.delta)).append(';')
                append(fmtUs(row.score)).append(';')
                append(row.state.replace(';', ',')).append(';')
                append(row.marker?.replace(';', ',') ?: "").appendLine()
            }
        }
    }

    private fun buildLabJson(): String {
        val base = labEngine.baseline
        val root = JSONObject()
        root.put("revision", "R9")
        root.put("sensitivity", labEngine.sensitivity.displayName)
        if (base != null) {
            root.put("baseline", JSONObject().put("mean_dbm", base.mean).put("stddev", base.stdDev).put("samples", base.samples))
        }
        val rows = JSONArray()
        labSession.forEach { row ->
            rows.put(JSONObject()
                .put("timestamp_ms", row.timestampMs)
                .put("rssi_dbm", row.rssi)
                .put("baseline_dbm", row.baseline)
                .put("delta_db", row.delta)
                .put("score", row.score)
                .put("state", row.state)
                .put("marker", row.marker ?: JSONObject.NULL))
        }
        root.put("readings", rows)
        return root.toString(2)
    }



    private fun applyCsiModeUi() {
        csiScene.demoMode = csiDemoMode
        csiScene.wifiDirectMode = true
        csiModeBadge.text = if (csiDemoMode) "DEMO" else "WI‑FI"
        csiModeBadge.setTextColor(if (csiDemoMode) Color.rgb(255, 216, 116) else Color.rgb(83, 255, 208))
        btnCsiDemo.alpha = if (csiDemoMode) 1f else .5f
        btnCsiReal.alpha = if (csiDemoMode) .5f else 1f

        if (csiDemoMode) {
            csiHeart.text = "-55 dBm"
            csiResp.text = "-56.2 dBm"
            csiConfidence.text = "82 %"
            csiMotion.text = "MOVIMIENTO PROBABLE"
            csiSummary.text = "Enlace: 1 · Muestras: 128 · Δ RSSI: 3.8 dB · Score: 3.70"
            csiStatus.text = "DEMO VISUAL · datos simulados para mostrar la interfaz"
            csiTarget.text = "DEMO · en WI‑FI se usa el router/AP al que está conectado el teléfono"
            csiScene.updateWifiSensing(null, false, false, "DEMO")
            return
        }

        btnCsiRun.text = if (wifiSensingEngine.baseline == null) "CALIBRAR 25s" else "RECALIBRAR"
        btnCsiTracking.text = if (wifiSensingRunning) "DETENER" else "INICIAR"
        csiTarget.text = buildWifiSenseLabel()
        val base = wifiSensingEngine.baseline
        if (base == null) {
            val value = currentWifiInfo()?.rssi?.takeIf { it in -126..-1 }
            csiHeart.text = value?.let { "$it dBm" } ?: "— dBm"
            csiResp.text = "— dBm"
            csiConfidence.text = "— %"
            csiMotion.text = "SIN CALIBRAR"
            csiSummary.text = "Enlace: ${if (value != null) 1 else 0} · Muestras: $wifiSensingSamples · Δ RSSI: — · Score: —"
            csiStatus.text = "WI‑FI DIRECTO · calibrá 25 segundos con el ambiente quieto"
            csiScene.updateWifiSensing(null, false, false, buildWifiSenseLabel())
        } else if (lastWifiSensingReading != null) {
            updateWifiSensingUi(lastWifiSensingReading!!)
        } else {
            csiResp.text = "${String.format(Locale.US, "%.1f", base.mean)} dBm"
            csiConfidence.text = "LISTO"
            csiMotion.text = "CALIBRADO"
            csiSummary.text = "Enlace: 1 · Muestras: $wifiSensingSamples · Δ RSSI: — · Score: —"
            csiStatus.text = "CALIBRACIÓN LISTA · presioná INICIAR"
            csiScene.updateWifiSensing(null, true, false, buildWifiSenseLabel())
        }
    }

    private fun startWifiSensingCalibration() {
        val value = currentWifiInfo()?.rssi?.takeIf { it in -126..-1 }
        if (value == null) {
            csiStatus.text = "No hay RSSI disponible. Conectate al Wi‑Fi del router y reintentá."
            return
        }
        csiDemoMode = false
        wifiSensingRunning = false
        wifiSensingCalibrating = true
        wifiSensingCalibration.clear()
        wifiSensingEngine.reset()
        lastWifiSensingReading = null
        wifiSensingSamples = 0
        csiScene.demoMode = false
        csiScene.wifiDirectMode = true
        csiModeBadge.text = "WI‑FI"
        btnCsiRun.text = "CALIBRANDO…"
        btnCsiTracking.text = "INICIAR"
        csiHeart.text = "$value dBm"
        csiResp.text = "— dBm"
        csiConfidence.text = "CAL"
        csiMotion.text = "CALIBRANDO"
        csiSummary.text = "Enlace: 1 · Muestras: 0 · Δ RSSI: — · Score: —"
        csiStatus.text = "CALIBRANDO AMBIENTE · 0/$WIFI_SENSING_CALIBRATION_SAMPLES · no muevas router ni teléfono"
        csiTarget.text = buildWifiSenseLabel()
        csiScene.updateWifiSensing(null, false, false, buildWifiSenseLabel())
        updateSamplingLoop()
    }

    private fun toggleWifiSensing() {
        if (csiDemoMode) {
            csiDemoMode = false
            csiScene.demoMode = false
            csiScene.wifiDirectMode = true
        }
        if (wifiSensingEngine.baseline == null) {
            csiStatus.text = "Primero calibrá 25 segundos con el ambiente quieto."
            applyCsiModeUi()
            return
        }
        wifiSensingCalibrating = false
        wifiSensingRunning = !wifiSensingRunning
        btnCsiTracking.text = if (wifiSensingRunning) "DETENER" else "INICIAR"
        csiStatus.text = if (wifiSensingRunning)
            "WI‑FI SENSING ACTIVO · analizando RSSI una vez por segundo"
        else "WI‑FI SENSING DETENIDO · calibración conservada"
        csiScene.updateWifiSensing(lastWifiSensingReading, true, wifiSensingRunning, buildWifiSenseLabel())
        updateSamplingLoop()
    }

    private fun resetWifiSensing() {
        wifiSensingRunning = false
        wifiSensingCalibrating = false
        wifiSensingCalibration.clear()
        wifiSensingEngine.reset()
        lastWifiSensingReading = null
        wifiSensingSamples = 0
        csiReceiver.clear()
        applyCsiModeUi()
        updateSamplingLoop()
    }

    private fun buildWifiSenseLabel(): String {
        val info = currentWifiInfo()
        val rawSsid = info?.ssid?.trim('"')?.takeUnless { it.isBlank() || it == "<unknown ssid>" } ?: "Wi‑Fi actual"
        val rawBssid = info?.bssid?.takeUnless { it.isBlank() || it == "02:00:00:00:00:00" } ?: "AP —"
        return "$rawSsid · AP $rawBssid · gateway ${gatewayAddress ?: "—"}"
    }

    private fun updateWifiSensingUi(reading: WifiSensingReading) {
        csiHeart.text = "${reading.rssi} dBm"
        csiResp.text = "${String.format(Locale.US, "%.1f", reading.baseline)} dBm"
        csiConfidence.text = "${(reading.confidence * 100).toInt()} %"
        csiMotion.text = reading.state
        csiSummary.text = "Enlace: 1 · Muestras: $wifiSensingSamples · Δ RSSI: ${String.format(Locale.US, "%.1f", reading.delta)} dB · Score: ${String.format(Locale.US, "%.2f", reading.score)}"
        csiStatus.text = "WI‑FI SENSING · ${reading.state} · RSSI ${reading.rssi} dBm · Δ ${String.format(Locale.US, "%.1f", reading.delta)} dB"
        csiTarget.text = buildWifiSenseLabel()
        csiScene.updateWifiSensing(reading, true, wifiSensingRunning, buildWifiSenseLabel())
    }

    private fun updateCsiUi(nodes: List<CsiNodeSnapshot>, total: Long, invalid: Long) {
        lastCsiNodes = nodes
        csiScene.updateNodes(nodes)
        csiNodes.text = if (nodes.isEmpty()) {
            "CSI opcional: sin sensores. El modo Wi‑Fi directo funciona sin ESP32."
        } else {
            "CSI opcional detectado · paquetes $total · inválidos $invalid\n\n" +
                nodes.joinToString("\n") { n -> "Nodo ${n.nodeId} · ${n.sourceIp} · ${n.state} · RSSI ${n.rssi}" }
        }
        // Los paquetes CSI opcionales no pisan la interfaz principal de Wi‑Fi directo.
        if (csiDemoMode || csiScene.wifiDirectMode) return
    }

    private fun runDnsLookup() {
        val target = netTarget.text.toString()
        netResult.text = "Resolviendo DNS…"
        thread {
            val result = NetworkToolkit.dnsLookup(target)
            lastNetworkResult = result
            runOnUiThread { netResult.text = result }
        }
    }

    private fun runPingWindow() {
        val target = netTarget.text.toString()
        netResult.text = "Midiendo 10 muestras de latencia y pérdida…"
        thread {
            val result = NetworkToolkit.pingWindow(target, 10, 1200)
            lastNetworkResult = result
            runOnUiThread { netResult.text = result }
        }
    }

    private fun runTraceRoute() {
        val target = netTarget.text.toString()
        netResult.text = "Ejecutando traceroute experimental…"
        thread {
            val result = NetworkToolkit.traceRoute(target, 16)
            lastNetworkResult = result
            runOnUiThread { netResult.text = result }
        }
    }

    private fun calculateCidr() {
        try {
            val prefix = cidrPrefix.text.toString().trim().toIntOrNull() ?: throw IllegalArgumentException("Prefijo inválido.")
            val result = NetworkToolkit.calculateCidr(cidrIp.text.toString(), prefix)
            cidrResult.text = NetworkToolkit.formatCidr(result)
        } catch (e: Exception) {
            cidrResult.text = "CIDR: ${e.message ?: "dato inválido"}"
        }
    }

    private fun scanLocalLan() {
        val local = currentLocalIpv4
        if (local.isNullOrBlank()) {
            lanStatus.text = "No hay una IPv4 local disponible. Conectate a Wi‑Fi y actualizá."
            return
        }
        lanStatus.text = "Iniciando exploración local /24…"
        lanDevices.text = "Buscando…"
        thread {
            val rows = NetworkToolkit.discoverLocal24(local) { status -> runOnUiThread { lanStatus.text = status } }
            lastLanText = rows.joinToString("\n")
            runOnUiThread {
                lanDevices.text = if (rows.isEmpty()) "Sin respuestas. Algunos equipos o routers bloquean ICMP." else lastLanText
                lanStatus.text = "Listo · ${rows.size} equipos respondieron. Android puede ocultar MAC de otros dispositivos."
            }
        }
    }

    private fun inspectMac() {
        val info = NetworkToolkit.inspectMac(macInput.text.toString())
        macResult.text = if (info.normalized == null) info.status
        else "MAC: ${info.normalized}\nOUI: ${info.oui}\n${info.status}"
    }

    private fun refreshRepositoryUi() {
        val repos = RepositoryUpdater.load(this)
        repoList.text = RepositoryUpdater.format(repos)
        repoStatus.text = "${repos.size} repositorios · revisión automática independiente · descarga sin autoejecución"
    }

    private fun addOrUpdateRepository() {
        val normalized = RepositoryUpdater.normalizeRepository(repoInput.text.toString())
        if (normalized == null) {
            repoStatus.text = "Repositorio inválido. Pegá una URL de GitHub o owner/repo."
            return
        }
        val hours = repoHours.text.toString().toIntOrNull()?.coerceIn(1, 168) ?: 6
        val repos = RepositoryUpdater.load(this)
        val existing = repos.firstOrNull { it.repository.equals(normalized, true) }
        if (existing == null) {
            repos += RepoWatch(repository = normalized, intervalHours = hours, enabled = true, autoDownload = true)
            repoStatus.text = "Agregado $normalized · cada $hours h · AUTO DESCARGA activa."
        } else {
            existing.intervalHours = hours
            existing.enabled = true
            repoStatus.text = "Actualizado $normalized · cada $hours h."
        }
        RepositoryUpdater.save(this, repos)
        RepositoryUpdater.schedule(applicationContext)
        repoInput.setText("")
        refreshRepositoryUi()
    }

    private fun toggleRepositoryEnabled() {
        val normalized = RepositoryUpdater.normalizeRepository(repoInput.text.toString())
        if (normalized == null) {
            repoStatus.text = "Pegá en el campo el repo que querés pausar o activar."
            return
        }
        val repos = RepositoryUpdater.load(this)
        val item = repos.firstOrNull { it.repository.equals(normalized, true) }
        if (item == null) {
            repoStatus.text = "$normalized no está agregado."
            return
        }
        item.enabled = !item.enabled
        RepositoryUpdater.save(this, repos)
        repoStatus.text = "$normalized · ${if (item.enabled) "ACTIVO" else "PAUSADO"}."
        refreshRepositoryUi()
    }

    private fun toggleRepositoryAutoDownload() {
        val normalized = RepositoryUpdater.normalizeRepository(repoInput.text.toString())
        if (normalized == null) {
            repoStatus.text = "Pegá en el campo el repo que querés modificar."
            return
        }
        val repos = RepositoryUpdater.load(this)
        val item = repos.firstOrNull { it.repository.equals(normalized, true) }
        if (item == null) {
            repoStatus.text = "$normalized no está agregado."
            return
        }
        item.autoDownload = !item.autoDownload
        RepositoryUpdater.save(this, repos)
        repoStatus.text = "$normalized · AUTO DESCARGA ${if (item.autoDownload) "ACTIVA" else "DESACTIVADA"}."
        refreshRepositoryUi()
    }

    private fun removeRepository() {
        val normalized = RepositoryUpdater.normalizeRepository(repoInput.text.toString())
        if (normalized == null) {
            repoStatus.text = "Pegá en el campo el repo que querés quitar."
            return
        }
        val repos = RepositoryUpdater.load(this)
        val removed = repos.removeAll { it.repository.equals(normalized, true) }
        if (!removed) {
            repoStatus.text = "$normalized no estaba en la lista."
            return
        }
        RepositoryUpdater.save(this, repos)
        repoStatus.text = "Quitado $normalized."
        repoInput.setText("")
        refreshRepositoryUi()
    }

    private fun checkOneRepositoryNow() {
        val normalized = RepositoryUpdater.normalizeRepository(repoInput.text.toString())
        if (normalized == null) {
            repoStatus.text = "Pegá el repo que querés revisar."
            return
        }
        repoStatus.text = "Revisando $normalized…"
        thread(name = "repo-check-one") {
            val repos = RepositoryUpdater.load(applicationContext)
            val item = repos.firstOrNull { it.repository.equals(normalized, true) }
            if (item == null) {
                runOnUiThread { repoStatus.text = "$normalized no está agregado." }
                return@thread
            }
            RepositoryUpdater.checkOne(applicationContext, item) { progress ->
                runOnUiThread { repoStatus.text = progress }
            }
            RepositoryUpdater.save(applicationContext, repos)
            runOnUiThread {
                repoList.text = RepositoryUpdater.format(repos)
                repoStatus.text = "$normalized: ${item.status}"
            }
        }
    }

    private fun checkRepositoriesNow() {
        repoStatus.text = "Revisando repositorios…"
        thread(name = "repo-check-manual") {
            val repos = RepositoryUpdater.checkDue(applicationContext, force = true) { progress ->
                runOnUiThread { repoStatus.text = progress }
            }
            runOnUiThread {
                repoList.text = RepositoryUpdater.format(repos)
                repoStatus.text = "Revisión terminada · ${repos.size} repositorios. Las descargas no se instalan solas."
            }
        }
    }

    private fun checkDueRepositoriesOnLaunch() {
        thread(name = "repo-check-launch") {
            val repos = RepositoryUpdater.checkDue(applicationContext, force = false)
            runOnUiThread { repoList.text = RepositoryUpdater.format(repos) }
        }
    }

    private fun exportNetworkDiagnostic() {
        val stamp = SimpleDateFormat("yyyyMMdd_HHmmss", Locale.US).format(Date())
        val intent = Intent(Intent.ACTION_CREATE_DOCUMENT).apply {
            addCategory(Intent.CATEGORY_OPENABLE)
            type = "text/plain"
            putExtra(Intent.EXTRA_TITLE, "diagnostico_red_${stamp}.txt")
        }
        @Suppress("DEPRECATION")
        startActivityForResult(intent, REQ_EXPORT_NET)
    }

    private fun buildNetworkDiagnostic(): String = buildString {
        appendLine("KIT HERRAMIENTAS · DIAGNÓSTICO DE RED · R9")
        appendLine("Fecha: ${SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.US).format(Date())}")
        appendLine(ssid.text)
        appendLine(bssid.text)
        appendLine(ip.text)
        appendLine(gateway.text)
        appendLine(rssi.text)
        appendLine(frequency.text)
        appendLine(link.text)
        if (lastNetworkResult.isNotBlank()) {
            appendLine()
            appendLine("ÚLTIMA PRUEBA")
            appendLine(lastNetworkResult)
        }
        if (lastLanText.isNotBlank()) {
            appendLine()
            appendLine("EQUIPOS LAN")
            appendLine(lastLanText)
        }
    }.trimEnd()

    @Suppress("DEPRECATION")
    private fun scanNearbyNetworks() {
        scanStatus.text = "Solicitando escaneo… Android puede limitar la frecuencia."
        val started = try { wifiManager.startScan() } catch (_: SecurityException) { false }
        handler.postDelayed({ readScanResults(started) }, 2800)
    }

    @Suppress("DEPRECATION")
    private fun readScanResults(scanStarted: Boolean) {
        val results: List<ScanResult> = try { wifiManager.scanResults } catch (_: SecurityException) { emptyList() }
        if (results.isEmpty()) {
            scanStatus.text = "No hay resultados disponibles. Revisá permisos, Ubicación y Wi‑Fi."
            networks.text = "—"
            channels.text = "—"
            return
        }

        val sorted = results.sortedByDescending { it.level }.take(60)
        networks.text = sorted.joinToString("\n\n") { r -> formatNetwork(r) }

        val channelSummary = sorted
            .mapNotNull { r -> channelFromFrequency(r.frequency)?.let { it to r.level } }
            .groupBy({ it.first }, { it.second })
            .toSortedMap()
            .entries
            .joinToString("\n") { (channel, levels) ->
                "Canal $channel: ${levels.size} AP · mejor ${levels.maxOrNull()} dBm"
            }
        channels.text = if (channelSummary.isBlank()) "Sin canales calculables" else channelSummary
        scanStatus.text = "${sorted.size} AP visibles · ${if (scanStarted) "escaneo solicitado" else "resultados en caché por límite de Android"}"
        refreshNetwork()
    }

    private fun formatNetwork(r: ScanResult): String {
        val name = r.SSID.takeIf { it.isNotBlank() } ?: "Red oculta"
        val channel = channelFromFrequency(r.frequency)?.toString() ?: "—"
        val security = securityLabel(r.capabilities)
        return "$name\n${r.BSSID} · ${r.level} dBm · ${r.frequency} MHz · canal $channel · $security"
    }

    private fun securityLabel(capabilities: String): String = when {
        capabilities.contains("WPA3", true) || capabilities.contains("SAE", true) -> "WPA3"
        capabilities.contains("WPA2", true) -> "WPA2"
        capabilities.contains("WPA", true) -> "WPA"
        capabilities.contains("WEP", true) -> "WEP"
        else -> "Abierta/otro"
    }

    private fun isLocationEnabled(): Boolean {
        val lm = getSystemService(Context.LOCATION_SERVICE) as LocationManager
        return if (Build.VERSION.SDK_INT >= 28) lm.isLocationEnabled
        else lm.isProviderEnabled(LocationManager.GPS_PROVIDER) || lm.isProviderEnabled(LocationManager.NETWORK_PROVIDER)
    }

    private fun findGateway(props: LinkProperties?): String? =
        props?.routes?.firstOrNull { it.isDefaultRoute && it.gateway is Inet4Address }?.gateway?.hostAddress

    private fun pingGateway() {
        val target = gatewayAddress
        if (target.isNullOrBlank()) {
            ping.text = "Ping: gateway no disponible"
            return
        }
        ping.text = "Ping: midiendo…"
        thread {
            val started = System.nanoTime()
            val ok = try { InetAddress.getByName(target).isReachable(1800) } catch (_: Exception) { false }
            val elapsed = (System.nanoTime() - started) / 1_000_000
            runOnUiThread { ping.text = if (ok) "Ping: ~${elapsed} ms" else "Ping: sin respuesta (puede bloquear ICMP)" }
        }
    }

    private fun qualityLabel(dbm: Int): String = when {
        dbm >= -50 -> "Excelente"
        dbm >= -60 -> "Muy buena"
        dbm >= -67 -> "Buena"
        dbm >= -75 -> "Regular"
        dbm >= -85 -> "Débil"
        else -> "Muy débil"
    }

    private fun channelFromFrequency(freq: Int): Int? = when {
        freq == 2484 -> 14
        freq in 2412..2472 -> (freq - 2407) / 5
        freq in 5000..5895 -> (freq - 5000) / 5
        freq in 5955..7115 -> (freq - 5950) / 5
        else -> null
    }

    private fun fmt(value: Double): String = String.format(Locale.getDefault(), "%.2f", value)
    private fun fmtUs(value: Double): String = String.format(Locale.US, "%.3f", value)

    companion object {
        private const val REQ_NEARBY = 100
        private const val REQ_SCAN = 101
        private const val REQ_EXPORT = 102
        private const val REQ_EXPORT_NET = 103
        private const val CALIBRATION_SAMPLES = 15
        private const val WIFI_SENSING_CALIBRATION_SAMPLES = 25
        private const val MAX_LAB_ROWS = 21600
    }
}
