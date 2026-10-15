import 'package:flutter_test/flutter_test.dart';
import 'package:desk_calendar_mobile/core.dart';

void main() {
  test('메모를 지우면 다른 기기의 옛 메모가 되살아나지 않는다', () {
    final phone = <String, dynamic>{'2026-10-20': {'memo': '옛 메모', 'memo_ts': 1000, 'todos': []}};
    final pc = <String, dynamic>{'2026-10-20': {'memo': '', 'memo_ts': 2000, 'todos': []}};
    final out = exportState(pc);
    expect((out['memos'] as Map)['2026-10-20'], {'memo': '', 'ts': 2000});
    expect(mergeState(phone, out), true);
    expect((phone['2026-10-20'] as Map)['memo'], '');
    expect(exportState({'2026-10-21': {'memo': '', 'todos': []}})['memos'], isEmpty);
  });
}
