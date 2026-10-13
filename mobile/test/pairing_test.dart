import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:desk_calendar_mobile/pairing.dart';

void main() {
  test('연결 코드: PC 가 만든 DC1: 코드와 QR 내용(JSON)을 모두 해석', () {
    const code = 'DC1:eyJ2IjoyLCJyZWxheSI6Imh0dHBzOi8vZXhhbXBsZS53b3JrZXJzLmRldiIsImlkIjoiQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUEiLCJrZXkiOiJhMnRyYTJ0cmEydHJhMnRyYTJ0cmEydHJhMnRyYTJ0cmEydHJhMnRyYTJzPSJ9';
    final a = parsePairing(code);
    expect(a?['relay'], 'https://example.workers.dev');
    expect(a?['id'], 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA');
    final b = parsePairing(jsonEncode({'relay': 'https://x', 'id': 'i', 'key': 'k'}));
    expect(b?['id'], 'i');
    expect(parsePairing('아무 글자'), isNull);
    expect(parsePairing('DC1:!!!'), isNull);
  });
}
