package com.max.kitherramientas

import java.net.DatagramPacket
import java.net.DatagramSocket
import java.nio.ByteBuffer
import java.nio.ByteOrder
import kotlin.concurrent.thread
import kotlin.math.abs
import kotlin.math.sqrt

data class CsiNodeSnapshot(
    val nodeId: Int,
    val sourceIp: String,
    val lastSeenMs: Long,
    val packets: Long,
    val rawFrames: Long,
    val vitalsFrames: Long,
    val rssi: Int,
    val frequencyMhz: Int,
    val subcarriers: Int,
    val presence: Boolean?,
    val motion: Boolean,
    val persons: Int,
    val motionEnergy: Float,
    val presenceScore: Float,
    val breathingBpm: Double,
    val heartBpm: Double,
    val rawActivity: Double,
    val state: String
)

class CsiUdpReceiver(
    private val onUpdate: (List<CsiNodeSnapshot>, Long, Long) -> Unit,
    private val onStatus: (String) -> Unit
) {
    companion object {
        const val DEFAULT_PORT = 5005
        private const val RAW_MAGIC = 0xC5110001L
        private const val VITALS_MAGIC = 0xC5110002L
    }

    @Volatile private var running = false
    private var socket: DatagramSocket? = null
    private val nodes = linkedMapOf<Int, MutableNode>()
    var totalPackets: Long = 0
        private set
    var invalidPackets: Long = 0
        private set

    fun start(port: Int = DEFAULT_PORT) {
        if (running) return
        running = true
        thread(name = "csi-udp", isDaemon = true) {
            try {
                val sock = DatagramSocket(port)
                sock.reuseAddress = true
                socket = sock
                onStatus("Escuchando CSI por UDP :$port")
                val buffer = ByteArray(4096)
                while (running) {
                    val packet = DatagramPacket(buffer, buffer.size)
                    sock.receive(packet)
                    val data = packet.data.copyOfRange(packet.offset, packet.offset + packet.length)
                    parse(data, packet.address.hostAddress ?: "—")
                }
            } catch (e: Exception) {
                if (running) onStatus("CSI: ${e.message ?: "error de recepción"}")
            } finally {
                running = false
                try { socket?.close() } catch (_: Exception) { }
                socket = null
            }
        }
    }

    fun stop() {
        running = false
        try { socket?.close() } catch (_: Exception) { }
        socket = null
        onStatus("Receptor CSI detenido")
    }

    fun isRunning(): Boolean = running

    @Synchronized
    fun clear() {
        nodes.clear()
        totalPackets = 0
        invalidPackets = 0
        publish()
    }

    private fun parse(data: ByteArray, sourceIp: String) {
        if (data.size < 5) { invalidPackets++; return }
        val bb = ByteBuffer.wrap(data).order(ByteOrder.LITTLE_ENDIAN)
        val magic = bb.int.toLong() and 0xffffffffL
        when (magic) {
            RAW_MAGIC -> parseRaw(data, sourceIp)
            VITALS_MAGIC -> parseVitals(data, sourceIp)
            else -> { invalidPackets++; return }
        }
        totalPackets++
        publish()
    }

    @Synchronized
    private fun parseRaw(data: ByteArray, sourceIp: String) {
        if (data.size < 20) { invalidPackets++; return }
        val bb = ByteBuffer.wrap(data).order(ByteOrder.LITTLE_ENDIAN)
        bb.position(4)
        val nodeId = bb.get().toInt() and 0xff
        val antennas = (bb.get().toInt() and 0xff).coerceAtLeast(1)
        val subcarriers = bb.short.toInt() and 0xffff
        val freq = bb.int
        bb.int // sequence
        val rssi = bb.get().toInt()
        bb.get() // noise floor
        bb.short // reserved

        val expectedPairs = antennas * subcarriers
        val availablePairs = minOf(expectedPairs, ((data.size - 20) / 2).coerceAtLeast(0))
        var sumAmp = 0.0
        var offset = 20
        repeat(availablePairs) {
            val ii = data[offset].toInt()
            val qq = data[offset + 1].toInt()
            sumAmp += sqrt((ii * ii + qq * qq).toDouble())
            offset += 2
        }
        val meanAmp = if (availablePairs > 0) sumAmp / availablePairs else 0.0
        val node = nodes.getOrPut(nodeId) { MutableNode(nodeId = nodeId) }
        val delta = if (node.lastMeanAmplitude > 0) abs(meanAmp - node.lastMeanAmplitude) / node.lastMeanAmplitude * 100.0 else 0.0
        node.rawActivity = if (node.rawFrames == 0L) delta else node.rawActivity * 0.78 + delta * 0.22
        node.lastMeanAmplitude = meanAmp
        node.rawFrames++
        node.packets++
        node.rssi = rssi
        node.frequencyMhz = freq
        node.subcarriers = subcarriers
        node.sourceIp = sourceIp
        node.lastSeenMs = System.currentTimeMillis()
    }

    @Synchronized
    private fun parseVitals(data: ByteArray, sourceIp: String) {
        if (data.size < 32) { invalidPackets++; return }
        val bb = ByteBuffer.wrap(data).order(ByteOrder.LITTLE_ENDIAN)
        bb.position(4)
        val nodeId = bb.get().toInt() and 0xff
        val flags = bb.get().toInt() and 0xff
        val breathingRaw = bb.short.toInt() and 0xffff
        val heartRaw = bb.int.toLong() and 0xffffffffL
        val rssi = bb.get().toInt()
        val persons = bb.get().toInt() and 0xff
        bb.short
        val motionEnergy = bb.float
        val presenceScore = bb.float

        val node = nodes.getOrPut(nodeId) { MutableNode(nodeId = nodeId) }
        node.vitalsFrames++
        node.packets++
        node.rssi = rssi
        node.presence = (flags and 0x01) != 0
        node.fall = (flags and 0x02) != 0
        node.motion = (flags and 0x04) != 0
        node.persons = persons
        node.breathingBpm = breathingRaw / 100.0
        node.heartBpm = heartRaw / 10000.0
        node.motionEnergy = motionEnergy
        node.presenceScore = presenceScore
        node.sourceIp = sourceIp
        node.lastSeenMs = System.currentTimeMillis()
    }

    @Synchronized
    private fun publish() {
        onUpdate(nodes.values.sortedBy { it.nodeId }.map { it.snapshot() }, totalPackets, invalidPackets)
    }

    private data class MutableNode(
        val nodeId: Int,
        var sourceIp: String = "—",
        var lastSeenMs: Long = 0,
        var packets: Long = 0,
        var rawFrames: Long = 0,
        var vitalsFrames: Long = 0,
        var rssi: Int = 0,
        var frequencyMhz: Int = 0,
        var subcarriers: Int = 0,
        var presence: Boolean? = null,
        var motion: Boolean = false,
        var fall: Boolean = false,
        var persons: Int = 0,
        var motionEnergy: Float = 0f,
        var presenceScore: Float = 0f,
        var breathingBpm: Double = 0.0,
        var heartBpm: Double = 0.0,
        var rawActivity: Double = 0.0,
        var lastMeanAmplitude: Double = 0.0
    ) {
        fun snapshot(): CsiNodeSnapshot {
            val state = when {
                fall -> "ALERTA CAÍDA"
                presence == true && motion -> "PRESENCIA + MOVIMIENTO"
                presence == true -> "PRESENCIA"
                presence == false -> "SIN PRESENCIA"
                rawFrames > 0 -> "CSI RAW"
                else -> "SIN DATOS"
            }
            return CsiNodeSnapshot(nodeId, sourceIp, lastSeenMs, packets, rawFrames, vitalsFrames, rssi,
                frequencyMhz, subcarriers, presence, motion, persons, motionEnergy, presenceScore,
                breathingBpm, heartBpm, rawActivity, state)
        }
    }
}
