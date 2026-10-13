// 한국 공휴일 (holidays_kr.py 와 같은 규칙). 음력 날짜는 2024~2030년분을 표로 둔다.
import 'core.dart';

const _fixed = {
  '1-1': '신정', '3-1': '삼일절', '5-5': '어린이날', '6-6': '현충일',
  '8-15': '광복절', '10-3': '개천절', '10-9': '한글날', '12-25': '성탄절',
};
const _substitutable = {'삼일절', '어린이날', '광복절', '개천절', '한글날', '성탄절', '부처님오신날'};
// 설날, 추석, 부처님오신날의 양력 [월, 일]
const _lunar = {
  2024: [[2, 10], [9, 17], [5, 15]], 2025: [[1, 29], [10, 6], [5, 5]],
  2026: [[2, 17], [9, 25], [5, 24]], 2027: [[2, 7], [9, 15], [5, 13]],
  2028: [[1, 27], [10, 3], [5, 2]], 2029: [[2, 13], [9, 22], [5, 20]],
  2030: [[2, 3], [9, 12], [5, 9]],
};
const _extra = {
  '2024-04-10': '국회의원선거', '2024-10-01': '국군의 날(임시)', '2025-01-27': '임시공휴일',
  '2025-06-03': '대통령선거', '2026-06-03': '지방선거', '2028-04-12': '국회의원선거',
};

final _cache = <int, Map<String, String>>{};

Map<String, String> holidaysOf(int year) {
  final cached = _cache[year];
  if (cached != null) return cached;
  final h = <String, String>{};
  final need = <DateTime>[];

  void put(DateTime d, String name) {
    if (h.containsKey(iso(d))) {
      need.add(d); // 다른 공휴일과 겹침 → 대체공휴일 필요
    } else {
      h[iso(d)] = name;
    }
  }

  _fixed.forEach((k, name) {
    final p = k.split('-');
    final d = DateTime(year, int.parse(p[0]), int.parse(p[1]));
    put(d, name);
    if (_substitutable.contains(name) && d.weekday >= 6) need.add(d);
  });
  final lunar = _lunar[year];
  if (lunar != null) {
    const names = ['설날', '추석'];
    const around = {'설날': '설 연휴', '추석': '추석 연휴'};
    for (var i = 0; i < 2; i++) {
      final c = DateTime(year, lunar[i][0], lunar[i][1]);
      final block = [addDays(c, -1), c, addDays(c, 1)];
      final clash = block.any((b) => h.containsKey(iso(b)));
      for (final b in block) {
        h.putIfAbsent(iso(b), () => sameDay(b, c) ? names[i] : around[names[i]]!);
      }
      if (clash || block.any((b) => b.weekday == 7)) need.add(block.last);
    }
    final b = DateTime(year, lunar[2][0], lunar[2][1]);
    put(b, '부처님오신날');
    if (b.weekday >= 6) need.add(b);
  }
  _extra.forEach((k, name) {
    if (k.startsWith('$year-')) h.putIfAbsent(k, () => name);
  });
  need.sort((a, b) => a.compareTo(b));
  for (final d in need) {
    var n = addDays(d, 1);
    while (n.weekday >= 6 || h.containsKey(iso(n))) {
      n = addDays(n, 1);
    }
    h[iso(n)] = '대체공휴일';
  }
  return _cache[year] = h;
}

String? holidayName(DateTime d) => holidaysOf(d.year)[iso(d)];
