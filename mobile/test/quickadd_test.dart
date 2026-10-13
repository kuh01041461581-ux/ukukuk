import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:desk_calendar_mobile/quickadd.dart';

void main() {
  test('빠른 입력: PC(Python)와 같은 문장에서 같은 결과', () {
    final j = jsonDecode(File('test/quickadd_cases.json').readAsStringSync()) as Map<String, dynamic>;
    final today = DateTime.parse(j['today'] as String);
    for (final c in j['cases'] as List) {
      final r = parseQuick(c['in'] as String, today);
      final o = c['out'] as Map<String, dynamic>;
      final got = {
        'text': r.text,
        'date': r.date == null ? null : '${r.date!.year}-${r.date!.month.toString().padLeft(2, '0')}-${r.date!.day.toString().padLeft(2, '0')}',
        'time': r.time,
        'pri': r.pri,
        'repeat': r.repeat,
      };
      expect(got, o, reason: c['in'] as String);
    }
  });
}
