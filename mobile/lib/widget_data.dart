// 홈 화면 위젯에 넣을 자료를 만든다 (월간 달력 위젯, 할 일 위젯). 화면과 무관한 순수 계산이라 시험할 수 있다.
import 'core.dart';
import 'holidays.dart';

const _maxLines = 3; // 달력 한 칸에 보이는 일정 줄 수

/// 중요도 → 위젯 글자색 번호 (0 보통, 1 중요, 2 완료, 3 낮음)
int _colorCode(Json t) {
  if (t['done'] == true) return 2;
  final p = ((baseOf(t)['pri'] as num?)?.toInt() ?? 2);
  if (p <= 1) return 1;
  if (p == 3) return 3;
  return 0;
}

/// 이번 달을 기준으로 offset 달 뒤(음수는 앞)의 달력 6주(42칸) 자료
Map<String, dynamic> monthWidgetData(Json data, DateTime today, int offset) {
  today = dayOnly(today);
  final first = DateTime(today.year, today.month + offset, 1);
  final start = addDays(first, -(first.weekday % 7)); // 일요일 시작
  final cells = <Map<String, dynamic>>[];
  for (var i = 0; i < 42; i++) {
    final d = addDays(start, i);
    final h = holidayName(d);
    final rows = dayItems(data, d).where((r) {
      final e = itemEnd(r.t, r.start);
      return !(e != null && !sameDay(r.start, d)); // 기간 일정은 시작일에만 표시
    }).toList();
    rows.sort((a, b) => ((a.t['done'] == true) ? 1 : 0).compareTo((b.t['done'] == true) ? 1 : 0));
    final lines = <Map<String, dynamic>>[];
    if (h != null) lines.add({'x': h, 'c': 4}); // 4: 공휴일(붉게)
    for (final r in rows) {
      if (lines.length >= _maxLines) break;
      lines.add({'x': (r.t['text'] as String?) ?? '', 'c': _colorCode(r.t)});
    }
    final more = (rows.length + (h != null ? 1 : 0)) - lines.length;
    cells.add({
      'iso': iso(d),
      'd': d.day,
      'o': d.month == first.month ? 0 : 1,
      'w': d.weekday % 7, // 0=일 … 6=토
      't': sameDay(d, today) ? 1 : 0,
      'e': lines,
      if (more > 0) 'more': more,
    });
  }
  return {'title': '${first.year}년 ${first.month}월', 'cells': cells};
}

/// 지난 미완료 일정 (반복 일정 제외)
List<ItemRow> overdueRows(Json data, DateTime today) {
  today = dayOnly(today);
  final out = <ItemRow>[];
  data.forEach((key, v) {
    if (nonDateKeys.contains(key) || v is! Map) return;
    final s = parseDate(key);
    if (s == null || !s.isBefore(today)) return;
    final list = (v['todos'] as List?) ?? const [];
    for (final t0 in list) {
      final t = t0 as Json;
      if (t['done'] == true || t['repeat'] != null) continue;
      if ((itemEnd(t, s) ?? s).isBefore(today)) out.add(ItemRow(list, t, s));
    }
  });
  out.sort((a, b) => a.start.compareTo(b.start));
  return out;
}

const todoWidgetMaxRows = 12;

/// 할 일 위젯: 지난 미완료 → 오늘 → 보관함 순서. 행: {k:'h'|'i', id, x(글자), s(보조: 시간/날짜), d(완료)}
Map<String, dynamic> todoWidgetData(Json data, DateTime today) {
  today = dayOnly(today);
  final rows = <Map<String, dynamic>>[];
  var left = todoWidgetMaxRows;
  var total = 0;

  void section(String title, List<Map<String, dynamic>> items) {
    if (items.isEmpty || left < 2) return;
    rows.add({'k': 'h', 'x': title});
    left--;
    for (final it in items) {
      if (left <= 0) break;
      rows.add(it);
      left--;
    }
  }

  Map<String, dynamic> item(Json t, {String sub = ''}) => {
        'k': 'i',
        'id': baseOf(t)['id'] ?? '',
        'x': (t['text'] as String?) ?? '',
        's': sub,
        'd': t['done'] == true,
        'c': _colorCode(t),
      };

  final late = overdueRows(data, today);
  total += late.length;
  section('지난 일정', [for (final r in late.take(4)) item(r.t, sub: '${r.start.month}/${r.start.day}')]);

  final todayRows = dayItems(data, today);
  todayRows.sort((a, b) {
    final da = a.t['done'] == true ? 1 : 0, db = b.t['done'] == true ? 1 : 0;
    if (da != db) return da.compareTo(db);
    return ((a.t['time'] as String?) ?? '99:99').compareTo((b.t['time'] as String?) ?? '99:99');
  });
  total += todayRows.where((r) => r.t['done'] != true).length;
  section('오늘', [for (final r in todayRows) item(r.t, sub: (r.t['time'] as String?) ?? '')]);

  final inbox = (((data['_inbox'] as Map?)?['todos'] as List?) ?? const []).cast<Json>().where((t) => t['done'] != true).toList();
  total += inbox.length;
  section('보관함', [for (final t in inbox) item(t)]);

  return {'title': '할 일', 'count': total, 'rows': rows};
}

/// id 로 오늘 보이는 일정(반복 포함) 또는 보관함·지난 일정을 찾는다 (위젯에서 체크할 때 사용)
Json? findForToggle(Json data, DateTime today, String id) {
  today = dayOnly(today);
  for (final r in dayItems(data, today)) {
    if (baseOf(r.t)['id'] == id) return r.t;
  }
  for (final r in overdueRows(data, today)) {
    if (r.t['id'] == id) return r.t;
  }
  for (final t in (((data['_inbox'] as Map?)?['todos'] as List?) ?? const [])) {
    if ((t as Json)['id'] == id) return t;
  }
  return null;
}
