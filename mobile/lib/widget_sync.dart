// 안드로이드 홈 화면 위젯: 오늘과 가까운 일정의 '내용'을 보여 준다 (점이 아니라 글자로)
import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:home_widget/home_widget.dart';

import 'core.dart';
import 'store.dart';
import 'widget_data.dart';

const _wd = ['월', '화', '수', '목', '금', '토', '일'];

String _line(Json t, {String prefix = ''}) {
  final done = t['done'] == true;
  final time = (t['time'] as String?)?.isNotEmpty == true ? '${t['time']} ' : '';
  return '${done ? '✓' : '•'} $prefix$time${t['text'] ?? ''}';
}

Future<void> updateHomeWidget(AppStore store) async {
  if (kIsWeb || !Platform.isAndroid) return;
  try {
    final today = dayOnly(DateTime.now());
    final (nToday, nLate, _) = summaryCounts(store.data);
    final title = '${today.month}월 ${today.day}일 ${_wd[today.weekday - 1]}요일' +
        (nLate > 0 ? '  ·  지연 $nLate' : '');
    final lines = <String>[];
    final todayRows = dayItems(store.data, today);
    if (todayRows.isEmpty) {
      lines.add('오늘 일정이 없어요');
    } else {
      todayRows.sort((a, b) => ((a.t['done'] == true) ? 1 : 0).compareTo((b.t['done'] == true) ? 1 : 0));
      for (final r in todayRows.take(6)) {
        lines.add(_line(r.t));
      }
      if (todayRows.length > 6) lines.add('… 외 ${todayRows.length - 6}개');
    }
    for (var k = 1; k <= 3 && lines.length < 10; k++) {
      final d = addDays(today, k);
      final rows = dayItems(store.data, d).where((r) => r.t['done'] != true).toList();
      for (final r in rows.take(3)) {
        if (lines.length >= 10) break;
        lines.add(_line(r.t, prefix: '${d.month}/${d.day}(${_wd[d.weekday - 1]}) '));
      }
    }
    await HomeWidget.saveWidgetData<String>('w_title', title);
    await HomeWidget.saveWidgetData<String>('w_body', lines.join('\n'));
    await HomeWidget.saveWidgetData<String>('w_count', '$nToday');
    // 월간 달력 위젯: 지난달~2달 뒤까지 미리 넣어 두면 위젯의 ‹ › 버튼이 앱 없이도 넘겨 본다
    for (var off = -1; off <= 2; off++) {
      await HomeWidget.saveWidgetData<String>('m_$off', jsonEncode(monthWidgetData(store.data, today, off)));
    }
    // 할 일 위젯
    await HomeWidget.saveWidgetData<String>('td', jsonEncode(todoWidgetData(store.data, today)));
    for (final name in ['DeskWidgetProvider', 'MonthWidgetProvider', 'TodoWidgetProvider']) {
      await HomeWidget.updateWidget(name: name, androidName: name);
    }
  } catch (_) {
    // 위젯이 없거나 지원하지 않는 환경이면 조용히 넘어간다
  }
}
