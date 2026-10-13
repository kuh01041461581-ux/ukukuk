package com.deskcalendar.desk_calendar_mobile

import android.appwidget.AppWidgetManager
import android.content.Context
import android.content.SharedPreferences
import android.widget.RemoteViews
import es.antonborri.home_widget.HomeWidgetLaunchIntent
import es.antonborri.home_widget.HomeWidgetProvider

/** 홈 화면 위젯: 오늘/가까운 일정의 글자를 보여 주고, 누르면 앱을 연다 */
class DeskWidgetProvider : HomeWidgetProvider() {
    override fun onUpdate(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetIds: IntArray,
        widgetData: SharedPreferences
    ) {
        appWidgetIds.forEach { widgetId ->
            val views = RemoteViews(context.packageName, R.layout.desk_widget).apply {
                setTextViewText(R.id.w_title, widgetData.getString("w_title", "DeskCalendar"))
                setTextViewText(R.id.w_body, widgetData.getString("w_body", "앱을 한 번 열어 주세요"))
                val pending = HomeWidgetLaunchIntent.getActivity(context, MainActivity::class.java)
                setOnClickPendingIntent(R.id.widget_root, pending)
            }
            appWidgetManager.updateAppWidget(widgetId, views)
        }
    }
}
