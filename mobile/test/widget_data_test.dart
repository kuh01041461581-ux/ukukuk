import 'package:flutter_test/flutter_test.dart';

import 'package:desk_calendar_mobile/core.dart';
import 'package:desk_calendar_mobile/widget_data.dart';

void main() {
  final today = DateTime(2026, 10, 13); // 화요일
  Json sample() {
    final a = <String, dynamic>{'_deleted': <String, dynamic>{}};
    addItem(a, '2026-10-13', newItem('오늘 회의', extra: {'time': '14:00', 'pri': 1}));
    addItem(a, '2026-10-13', newItem('끝낸 일', extra: {'done': true}));
    addItem(a, '2026-10-10', newItem('지난 보고'));
    addItem(a, '2026-10-20', newItem('다음 주 일정'));
    addItem(a, '_inbox', newItem('날짜 없는 할 일'));
    ensureIds(a);
    return a;
  }

  test('월간 위젯 자료: 42칸, 오늘 표시, 일정·공휴일·지난 일정', () {
    final m = monthWidgetData(sample(), today, 0);
    expect(m['title'], '2026년 10월');
    final cells = (m['cells'] as List).cast<Map<String, dynamic>>();
    expect(cells.length, 42);
    expect(cells.first['w'], 0); // 일요일 시작
    final t = cells.firstWhere((c) => c['t'] == 1);
    expect(t['iso'], '2026-10-13');
    expect((t['e'] as List).first['x'], '오늘 회의');
    expect((t['e'] as List).first['c'], 1); // 중요
    expect((t['e'] as List).last['c'], 2); // 완료는 뒤로, 회색
    expect(cells.firstWhere((c) => c['iso'] == '2026-10-09')['e'][0]['x'], '한글날');
    expect(cells.firstWhere((c) => c['iso'] == '2026-10-20')['e'][0]['x'], '다음 주 일정');
    expect(cells.firstWhere((c) => c['iso'] == '2026-09-30')['o'], 1); // 이전 달
    expect(monthWidgetData(sample(), today, 1)['title'], '2026년 11월');
  });

  test('할 일 위젯 자료: 지난 일정 → 오늘 → 보관함, 완료는 오늘 아래', () {
    final w = todoWidgetData(sample(), today);
    final rows = (w['rows'] as List).cast<Map<String, dynamic>>();
    expect(rows.map((r) => r['x']).toList(), ['지난 일정', '지난 보고', '오늘', '오늘 회의', '끝낸 일', '보관함', '날짜 없는 할 일']);
    expect(w['count'], 3); // 미완료만 센다
    expect(rows.firstWhere((r) => r['x'] == '끝낸 일')['d'], true);
    final id = rows.firstWhere((r) => r['x'] == '오늘 회의')['id'] as String;
    expect(findForToggle(sample(), today, id) == null, true); // 새 sample() 은 다른 id → 못 찾음
    final data = sample();
    final id2 = (todoWidgetData(data, today)['rows'] as List).firstWhere((r) => r['x'] == '지난 보고')['id'] as String;
    expect(findForToggle(data, today, id2)?['text'], '지난 보고');
  });

  test('할 일 위젯: 마감이 가까운 업무가 맨 위에 D-day 로 나온다', () {
    final data = sample();
    data['_work'] = {
      'a': {'id': 'a', 'title': '수요조사 제출', 'due': iso(addDays(today, 2)), 'done': false, 'created': 1},
      'b': {'id': 'b', 'title': '먼 업무', 'due': iso(addDays(today, 30)), 'done': false, 'created': 2},
    };
    final rows = (todoWidgetData(data, today)['rows'] as List).cast<Map<String, dynamic>>();
    expect(rows[0]['x'], '마감 임박');
    expect(rows[1]['k'], 'w');
    expect(rows[1]['s'], 'D-2');
    expect(rows.where((r) => r['k'] == 'w').length, 1);
  });
}
