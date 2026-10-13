import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:desk_calendar_mobile/gongmun.dart';

String iso(DateTime d) => '${d.year}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

void main() {
  test('공문 파서: PC(Python)와 같은 문서에서 같은 일정', () {
    final cases = jsonDecode(File('test/gongmun_cases.json').readAsStringSync()) as List;
    for (final c in cases) {
      final (title, items) = parseGongmun(c['text'] as String, DateTime.parse(c['today'] as String));
      expect(title, c['title']);
      final got = [
        for (final i in items)
          {
            'date': iso(i.date),
            'end': i.end == null ? null : iso(i.end!),
            'time': i.time,
            'text': i.text,
            'doc': i.doc,
            'period': i.period,
            'pri': i.pri,
          }
      ];
      expect(got, c['items'], reason: title);
    }
  });
}
