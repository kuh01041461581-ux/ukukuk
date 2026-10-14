package com.deskcalendar.desk_calendar_mobile

import android.appwidget.AppWidgetManager
import android.content.Context
import android.content.SharedPreferences
import android.net.Uri
import android.os.Bundle
import android.view.View
import android.widget.RemoteViews
import es.antonborri.home_widget.HomeWidgetLaunchIntent
import es.antonborri.home_widget.HomeWidgetPlugin
import es.antonborri.home_widget.HomeWidgetProvider

/** 홈 화면 '오늘 일정' 위젯: 오늘과 가까운 일정 글자를 보여 주고, 본문을 누르면 앱, ＋ 는 바로 추가. 크기에 맞춰 줄 수를 줄인다 */
class DeskWidgetProvider : HomeWidgetProvider() {
    override fun onUpdate(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetIds: IntArray,
        widgetData: SharedPreferences
    ) {
        appWidgetIds.forEach { render(context, appWidgetManager, it, widgetData) }
    }

    override fun onAppWidgetOptionsChanged(context: Context, mgr: AppWidgetManager, id: Int, newOptions: Bundle) {
        super.onAppWidgetOptionsChanged(context, mgr, id, newOptions)
        render(context, mgr, id, HomeWidgetPlugin.getData(context))
    }

    private fun render(context: Context, mgr: AppWidgetManager, widgetId: Int, data: SharedPreferences) {
        val opt = mgr.getAppWidgetOptions(widgetId)
        val wDp = opt.getInt(AppWidgetManager.OPTION_APPWIDGET_MIN_WIDTH, 250)
        val hDp = opt.getInt(AppWidgetManager.OPTION_APPWIDGET_MIN_HEIGHT, 110)
        val lines = ((hDp - 52) / 20).coerceIn(0, 12)             // 제목줄을 뺀 높이에 들어가는 줄 수
        val views = RemoteViews(context.packageName, R.layout.desk_widget)
        val body = (data.getString("w_body", null) ?: "앱을 한 번 열어 주세요")
        views.setTextViewText(R.id.w_title, data.getString("w_title", "DeskCalendar"))
        views.setTextViewText(R.id.w_body, body)
        views.setInt(R.id.w_body, "setMaxLines", maxOf(lines, 1))
        views.setViewVisibility(R.id.w_body, if (lines == 0) View.GONE else View.VISIBLE)
        views.setViewVisibility(R.id.w_add, if (wDp < 130) View.GONE else View.VISIBLE)
        views.setOnClickPendingIntent(R.id.widget_root, HomeWidgetLaunchIntent.getActivity(context, MainActivity::class.java))
        views.setOnClickPendingIntent(R.id.w_body, HomeWidgetLaunchIntent.getActivity(context, MainActivity::class.java))
        views.setOnClickPendingIntent(R.id.w_add, HomeWidgetLaunchIntent.getActivity(context, MainActivity::class.java, Uri.parse("deskcalendar://add")))
        mgr.updateAppWidget(widgetId, views)
    }
}
