// PC 앱(core.py)과 같은 규칙: 일정 항목, 반복, 동기화 병합. 데이터 모양도 같아서 서로 주고받을 수 있다.
import 'dart:math';

typedef Json = Map<String, dynamic>;

const nonDateKeys = {'_inbox', '_deleted', '_work'}; // _work: 업무(공문·제출 현황) 묶음
final _rnd = Random.secure();

int nowMs() => DateTime.now().millisecondsSinceEpoch;
String newId() => List.generate(12, (_) => '0123456789abcdef'[_rnd.nextInt(16)]).join();

String two(int n) => n.toString().padLeft(2, '0');
String iso(DateTime d) => '${d.year.toString().padLeft(4, '0')}-${two(d.month)}-${two(d.day)}';
DateTime dayOnly(DateTime d) => DateTime(d.year, d.month, d.day);
DateTime addDays(DateTime d, int n) => DateTime(d.year, d.month, d.day + n);
bool sameDay(DateTime a, DateTime b) => a.year == b.year && a.month == b.month && a.day == b.day;
int daysBetween(DateTime a, DateTime b) =>
    DateTime.utc(b.year, b.month, b.day).difference(DateTime.utc(a.year, a.month, a.day)).inDays;

DateTime? parseDate(String? s) {
  if (s == null || s.length < 10) return null;
  try {
    return DateTime(int.parse(s.substring(0, 4)), int.parse(s.substring(5, 7)), int.parse(s.substring(8, 10)));
  } catch (_) {
    return null;
  }
}

Json touch(Json t) {
  t['updated'] = nowMs();
  return t;
}

Json newItem(String text, {Json? extra}) {
  final t = <String, dynamic>{'id': newId(), 'text': text, 'done': false, 'pri': 2, 'updated': nowMs()};
  if (extra != null) t.addAll(extra);
  return t;
}

bool ensureIds(Json data) {
  var changed = false;
  data.forEach((key, v) {
    if (key == '_deleted' || key == '_work' || v is! Map) return;
    for (final t in (v['todos'] as List? ?? const [])) {
      if (t['id'] == null || t['id'] == '') {
        t['id'] = newId();
        changed = true;
      }
      if (t['updated'] == null) {
        t['updated'] = nowMs();
        changed = true;
      }
    }
  });
  data.putIfAbsent('_deleted', () => <String, dynamic>{});
  return changed;
}

/// 기간 일정의 종료일 (하루짜리면 null)
DateTime? itemEnd(Json t, DateTime start) {
  final e = parseDate(t['end'] as String?);
  return (e != null && e.isAfter(start)) ? e : null;
}

// 고르는 이름(code) → 저장 모양. 분기·반기는 '매월'의 간격(n)으로 저장 (PC core.py 와 같음)
const repeatLabels = [
  ['', '없음'], ['d', '매일'], ['wd', '평일'], ['w', '매주'], ['m', '매월'], ['ml', '매월 말일'],
  ['mw', '매월 n째 요일'], ['q', '분기마다'], ['h', '반기마다'], ['y', '매년']
];
const _nth = {1: '첫째', 2: '둘째', 3: '셋째', 4: '넷째', -1: '마지막'};
const wdKo = '월화수목금토일';

int nthOf(DateTime d) {
  final k = (d.day - 1) ~/ 7 + 1;
  return k >= 5 ? -1 : k;
}

Json? repeatFromCode(String code, DateTime? start, String? until) {
  if (code.isEmpty) return null;
  final Json rep;
  if (code == 'q') {
    rep = {'f': 'm', 'n': 3};
  } else if (code == 'h') {
    rep = {'f': 'm', 'n': 6};
  } else if (code == 'mw') {
    rep = {'f': 'mw', 'n': 1, 'k': start == null ? 1 : nthOf(start)};
  } else {
    rep = {'f': code, 'n': 1};
  }
  if (until != null && until.isNotEmpty) rep['until'] = until;
  return rep;
}

String repeatCode(Json? rep) {
  if (rep == null) return '';
  final f = (rep['f'] as String?) ?? '';
  final n = (rep['n'] as num?)?.toInt() ?? 1;
  if (f == 'm' && n == 3) return 'q';
  if (f == 'm' && n == 6) return 'h';
  return f;
}

String repeatLabel(Json rep, [DateTime? start]) {
  final code = repeatCode(rep);
  if (code == 'mw') {
    final wd = start == null ? '' : wdKo[start.weekday - 1];
    return '매월 ${_nth[(rep['k'] as num?)?.toInt() ?? 1] ?? ''} $wd요일'.replaceAll('  ', ' ');
  }
  final n = (rep['n'] as num?)?.toInt() ?? 1;
  if (code == 'm' && n > 1) return '$n개월마다';
  return repeatLabels.firstWhere((e) => e[0] == code, orElse: () => ['', ''])[1];
}

bool repeatHit(Json rep, DateTime start, DateTime d) {
  if (d.isBefore(start)) return false;
  final until = parseDate(rep['until'] as String?);
  if (until != null && d.isAfter(until)) return false;
  final n = max(1, (rep['n'] as num?)?.toInt() ?? 1);
  final f = rep['f'];
  switch (f) {
    case 'd':
      return daysBetween(start, d) % n == 0;
    case 'wd':
      return d.weekday <= 5;
    case 'w':
      return daysBetween(start, d) % (7 * n) == 0;
    case 'm':
    case 'ml':
    case 'mw':
      final months = (d.year - start.year) * 12 + d.month - start.month;
      if (months % n != 0) return false;
      final last = DateTime(d.year, d.month + 1, 0).day;
      if (f == 'ml') return d.day == last;
      if (f == 'mw') {
        if (d.weekday != start.weekday) return false;
        final k = (rep['k'] as num?)?.toInt() ?? 1;
        return k < 0 ? d.day + 7 > last : (d.day - 1) ~/ 7 + 1 == k;
      }
      return d.day == min(start.day, last);
    case 'y':
      if ((d.year - start.year) % n != 0) return false;
      if (start.month == 2 && start.day == 29) {
        return d.month == 2 && d.day == min(29, DateTime(d.year, 3, 0).day);
      }
      return d.month == start.month && d.day == start.day;
  }
  return false;
}

// ── 근무일 (주말·공휴일 빼고 세기) ──
bool isWorkday(DateTime d, [bool Function(DateTime)? holiday]) =>
    d.weekday <= 5 && !(holiday?.call(d) ?? false);

DateTime addWorkdays(DateTime start, int n, [bool Function(DateTime)? holiday]) {
  var d = dayOnly(start);
  if (n <= 0) {
    while (!isWorkday(d, holiday)) {
      d = addDays(d, 1);
    }
    return d;
  }
  while (n > 0) {
    d = addDays(d, 1);
    if (isWorkday(d, holiday)) n--;
  }
  return d;
}

int workdaysBetween(DateTime a, DateTime b, [bool Function(DateTime)? holiday]) {
  if (b.isBefore(a)) return -workdaysBetween(b, a, holiday);
  var c = 0;
  for (var k = 1; k <= daysBetween(a, b); k++) {
    if (isWorkday(addDays(a, k), holiday)) c++;
  }
  return c;
}

/// 반복 일정의 d 날짜용 사본 (완료 여부는 날짜별로 따로)
Json occurrence(Json t, DateTime d) {
  final v = Map<String, dynamic>.from(t);
  v['_src'] = t;
  v['_occ'] = d;
  v['done'] = ((t['done_dates'] as List?) ?? const []).contains(iso(d));
  return v;
}

Json baseOf(Json t) => (t['_src'] as Json?) ?? t;

class ItemRow {
  final List list; // 항목이 들어 있는 리스트
  final Json t; // 항목(반복이면 사본)
  final DateTime start;
  ItemRow(this.list, this.t, this.start);
}

List<ItemRow> dayItems(Json data, DateTime d) {
  final out = <ItemRow>[];
  data.forEach((key, v) {
    if (nonDateKeys.contains(key) || v is! Map) return;
    final s = parseDate(key);
    if (s == null || s.isAfter(d)) return;
    final todos = (v['todos'] as List?) ?? const [];
    for (final t0 in todos) {
      final t = t0 as Json;
      final rep = t['repeat'];
      if (rep != null) {
        if (repeatHit(rep as Json, s, d)) out.add(ItemRow(todos, occurrence(t, d), s));
        continue;
      }
      final e = itemEnd(t, s);
      if (sameDay(s, d) || (e != null && !e.isBefore(d))) out.add(ItemRow(todos, t, s));
    }
  });
  out.sort((a, b) => sortCompare(a.t, b.t));
  return out;
}

int sortCompare(Json a, Json b) {
  final da = a['done'] == true ? 1 : 0, db = b['done'] == true ? 1 : 0;
  if (da != db) return da - db;
  final pa = (a['pri'] as num?)?.toInt() ?? 2, pb = (b['pri'] as num?)?.toInt() ?? 2;
  if (pa != pb) return pa - pb;
  return ((a['time'] as String?) ?? '99:99').compareTo((b['time'] as String?) ?? '99:99');
}

/// 항목이 들어 있는 (키, 리스트)
(String?, List?) findOwner(Json data, Json t0) {
  final t = baseOf(t0);
  for (final e in data.entries) {
    if (e.key == '_deleted' || e.key == '_work' || e.value is! Map) continue;
    final list = e.value['todos'] as List?;
    if (list == null) continue;
    for (final x in list) {
      if (identical(x, t)) return (e.key, list);
    }
  }
  return (null, null);
}

bool removeItem(Json data, Json t0) {
  final t = baseOf(t0);
  final (_, list) = findOwner(data, t);
  if (list == null) return false;
  list.removeWhere((x) => identical(x, t));
  return true;
}

void deleteItem(Json data, Json t0) {
  final t = baseOf(t0);
  if (removeItem(data, t) && t['id'] != null) {
    (data.putIfAbsent('_deleted', () => <String, dynamic>{}) as Map)[t['id']] = nowMs();
  }
}

void addItem(Json data, String key, Json t) {
  final e = data.putIfAbsent(key, () => <String, dynamic>{'memo': '', 'todos': <dynamic>[]}) as Map;
  (e.putIfAbsent('todos', () => <dynamic>[]) as List).add(t);
}

// ── 동기화 ──
Json exportState(Json data) {
  final items = <Json>[];
  final memos = <String, dynamic>{};
  data.forEach((key, v) {
    if (key == '_deleted' || key == '_work' || v is! Map) return;
    for (final t in (v['todos'] as List? ?? const [])) {
      if (t['id'] == null) continue;
      final r = <String, dynamic>{};
      (t as Map).forEach((k, val) {
        if (!k.toString().startsWith('_')) r[k.toString()] = val;
      });
      r['date'] = key == '_inbox' ? null : key;
      items.add(r);
    }
    // 지운 메모도 '비웠다'는 기록(ts)으로 보낸다: 안 그러면 다른 기기의 옛 메모가 되살아난다
    if (key != '_inbox' && (((v['memo'] as String?) ?? '').trim().isNotEmpty || (v['memo_ts'] ?? 0) != 0)) {
      memos[key] = {'memo': v['memo'] ?? '', 'ts': v['memo_ts'] ?? 0};
    }
  });
  final work = <String, dynamic>{};
  (data['_work'] as Map? ?? {}).forEach((i, c) {
    final m = <String, dynamic>{};
    (c as Map).forEach((k, v) {
      if (!k.toString().startsWith('_')) m[k.toString()] = v;
    });
    work[i.toString()] = m;
  });
  return {
    'items': items,
    'memos': memos,
    'deleted': Map<String, dynamic>.from(data['_deleted'] as Map? ?? {}),
    'work': work
  };
}

int _i(dynamic v) => (v as num?)?.toInt() ?? 0;

/// 상대 기기의 상태를 합친다 (항목마다 더 늦게 수정된 쪽이 이김). 바뀐 게 있으면 true
bool mergeState(Json data, Json remote) {
  var changed = false;
  final dele = data.putIfAbsent('_deleted', () => <String, dynamic>{}) as Map;
  (remote['deleted'] as Map? ?? {}).forEach((i, ts) {
    if (_i(ts) > _i(dele[i])) {
      dele[i] = ts;
      changed = true;
    }
  });
  final idx = <String, (String, Json)>{};
  data.forEach((key, v) {
    if (key == '_deleted' || key == '_work' || v is! Map) return;
    for (final t in (v['todos'] as List? ?? const [])) {
      if (t['id'] != null) idx[t['id']] = (key, t as Json);
    }
  });
  for (final e in dele.entries.toList()) {
    final hit = idx[e.key];
    if (hit != null && _i(hit.$2['updated']) <= _i(e.value)) {
      (data[hit.$1]['todos'] as List).removeWhere((x) => identical(x, hit.$2));
      idx.remove(e.key);
      changed = true;
    }
  }
  for (final r0 in (remote['items'] as List? ?? const [])) {
    final r = r0 as Map;
    final id = r['id'];
    if (id == null || _i(dele[id]) >= _i(r['updated'])) continue;
    final key = (r['date'] as String?) ?? '_inbox';
    final t = <String, dynamic>{};
    r.forEach((k, v) {
      if (k != 'date') t[k.toString()] = v;
    });
    final old = idx[id];
    if (old != null) {
      if (_i(r['updated']) > _i(old.$2['updated'])) {
        (data[old.$1]['todos'] as List).removeWhere((x) => identical(x, old.$2));
        addItem(data, key, t);
        changed = true;
      }
    } else {
      addItem(data, key, t);
      changed = true;
    }
  }
  (remote['memos'] as Map? ?? {}).forEach((d, m) {
    final e = data[d];
    if (_i(m['ts']) > (e is Map ? _i(e['memo_ts']) : 0)) {
      final x = data.putIfAbsent(d, () => <String, dynamic>{'memo': '', 'todos': <dynamic>[]}) as Map;
      x['memo'] = m['memo'] ?? '';
      x['memo_ts'] = m['ts'];
      changed = true;
    }
  });
  // 업무 묶음: 통째로, 더 늦게 고친 쪽이 이김
  final work = Map<String, dynamic>.from(data['_work'] as Map? ?? {});
  final gone = work.keys.where((i) => _i(dele[i]) >= _i((work[i] as Map)['updated'])).toList();
  for (final i in gone) {
    work.remove(i);
    changed = true;
  }
  (remote['work'] as Map? ?? {}).forEach((i, c) {
    if (_i(dele[i]) >= _i(c['updated'])) return;
    final cur = work[i] as Map?;
    if (cur == null || _i(c['updated']) > _i(cur['updated'])) {
      work[i.toString()] = Map<String, dynamic>.from(c as Map);
      changed = true;
    }
  });
  if (work.isNotEmpty || data.containsKey('_work')) data['_work'] = work;
  final cutoff = nowMs() - 90 * 86400 * 1000;
  dele.removeWhere((_, ts) => _i(ts) < cutoff);
  return changed;
}

/// (오늘 남은 일정, 지난 미완료, 앞으로 3일 안 미완료)
(int, int, int) summaryCounts(Json data) {
  final today = dayOnly(DateTime.now());
  var late = 0;
  data.forEach((key, v) {
    if (nonDateKeys.contains(key) || v is! Map) return;
    final s0 = parseDate(key);
    if (s0 == null || !s0.isBefore(today)) return;
    for (final t0 in (v['todos'] as List? ?? const [])) {
      final t = t0 as Json;
      if (t['done'] == true || t['repeat'] != null) continue;
      if ((itemEnd(t, s0) ?? s0).isBefore(today)) late++;
    }
  });
  int undone(DateTime d) => dayItems(data, d).where((r) => r.t['done'] != true).length;
  var soon = 0;
  for (var k = 1; k <= 3; k++) {
    soon += undone(addDays(today, k));
  }
  return (undone(today), late, soon);
}

