import 'package:flutter_test/flutter_test.dart';

import 'package:desk_calendar_mobile/core.dart';
import 'package:desk_calendar_mobile/holidays.dart';

void main() {
  test('병합: 수정·삭제·신규가 합쳐진다', () {
    final a = <String, dynamic>{'_deleted': <String, dynamic>{}};
    addItem(a, '2026-10-13', newItem('회의'));
    ensureIds(a);
    final b = exportState(a);
    final phone = <String, dynamic>{'_deleted': <String, dynamic>{}};
    mergeState(phone, b);
    final t = (phone['2026-10-13']['todos'] as List).first as Map<String, dynamic>;
    t['text'] = '회의(폰에서 수정)';
    t['updated'] = nowMs() + 10;
    addItem(phone, '2026-10-14', newItem('폰 일정'));
    mergeState(a, exportState(phone));
    expect((a['2026-10-13']['todos'] as List).first['text'], '회의(폰에서 수정)');
    expect(a.containsKey('2026-10-14'), true);
    deleteItem(a, (a['2026-10-14']['todos'] as List).first as Map<String, dynamic>);
    mergeState(phone, exportState(a));
    expect((phone['2026-10-14']['todos'] as List).isEmpty, true);
  });

  test('반복·기간', () {
    final r = newItem('주간회의', extra: {'repeat': {'f': 'w', 'n': 1}});
    expect(repeatHit(r['repeat'] as Map<String, dynamic>, DateTime(2026, 10, 5), DateTime(2026, 10, 19)), true);
    expect(repeatHit(r['repeat'] as Map<String, dynamic>, DateTime(2026, 10, 5), DateTime(2026, 10, 20)), false);
    final m = {'f': 'm'};
    expect(repeatHit(m, DateTime(2026, 1, 31), DateTime(2026, 2, 28)), true);
  });

  test('공휴일(2026) — PC와 같은 결과', () {
    expect(holidayName(DateTime(2026, 3, 2)), '대체공휴일');
    expect(holidayName(DateTime(2026, 5, 25)), '대체공휴일');
    expect(holidayName(DateTime(2026, 8, 17)), '대체공휴일');
    expect(holidayName(DateTime(2026, 10, 5)), '대체공휴일');
    expect(holidayName(DateTime(2026, 9, 25)), '추석');
    expect(holidayName(DateTime(2025, 10, 8)), '대체공휴일');
    expect(holidayName(DateTime(2026, 10, 6)), null);
  });
}
