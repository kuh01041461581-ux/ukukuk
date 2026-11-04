import 'package:flutter_test/flutter_test.dart';
import 'package:desk_calendar_mobile/core.dart';

void main() {
  teamTests();
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

void teamTests() {
  test('부서 공유: 작성자가 붙고, 명단이 합쳐지고, 남의 일정으로 바꾼 수정은 받지 않는다', () {
    final kim = <String, dynamic>{'_deleted': {}};
    final lee = <String, dynamic>{'_deleted': {}};
    setAuthor('u_kim');
    final a = newItem('행사 준비 회의');
    addItem(kim, '2026-11-10', a);
    kim['_team'] = {'u_kim': {'name': '김주무관', 'color': '#ff6b81', 'updated': 5}};
    setAuthor(null);
    expect(a['au'], 'u_kim');
    expect(mergeState(lee, exportState(kim)), true);
    expect(((lee['2026-11-10'] as Map)['todos'] as List).single['text'], '행사 준비 회의');
    expect(((lee['_team'] as Map)['u_kim'] as Map)['name'], '김주무관');
    final forged = exportState(lee);
    final r = (forged['items'] as List).single as Map;
    r['au'] = 'u_lee';
    r['text'] = '바꿔치기';
    r['updated'] = nowMs() + 5000;
    mergeState(kim, forged);
    expect(((kim['2026-11-10'] as Map)['todos'] as List).single['text'], '행사 준비 회의');
  });
}
