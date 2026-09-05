package com.max.kitherramientas

import android.content.Context
import android.graphics.Canvas
import android.graphics.Paint
import android.util.AttributeSet
import android.view.View
import kotlin.math.max

class SignalGraphView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : View(context, attrs) {

    private val points = ArrayDeque<Int>()
    private val gridPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = 0xFF293241.toInt()
        strokeWidth = 1f
    }
    private val linePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = 0xFF8EC5FF.toInt()
        strokeWidth = 5f
        style = Paint.Style.STROKE
        strokeJoin = Paint.Join.ROUND
        strokeCap = Paint.Cap.ROUND
    }
    private val textPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = 0xFF7E8998.toInt()
        textSize = 28f
    }

    fun addRssi(value: Int) {
        points.addLast(value.coerceIn(-100, -20))
        while (points.size > 120) points.removeFirst()
        invalidate()
    }

    fun clearHistory() {
        points.clear()
        invalidate()
    }

    fun history(): List<Int> = points.toList()

    override fun onDraw(canvas: Canvas) {
        super.onDraw(canvas)
        val w = width.toFloat()
        val h = height.toFloat()
        if (w <= 0 || h <= 0) return

        val pad = 26f
        val graphW = max(1f, w - pad * 2)
        val graphH = max(1f, h - pad * 2)

        listOf(-30, -50, -70, -90).forEach { dbm ->
            val y = pad + ((-30 - dbm) / 70f) * graphH
            canvas.drawLine(pad, y, w - pad, y, gridPaint)
            canvas.drawText("$dbm", 4f, y - 5f, textPaint)
        }

        if (points.size < 2) return
        val data = points.toList()
        val step = graphW / (data.size - 1).coerceAtLeast(1)
        val path = android.graphics.Path()
        data.forEachIndexed { index, dbm ->
            val x = pad + index * step
            val normalized = ((dbm + 100) / 70f).coerceIn(0f, 1f)
            val y = pad + (1f - normalized) * graphH
            if (index == 0) path.moveTo(x, y) else path.lineTo(x, y)
        }
        canvas.drawPath(path, linePaint)
    }
}
