package com.max.kitherramientas

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Path
import android.util.AttributeSet
import android.view.View
import kotlin.math.PI
import kotlin.math.max
import kotlin.math.min
import kotlin.math.sin

class CsiSceneView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : View(context, attrs) {

    private val paint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val stroke = Paint(Paint.ANTI_ALIAS_FLAG).apply { style = Paint.Style.STROKE }
    private var phase = 0.0
    private var nodes: List<CsiNodeSnapshot> = emptyList()
    private var wifiReading: WifiSensingReading? = null
    private var wifiCalibrated: Boolean = false
    private var wifiSensingRunning: Boolean = false
    private var wifiLabel: String = "ROUTER ↔ TELÉFONO"

    var demoMode: Boolean = true
        set(value) { field = value; invalidate() }
    var wifiDirectMode: Boolean = true
        set(value) { field = value; invalidate() }
    var showTracking: Boolean = true
        set(value) { field = value; invalidate() }
    var showHeatmap: Boolean = true
        set(value) { field = value; invalidate() }

    fun updateWifiSensing(reading: WifiSensingReading?, calibrated: Boolean, running: Boolean, linkLabel: String? = null) {
        wifiReading = reading
        wifiCalibrated = calibrated
        wifiSensingRunning = running
        if (!linkLabel.isNullOrBlank()) wifiLabel = linkLabel
        invalidate()
    }

    fun updateNodes(value: List<CsiNodeSnapshot>) {
        nodes = value
        invalidate()
    }

    override fun onAttachedToWindow() {
        super.onAttachedToWindow()
        post(animator)
    }

    override fun onDetachedFromWindow() {
        removeCallbacks(animator)
        super.onDetachedFromWindow()
    }

    private val animator = object : Runnable {
        override fun run() {
            phase += 0.06
            if (phase > PI * 2) phase -= PI * 2
            invalidate()
            postDelayed(this, 45L)
        }
    }

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        val w = width.toFloat().coerceAtLeast(1f)
        val h = height.toFloat().coerceAtLeast(1f)
        canvas.drawColor(Color.rgb(3, 8, 14))
        drawGrid(canvas, w, h)
        drawRings(canvas, w, h)
        if (demoMode || !wifiDirectMode) drawSensors(canvas, w, h)
        if (demoMode) drawDemo(canvas, w, h)
        else if (wifiDirectMode) drawWifiDirect(canvas, w, h)
        else drawReal(canvas, w, h)
        drawHud(canvas, w, h)
    }

    private fun drawGrid(c: Canvas, w: Float, h: Float) {
        stroke.strokeWidth = dp(1f)
        stroke.color = Color.argb(45, 30, 210, 235)
        val horizon = h * .58f
        val bottom = h * .94f
        for (i in -8..8) {
            val xb = w * .5f + i * (w / 12f)
            c.drawLine(w * .5f, horizon, xb, bottom, stroke)
        }
        for (i in 0..8) {
            val t = i / 8f
            val eased = t * t
            val y = horizon + eased * (bottom - horizon)
            val half = w * .46f * eased
            c.drawLine(w * .5f - half, y, w * .5f + half, y, stroke)
        }
    }

    private fun drawRings(c: Canvas, w: Float, h: Float) {
        val centers = arrayOf(w * .46f to h * .48f, w * .72f to h * .50f)
        centers.forEachIndexed { idx, pair ->
            for (i in 0..3) {
                val pulse = ((sin(phase + i * .8 + idx) + 1.0) * .5).toFloat()
                val rx = dp(38f + i * 25f) + pulse * dp(4f)
                val ry = rx * .72f
                stroke.strokeWidth = dp(1f)
                stroke.color = Color.argb(max(18, 72 - i * 13), 35, 105, 255)
                c.drawOval(pair.first - rx, pair.second - ry, pair.first + rx, pair.second + ry, stroke)
            }
        }
    }

    private fun drawSensors(c: Canvas, w: Float, h: Float) {
        val points = arrayOf(w * .16f to h * .72f, w * .50f to h * .79f, w * .84f to h * .72f)
        points.forEachIndexed { i, p ->
            paint.style = Paint.Style.FILL
            paint.color = Color.argb(115, 0, 226, 255)
            c.drawCircle(p.first, p.second, dp(5f), paint)
            stroke.color = Color.argb(85, 0, 226, 255)
            stroke.strokeWidth = dp(1f)
            c.drawCircle(p.first, p.second, dp((12.0 + (sin(phase + i) + 1.0) * 1.5).toFloat()), stroke)
            drawText(c, "S${i + 1}", p.first - dp(8f), p.second + dp(18f), 9f, Color.rgb(90, 221, 238), true)
        }
    }

    private fun drawDemo(c: Canvas, w: Float, h: Float) {
        val bob1 = (sin(phase) * dp(2f)).toFloat()
        val bob2 = (sin(phase + 1.8) * dp(2f)).toFloat()
        drawHeat(c, w * .46f, h * .59f + bob1, dp(38f), dp(56f), .95f)
        drawPerson(c, w * .46f, h * .60f + bob1, 1f, "P1 · DEMO", .93f)
        drawHeat(c, w * .72f, h * .60f + bob2, dp(34f), dp(52f), .78f)
        drawPerson(c, w * .72f, h * .61f + bob2, .9f, "P2 · DEMO", .82f)

        if (showTracking) {
            stroke.color = Color.argb(130, 0, 255, 183)
            stroke.strokeWidth = dp(1.2f)
            val path = Path()
            path.moveTo(w * .34f, h * .78f)
            path.cubicTo(w * .38f, h * .72f, w * .42f, h * .69f, w * .46f, h * .67f)
            c.drawPath(path, stroke)
        }
    }

    private fun drawWifiDirect(c: Canvas, w: Float, h: Float) {
        if (!wifiCalibrated) {
            drawText(c, "CALIBRÁ EL AMBIENTE", w * .5f - dp(72f), h * .44f, 14f, Color.rgb(120, 145, 158), true)
            drawText(c, "Router + Wi‑Fi + teléfono", w * .5f - dp(62f), h * .50f, 9f, Color.rgb(84, 107, 120), false)
            return
        }

        val reading = wifiReading
        if (!wifiSensingRunning && reading == null) {
            drawText(c, "CALIBRACIÓN LISTA", w * .5f - dp(58f), h * .44f, 14f, Color.rgb(83, 255, 196), true)
            drawText(c, "Iniciá sensing", w * .5f - dp(35f), h * .50f, 9f, Color.rgb(92, 132, 143), false)
            return
        }
        if (reading == null) {
            drawText(c, "ESPERANDO RSSI", w * .5f - dp(50f), h * .46f, 13f, Color.rgb(116, 137, 151), true)
            return
        }

        val intensity = reading.confidence.coerceIn(.08, 1.0).toFloat()
        val pulse = ((sin(phase * 1.5) + 1.0) * .5).toFloat()
        val cx = w * .59f
        val cy = h * .58f
        val rx = dp(30f + intensity * 52f + pulse * 5f)
        val ry = dp(42f + intensity * 76f + pulse * 7f)

        if (showHeatmap) {
            for (i in 5 downTo 1) {
                val scale = i / 5f
                val alpha = (10 + intensity * (56 - i * 5)).toInt().coerceIn(8, 72)
                paint.style = Paint.Style.FILL
                paint.color = if (reading.state == "AMBIENTE ESTABLE") Color.argb(alpha, 0, 170, 255)
                else Color.argb(alpha, 0, 255, 167)
                c.drawOval(cx - rx * scale, cy - ry * scale, cx + rx * scale, cy + ry * scale, paint)
            }
        }

        for (i in 0..3) {
            val extra = dp(i * 13f + pulse * 4f)
            stroke.strokeWidth = dp(1f)
            stroke.color = if (reading.state == "AMBIENTE ESTABLE") Color.argb(130, 35, 150, 255)
            else Color.argb(150, 0, 255, 183)
            c.drawOval(cx - rx - extra, cy - (ry + extra) * .72f, cx + rx + extra, cy + (ry + extra) * .72f, stroke)
        }

        val stateColor = if (reading.state == "MOVIMIENTO PROBABLE") Color.rgb(255, 211, 96) else Color.rgb(83, 255, 196)
        drawText(c, reading.state, cx - dp(58f), cy + ry * .72f + dp(14f), 9f, stateColor, true)
        drawText(c, "RF ${(reading.confidence * 100).toInt()}% · Δ ${fmt1(reading.delta)} dB", cx - dp(58f), cy + ry * .72f + dp(29f), 8f, Color.rgb(111, 167, 179), false)

        val routerX = w * .16f
        val deviceX = w * .87f
        val linkY = h * .72f
        stroke.color = Color.argb(90, 0, 226, 255)
        stroke.strokeWidth = dp(1f)
        c.drawLine(routerX, linkY, deviceX, linkY, stroke)
        paint.style = Paint.Style.FILL
        paint.color = Color.argb(160, 0, 226, 255)
        c.drawCircle(routerX, linkY, dp(5f), paint)
        paint.color = Color.argb(160, 0, 255, 183)
        c.drawCircle(deviceX, linkY, dp(5f), paint)
        drawText(c, "ROUTER/AP", routerX - dp(24f), linkY + dp(18f), 7f, Color.rgb(90, 221, 238), true)
        drawText(c, "TELÉFONO", deviceX - dp(22f), linkY + dp(18f), 7f, Color.rgb(83, 255, 196), true)

        if (showTracking && reading.state != "AMBIENTE ESTABLE") {
            stroke.color = Color.argb((70 + intensity * 90).toInt(), 0, 255, 183)
            stroke.strokeWidth = dp(1.2f)
            val path = Path()
            path.moveTo(cx - dp(60f), cy + dp(50f))
            path.cubicTo(cx - dp(30f), cy + dp(26f), cx + dp(5f), cy + dp(55f), cx + dp(48f), cy + dp(32f))
            c.drawPath(path, stroke)
        }

        drawText(c, wifiLabel, dp(12f), h - dp(12f), 7f, Color.rgb(74, 116, 132), true)
    }

    private fun drawReal(c: Canvas, w: Float, h: Float) {
        val now = System.currentTimeMillis()
        val live = nodes.filter { now - it.lastSeenMs < 5000 }.take(6)
        if (live.isEmpty()) {
            drawText(c, "ESPERANDO CSI", w * .5f - dp(58f), h * .46f, 15f, Color.rgb(120, 145, 158), true)
            drawText(c, "Sin paquetes compatibles", w * .5f - dp(70f), h * .51f, 10f, Color.rgb(84, 107, 120), false)
            return
        }
        var personIndex = 0
        live.forEach { n ->
            if (n.vitalsFrames > 0 && n.presence == true) {
                val count = (if (n.persons <= 0) 1 else n.persons).coerceIn(1, 3)
                repeat(count) {
                    val x = w * (.31f + ((personIndex * .20f) % .58f))
                    val y = h * (.60f + (personIndex % 2) * .07f)
                    val conf = n.presenceScore.coerceIn(0f, 1f)
                    drawHeat(c, x, y, dp(34f), dp(52f), max(.25f, conf))
                    drawPerson(c, x, y, .86f, "N${n.nodeId} · EST.", conf)
                    personIndex++
                }
            } else if (n.rawFrames > 0) {
                val x = w * (.30f + ((n.nodeId * .137f) % .58f))
                val y = h * (.61f + (n.nodeId % 2) * .08f)
                drawRaw(c, x, y, (n.rawActivity / 35.0).toFloat().coerceIn(.15f, 1f), n.nodeId)
            }
        }
    }

    private fun drawRaw(c: Canvas, x: Float, y: Float, activity: Float, nodeId: Int) {
        val r = dp(22f + activity * 25f)
        paint.style = Paint.Style.FILL
        paint.color = Color.argb((30 + 75 * activity).toInt(), 0, 210, 255)
        c.drawOval(x - r, y - r * .7f, x + r, y + r * .7f, paint)
        stroke.color = Color.argb(120, 0, 230, 255)
        stroke.strokeWidth = dp(1f)
        c.drawOval(x - r - dp(5f), y - r * .7f - dp(4f), x + r + dp(5f), y + r * .7f + dp(4f), stroke)
        drawText(c, "N$nodeId · CSI RAW", x - dp(30f), y + r, 8f, Color.rgb(83, 220, 238), true)
    }

    private fun drawHeat(c: Canvas, x: Float, y: Float, rx: Float, ry: Float, intensity: Float) {
        if (!showHeatmap) return
        for (i in 4 downTo 1) {
            val s = i / 4f
            paint.style = Paint.Style.FILL
            paint.color = Color.argb((14 + intensity * 18f * (5 - i)).toInt().coerceIn(10, 72), 0, 255, 167)
            c.drawOval(x - rx * s, y - ry * s, x + rx * s, y + ry * s, paint)
        }
    }

    private fun drawPerson(c: Canvas, x: Float, y: Float, scale: Float, label: String, confidence: Float) {
        val headX = x
        val headY = y - dp(54f) * scale
        val neckY = y - dp(38f) * scale
        val hipY = y - dp(5f) * scale
        val shoulder = dp(14f) * scale
        val hand = dp(24f) * scale
        val foot = dp(38f) * scale

        stroke.color = Color.rgb(73, 255, 190)
        stroke.strokeWidth = dp(1.8f) * scale
        c.drawCircle(headX, headY, dp(7f) * scale, stroke)
        c.drawLine(x, neckY, x, hipY, stroke)
        c.drawLine(x - shoulder, neckY + dp(3f), x + shoulder, neckY + dp(3f), stroke)
        c.drawLine(x - shoulder, neckY + dp(3f), x - hand, hipY + dp(1f), stroke)
        c.drawLine(x + shoulder, neckY + dp(3f), x + hand, hipY + dp(1f), stroke)
        c.drawLine(x, hipY, x - dp(12f) * scale, y + foot, stroke)
        c.drawLine(x, hipY, x + dp(12f) * scale, y + foot, stroke)
        val boxW = dp(31f) * scale
        stroke.color = Color.argb(105, 33, 255, 196)
        stroke.strokeWidth = dp(.8f)
        c.drawRect(x - boxW, headY - dp(12f), x + boxW, y + foot + dp(5f), stroke)
        drawText(c, label, x - dp(27f), y + foot + dp(14f), 8f, Color.rgb(79, 255, 197), true)
        drawText(c, "${(confidence * 100).toInt()}%", x - dp(9f), y + foot + dp(26f), 7f, Color.rgb(104, 179, 164), false)
    }

    private fun drawHud(c: Canvas, w: Float, h: Float) {
        val mode = if (demoMode) "DEMO VISUAL" else if (wifiDirectMode) "WI‑FI DIRECT" else "CSI REAL"
        drawText(c, mode, dp(12f), dp(18f), 9f,
            if (demoMode) Color.rgb(245, 181, 60) else Color.rgb(0, 255, 183), true)
        drawText(c, "RF SENSING SCENE", dp(12f), dp(35f), 8f, Color.rgb(84, 116, 131), true)
        val status = if (demoMode) "SIMULACIÓN"
        else if (wifiDirectMode) {
            if (!wifiCalibrated) "CALIBRATE" else if (wifiSensingRunning) "RSSI LIVE" else "READY"
        } else if (nodes.isEmpty()) "WAITING" else "DATA LINK"
        drawText(c, status, w - dp(70f), dp(18f), 8f,
            if (demoMode) Color.rgb(245, 181, 60) else Color.rgb(0, 255, 183), true)
    }

    private fun drawText(c: Canvas, text: String, x: Float, y: Float, sp: Float, color: Int, bold: Boolean) {
        paint.style = Paint.Style.FILL
        paint.color = color
        paint.textSize = sp * resources.displayMetrics.scaledDensity
        paint.typeface = if (bold) android.graphics.Typeface.DEFAULT_BOLD else android.graphics.Typeface.DEFAULT
        c.drawText(text, x, y, paint)
    }

    private fun fmt1(value: Double): String = String.format(java.util.Locale.US, "%.1f", value)

    private fun dp(value: Float): Float = value * resources.displayMetrics.density
}
