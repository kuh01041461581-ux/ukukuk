import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

import 'package:desk_calendar_mobile/sync.dart';

void main() {
  test('PC(Python)가 암호화한 데이터를 폰(Dart)이 읽는다', () async {
    final f = jsonDecode(File('test/fixture_py.json').readAsStringSync()) as Map<String, dynamic>;
    final state = await decryptState(f['key'] as String, base64.decode(f['blob'] as String));
    expect(jsonEncode(state), jsonEncode(f['state']));
  });

  test('폰이 암호화한 데이터를 PC가 읽을 수 있도록 파일로 남긴다', () async {
    final f = jsonDecode(File('test/fixture_py.json').readAsStringSync()) as Map<String, dynamic>;
    final blob = await encryptState(f['key'] as String, {
      'items': [
        {'id': 'dart1', 'text': '폰에서 만든 일정', 'pri': 0, 'done': false, 'updated': 1760000001234, 'date': null}
      ],
      'memos': {},
      'deleted': {}
    });
    File('test/fixture_dart.b64').writeAsStringSync(base64.encode(blob));
    expect((await decryptState(f['key'] as String, blob))['items'][0]['text'], '폰에서 만든 일정');
  });
}
