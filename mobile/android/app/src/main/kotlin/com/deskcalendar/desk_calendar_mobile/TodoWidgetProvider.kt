package com.deskcalendar.desk_calendar_mobile

import android.appwidget.AppWidgetManager
import android.content.Context
import android.content.SharedPreferences
import android.graphics.Color
import android.graphics.Typeface
import android.net.Uri
import android.text.SpannableStringBuilder
import android.text.Spanned
import android.text.style.ForegroundColorSpan
import android.text.style.RelativeSizeSpan
import android.text.style.StrikethroughSpan
import android.text.style.StyleSpan
import android.view.View
import android.widget.RemoteViews
import es.antonborri.home_widget.HomeWidgetBackgroundIntent
import es.antonborri.home_widget.HomeWidgetLaunchIntent
import es.antonborri.home_widget.HomeWidgetProvider
import org.json.JSONObject

/** 홈 화면 '할 일' 위젯: 지난 일정 → 오늘 → 보관함의 할 일만 모아 보여 주고, 항목을 누르면 앱을 열지 않고 바로 체크한다 */
class TodoWidgetProvider : HomeWidgetProvider() {
    override fun onUpdate(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetIds: IntArray,
        widgetData: SharedPreferences
    ) {
        val views = RemoteViews(context.packageName, R.layout.todo_widget)
        views.setOnClickPendingIntent(R.id.t_add, HomeWidgetLaunchIntent.getActivity(context, MainActivity::class.java, Uri.parse("deskcalendar://add")))
        views.setOnClickPendingIntent(R.id.t_title, HomeWidgetLaunchIntent.getActivity(context, MainActivity::class.java, Uri.parse("deskcalendar://tasks")))
        val raw = widgetData.getString("td", null)
        val rowIds = (0 until 12).map { context.resources.getIdentifier("r$it", "id", context.packageName) }
        if (raw == null) {
            rowIds.forEach { views.setViewVisibility(it, View.GONE) }
            views.setTextViewText(R.id.t_empty, "앱을 한 번 열어 주세요")
            views.setViewVisibility(R.id.t_empty, View.VISIBLE)
            appWidgetIds.forEach { appWidgetManager.updateAppWidget(it, views) }
            return
        }
        val json = JSONObject(raw)
        val count = json.getInt("count")
        views.setTextViewText(R.id.t_count, if (count > 0) "$count" else "")
        val rows = json.getJSONArray("rows")
        views.setViewVisibility(R.id.t_empty, if (rows.length() == 0) View.VISIBLE else View.GONE)
        if (rows.length() == 0) views.setTextViewText(R.id.t_empty, "할 일이 없어요 🎉")
        for (i in 0 until 12) {
            val id = rowIds[i]
            if (i >= rows.length()) {
                views.setViewVisibility(id, View.GONE)
                continue
            }
            val r = rows.getJSONObject(i)
            val sb = SpannableStringBuilder()
            if (r.getString("k") == "h") {
                sb.append(r.getString("x"))
                sb.setSpan(ForegroundColorSpan(Color.parseColor("#9AA4B8")), 0, sb.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
                sb.setSpan(StyleSpan(Typeface.BOLD), 0, sb.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
                sb.setSpan(RelativeSizeSpan(0.85f), 0, sb.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
            } else {
                val done = r.getBoolean("d")
                val code = r.optInt("c", 0)
                val box = if (done) "☑  " else "☐  "
                sb.append(box)
                sb.setSpan(ForegroundColorSpan(if (done) Color.parseColor("#7A8396") else Color.parseColor("#7C6CFF")), 0, box.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
                val sub = r.optString("s", "")
                if (sub.isNotEmpty()) {
                    val s = sb.length
                    sb.append("$sub  ")
                    sb.setSpan(ForegroundColorSpan(Color.parseColor("#9AA4B8")), s, sb.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
                }
                val s = sb.length
                sb.append(r.getString("x"))
                val fg = when {
                    done -> Color.parseColor("#7A8396")
                    code == 1 -> Color.parseColor("#FF8F9B")
                    else -> Color.parseColor("#E6E9F0")
                }
                sb.setSpan(ForegroundColorSpan(fg), s, sb.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
                if (done) sb.setSpan(StrikethroughSpan(), s, sb.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
                val itemId = r.optString("id", "")
                if (itemId.isNotEmpty()) {
                    views.setOnClickPendingIntent(id, HomeWidgetBackgroundIntent.getBroadcast(context, Uri.parse("deskcalendar://toggle/$itemId")))
                }
            }
            views.setTextViewText(id, sb)
            views.setViewVisibility(id, View.VISIBLE)
        }
        appWidgetIds.forEach { appWidgetManager.updateAppWidget(it, views) }
    }
}
