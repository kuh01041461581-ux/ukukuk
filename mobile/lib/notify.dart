// 일정 알림 (폰 안에서 예약하는 알림이라 인터넷·PC 없이도 울린다)
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:timezone/data/latest.dart' as tzdata;
import 'package:timezone/timezone.dart' as tz;

import 'core.dart';
import 'store.dart';

class Notifier {
  static final plugin = FlutterLocalNotificationsPlugin();
  static bool _ready = false;

  static Future<void> init() async {
    tzdata.initializeTimeZones();
    tz.setLocalLocation(tz.getLocation('Asia/Seoul'));
    const init = InitializationSettings(
      android: AndroidInitializationSettings('@mipmap/ic_launcher'),
      iOS: DarwinInitializationSettings(
          requestAlertPermission: false, requestBadgePermission: false, requestSoundPermission: false),
    );
    await plugin.initialize(init);
    _ready = true;
  }

  static Future<bool> requestPermission() async {
    final a = plugin.resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>();
    if (a != null) return await a.requestNotificationsPermission() ?? false;
    final i = plugin.resolvePlatformSpecificImplementation<IOSFlutterLocalNotificationsPlugin>();
    return await i?.requestPermissions(alert: true, badge: true, sound: true) ?? false;
  }

  /// 앞으로 3주 안의 일정 알림을 모두 다시 예약한다 (최대 60개)
  static Future<void> reschedule(AppStore s) async {
    if (!_ready) return;
    try {
      await plugin.cancelAll();
      if (!s.notify) return;
      final now = DateTime.now();
      final list = <(DateTime, String, String)>[];
      for (var k = 0; k < 21; k++) {
        final d = addDays(dayOnly(now), k);
        final rows = dayItems(s.data, d);
        var open = 0;
        final lines = <String>[];
        for (final r in rows) {
          final t = r.t;
          if (t['done'] == true) continue;
          open++;
          final tm = (t['time'] as String?) ?? '';
          if (lines.length < 4) lines.add('${tm.isEmpty ? '' : '$tm '}${t['text']}');
          if (tm.length < 5) continue;
          final at = DateTime(d.year, d.month, d.day, int.parse(tm.substring(0, 2)), int.parse(tm.substring(3, 5)));
          final when = at.subtract(Duration(minutes: s.lead));
          if (when.isAfter(now)) {
            list.add((when, s.lead == 0 ? '$tm  지금' : '$tm  ${s.lead}분 전', t['text'] as String));
          }
        }
        if (s.brief.length == 5 && open > 0) {
          final at = DateTime(d.year, d.month, d.day, int.parse(s.brief.substring(0, 2)), int.parse(s.brief.substring(3, 5)));
          if (at.isAfter(now)) list.add((at, '오늘 할 일 $open개', lines.join('\n')));
        }
      }
      list.sort((a, b) => a.$1.compareTo(b.$1));
      const details = NotificationDetails(
        android: AndroidNotificationDetails('schedule', '일정 알림',
            channelDescription: '일정 시작 전에 알려줘요', importance: Importance.high, priority: Priority.high),
        iOS: DarwinNotificationDetails(presentAlert: true, presentSound: true),
      );
      var id = 1;
      for (final e in list.take(60)) {
        await plugin.zonedSchedule(id++, e.$2, e.$3, tz.TZDateTime.from(e.$1, tz.local), details,
            androidScheduleMode: AndroidScheduleMode.inexactAllowWhileIdle,
            uiLocalNotificationDateInterpretation: UILocalNotificationDateInterpretation.absoluteTime);
      }
    } catch (_) {
      // 알림 예약이 실패해도 앱은 계속 동작해야 한다
    }
  }
}
