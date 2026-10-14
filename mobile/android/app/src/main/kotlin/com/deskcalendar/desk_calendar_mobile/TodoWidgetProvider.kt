package com.deskcalendar.desk_calendar_mobile

import android.appwidget.AppWidgetManager
import android.content.Context
import android.content.SharedPreferences
import android.os.Bundle
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
import es.antonborri.home_widget.HomeWidgetPlugin
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
        appWidgetIds.forEach { render(context, appWidgetManager, it, widgetData) }
    }

    /** 위젯을 줄이거나 늘리면 크기에 맞게 보이는 줄 수와 머리줄을 다시 정한다 */
    override fun onAppWidgetOptionsChanged(context: Context, mgr: AppWidgetManager, id: Int, newOptions: Bundle) {
        super.onAppWidgetOptionsChanged(context, mgr, id, newOptions)
        render(context, mgr, id, HomeWidgetPlugin.getData(context))
    }

    private fun render(context: Context, mgr: AppWidgetManager, widgetId: Int, widgetData: SharedPreferences) {
        val opt = mgr.getAppWidgetOptions(widgetId)
        val wDp = opt.getInt(AppWidgetManager.OPTION_APPWIDGET_MIN_WIDTH, 180)
        val hDp = opt.getInt(AppWidgetManager.OPTION_APPWIDGET_MIN_HEIGHT, 180)
        val maxRows = ((hDp - 56) / 36).coerceIn(1, 12)       // 머리줄을 뺀 높이에 들어가는 줄 수
        val narrow = wDp < 150
        val views = RemoteViews(context.packageName, R.layout.todo_widget)
        views.setViewVisibility(R.id.t_title, if (narrow) View.GONE else View.VISIBLE)
        views.setOnClickPendingIntent(R.id.t_add, HomeWidgetLaunchIntent.getActivity(context, MainActivity::class.java, Uri.parse("deskcalendar://add")))
        views.setOnClickPendingIntent(R.id.t_title, HomeWidgetLaunchIntent.getActivity(context, MainActivity::class.java, Uri.parse("deskcalendar://tasks")))
        views.setOnClickPendingIntent(R.id.t_count, HomeWidgetLaunchIntent.getActivity(context, MainActivity::class.java, Uri.parse("deskcalendar://tasks")))
        val raw = widgetData.getString("td", null)
        val rowIds = (0 until 12).map { context.resources.getIdentifier("r$it", "id", context.packageName) }
        if (raw == null) {
            rowIds.forEach { views.setViewVisibility(it, View.GONE) }
            views.setTextViewText(R.id.t_empty, "앱을 한 번 열어 주세요")
            views.setViewVisibility(R.id.t_empty, View.VISIBLE)
            mgr.updateAppWidget(widgetId, views)
            return
        }
        val json = JSONObject(raw)
        val count = json.getInt("count")
        views.setTextViewText(R.id.t_count, if (count > 0) (if (narrow) "할 일 $count" else "$count") else "")
        // 칸이 모자라면 제목 줄(구분선)부터 덜어 내고 실제 할 일을 먼저 보여 준다
        val all = json.getJSONArray("rows")
        val picked = ArrayList<JSONObject>()
        val real = (0 until all.length()).count { all.getJSONObject(it).getString("k") != "h" }
        val showHeads = maxRows >= real + 2
        for (i in 0 until all.length()) {
            val r = all.getJSONObject(i)
            if (r.getString("k") == "h" && !showHeads) continue
            if (picked.size < maxRows) picked.add(r)
        }
        views.setViewVisibility(R.id.t_empty, if (picked.isEmpty()) View.VISIBLE else View.GONE)
        if (picked.isEmpty()) views.setTextViewText(R.id.t_empty, "할 일이 없어요 🎉")
        for (i in 0 until 12) {
            val id = rowIds[i]
            if (i >= picked.size) {
                views.setViewVisibility(id, View.GONE)
                continue
            }
            val r = picked[i]
            val sb = SpannableStringBuilder()
            when (r.getString("k")) {
                "h" -> {
                    sb.append(r.getString("x"))
                    sb.setSpan(ForegroundColorSpan(Color.parseColor("#9AA4B8")), 0, sb.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
                    sb.setSpan(StyleSpan(Typeface.BOLD), 0, sb.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
                    sb.setSpan(RelativeSizeSpan(0.85f), 0, sb.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
                }
                "w" -> {
                    // 마감 임박 업무: D-day 를 색으로 (지났으면 붉게, 사흘 안이면 노랗게)
                    val n = r.optInt("n", 99)
                    val col = Color.parseColor(if (n < 0) "#F08A94" else if (n <= 2) "#F2C374" else "#B7AEFF")
                    val tag = r.getString("s") + "  "
                    sb.append(tag)
                    sb.setSpan(ForegroundColorSpan(col), 0, tag.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
                    sb.setSpan(StyleSpan(Typeface.BOLD), 0, tag.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
                    val s = sb.length
                    sb.append(r.getString("x"))
                    sb.setSpan(ForegroundColorSpan(Color.parseColor("#E6E9F0")), s, sb.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
                    views.setOnClickPendingIntent(id, HomeWidgetLaunchIntent.getActivity(context, MainActivity::class.java, Uri.parse("deskcalendar://work")))
                }
                else -> {
                    val done = r.getBoolean("d")
                    val code = r.optInt("c", 0)
                    val box = if (done) "☑  " else "☐  "
                    sb.append(box)
                    sb.setSpan(ForegroundColorSpan(if (done) Color.parseColor("#7A8396") else Color.parseColor("#7C6CFF")), 0, box.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
                    val sub = r.optString("s", "")
                    if (sub.isNotEmpty() && !narrow) {
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
            }
            views.setTextViewText(id, sb)
            views.setViewVisibility(id, View.VISIBLE)
        }
        mgr.updateAppWidget(widgetId, views)
    }
}
