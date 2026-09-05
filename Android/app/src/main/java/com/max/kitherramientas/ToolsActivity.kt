package com.max.kitherramientas

import android.app.Activity
import android.content.Intent
import android.graphics.Color
import android.net.Uri
import android.os.Bundle
import android.text.InputType
import android.view.Gravity
import android.view.View
import android.widget.*
import kotlin.concurrent.thread

class ToolsActivity : Activity() {
    private lateinit var metaTarget: EditText
    private lateinit var metaKey: EditText
    private lateinit var metaBusiness: CheckBox
    private lateinit var metaStatus: TextView
    private lateinit var metaOutput: TextView

    private lateinit var osintSearch: EditText
    private lateinit var osintStatus: TextView
    private lateinit var osintList: LinearLayout
    private var osintTools: MutableList<CatalogTool> = mutableListOf()
    private var selectedTool: CatalogTool? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        title = "NexoKit · Herramientas"
        setContentView(buildUi())
        reloadOsint()
    }

    private fun buildUi(): View {
        val scroll = ScrollView(this).apply { setBackgroundColor(Color.rgb(14, 17, 23)) }
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(16), dp(16), dp(16), dp(30))
        }
        scroll.addView(root)

        root.addView(text("HERRAMIENTAS INTEGRADAS", 24f, Color.WHITE, true))
        root.addView(text("Usá los repositorios desde NexoKit. Actualizaciones queda sólo para mantenerlos al día.", 14f, Color.rgb(170,179,194), false).apply {
            setPadding(0, dp(4), 0, dp(14))
        })

        root.addView(section("🌐 META SCAN · FACEBOOK OSINT"))
        root.addView(text("Integración de HackUnderway/meta_scan. Usa tu propia clave de RapidAPI; NexoKit no la guarda.", 13f, Color.rgb(170,179,194), false))
        metaTarget = edit("Usuario o URL de Facebook")
        root.addView(metaTarget)
        metaKey = edit("RapidAPI key").apply {
            inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD
        }
        root.addView(metaKey)
        metaBusiness = CheckBox(this).apply {
            text = "Incluir Business / About / Transparencia"
            setTextColor(Color.WHITE)
            isChecked = true
        }
        root.addView(metaBusiness)

        val metaActions = horizontal()
        metaActions.addView(button("BUSCAR") { runMetaScan() })
        metaActions.addView(button("REPO") { openUrl("https://github.com/HackUnderway/meta_scan") })
        root.addView(metaActions)
        metaStatus = text("Listo para consultar.", 13f, Color.rgb(170,179,194), false)
        root.addView(metaStatus)
        metaOutput = text("", 12f, Color.rgb(216,225,235), false).apply {
            setBackgroundColor(Color.rgb(13,18,26))
            setPadding(dp(10), dp(10), dp(10), dp(10))
            setTextIsSelectable(true)
        }
        root.addView(metaOutput, LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT).apply {
            topMargin = dp(8)
        })

        root.addView(section("🔎 OSINT HUB"))
        root.addView(text("Catálogo navegable de las fuentes de Descubrir. Tocá un recurso para seleccionarlo; después podés abrirlo o seguir sus actualizaciones.", 13f, Color.rgb(170,179,194), false))
        osintSearch = edit("Buscar repo o categoría")
        osintSearch.setOnEditorActionListener { _, _, _ -> applyFilter(); true }
        root.addView(osintSearch)

        val osintActions = horizontal()
        osintActions.addView(button("FILTRAR") { applyFilter() })
        osintActions.addView(button("ACTUALIZAR") { refreshCatalog() })
        root.addView(osintActions)

        val selectedActions = horizontal()
        selectedActions.addView(button("ABRIR") {
            selectedTool?.let { openUrl("https://github.com/${it.repository}") }
                ?: run { osintStatus.text = "Seleccioná una herramienta." }
        })
        selectedActions.addView(button("+ SEGUIR") { followSelected() })
        root.addView(selectedActions)

        osintStatus = text("", 13f, Color.rgb(170,179,194), false)
        root.addView(osintStatus)
        osintList = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL }
        root.addView(osintList)

        root.addView(section("📡 WI‑FI SENSING"))
        root.addView(text("El módulo router + teléfono sigue en la pantalla principal de NexoKit. RuView queda como referencia avanzada para hardware CSI compatible.", 13f, Color.rgb(170,179,194), false))
        val wifiActions = horizontal()
        wifiActions.addView(button("VOLVER AL SENSING") { finish() })
        wifiActions.addView(button("VER RUVIEW") { openUrl("https://github.com/ruvnet/RuView") })
        root.addView(wifiActions)

        return scroll
    }

    private fun runMetaScan() {
        val target = metaTarget.text.toString()
        val key = metaKey.text.toString()
        metaStatus.text = "Consultando Meta Scan…"
        metaOutput.text = ""
        thread(name = "meta-scan-r10") {
            try {
                val result = MetaScanClient.scan(target, key, metaBusiness.isChecked)
                val formatted = MetaScanClient.format(result)
                runOnUiThread {
                    metaOutput.text = formatted
                    metaStatus.text = if (result.errors.isEmpty()) "LISTO · @${result.username}" else "LISTO CON ${result.errors.size} aviso(s)"
                }
            } catch (e: Exception) {
                runOnUiThread { metaStatus.text = "ERROR · ${e.message ?: e.javaClass.simpleName}" }
            }
        }
    }

    private fun reloadOsint() {
        osintTools = CatalogDiscovery.loadTools(this)
        applyFilter()
        osintStatus.text = if (osintTools.isEmpty()) "Sin catálogo local. Tocá ACTUALIZAR." else "${osintTools.size} herramientas cargadas."
    }

    private fun applyFilter() {
        if (!::osintList.isInitialized) return
        val q = osintSearch.text.toString().trim()
        val filtered = osintTools.filter {
            q.isBlank() || it.repository.contains(q, true) || it.category.contains(q, true) || it.source.contains(q, true)
        }.take(100)

        osintList.removeAllViews()
        filtered.forEach { tool ->
            val row = TextView(this).apply {
                text = (if (tool.isNew) "🆕 " else "• ") + tool.repository + "\n   " + tool.category
                setTextColor(Color.rgb(220,226,235))
                textSize = 14f
                setPadding(dp(10), dp(10), dp(10), dp(10))
                isClickable = true
                setOnClickListener {
                    selectedTool = tool
                    osintStatus.text = "Seleccionado: ${tool.repository}"
                    setBackgroundColor(Color.rgb(37,48,68))
                }
            }
            osintList.addView(row, LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT).apply {
                bottomMargin = dp(3)
            })
        }
        if (filtered.isEmpty()) osintStatus.text = "Sin coincidencias."
    }

    private fun refreshCatalog() {
        osintStatus.text = "Actualizando catálogo…"
        thread(name = "osint-catalog-r10") {
            val result = runCatching {
                CatalogDiscovery.checkDue(this, force = true)
            }
            runOnUiThread {
                result.onSuccess {
                    osintTools = it.second
                    applyFilter()
                    osintStatus.text = "Catálogo actualizado · ${osintTools.size} recursos."
                }.onFailure {
                    osintStatus.text = "ERROR · ${it.message ?: it.javaClass.simpleName}"
                }
            }
        }
    }

    private fun followSelected() {
        val tool = selectedTool ?: run { osintStatus.text = "Seleccioná una herramienta."; return }
        val repos = RepositoryUpdater.load(this)
        if (repos.any { it.repository.equals(tool.repository, true) }) {
            osintStatus.text = "${tool.repository} ya está en Actualizaciones."
            return
        }
        repos += RepoWatch(
            repository = tool.repository,
            enabled = true,
            autoDownload = false,
            intervalHours = 12,
            status = "AGREGADO DESDE HERRAMIENTAS · SOLO AVISA"
        )
        RepositoryUpdater.save(this, repos)
        osintStatus.text = "${tool.repository} agregado a Actualizaciones · SOLO AVISA."
    }

    private fun openUrl(url: String) {
        runCatching { startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url))) }
    }

    private fun section(title: String) = text(title, 19f, Color.WHITE, true).apply {
        setPadding(0, dp(24), 0, dp(8))
    }

    private fun edit(hintText: String) = EditText(this).apply {
        hint = hintText
        setHintTextColor(Color.rgb(115,127,145))
        setTextColor(Color.WHITE)
        setSingleLine(true)
        setBackgroundColor(Color.rgb(23,28,37))
        setPadding(dp(10), 0, dp(10), 0)
        layoutParams = LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, dp(46)).apply { bottomMargin = dp(8) }
    }

    private fun button(label: String, action: () -> Unit) = Button(this).apply {
        text = label
        setTextColor(Color.WHITE)
        setBackgroundColor(Color.rgb(37,48,68))
        setOnClickListener { action() }
        layoutParams = LinearLayout.LayoutParams(0, dp(44), 1f).apply { marginEnd = dp(6); bottomMargin = dp(6) }
    }

    private fun horizontal() = LinearLayout(this).apply {
        orientation = LinearLayout.HORIZONTAL
        gravity = Gravity.CENTER_VERTICAL
    }

    private fun text(value: String, size: Float, color: Int, bold: Boolean) = TextView(this).apply {
        text = value
        textSize = size
        setTextColor(color)
        if (bold) setTypeface(typeface, android.graphics.Typeface.BOLD)
    }

    private fun dp(value: Int): Int = (value * resources.displayMetrics.density).toInt()
}
