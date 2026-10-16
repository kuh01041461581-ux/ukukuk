// 한 줄 빠른 입력 파서 (PC 의 quickadd.py 와 같은 규칙, 같은 시험 문장으로 맞춰 봄)
const _wd = {'월': 0, '화': 1, '수': 2, '목': 3, '금': 4, '토': 5, '일': 6};
const _priWords = {'중요': 1, '긴급': 1, '보통': 2, '낮음': 3};
const _repeatWords = [
  ['평일마다', 'wd'], ['매 평일', 'wd'], ['매일', 'd'], ['매주', 'w'], ['매월', 'm'], ['매년', 'y'],
];

class QuickResult {
  String text;
  DateTime? date;
  String? time;
  int? pri;
  String? repeat;
  QuickResult(this.text, {this.date, this.time, this.pri, this.repeat});

  /// 미리보기: '10/14(수) 내일 · 15:00 · 중요'
  String describe(DateTime today) {
    final parts = <String>[];
    final d = date;
    if (d != null) {
      final diff = DateTime(d.year, d.month, d.day).difference(DateTime(today.year, today.month, today.day)).inDays;
      final tag = {0: '오늘', 1: '내일', 2: '모레'}[diff];
      parts.add('${d.month}/${d.day}(${'월화수목금토일'[d.weekday - 1]})${tag != null ? ' $tag' : ''}');
    }
    if (time != null) parts.add(time!);
    if (pri != null) parts.add({1: '중요', 2: '보통', 3: '낮음'}[pri]!);
    if (repeat != null) parts.add({'d': '매일', 'wd': '평일마다', 'w': '매주', 'm': '매월', 'y': '매년'}[repeat]!);
    return parts.join(' · ');
  }
}

DateTime? _mk(int y, int m, int d) {
  final x = DateTime(y, m, d);
  return (x.year == y && x.month == m && x.day == d) ? x : null;
}

DateTime _addDays(DateTime d, int n) => DateTime(d.year, d.month, d.day + n);

DateTime _nextWeekday(DateTime today, int wd, int? weekShift) {
  final cur = today.weekday - 1;
  if (weekShift == null) return _addDays(today, (wd - cur) % 7);
  final monday = _addDays(today, -cur);
  return _addDays(monday, 7 * weekShift + wd);
}

DateTime? _dayOfMonth(DateTime today, int day, int? monthShift) {
  DateTime? at(int add) {
    var y = today.year, m = today.month + add;
    y += (m - 1) ~/ 12;
    m = (m - 1) % 12 + 1;
    return _mk(y, m, day);
  }

  if (monthShift == null) {
    for (final add in [0, 1, 2]) {
      final d = at(add);
      if (d != null && !d.isBefore(DateTime(today.year, today.month, today.day))) return d;
    }
    return null;
  }
  return at(monthShift);
}

QuickResult parseQuick(String text, DateTime today) {
  today = DateTime(today.year, today.month, today.day);
  var s = ' ${text.trim()} ';
  DateTime? date;
  String? time;
  int? pri;
  String? repeat;

  RegExpMatch? cut(String rx) {
    final m = RegExp(rx).firstMatch(s);
    if (m != null) s = '${s.substring(0, m.start)} ${s.substring(m.end)}';
    return m;
  }

  var m = cut(r'(?<=\s)!(중요|긴급|낮음|보통)(?=\s)');
  if (m != null) {
    pri = _priWords[m.group(1)];
  } else if (cut(r'(?<=\s)!{2,3}(?=\s)') != null) {
    pri = 1;
  }

  for (final w in _repeatWords) {
    if (cut(RegExp.escape(w[0]) + r'(?=\s|[가-힣]|$)') != null) {
      repeat = w[1];
      break;
    }
  }

  DateTime? d;
  m = cut(r'(20\d{2})\s*[.\-/년]\s*(\d{1,2})\s*[.\-/월]\s*(\d{1,2})\s*일?(?!\d)');
  if (m != null) d = _mk(int.parse(m.group(1)!), int.parse(m.group(2)!), int.parse(m.group(3)!));
  // 붙여 쓴 날짜: 261024 (연월일 6자리) · 20261024 (8자리)
  if (d == null) {
    final cm = RegExp(r'(?<![\d.:/\-])(\d{2}|20\d{2})(\d{2})(\d{2})(?![\d.:/\-])').firstMatch(s);
    if (cm != null) {
      final yy = int.parse(cm.group(1)!);
      final y = yy < 100 ? 2000 + yy : yy;
      final cd = _mk(y, int.parse(cm.group(2)!), int.parse(cm.group(3)!));
      if (cd != null && y >= 2020 && y <= 2049) {
        s = '${s.substring(0, cm.start)} ${s.substring(cm.end)}';
        d = cd;
      }
    }
  }
  DateTime? nextYearIfOld(DateTime? x, int mo, int da) {
    if (x != null && x.isBefore(today) && today.difference(x).inDays > 180) return _mk(today.year + 1, mo, da);
    return x;
  }

  if (d == null) {
    m = cut(r'(?<!\d)(\d{1,2})\s*월\s*(\d{1,2})\s*일?(?![가-힣\d])');
    if (m != null) {
      final mo = int.parse(m.group(1)!), da = int.parse(m.group(2)!);
      d = nextYearIfOld(_mk(today.year, mo, da), mo, da);
    }
  }
  if (d == null) {
    m = cut(r'(?<![\d:.])(\d{1,2})\s*[/.]\s*(\d{1,2})(?![\d:.])');
    if (m != null) {
      final mo = int.parse(m.group(1)!), da = int.parse(m.group(2)!);
      if (mo >= 1 && mo <= 12 && da >= 1 && da <= 31) d = nextYearIfOld(_mk(today.year, mo, da), mo, da);
    }
  }
  if (d == null) {
    m = cut(r'(?<![가-힣])(내일모레|모레|글피|내일|오늘)');
    if (m != null) d = _addDays(today, {'오늘': 0, '내일': 1, '모레': 2, '내일모레': 2, '글피': 3}[m.group(1)]!);
  }
  if (d == null) {
    m = cut(r'(\d{1,3})\s*(일|주)\s*(뒤|후)');
    if (m != null) d = _addDays(today, int.parse(m.group(1)!) * (m.group(2) == '주' ? 7 : 1));
  }
  if (d == null) {
    m = cut(r'(다다음\s*주|다음\s*주|담주|이번\s*주)\s*(월|화|수|목|금|토|일)\s*(?:요일|(?![가-힣]))');
    if (m != null) {
      final g = m.group(1)!;
      final shift = g.startsWith('다다음') ? 2 : (g.startsWith('다음') || g == '담주') ? 1 : 0;
      d = _nextWeekday(today, _wd[m.group(2)]!, shift);
    }
  }
  if (d == null) {
    m = cut(r'(?<![가-힣])(월|화|수|목|금|토|일)요일');
    if (m != null) d = _nextWeekday(today, _wd[m.group(1)]!, null);
  }
  if (d == null) {
    m = cut(r'(다음\s*주|담주)(?![가-힣])');
    if (m != null) d = _addDays(today, 7);
  }
  if (d == null) {
    m = cut(r'(다음\s*달|이번\s*달)\s*(\d{1,2})\s*일(?![가-힣])');
    if (m != null) d = _dayOfMonth(today, int.parse(m.group(2)!), m.group(1)!.startsWith('다음') ? 1 : 0);
  }
  if (d == null) {
    m = cut(r'(월말|이번\s*달\s*말|말일)(?![가-힣])');
    if (m != null) d = DateTime(today.year, today.month + 1, 0);
  }
  if (d == null) {
    m = cut(r'(?<![\d가-힣])(\d{1,2})\s*일(?![가-힣\d])');
    if (m != null) d = _dayOfMonth(today, int.parse(m.group(1)!), null);
  }
  date = d;

  m = cut(r'(?<!\d)(\d{1,2}):(\d{2})(?!\d)');
  if (m != null && int.parse(m.group(1)!) < 24 && int.parse(m.group(2)!) < 60) {
    time = '${m.group(1)!.padLeft(2, '0')}:${m.group(2)}';
  } else if (cut(r'정오(?![가-힣])') != null) {
    time = '12:00';
  } else {
    m = cut(r'(오전|오후|아침|점심|저녁|밤|새벽)?\s*(\d{1,2})\s*시\s*(?:(\d{1,2})\s*분|(반))?');
    if (m != null) {
      var h = int.parse(m.group(2)!);
      final pre = m.group(1) ?? '';
      if ((pre == '오후' || pre == '저녁' || pre == '밤') && h < 12) {
        h += 12;
      } else if ((pre == '오전' || pre == '아침' || pre == '새벽') && h == 12) {
        h = 0;
      } else if (pre.isEmpty && h >= 1 && h <= 6) {
        h += 12;
      }
      if (h < 24) {
        final mi = m.group(4) != null ? 30 : int.parse(m.group(3) ?? '0');
        time = '${h.toString().padLeft(2, '0')}:${mi.toString().padLeft(2, '0')}';
      }
    }
  }

  s = s.replaceAll(RegExp(r'\s+'), ' ').trim();
  final toks = s.split(' ');
  if (pri == null && toks.length >= 2 && (toks.last == '중요' || toks.last == '긴급' || toks.last == '낮음')) {
    pri = _priWords[toks.last]; // '주간업무취합 261024 중요' — 끝에 붙인 중요도
    s = toks.sublist(0, toks.length - 1).join(' ');
  }
  s = s.replaceFirst(RegExp(r'^(에|까지|부터|은|는|에는|에서)\s+'), '').trim();
  if (repeat != null && date == null) date = today;
  return QuickResult(s.isEmpty ? text.trim() : s, date: date, time: time, pri: pri, repeat: repeat);
}
