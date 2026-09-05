package com.max.kitherramientas

import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder

data class MetaScanAndroidBundle(
    val username: String,
    val profile: JSONObject?,
    val businessHome: JSONObject?,
    val businessAbout: JSONObject?,
    val businessTransparency: JSONObject?,
    val errors: Map<String, String>
)

object MetaScanClient {
    private const val HOST = "facebook-pages-scraper3.p.rapidapi.com"
    private const val PROFILE = "/get-profile-home-page-details"
    private const val BUSINESS_HOME = "/get-business-home-page-details"
    private const val BUSINESS_ABOUT = "/get-business-about-details-page"
    private const val BUSINESS_TRANSPARENCY = "/get-business-about-profile-transparency-page-details"

    fun normalizeUsername(input: String): String {
        var value = input.trim().removePrefix("@").trim()
        runCatching {
            val uri = java.net.URI(value)
            if (uri.host?.contains("facebook.com", ignoreCase = true) == true) {
                value = uri.path.trim('/').split('/').firstOrNull().orEmpty()
            }
        }
        return value.trim().trim('/')
    }

    fun scan(target: String, apiKey: String, includeBusiness: Boolean = true): MetaScanAndroidBundle {
        val username = normalizeUsername(target)
        require(username.isNotBlank()) { "Ingresá un usuario o URL de Facebook." }
        require(apiKey.isNotBlank()) { "Ingresá tu API key de RapidAPI." }

        var profile: JSONObject? = null
        var businessHome: JSONObject? = null
        var businessAbout: JSONObject? = null
        var businessTransparency: JSONObject? = null
        val errors = linkedMapOf<String, String>()

        runCatching { request(PROFILE, username, apiKey, true) }
            .onSuccess { profile = it }
            .onFailure { errors["Perfil"] = it.message ?: it.javaClass.simpleName }

        if (includeBusiness) {
            runCatching { request(BUSINESS_HOME, username, apiKey, false) }
                .onSuccess { businessHome = it }
                .onFailure { errors["Business Home"] = it.message ?: it.javaClass.simpleName }
            runCatching { request(BUSINESS_ABOUT, username, apiKey, false) }
                .onSuccess { businessAbout = it }
                .onFailure { errors["About"] = it.message ?: it.javaClass.simpleName }
            runCatching { request(BUSINESS_TRANSPARENCY, username, apiKey, false) }
                .onSuccess { businessTransparency = it }
                .onFailure { errors["Transparencia"] = it.message ?: it.javaClass.simpleName }
        }

        return MetaScanAndroidBundle(username, profile, businessHome, businessAbout, businessTransparency, errors)
    }

    private fun request(path: String, username: String, apiKey: String, includeUrl: Boolean): JSONObject {
        val fb = "https://www.facebook.com/$username"
        var query = "urlSupplier=${URLEncoder.encode(fb, "UTF-8")}" 
        if (includeUrl) query += "&url=${URLEncoder.encode(fb, "UTF-8")}" 
        val c = URL("https://$HOST$path?$query").openConnection() as HttpURLConnection
        try {
            c.requestMethod = "GET"
            c.connectTimeout = 15_000
            c.readTimeout = 45_000
            c.instanceFollowRedirects = true
            c.setRequestProperty("User-Agent", "NexoKit-MetaScan-Android/1.0")
            c.setRequestProperty("x-rapidapi-host", HOST)
            c.setRequestProperty("x-rapidapi-key", apiKey.trim())
            val code = c.responseCode
            val text = if (code in 200..299) c.inputStream.bufferedReader().use { it.readText() }
            else c.errorStream?.bufferedReader()?.use { it.readText() }.orEmpty()
            if (code !in 200..299) error("HTTP $code: ${text.replace('\n', ' ').take(220)}")
            return JSONObject(text)
        } finally {
            c.disconnect()
        }
    }

    fun format(bundle: MetaScanAndroidBundle): String = buildString {
        append("META SCAN · @${bundle.username}\n")
        append("────────────────────────\n")
        val p = bundle.profile
        if (p != null) {
            appendKnown(this, p, "Nombre", "name")
            appendKnown(this, p, "ID", "id")
            appendKnown(this, p, "Email", "email")
            appendKnown(this, p, "Teléfono", "phone")
            appendKnown(this, p, "Web", "website")
            appendKnown(this, p, "Seguidores", "followers")
            appendKnown(this, p, "Me gusta", "likes")
            appendKnown(this, p, "Descripción", "best_description")
            appendKnown(this, p, "Perfil", "profile_url")
        } else append("Perfil: sin datos\n")

        bundle.businessTransparency?.let {
            append("\nTRANSPARENCIA\n")
            appendKnown(this, it, "Estado anuncios", "ad_status")
            appendKnown(this, it, "Fecha creación", "creation_date")
        }

        if (bundle.errors.isNotEmpty()) {
            append("\nAVISOS\n")
            bundle.errors.forEach { (k, v) -> append("• $k: $v\n") }
        }

        append("\nJSON CRUDO\n")
        append(toJson(bundle).toString(2))
    }

    fun toJson(bundle: MetaScanAndroidBundle): JSONObject = JSONObject().apply {
        put("username", bundle.username)
        put("profile", bundle.profile ?: JSONObject.NULL)
        put("business_home", bundle.businessHome ?: JSONObject.NULL)
        put("business_about", bundle.businessAbout ?: JSONObject.NULL)
        put("business_transparency", bundle.businessTransparency ?: JSONObject.NULL)
        put("errors", JSONObject(bundle.errors))
    }

    private fun appendKnown(builder: StringBuilder, root: JSONObject, label: String, key: String) {
        val value = find(root, key) ?: return
        if (value.isNotBlank() && value != "null") builder.append("$label: $value\n")
    }

    private fun find(value: Any?, key: String): String? {
        when (value) {
            is JSONObject -> {
                val names = value.keys()
                while (names.hasNext()) {
                    val name = names.next()
                    val child = value.opt(name)
                    if (name.equals(key, ignoreCase = true)) return child?.toString()
                    val nested = find(child, key)
                    if (nested != null) return nested
                }
            }
            is org.json.JSONArray -> {
                for (i in 0 until value.length()) {
                    val nested = find(value.opt(i), key)
                    if (nested != null) return nested
                }
            }
        }
        return null
    }
}
