package com.deskcalendar.desk_calendar_mobile

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.graphics.Color
import android.graphics.Typeface
import android.net.Uri
import android.text.SpannableStringBuilder
import android.text.Spanned
import android.text.style.AbsoluteSizeSpan
import android.text.style.ForegroundColorSpan
import android.text.style.StrikethroughSpan
import android.text.style.StyleSpan
import android.view.View
import android.widget.RemoteViews
import es.antonborri.home_widget.HomeWidgetLaunchIntent
import es.antonborri.home_widget.HomeWidgetPlugin
import org.json.JSONObject

/** 홈 화면 월간 달력 위젯: 칸마다 일정 글자가 보이고, ‹ › 로 달을 넘기며, 날짜를 누르면 앱의 그 날짜가 열린다 */
class MonthWidgetProvider : AppWidgetProvider() {
    companion object {
        const val ACTION_PREV = "com.deskcalendar.desk_calendar_mobile.MONTH_PREV"
        const val ACTION_NEXT = "com.deskcalendar.desk_calendar_mobile.MONTH_NEXT"
        const val ACTION_TODAY = "com.deskcalendar.desk_calendar_mobile.MONTH_TODAY"
        private const val PREFS = "deskcal_widget"
        private const val OFFSET = "month_offset"

        private fun color(code: Int): Int = when (code) {
            1 -> Color.parseColor("#FF8F9B")   // 중요
            2 -> Color.parseColor("#7A8396")   // 완료
            3 -> Color.parseColor("#82D9A8")   // 낮음
            4 -> Color.parseColor("#F08A94")   // 공휴일
            else -> Color.parseColor("#E6E9F0")
        }

        private fun span(sb: SpannableStringBuilder, text: String, fg: Int, sizeDp: Int, bold: Boolean = false, strike: Boolean = false) {
            val s = sb.length
            sb.append(text)
            val e = sb.length
            sb.setSpan(ForegroundColorSpan(fg), s, e, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
            sb.setSpan(AbsoluteSizeSpan(sizeDp, true), s, e, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
            if (bold) sb.setSpan(StyleSpan(Typeface.BOLD), s, e, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
            if (strike) sb.setSpan(StrikethroughSpan(), s, e, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
        }

        private fun broadcast(context: Context, action: String): PendingIntent {
            val i = Intent(context, MonthWidgetProvider::class.java).setAction(action)
            return PendingIntent.getBroadcast(context, action.hashCode(), i, PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
        }

        fun renderAll(context: Context) {
            val mgr = AppWidgetManager.getInstance(context)
            val ids = mgr.getAppWidgetIds(ComponentName(context, MonthWidgetProvider::class.java))
            if (ids.isNotEmpty()) render(context, mgr, ids)
        }

        fun render(context: Context, mgr: AppWidgetManager, ids: IntArray) {
            val off = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getInt(OFFSET, 0)
            val raw = HomeWidgetPlugin.getData(context).getString("m_$off", null)
            val json = raw?.let { JSONObject(it) }
            ids.forEach { renderOne(context, mgr, it, json) }
        }

        /** 작게 줄이면 일정 글자 대신 숫자와 점만, 더 줄이면 머리줄 버튼도 줄여 한눈에 보게 한다 */
        private fun renderOne(context: Context, mgr: AppWidgetManager, widgetId: Int, json: JSONObject?) {
            val opt = mgr.getAppWidgetOptions(widgetId)
            val wDp = opt.getInt(AppWidgetManager.OPTION_APPWIDGET_MIN_WIDTH, 250)
            val hDp = opt.getInt(AppWidgetManager.OPTION_APPWIDGET_MIN_HEIGHT, 250)
            val compact = wDp < 240 || hDp < 240
            val tiny = wDp < 150 || hDp < 150
            val views = RemoteViews(context.packageName, R.layout.month_widget)
            views.setOnClickPendingIntent(R.id.m_prev, broadcast(context, ACTION_PREV))
            views.setOnClickPendingIntent(R.id.m_next, broadcast(context, ACTION_NEXT))
            views.setOnClickPendingIntent(R.id.m_today, broadcast(context, ACTION_TODAY))
            views.setOnClickPendingIntent(R.id.m_add, HomeWidgetLaunchIntent.getActivity(context, MainActivity::class.java, Uri.parse("deskcalendar://add")))
            views.setOnClickPendingIntent(R.id.m_title, HomeWidgetLaunchIntent.getActivity(context, MainActivity::class.java))
            views.setViewVisibility(R.id.m_today, if (tiny) View.GONE else View.VISIBLE)
            views.setViewVisibility(R.id.m_add, if (tiny) View.GONE else View.VISIBLE)
            if (json == null) {
                views.setTextViewText(R.id.m_title, "앱을 한 번 열어 주세요")
                mgr.updateAppWidget(widgetId, views)
                return
            }
            views.setTextViewText(R.id.m_title, json.getString("title"))
            val cells = json.getJSONArray("cells")
            for (i in 0 until minOf(42, cells.length())) {
                val c = cells.getJSONObject(i)
                val viewId = context.resources.getIdentifier("c$i", "id", context.packageName)
                val other = c.getInt("o") == 1
                val w = c.getInt("w")
                val dayColor = when {
                    other -> Color.parseColor("#5E667A")
                    w == 0 -> Color.parseColor("#F08A94")
                    w == 6 -> Color.parseColor("#8FB4F2")
                    else -> Color.parseColor("#F1F3F8")
                }
                val sb = SpannableStringBuilder()
                span(sb, c.getInt("d").toString(), dayColor, if (tiny) 10 else 12, bold = true)
                val ev = c.getJSONArray("e")
                if (compact) {
                    if (ev.length() > 0) {
                        // 일정이 있는 날은 가장 중요한 일정 색으로 점 하나
                        sb.append("\n")
                        span(sb, "●", if (other) Color.parseColor("#5E667A") else color(ev.getJSONObject(0).getInt("c")), 7)
                    }
                } else {
                    for (k in 0 until ev.length()) {
                        val e = ev.getJSONObject(k)
                        val code = e.getInt("c")
                        sb.append("\n")
                        span(sb, e.getString("x"), if (other) Color.parseColor("#5E667A") else color(code), 9, strike = code == 2)
                    }
                    if (c.has("more")) {
                        sb.append("\n")
                        span(sb, "+" + c.getInt("more"), Color.parseColor("#9AA4B8"), 9)
                    }
                }
                views.setTextViewText(viewId, sb)
                views.setInt(viewId, "setBackgroundResource",
                    if (c.getInt("t") == 1) R.drawable.w_cell_today else if (other) R.drawable.w_cell_dim else R.drawable.w_cell)
                views.setOnClickPendingIntent(viewId, HomeWidgetLaunchIntent.getActivity(
                    context, MainActivity::class.java, Uri.parse("deskcalendar://date/" + c.getString("iso"))))
            }
            mgr.updateAppWidget(widgetId, views)
        }
    }

    override fun onUpdate(context: Context, appWidgetManager: AppWidgetManager, appWidgetIds: IntArray) {
        render(context, appWidgetManager, appWidgetIds)
    }

    override fun onAppWidgetOptionsChanged(context: Context, mgr: AppWidgetManager, id: Int, newOptions: Bundle) {
        super.onAppWidgetOptionsChanged(context, mgr, id, newOptions)
        renderAll(context)
    }

    override fun onReceive(context: Context, intent: Intent) {
        super.onReceive(context, intent)
        val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        val cur = prefs.getInt(OFFSET, 0)
        val next = when (intent.action) {
            ACTION_PREV -> maxOf(-1, cur - 1)
            ACTION_NEXT -> minOf(2, cur + 1)
            ACTION_TODAY -> 0
            else -> return
        }
        prefs.edit().putInt(OFFSET, next).apply()
        renderAll(context)
    }
}
