package com.max.kitherramientas

import android.app.job.JobInfo
import android.app.job.JobParameters
import android.app.job.JobScheduler
import android.app.job.JobService
import android.content.ComponentName
import android.content.Context
import android.os.Environment
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.net.HttpURLConnection
import java.net.URL
import java.util.concurrent.TimeUnit
import kotlin.concurrent.thread

data class RepoWatch(
    var repository: String,
    var enabled: Boolean = true,
    var autoDownload: Boolean = true,
    var intervalHours: Int = 6,
    var lastDownloadedId: String = "",
    var latestId: String = "",
    var latestName: String = "",
    var lastCheckedMs: Long = 0L,
    var lastDownloadPath: String = "",
    var status: String = "Pendiente"
)

data class RepoRemoteVersion(
    val id: String,
    val name: String,
    val downloadUrl: String,
    val fileName: String,
    val isRelease: Boolean
)

object RepositoryUpdater {
    private const val PREFS = "repo_updater_r8"
    private const val KEY_REPOS = "repositories"
    private const val GLOBAL_CHECK_HOURS = 1L
    private const val JOB_ID = 8808

    fun normalizeRepository(input: String): String? {
        val raw = input.trim().trimEnd('/')
        if (raw.isBlank()) return null
        return try {
            val uri = java.net.URI(raw)
            if (uri.scheme != null && uri.host?.endsWith("github.com", true) == true) {
                val parts = uri.path.trim('/').split('/').filter { it.isNotBlank() }
                if (parts.size >= 2) "${parts[0]}/${parts[1].removeSuffix(".git")}" else null
            } else {
                val parts = raw.removeSuffix(".git").split('/').filter { it.isNotBlank() }
                if (parts.size == 2) "${parts[0]}/${parts[1]}" else null
            }
        } catch (_: Exception) {
            val parts = raw.removeSuffix(".git").split('/').filter { it.isNotBlank() }
            if (parts.size == 2) "${parts[0]}/${parts[1]}" else null
        }
    }

    fun load(context: Context): MutableList<RepoWatch> {
        val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        val text = prefs.getString(KEY_REPOS, null)
        if (!text.isNullOrBlank()) {
            try {
                val arr = JSONArray(text)
                val result = mutableListOf<RepoWatch>()
                for (i in 0 until arr.length()) {
                    val o = arr.getJSONObject(i)
                    result += RepoWatch(
                        repository = o.optString("repository"),
                        enabled = o.optBoolean("enabled", true),
                        autoDownload = o.optBoolean("autoDownload", true),
                        intervalHours = o.optInt("intervalHours", 6).coerceIn(1, 168),
                        lastDownloadedId = o.optString("lastDownloadedId"),
                        latestId = o.optString("latestId"),
                        latestName = o.optString("latestName"),
                        lastCheckedMs = o.optLong("lastCheckedMs", 0L),
                        lastDownloadPath = o.optString("lastDownloadPath"),
                        status = o.optString("status", "Pendiente")
                    )
                }
                if (result.isNotEmpty()) {
                    // Migración de una sola vez para instalaciones R9 existentes.
                    // Si el usuario luego elimina Meta Scan, no se vuelve a agregar.
                    if (!prefs.getBoolean("seed_meta_scan_v1", false)) {
                        if (result.none { it.repository.equals("HackUnderway/meta_scan", ignoreCase = true) })
                            result += RepoWatch("HackUnderway/meta_scan", intervalHours = 12, autoDownload = true)
                        save(context, result)
                        prefs.edit().putBoolean("seed_meta_scan_v1", true).apply()
                    }
                    return result
                }
            } catch (_: Exception) { }
        }

        val defaults = mutableListOf(
            RepoWatch("maxijones012/PruebaRepositorio"),
            RepoWatch("maxijones012/FACELY-Releases"),
            RepoWatch("maxijones012/IrisTrack_AI"),
            RepoWatch("HackUnderway/meta_scan", intervalHours = 12, autoDownload = true)
        )
        save(context, defaults)
        prefs.edit().putBoolean("seed_meta_scan_v1", true).apply()
        return defaults
    }

    fun save(context: Context, repositories: List<RepoWatch>) {
        val arr = JSONArray()
        repositories.forEach { r ->
            arr.put(JSONObject().apply {
                put("repository", r.repository)
                put("enabled", r.enabled)
                put("autoDownload", r.autoDownload)
                put("intervalHours", r.intervalHours.coerceIn(1, 168))
                put("lastDownloadedId", r.lastDownloadedId)
                put("latestId", r.latestId)
                put("latestName", r.latestName)
                put("lastCheckedMs", r.lastCheckedMs)
                put("lastDownloadPath", r.lastDownloadPath)
                put("status", r.status)
            })
        }
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putString(KEY_REPOS, arr.toString()).apply()
    }

    fun isDue(item: RepoWatch, nowMs: Long = System.currentTimeMillis()): Boolean {
        if (!item.enabled) return false
        if (item.lastCheckedMs <= 0L) return true
        val interval = TimeUnit.HOURS.toMillis(item.intervalHours.coerceIn(1, 168).toLong())
        return nowMs - item.lastCheckedMs >= interval
    }

    fun schedule(context: Context) {
        val scheduler = context.getSystemService(Context.JOB_SCHEDULER_SERVICE) as JobScheduler
        val component = ComponentName(context, RepositoryUpdateJobService::class.java)
        val job = JobInfo.Builder(JOB_ID, component)
            .setRequiredNetworkType(JobInfo.NETWORK_TYPE_UNMETERED)
            .setPeriodic(TimeUnit.HOURS.toMillis(GLOBAL_CHECK_HOURS))
            .setPersisted(true)
            .build()
        scheduler.schedule(job)
    }

    fun checkDue(context: Context, force: Boolean = false, onProgress: ((String) -> Unit)? = null): MutableList<RepoWatch> {
        val repos = load(context)
        for (repo in repos) {
            if (!repo.enabled) continue
            if (!force && !isDue(repo)) continue
            checkOne(context, repo, onProgress)
            save(context, repos)
        }
        return repos
    }

    fun checkOne(context: Context, repo: RepoWatch, onProgress: ((String) -> Unit)? = null) {
        repo.status = "Buscando actualización…"
        onProgress?.invoke("Revisando ${repo.repository}…")
        try {
            val latest = getLatest(repo.repository)
            repo.latestId = latest.id
            repo.latestName = latest.name
            repo.lastCheckedMs = System.currentTimeMillis()
            if (repo.lastDownloadedId.equals(latest.id, true)) {
                repo.status = "AL DÍA"
                onProgress?.invoke("${repo.repository}: al día")
            } else if (!repo.autoDownload) {
                repo.status = "ACTUALIZACIÓN DISPONIBLE"
                onProgress?.invoke("${repo.repository}: ${latest.name} disponible")
            } else {
                repo.status = "Descargando…"
                onProgress?.invoke("${repo.repository}: descargando ${latest.name}…")
                val file = download(context, repo.repository, latest)
                repo.lastDownloadedId = latest.id
                repo.lastDownloadPath = file.absolutePath
                repo.status = "DESCARGADA · ${file.name}"
                onProgress?.invoke("${repo.repository}: descargada. No se instala sola.")
            }
        } catch (e: Exception) {
            repo.lastCheckedMs = System.currentTimeMillis()
            repo.status = "ERROR · ${e.message ?: e.javaClass.simpleName}"
            onProgress?.invoke("${repo.repository}: ${e.message ?: "error"}")
        }
    }

    private fun getLatest(repository: String): RepoRemoteVersion {
        val repo = normalizeRepository(repository) ?: error("Repositorio inválido")
        val release = requestJson("https://api.github.com/repos/$repo/releases/latest", allow404 = true)
        if (release != null) {
            val tag = release.optString("tag_name", "release")
            val name = release.optString("name").ifBlank { tag }
            val assets = release.optJSONArray("assets") ?: JSONArray()
            var chosenName: String? = null
            var chosenUrl: String? = null
            var chosenRank = 999
            for (i in 0 until assets.length()) {
                val a = assets.optJSONObject(i) ?: continue
                val n = a.optString("name")
                val u = a.optString("browser_download_url")
                val rank = androidAssetRank(n)
                if (u.isNotBlank() && rank < chosenRank) {
                    chosenRank = rank
                    chosenName = n
                    chosenUrl = u
                }
            }
            if (chosenUrl != null && chosenName != null && chosenRank < 100) {
                return RepoRemoteVersion(tag, name, chosenUrl, chosenName, true)
            }
            val zip = release.optString("zipball_url")
            if (zip.isNotBlank()) {
                return RepoRemoteVersion(tag, name, zip, "${safe(repo.replace('/', '_'))}_${safe(tag)}_source.zip", true)
            }
        }

        val info = requestJson("https://api.github.com/repos/$repo")
            ?: error("Repositorio no encontrado o privado. Los privados necesitan autenticación segura.")
        val branch = info.optString("default_branch", "main")
        val commit = requestJson("https://api.github.com/repos/$repo/commits/${java.net.URLEncoder.encode(branch, "UTF-8")}")
            ?: error("No se pudo leer el commit actual")
        val sha = commit.optString("sha")
        if (sha.isBlank()) error("GitHub no devolvió SHA")
        return RepoRemoteVersion(
            id = sha,
            name = "$branch · ${sha.take(8)}",
            downloadUrl = "https://github.com/$repo/archive/refs/heads/${java.net.URLEncoder.encode(branch, "UTF-8")}.zip",
            fileName = "${safe(repo.replace('/', '_'))}_${safe(branch)}_${sha.take(8)}.zip",
            isRelease = false
        )
    }

    private fun requestJson(url: String, allow404: Boolean = false): JSONObject? {
        val connection = URL(url).openConnection() as HttpURLConnection
        try {
            connection.requestMethod = "GET"
            connection.connectTimeout = 12_000
            connection.readTimeout = 20_000
            connection.instanceFollowRedirects = true
            connection.setRequestProperty("User-Agent", "NexoKit-Android-Updater/0.9")
            connection.setRequestProperty("Accept", "application/vnd.github+json")
            val code = connection.responseCode
            if (code == 404 && allow404) return null
            if (code !in 200..299) {
                if (code == 404) return null
                val msg = runCatching { connection.errorStream?.bufferedReader()?.use { it.readText() } }.getOrNull().orEmpty()
                error("GitHub HTTP $code${if (msg.isNotBlank()) ": ${msg.take(120)}" else ""}")
            }
            val text = connection.inputStream.bufferedReader().use { it.readText() }
            return JSONObject(text)
        } finally {
            connection.disconnect()
        }
    }

    private fun download(context: Context, repository: String, version: RepoRemoteVersion): File {
        val repoName = safe((normalizeRepository(repository) ?: repository).replace('/', '_'))
        val base = context.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS) ?: context.filesDir
        val dir = File(base, "NexoKitUpdates/$repoName").apply { mkdirs() }
        val finalFile = File(dir, safe(version.fileName).ifBlank { "update_${System.currentTimeMillis()}.bin" })
        val part = File(finalFile.absolutePath + ".part")

        val connection = URL(version.downloadUrl).openConnection() as HttpURLConnection
        try {
            connection.requestMethod = "GET"
            connection.connectTimeout = 15_000
            connection.readTimeout = 60_000
            connection.instanceFollowRedirects = true
            connection.setRequestProperty("User-Agent", "NexoKit-Android-Updater/0.9")
            if (connection.responseCode !in 200..299) error("Descarga HTTP ${connection.responseCode}")
            connection.inputStream.use { input ->
                part.outputStream().buffered().use { output -> input.copyTo(output) }
            }
            if (finalFile.exists()) finalFile.delete()
            if (!part.renameTo(finalFile)) {
                part.copyTo(finalFile, overwrite = true)
                part.delete()
            }
            return finalFile
        } finally {
            connection.disconnect()
        }
    }

    fun format(repos: List<RepoWatch>): String {
        if (repos.isEmpty()) return "Sin repositorios."
        return repos.mapIndexed { index, r ->
            buildString {
                append("${index + 1}. ${r.repository}\n")
                append(if (r.enabled) "   ACTIVO" else "   PAUSADO")
                append(" · cada ${r.intervalHours} h")
                append(if (r.autoDownload) " · AUTO DESCARGA" else " · SOLO AVISA")
                append("\n   ${r.status}")
                if (r.latestName.isNotBlank()) append(" · ${r.latestName}")
                if (r.lastCheckedMs > 0) append("\n   última revisión: ${java.text.SimpleDateFormat("dd/MM HH:mm", java.util.Locale.getDefault()).format(java.util.Date(r.lastCheckedMs))}")
                if (r.lastDownloadPath.isNotBlank()) append("\n   archivo: ${File(r.lastDownloadPath).name}")
            }
        }.joinToString("\n\n")
    }

    private fun androidAssetRank(name: String): Int {
        val n = name.lowercase()
        return when {
            n.endsWith(".apk") -> 0
            n.endsWith(".aab") -> 1
            n.endsWith(".zip") && n.contains("android") -> 2
            n.endsWith(".zip") -> 3
            else -> 100
        }
    }

    private fun safe(value: String): String = value.replace(Regex("[^A-Za-z0-9._-]"), "_")
}


class RepositoryUpdateJobService : JobService() {
    override fun onStartJob(params: JobParameters): Boolean {
        thread(name = "repo-update-job") {
            try {
                RepositoryUpdater.checkDue(applicationContext, force = false)
                CatalogDiscovery.checkDue(applicationContext, force = false)
            } finally {
                jobFinished(params, false)
            }
        }
        return true
    }

    override fun onStopJob(params: JobParameters): Boolean = true
}
