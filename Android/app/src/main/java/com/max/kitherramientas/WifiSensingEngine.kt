package com.max.kitherramientas

import kotlin.math.abs
import kotlin.math.max
import kotlin.math.sqrt
import java.util.ArrayDeque

data class WifiSensingBaseline(val mean: Double, val stdDev: Double, val samples: Int)

data class WifiSensingReading(
    val timestampMs: Long,
    val rssi: Int,
    val smoothedRssi: Double,
    val baseline: Double,
    val delta: Double,
    val score: Double,
    val confidence: Double,
    val state: String
)

class WifiSensingEngine {
    var baseline: WifiSensingBaseline? = null
        private set

    var windowSize: Int = 5
    var sensitivity: Double = 1.0
    private val window = ArrayDeque<Double>()
    private var previousSmoothed: Double? = null

    fun reset() {
        baseline = null
        window.clear()
        previousSmoothed = null
    }

    fun calibrate(samples: List<Int>): WifiSensingBaseline? {
        if (samples.size < 10) return null
        val mean = samples.average()
        val variance = samples.sumOf { (it - mean) * (it - mean) } / samples.size
        val std = max(0.95, sqrt(variance))
        val result = WifiSensingBaseline(mean, std, samples.size)
        baseline = result
        window.clear()
        samples.takeLast(max(1, windowSize)).forEach { window.addLast(it.toDouble()) }
        previousSmoothed = null
        return result
    }

    fun evaluate(rssi: Int, timestampMs: Long = System.currentTimeMillis()): WifiSensingReading? {
        val base = baseline ?: return null
        window.addLast(rssi.toDouble())
        while (window.size > max(1, windowSize)) window.removeFirst()
        val smoothed = window.average()
        val delta = abs(smoothed - base.mean)
        val jump = previousSmoothed?.let { abs(smoothed - it) } ?: 0.0
        val noise = max(1.0, base.stdDev + 0.55)
        val rawScore = max(delta / noise, (jump / noise) * 0.72)
        val score = rawScore / sensitivity.coerceIn(0.65, 1.45)
        val confidence = ((score - 0.95) / 3.0).coerceIn(0.0, 1.0)
        val state = when {
            score < 1.25 -> "AMBIENTE ESTABLE"
            score < 2.00 -> "VARIACIÓN LEVE"
            score < 3.20 -> "ACTIVIDAD RF ALTA"
            else -> "MOVIMIENTO PROBABLE"
        }
        previousSmoothed = smoothed
        return WifiSensingReading(timestampMs, rssi, smoothed, base.mean, delta, score, confidence, state)
    }
}
