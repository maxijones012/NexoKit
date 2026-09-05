package com.max.kitherramientas

import android.app.Activity
import android.app.Application
import android.content.Intent
import android.os.Bundle
import android.view.Gravity
import android.view.ViewGroup
import android.widget.Button
import android.widget.FrameLayout

class NexoKitApplication : Application(), Application.ActivityLifecycleCallbacks {
    override fun onCreate() {
        super.onCreate()
        registerActivityLifecycleCallbacks(this)
    }

    override fun onActivityResumed(activity: Activity) {
        if (activity is MainActivity) installToolsButton(activity)
    }

    private fun installToolsButton(activity: MainActivity) {
        val content = activity.findViewById<ViewGroup>(android.R.id.content) ?: return
        if (content.findViewWithTag<Button>("nexokit_tools_button") != null) return

        val button = Button(activity).apply {
            tag = "nexokit_tools_button"
            text = "🧰 Herramientas"
            isAllCaps = false
            elevation = dp(activity, 6).toFloat()
            setOnClickListener {
                activity.startActivity(Intent(activity, ToolsActivity::class.java))
            }
        }

        val params = FrameLayout.LayoutParams(dp(activity, 170), dp(activity, 48)).apply {
            gravity = Gravity.TOP or Gravity.END
            topMargin = dp(activity, 10)
            marginEnd = dp(activity, 10)
        }
        content.addView(button, params)
    }

    private fun dp(activity: Activity, value: Int): Int = (value * activity.resources.displayMetrics.density).toInt()

    override fun onActivityCreated(activity: Activity, savedInstanceState: Bundle?) = Unit
    override fun onActivityStarted(activity: Activity) = Unit
    override fun onActivityPaused(activity: Activity) = Unit
    override fun onActivityStopped(activity: Activity) = Unit
    override fun onActivitySaveInstanceState(activity: Activity, outState: Bundle) = Unit
    override fun onActivityDestroyed(activity: Activity) = Unit
}
