import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:desk_calendar_mobile/analyze.dart';

String iso(DateTime? d) => d == null ? '' : '${d.year}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

void main() {
  test('붙여넣기 분석: PC(Python)와 같은 글에서 같은 후보', () {
    final cases = jsonDecode(File('test/analyze_cases.json').readAsStringSync()) as List;
    for (final c in cases) {
      final items = analyzeText(c['text'] as String, DateTime.parse(c['today'] as String));
      final got = [
        for (final i in items)
          {
            'date': i.date == null ? null : iso(i.date),
            'end': i.end == null ? null : iso(i.end),
            'time': i.time,
            'text': i.text,
            'pri': i.pri,
            'kind': i.kind,
          }
      ];
      expect(got, c['items'], reason: (c['text'] as String).split('\n').first);
    }
  });
}
