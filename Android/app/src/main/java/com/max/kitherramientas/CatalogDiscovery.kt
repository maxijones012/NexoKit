package com.max.kitherramientas

import android.content.Context
import android.util.Base64
import org.json.JSONArray
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL
import java.util.concurrent.TimeUnit


data class CatalogSource(
    var repository: String,
    var enabled: Boolean = true,
    var intervalHours: Int = 12,
    var lastCommitId: String = "",
    var lastCheckedMs: Long = 0L,
    var totalCount: Int = 0,
    var newCount: Int = 0,
    var status: String = "Pendiente"
)

data class CatalogTool(
    var repository: String,
    var category: String,
    var source: String,
    var firstSeenMs: Long = System.currentTimeMillis(),
    var isNew: Boolean = false
)

object CatalogDiscovery {
    private const val PREFS = "nexokit_discovery"
    private const val KEY_SOURCES = "sources"
    private const val KEY_TOOLS = "tools"
    private val githubRegex = Regex("https?://github\\.com/([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+)", RegexOption.IGNORE_CASE)

    fun loadSources(context: Context): MutableList<CatalogSource> {
        val text = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString(KEY_SOURCES, null)
        if (!text.isNullOrBlank()) {
            runCatching {
                val arr = JSONArray(text)
                return MutableList(arr.length()) { i ->
                    val o = arr.getJSONObject(i)
                    CatalogSource(
                        repository = o.optString("repository"),
                        enabled = o.optBoolean("enabled", true),
                        intervalHours = o.optInt("intervalHours", 12).coerceIn(1, 168),
                        lastCommitId = o.optString("lastCommitId"),
                        lastCheckedMs = o.optLong("lastCheckedMs"),
                        totalCount = o.optInt("totalCount"),
                        newCount = o.optInt("newCount"),
                        status = o.optString("status", "Pendiente")
                    )
                }
            }
        }
        return mutableListOf(CatalogSource("Astrosp/Awesome-OSINT-List", intervalHours = 12)).also { saveSources(context, it) }
    }

    fun saveSources(context: Context, sources: List<CatalogSource>) {
        val arr = JSONArray()
        sources.forEach { s -> arr.put(JSONObject().apply {
            put("repository", s.repository); put("enabled", s.enabled); put("intervalHours", s.intervalHours)
            put("lastCommitId", s.lastCommitId); put("lastCheckedMs", s.lastCheckedMs); put("totalCount", s.totalCount)
            put("newCount", s.newCount); put("status", s.status)
        }) }
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putString(KEY_SOURCES, arr.toString()).apply()
    }

    fun loadTools(context: Context): MutableList<CatalogTool> {
        val text = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString(KEY_TOOLS, null) ?: return mutableListOf()
        return runCatching {
            val arr = JSONArray(text)
            MutableList(arr.length()) { i ->
                val o = arr.getJSONObject(i)
                CatalogTool(o.optString("repository"), o.optString("category", "General"), o.optString("source"), o.optLong("firstSeenMs"), o.optBoolean("isNew"))
            }
        }.getOrElse { mutableListOf() }
    }

    fun saveTools(context: Context, tools: List<CatalogTool>) {
        val arr = JSONArray()
        tools.forEach { t -> arr.put(JSONObject().apply {
            put("repository", t.repository); put("category", t.category); put("source", t.source)
            put("firstSeenMs", t.firstSeenMs); put("isNew", t.isNew)
        }) }
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putString(KEY_TOOLS, arr.toString()).apply()
    }

    fun normalizeRepository(value: String): String? = RepositoryUpdater.normalizeRepository(value)

    fun checkDue(context: Context, force: Boolean = false, progress: ((String) -> Unit)? = null): Pair<MutableList<CatalogSource>, MutableList<CatalogTool>> {
        val sources = loadSources(context)
        val tools = loadTools(context)
        val now = System.currentTimeMillis()
        for (source in sources) {
            if (!source.enabled) continue
            val dueMs = TimeUnit.HOURS.toMillis(source.intervalHours.coerceIn(1, 168).toLong())
            if (!force && source.lastCheckedMs > 0L && now - source.lastCheckedMs < dueMs) continue
            progress?.invoke("Revisando catálogo ${source.repository}…")
            try {
                val snapshot = fetch(source.repository)
                val firstRun = source.lastCommitId.isBlank()
                val existing = tools.associateBy { "${it.source}|${it.repository}".lowercase() }.toMutableMap()
                var added = 0
                snapshot.second.forEach { incoming ->
                    val key = "${source.repository}|${incoming.repository}".lowercase()
                    val old = existing[key]
                    if (old != null) {
                        old.category = incoming.category
                        old.isNew = false
                    } else {
                        incoming.source = source.repository
                        incoming.firstSeenMs = now
                        incoming.isNew = !firstRun
                        tools += incoming
                        existing[key] = incoming
                        if (!firstRun) added++
                    }
                }
                source.lastCommitId = snapshot.first
                source.lastCheckedMs = now
                source.totalCount = snapshot.second.size
                source.newCount = added
                source.status = if (firstRun) "BASE CREADA · ${snapshot.second.size}" else if (added > 0) "$added NUEVOS" else "SIN NOVEDADES · ${snapshot.second.size}"
            } catch (e: Exception) {
                source.lastCheckedMs = now
                source.status = "ERROR · ${e.message ?: e.javaClass.simpleName}"
            }
            saveSources(context, sources)
            saveTools(context, tools)
        }
        return sources to tools
    }

    private fun fetch(repository: String): Pair<String, List<CatalogTool>> {
        val repo = normalizeRepository(repository) ?: error("Fuente inválida")
        val info = requestJson("https://api.github.com/repos/$repo") ?: error("Repositorio no encontrado")
        val branch = info.optString("default_branch", "main")
        val commit = requestJson("https://api.github.com/repos/$repo/commits/${java.net.URLEncoder.encode(branch, "UTF-8")}")?.optString("sha").orEmpty()
        val readme = requestJson("https://api.github.com/repos/$repo/readme") ?: error("README no disponible")
        val encoded = readme.optString("content").replace("\n", "")
        val markdown = String(Base64.decode(encoded, Base64.DEFAULT), Charsets.UTF_8)
        val categoryTools = linkedMapOf<String, CatalogTool>()
        var category = "General"
        markdown.replace("\r", "").lines().forEach { raw ->
            val line = raw.trim()
            if (line.startsWith("#")) {
                category = line.trimStart('#').trim().replace(Regex("[`*_#]"), "").take(70).ifBlank { "General" }
            }
            githubRegex.findAll(line).forEach { m ->
                val owner = m.groupValues[1]
                var name = m.groupValues[2].trimEnd('.', ',', ')', ']', ';', ':')
                if (name.endsWith(".git", true)) name = name.dropLast(4)
                val discovered = "$owner/$name"
                if (!discovered.equals(repo, true)) categoryTools.putIfAbsent(discovered.lowercase(), CatalogTool(discovered, category, repo))
            }
        }
        return commit to categoryTools.values.sortedWith(compareBy<CatalogTool> { it.category }.thenBy { it.repository })
    }

    private fun requestJson(url: String): JSONObject? {
        val c = URL(url).openConnection() as HttpURLConnection
        try {
            c.requestMethod = "GET"; c.connectTimeout = 12_000; c.readTimeout = 25_000; c.instanceFollowRedirects = true
            c.setRequestProperty("User-Agent", "NexoKit-Android-Discovery/0.9")
            c.setRequestProperty("Accept", "application/vnd.github+json")
            val code = c.responseCode
            if (code !in 200..299) error("GitHub HTTP $code")
            return JSONObject(c.inputStream.bufferedReader().use { it.readText() })
        } finally { c.disconnect() }
    }

    fun markSeen(context: Context): MutableList<CatalogTool> = loadTools(context).also { list ->
        list.forEach { it.isNew = false }
        saveTools(context, list)
        val sources = loadSources(context).also { it.forEach { s -> s.newCount = 0 } }
        saveSources(context, sources)
    }

    fun formatSources(sources: List<CatalogSource>): String = if (sources.isEmpty()) "Sin fuentes." else sources.mapIndexed { i, s ->
        "${i + 1}. ${s.repository}\n   ${if (s.enabled) "ACTIVA" else "PAUSADA"} · cada ${s.intervalHours} h · ${s.status}"
    }.joinToString("\n\n")

    fun formatTools(tools: List<CatalogTool>, limit: Int = 80): String {
        if (tools.isEmpty()) return "Todavía no hay recursos. Tocá REVISAR CATÁLOGOS."
        val ordered = tools.sortedWith(compareByDescending<CatalogTool> { it.isNew }.thenBy { it.category }.thenBy { it.repository })
        val shown = ordered.take(limit)
        return buildString {
            shown.forEach { t ->
                append(if (t.isNew) "🆕 " else "• ")
                append(t.repository).append("\n   ").append(t.category).append("\n")
            }
            if (ordered.size > shown.size) append("\n… y ${ordered.size - shown.size} más")
        }.trim()
    }
}
