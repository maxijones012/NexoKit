package com.max.kitherramientas

import java.net.Inet4Address
import java.net.InetAddress
import java.util.Locale
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit

object NetworkToolkit {
    data class CidrInfo(
        val address: String,
        val prefix: Int,
        val mask: String,
        val wildcard: String,
        val network: String,
        val broadcast: String,
        val firstHost: String,
        val lastHost: String,
        val addressCount: Long,
        val usableHosts: Long
    )

    data class MacInfo(val normalized: String?, val oui: String?, val status: String)

    fun dnsLookup(queryRaw: String): String {
        val query = queryRaw.trim().take(253)
        if (query.isBlank()) return "Ingresá un dominio o una dirección IP."
        return try {
            val addresses = InetAddress.getAllByName(query).distinctBy { it.hostAddress }
            buildString {
                appendLine("Consulta: $query")
                addresses.forEach { addr ->
                    val kind = if (addr is Inet4Address) "IPv4" else "IPv6"
                    appendLine("$kind: ${addr.hostAddress}")
                }
                if (isIpv4(query) || query.contains(':')) {
                    val reverse = InetAddress.getByName(query).canonicalHostName
                    if (reverse.isNotBlank() && reverse != query) appendLine("Nombre: $reverse")
                }
            }.trimEnd()
        } catch (e: Exception) {
            "DNS: ${e.message ?: "sin respuesta"}"
        }
    }

    fun pingWindow(targetRaw: String, count: Int = 10, timeoutMs: Int = 1200): String {
        val target = targetRaw.trim().take(253)
        if (target.isBlank()) return "Ingresá un destino."
        val safeCount = count.coerceIn(1, 30)
        val times = mutableListOf<Long>()
        val lines = mutableListOf<String>()
        repeat(safeCount) { index ->
            val started = System.nanoTime()
            val ok = try { InetAddress.getByName(target).isReachable(timeoutMs.coerceIn(250, 5000)) } catch (_: Exception) { false }
            val elapsed = (System.nanoTime() - started) / 1_000_000
            if (ok) {
                times += elapsed
                lines += String.format(Locale.US, "%02d: ~%d ms", index + 1, elapsed)
            } else {
                lines += String.format(Locale.US, "%02d: sin respuesta", index + 1)
            }
            if (index + 1 < safeCount) Thread.sleep(120)
        }
        val received = times.size
        val loss = (safeCount - received) * 100.0 / safeCount
        return buildString {
            appendLine("PING · $target")
            lines.forEach { appendLine(it) }
            appendLine()
            appendLine("Enviados: $safeCount · Recibidos: $received · Pérdida: ${String.format(Locale.US, "%.1f", loss)}%")
            if (times.isNotEmpty()) append("Mín: ${times.minOrNull()} ms · Prom: ${String.format(Locale.US, "%.1f", times.average())} ms · Máx: ${times.maxOrNull()} ms")
            else append("Android puede no recibir ICMP aunque el equipo esté activo.")
        }.trimEnd()
    }

    fun traceRoute(targetRaw: String, maxHops: Int = 16): String {
        val target = targetRaw.trim().take(253)
        if (target.isBlank()) return "Ingresá un destino."
        val fromRegex = Regex("(?i)\\bfrom\\s+((?:\\d{1,3}\\.){3}\\d{1,3})")
        val rows = mutableListOf<String>()
        val destination = try { InetAddress.getByName(target).hostAddress } catch (_: Exception) { null }
        for (ttl in 1..maxHops.coerceIn(1, 20)) {
            try {
                val process = ProcessBuilder("/system/bin/ping", "-c", "1", "-W", "1", "-t", ttl.toString(), target)
                    .redirectErrorStream(true)
                    .start()
                process.waitFor(2200, TimeUnit.MILLISECONDS)
                val text = process.inputStream.bufferedReader().use { it.readText() }
                if (process.isAlive) process.destroyForcibly()
                val hop = fromRegex.find(text)?.groupValues?.getOrNull(1)
                rows += String.format(Locale.US, "%02d  %s", ttl, hop ?: "*")
                if (destination != null && hop == destination) break
            } catch (e: Exception) {
                return "Traceroute no disponible en este Android (${e.message ?: "comando ping restringido"})."
            }
        }
        return buildString {
            appendLine("TRACEROUTE · $target")
            rows.forEach { appendLine(it) }
            append("Los * pueden significar filtrado o falta de respuesta ICMP.")
        }
    }

    fun calculateCidr(ipText: String, prefix: Int): CidrInfo {
        require(prefix in 0..32) { "El prefijo debe estar entre 0 y 32." }
        val value = ipv4ToLong(ipText) ?: throw IllegalArgumentException("Ingresá una IPv4 válida.")
        val mask = if (prefix == 0) 0L else (0xffffffffL shl (32 - prefix)) and 0xffffffffL
        val wildcard = mask.inv() and 0xffffffffL
        val network = value and mask
        val broadcast = network or wildcard
        val count = 1L shl (32 - prefix)
        val (first, last, usable) = when {
            prefix <= 30 -> Triple(network + 1, broadcast - 1, (count - 2).coerceAtLeast(0))
            prefix == 31 -> Triple(network, broadcast, 2L)
            else -> Triple(value, value, 1L)
        }
        return CidrInfo(
            longToIpv4(value), prefix, longToIpv4(mask), longToIpv4(wildcard),
            longToIpv4(network), longToIpv4(broadcast), longToIpv4(first), longToIpv4(last), count, usable
        )
    }

    fun formatCidr(c: CidrInfo): String = buildString {
        appendLine("IP: ${c.address}/${c.prefix}")
        appendLine("Máscara: ${c.mask}")
        appendLine("Wildcard: ${c.wildcard}")
        appendLine("Red: ${c.network}")
        appendLine("Broadcast: ${c.broadcast}")
        appendLine("Hosts: ${c.firstHost} — ${c.lastHost}")
        append("Direcciones: ${c.addressCount} · Utilizables: ${c.usableHosts}")
    }

    fun inspectMac(input: String): MacInfo {
        val hex = input.replace(Regex("[^0-9A-Fa-f]"), "").uppercase(Locale.US)
        if (hex.length != 12) return MacInfo(null, null, "Ingresá una MAC de 12 dígitos hexadecimales.")
        val normalized = (0 until 6).joinToString(":") { hex.substring(it * 2, it * 2 + 2) }
        val oui = hex.substring(0, 6).chunked(2).joinToString(":")
        val first = hex.substring(0, 2).toInt(16)
        val status = when {
            first and 0x02 != 0 -> "MAC local/aleatorizada: el OUI puede no representar al fabricante real."
            first and 0x01 != 0 -> "Dirección multicast/grupal."
            else -> "OUI extraído. Android no expone de forma fiable las MAC de otros equipos sin privilegios."
        }
        return MacInfo(normalized, oui, status)
    }

    fun discoverLocal24(localIp: String, progress: (String) -> Unit): List<String> {
        val parts = localIp.trim().split('.')
        if (parts.size != 4 || parts.any { it.toIntOrNull() !in 0..255 }) return emptyList()
        val prefix = parts.take(3).joinToString(".")
        progress("Explorando $prefix.0/24…")

        val pool = Executors.newFixedThreadPool(32)
        val live = java.util.Collections.synchronizedList(mutableListOf<String>())
        for (host in 1..254) {
            pool.execute {
                val ip = "$prefix.$host"
                if (ip == localIp) {
                    live += ip
                } else {
                    val ok = try { InetAddress.getByName(ip).isReachable(280) } catch (_: Exception) { false }
                    if (ok) live += ip
                }
            }
        }
        pool.shutdown()
        pool.awaitTermination(18, TimeUnit.SECONDS)
        progress("${live.size} respuestas · resolviendo nombres…")
        val sorted = live.distinct().sortedBy { it.substringAfterLast('.').toIntOrNull() ?: 999 }
        val dnsPool = Executors.newFixedThreadPool(12)
        val futures = sorted.associateWith { ip ->
            dnsPool.submit<String> {
                try {
                    val canonical = InetAddress.getByName(ip).canonicalHostName
                    canonical.takeIf { it != ip } ?: "—"
                } catch (_: Exception) { "—" }
            }
        }
        dnsPool.shutdown()
        dnsPool.awaitTermination(5, TimeUnit.SECONDS)
        return sorted.map { ip ->
            val future = futures[ip]
            val name = try {
                if (future != null && future.isDone && !future.isCancelled) future.get() else "—"
            } catch (_: Exception) { "—" }
            "$ip · $name"
        }
    }

    private fun isIpv4(value: String): Boolean = ipv4ToLong(value) != null

    private fun ipv4ToLong(value: String): Long? {
        val parts = value.trim().split('.')
        if (parts.size != 4) return null
        var out = 0L
        for (part in parts) {
            val v = part.toIntOrNull() ?: return null
            if (v !in 0..255) return null
            out = (out shl 8) or v.toLong()
        }
        return out and 0xffffffffL
    }

    private fun longToIpv4(value: Long): String {
        val v = value and 0xffffffffL
        return listOf(24, 16, 8, 0).joinToString(".") { shift -> ((v shr shift) and 0xff).toString() }
    }
}
