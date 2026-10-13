import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:desk_calendar_mobile/core.dart';

void main() {
  final j = jsonDecode(File('test/core_cases.json').readAsStringSync()) as Map<String, dynamic>;

  test('반복 규칙: PC(Python)와 같은 날짜에 나온다', () {
    for (final c in j['repeat'] as List) {
      final start = parseDate(c['start'] as String)!;
      final rep = repeatFromCode(c['code'] as String, start, null)!;
      expect(rep, c['rep'], reason: c['code'] as String);
      expect(repeatLabel(rep, start), c['label']);
      expect(repeatCode(rep), c['code_back']);
      final hits = <String>[];
      for (var k = 0; k < 800 && hits.length < 14; k++) {
        final d = addDays(start, k);
        if (repeatHit(rep, start, d)) hits.add(iso(d));
      }
      expect(hits, c['hits'], reason: '${c['code']} ${c['start']}');
    }
  });

  test('근무일 계산: 주말·공휴일 빼고', () {
    final hol = (j['holidays'] as List).cast<String>().toSet();
    bool h(DateTime d) => hol.contains(iso(d));
    for (final c in j['workdays'] as List) {
      final s = parseDate(c['start'] as String)!;
      final r = addWorkdays(s, c['n'] as int, h);
      expect(iso(r), c['out'], reason: '${c['start']} +${c['n']}');
      expect(workdaysBetween(s, r, h), c['between']);
    }
  });

  test('업무 묶음 동기화: 늦게 고친 쪽이 이기고 삭제 기록을 따른다', () {
    final a = <String, dynamic>{'_deleted': <String, dynamic>{}};
    final remote = {
      'items': [],
      'memos': {},
      'deleted': {'gone': nowMs()},
      'work': {
        'c1': {'id': 'c1', 'title': '자료 제출', 'updated': 10, 'targets': []},
        'gone': {'id': 'gone', 'title': 'x', 'updated': 5},
      }
    };
    expect(mergeState(a, remote), true);
    expect((a['_work'] as Map).keys.toList(), ['c1']);
    final older = {
      'work': {
        'c1': {'id': 'c1', 'title': '옛 이름', 'updated': 3}
      }
    };
    mergeState(a, older);
    expect((a['_work'] as Map)['c1']['title'], '자료 제출');
    final out = exportState(a);
    expect((out['work'] as Map)['c1']['title'], '자료 제출');
    expect(dayItems(a, DateTime(2026, 10, 13)), isEmpty);
  });
}
