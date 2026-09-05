package com.max.kitherramientas

import kotlin.math.abs
import kotlin.math.max
import kotlin.math.sqrt

enum class LabSensitivity(val displayName: String, val scale: Double) {
    HIGH("ALTA", 0.78),
    NORMAL("NORMAL", 1.0),
    LOW("BAJA", 1.30)
}

data class RssiBaseline(val mean: Double, val stdDev: Double, val samples: Int)

data class RssiLabReading(
    val timestampMs: Long,
    val rssi: Int,
    val baseline: Double,
    val delta: Double,
    val score: Double,
    val state: String,
    val marker: String? = null
)

class RssiLabEngine {
    var baseline: RssiBaseline? = null
        private set

    var sensitivity: LabSensitivity = LabSensitivity.NORMAL
    private var previousRssi: Int? = null

    fun clearCalibration() {
        baseline = null
        previousRssi = null
    }

    fun resetTracking() {
        previousRssi = null
    }

    fun calibrate(samples: List<Int>): RssiBaseline? {
        if (samples.size < 5) return null
        val mean = samples.average()
        val variance = samples.sumOf { (it - mean) * (it - mean) } / samples.size
        val std = max(1.0, sqrt(variance))
        return RssiBaseline(mean, std, samples.size).also {
            baseline = it
            previousRssi = samples.lastOrNull()
        }
    }

    fun evaluate(rssi: Int, timestampMs: Long = System.currentTimeMillis(), marker: String? = null): RssiLabReading? {
        val base = baseline ?: return null
        val delta = abs(rssi - base.mean)
        val jump = previousRssi?.let { abs(rssi - it).toDouble() } ?: 0.0
        val noise = max(1.25, base.stdDev + 0.60)
        val rawScore = max(delta / noise, (jump / noise) * 0.90)
        val score = rawScore / sensitivity.scale
        val state = when {
            score < 1.40 -> "ESTABLE"
            score < 2.40 -> "CAMBIO LEVE"
            score < 3.60 -> "CAMBIO FUERTE"
            else -> "VARIACIÓN COMPATIBLE CON MOVIMIENTO"
        }
        previousRssi = rssi
        return RssiLabReading(timestampMs, rssi, base.mean, delta, score, state, marker)
    }
}
