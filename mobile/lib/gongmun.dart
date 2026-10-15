// 공문(PDF) 텍스트에서 일정을 뽑는 파서: PC 의 gongmun.py 와 같은 규칙 (같은 시험 문서로 결과를 맞춰 봄)
const _markers = '○ㅇ□■※•▪◦·ㆍ❍◾◇▶►➢●∙⇒▷◆\\-–—';
final _paraStart = RegExp(
    r'^\s*(\d+[.)]\s|[가-하][.)]\s|[' + _markers + r']\s?|\(\d+\)|[①-⑩]|<|'
    r'제목|수신|참조|붙임|경유|발신|시행|접수|문서번호|결재|협조자|기안자|관련|등록|공개)');
final _skip = RegExp(r'^\s*(시행|접수|문서번호|결재|협조자|기안자|붙임|관련|우\s?\d|전화|주소|수신|참조|발신|'
    r'경유|등록|공개|전결|주무관|계\s?장|과\s?장|국\s?장)');
final _mark = RegExp(r'^\s*(\d+[.)]|[가-하][.)]|[' + _markers + r']|\(\d+\)|[①-⑩])\s*');
final _pageNo = RegExp(r'^\s*[-–—]\s*\d+\s*[-–—]\s*$');

const _quote = "'‘’´`";
final _date = RegExp(
    r'(?<y>20\d{2})\s*[.\-/년]\s*(?<m>\d{1,2})\s*[.\-/월]\s*(?<d>\d{1,2})(?!\d)\s*(?:일|\.)?'
    r"|(?:['‘’´`]\s*|(?<![\d.\-/]))(?<y2>\d{2})\s*\.\s*(?<m2>\d{1,2})\s*\.\s*(?<d2>\d{1,2})(?!\d)\s*(?:일|\.)?"
    r'|(?<![\d.\-/])(?<m3>\d{1,2})\s*[.월]\s*(?<d3>\d{1,2})(?!\d)'
    r'(?:\s*일|\s*\.(?!\d)|(?=\s*\([월화수목금토일]\)))');
final _rangeEnd = RegExp(r'(\d{1,2})\s*[.월]\s*(\d{1,2})(?!\d)\s*(?:일|\.)?');
final _mrange = RegExp(r'(?<![\d.])(\d{1,2})\s*월\s*[~∼～]\s*(\d{1,2})\s*월(?!\s*\d)');
final _timeColon = RegExp(r'(?<!\d)(\d{1,2})\s*:\s*(\d{2})(?!\d)');
final _timeKo = RegExp(r'(오전|오후)?\s*(\d{1,2})\s*시\s*(?:(\d{1,2})\s*분)?');
final _range = RegExp(r'^\s*(?:\([^)]*\))?\s*(?:\d{1,2}\s*:\s*\d{2})?\s*[~∼～\-–]\s*');
final _period = RegExp(r'감사|의회|회기|훈련|연수|교육|워크숍|워크샵|파견|수련|캠프');
final _urgent = RegExp(r'기한|마감|제출|까지|회신|신청|접수');
final _asof = RegExp(r'^\s*\)?\s*(기준|현재)');

class GongmunItem {
  final DateTime date;
  final DateTime? end;
  final String time, text, doc;
  final String detail; // 일정 이름은 짧게('공유재산 실태조사 제출기한'), 원래 문장은 메모로
  final bool period;
  final int pri;
  GongmunItem(this.date, this.end, this.time, this.text, this.doc, this.period, this.pri, {this.detail = ''});
}

String _clean(String raw) => raw.replaceAll(RegExp(r'\s+'), ' ').trim();

List<String> paragraphs(String text) {
  final out = <String>[];
  for (final raw in text.replaceAll('\r', '').split('\n')) {
    final line = _clean(raw);
    if (line.isEmpty || _pageNo.hasMatch(line)) continue;
    if (out.isEmpty || _paraStart.hasMatch(line)) {
      out.add(line);
    } else {
      out[out.length - 1] += ' $line';
    }
  }
  return out;
}

String findTitle(String text, List<String> paras) {
  for (final p in paras) {
    if (p.startsWith('제목')) return p.replaceFirst(RegExp(r'^제목\s*'), '').trim();
  }
  final lines = text
      .split('\n')
      .map(_clean)
      .where((x) => x.length >= 8 && RegExp(r'[가-힣]').hasMatch(x) && !_pageNo.hasMatch(x) && !_paraStart.hasMatch(x))
      .toList();
  final cnt = <String, int>{};
  for (final x in lines) {
    cnt[x] = (cnt[x] ?? 0) + 1;
  }
  for (final x in lines) {
    if (cnt[x]! >= 2) return x;
  }
  return lines.isNotEmpty ? lines.first : (paras.isNotEmpty ? paras.first : '');
}

String _two(int n) => n.toString().padLeft(2, '0');

String _timeAfter(String s) {
  final head = s.length > 40 ? s.substring(0, 40) : s;
  var m = _timeColon.firstMatch(head);
  if (m != null && int.parse(m.group(1)!) < 24) return '${_two(int.parse(m.group(1)!))}:${m.group(2)}';
  m = _timeKo.firstMatch(head);
  if (m != null) {
    var h = int.parse(m.group(2)!);
    if (m.group(1) == '오후' && h < 12) h += 12;
    if (m.group(1) == '오전' && h == 12) h = 0;
    if (h < 24) return '${_two(h)}:${_two(int.parse(m.group(3) ?? '0'))}';
  }
  return '';
}

DateTime? _mkDate(int y, int m, int d) {
  final x = DateTime(y, m, d);
  return (x.year == y && x.month == m && x.day == d) ? x : null;
}

DateTime? _dateOf(RegExpMatch m, int year) {
  if (m.namedGroup('y') != null) {
    return _mkDate(int.parse(m.namedGroup('y')!), int.parse(m.namedGroup('m')!), int.parse(m.namedGroup('d')!));
  }
  if (m.namedGroup('y2') != null) {
    final first = m.group(0)!.trimLeft();
    final quoted = first.isNotEmpty && _quote.contains(first[0]);
    final y2 = int.parse(m.namedGroup('y2')!);
    if (!quoted && !(y2 >= 24 && y2 <= 39)) return null; // 따옴표가 없으면 24~39 만 연도로 본다
    return _mkDate(2000 + y2, int.parse(m.namedGroup('m2')!), int.parse(m.namedGroup('d2')!));
  }
  return _mkDate(year, int.parse(m.namedGroup('m3')!), int.parse(m.namedGroup('d3')!));
}

final _stripTime = [
  _timeColon,
  RegExp(r'(오전|오후)\s*\d{1,2}\s*시(?:\s*\d{1,2}\s*분)?'),
  RegExp(r'\d{1,2}\s*시\s*\d{0,2}\s*분?'),
];
final _weekday = RegExp(r'\(\s*[월화수목금토일]\s*\)');
final _tail = RegExp(r'(?:하여|해|하시기)?\s*(?:주시기\s*)?(?:바랍니다|바람|부탁드립니다|요망|협조\s*바랍니다)\s*\.?\s*$');
final _leadWord = RegExp(r'^\s*(?:및|또는|그리고|이후|까지|부터|에서|에는|에|행정사항|추진일정|향후일정|주요일정)\s+');

String _stripChars(String t, String chars) {
  var a = 0, b = t.length;
  while (a < b && chars.contains(t[a])) {
    a++;
  }
  while (b > a && chars.contains(t[b - 1])) {
    b--;
  }
  return t.substring(a, b);
}

String _tidy(String t) {
  t = t.replaceAll(_weekday, ' ');
  for (final rx in _stripTime) {
    t = t.replaceAll(rx, ' ');
  }
  t = t.replaceAll(RegExp(r'(?:까지|부터|이내|전까지)(?=\s|$|[,.;:)])'), ' ');
  t = t.replaceAll(RegExp('[‘’\'`´"“”]'), '');
  t = t.replaceAll(RegExp(r'\(\s*[,.~∼～\-:·\s]*\)'), ' ');
  t = t.trim().replaceAll(_tail, '');
  t = t.replaceAll(RegExp(r'\s*(?:은|는|이|가)?\s*(?:입니다|이며|이고|임)\s*[.,]?\s*$'), ''); // "조사 기간은 입니다" → "조사 기간"
  t = t.replaceAll(RegExp(r'(?:기한|마감일?)\s*[:：]\s*(?=\S)'), ' ');
  t = t.replaceAll(RegExp(r'\s*[:：]\s*(?=[,，;])'), ''); // 날짜를 빼고 남은 "설명회: , 장소"
  t = t.replaceAllMapped(RegExp(r'\s*([,，;])(?:\s*[,，;])+'), (m) => m.group(1)!);
  t = t.replaceAllMapped(RegExp(r'(?<!\S)(\S{2,})(\s+\1)+(?!\S)'), (m) => m.group(1)!);
  t = _stripChars(t.replaceAll(RegExp(r'\s+'), ' '), ' \t:;,.~∼～-–—·ㆍ');
  final m = RegExp(r'^\(([^()]*)\)$').firstMatch(t);
  if (m != null) t = m.group(1)!.trim();
  t = _stripChars(t.replaceFirst(_leadWord, ''), ' :;,.~-');
  return t; // 길다고 줄이지 않는다 (줄여 보이는 건 화면에서 자리가 모자랄 때만)
}

/// 다른 파일(붙여넣기 분석)에서도 쓰는 문장 정리
String tidyText(String t) => _tidy(t);

bool _meaningful(String t) => t.replaceAll(RegExp(r'[^0-9A-Za-z가-힣]'), '').length >= 2;

class _Span {
  final int start, stop;
  final DateTime d;
  final DateTime? end;
  final String when;
  _Span(this.start, this.stop, this.d, this.end, this.when);
}

String _coreText(String body, List<_Span> spans, int k) {
  final s0 = spans[k].start, e0 = spans[k].stop;
  final prevEnd = k > 0 ? spans[k - 1].stop : 0;
  final nextStart = k + 1 < spans.length ? spans[k + 1].start : body.length;
  final before = _tidy(body.substring(prevEnd, s0));
  final after = _tidy(body.substring(e0, nextStart));
  String core;
  if (spans.length == 1 || (k == 0 && !_meaningful(after))) {
    core = spans.length == 1 ? _tidy('${body.substring(0, s0)} ${body.substring(e0)}') : before;
  } else if (_meaningful(before)) {
    core = before;
  } else {
    core = after;
  }
  if (!_meaningful(core)) core = _meaningful(after) ? after : before;
  return core;
}

String _iso(DateTime d) => '${d.year}-${_two(d.month)}-${_two(d.day)}';

final _titleTail = RegExp(r'\s*(?:\([^)]*\)|\[[^\]]*\]|추진\s*계획|시행\s*계획|계획\(?안?\)?|알림|안내|통보|공고|제출\s*요청|협조\s*요청|'
    r'회신\s*요청|요청|협조|보고|\s*등)\s*$');

final _kindCollect = RegExp('취합');
final _kindSubmit = RegExp('제출|회신|송부|보고|입력|등록');
final _kindApply = RegExp('신청|접수');
final _kindEvent = RegExp('설명회|간담회|워크숍|워크샵|연수|교육|훈련|회의|점검|감사|심사|발표|행사|면담|협의');

/// 일정 한 줄의 성격을 짧은 말로: 긴 설명 문장 → '제출기한' · '취합기한' · '신청기한' · '설명회' …
String kindOf(String core) {
  final c = core.trim();
  if (_kindCollect.hasMatch(c)) return '취합기한';
  if (_kindSubmit.hasMatch(c)) return '제출기한';
  if (_kindApply.hasMatch(c)) return '신청기한';
  if (c.length <= 12) return c;
  final m = _kindEvent.firstMatch(c);
  if (m != null) return m.group(0)!;
  return c;
}

/// 공문 제목에서 '무엇에 관한 건지' 만 남긴 짧은 이름: '2026년 공유재산 실태조사 추진계획' → '공유재산 실태조사'
String topicOf(String title) {
  var t = title.replaceAll(RegExp('[「」『』“”"\'‘’]'), '').trim();
  t = t.replaceFirst(RegExp(r'^20\d{2}\s*(년도|년|학년도)?\s*'), '').trim();
  for (var i = 0; i < 3; i++) {
    final n = t.replaceFirst(_titleTail, '').trim();
    if (n.length < 4) break;
    t = n;
  }
  t = _stripChars(t.replaceAll(RegExp(r'\s+'), ' '), ' ,.:-·');
  return t;
}

final _submitList = RegExp(
    r'^\s*(?:\d+\.\s*)?(?:[가-힣]\.\s*)?[<\[(]?\s*제출\s*(?:서류|자료|물|내용|서식|양식)\s*[>\])]?\s*[:：]\s*(.+)$');
const _objNoun = r'(?:자료|서류|현황표?|계획서|보고서|결과|내역|명단|목록|조사표|신청서|서식|양식|통계|실적|의견|증빙|사진|'
    r'점검표|확인서|동의서|대장|보고|계획|자료집)';
final _obj = RegExp(r'((?:[가-힣A-Za-z0-9·\-]+\s+){0,4}[가-힣A-Za-z0-9·\-]*' + _objNoun +
    r'(?:\s+[가-힣A-Za-z0-9·\-]*' + _objNoun + r')*)(?:을|를|은|는|이|가)?(?=[\s,.)\]]|$)');
final _objDrop = RegExp(r'^(?:각|해당|관련|위|아래|다음|소관|부서별?|별첨|붙임|동|본|귀|우리|및|또는|까지|부터|이내|전까지|작성|작성한|\d+[.)]?)$');
final _partEnd = RegExp(r'.*(?:은|는|에서|에게|으로|로|에|와|과|하여|하고|하며|하니|위해|대한|관한|따라|같이)$');
final _verb = RegExp(r'(?:작성|취합|제출|회신|송부|보고|입력|등록)');

/// '각 부서에서는 소관 공유재산 현황 자료' → '공유재산 현황 자료'
String _cleanObj(String raw) {
  var words = raw.split(RegExp(r'\s+')).where((w) => w.isNotEmpty).toList();
  while (words.isNotEmpty && (_objDrop.hasMatch(words.first) || _partEnd.hasMatch(words.first))) {
    words.removeAt(0);
  }
  for (var k = words.length - 2; k >= 0; k--) {
    if (_partEnd.hasMatch(words[k])) {
      words = words.sublist(k + 1);
      break;
    }
  }
  words = words.where((w) => !_objDrop.hasMatch(w)).toList();
  final tail = words.length > 4 ? words.sublist(words.length - 4) : words;
  return _stripChars(tail.join(' ').trim(), ' ,.·-');
}

/// '제출 서류: 현황표(별지 제1호), 사용허가 현황 1부' → '현황표·사용허가 현황' (문서에 서류 목록이 있을 때)
String submitList(List<String> paras) {
  for (final p in paras) {
    final m = _submitList.firstMatch(p.replaceFirst(_mark, ''));
    if (m == null) continue;
    final out = <String>[];
    for (var it in m.group(1)!.split(RegExp(r'[,，、·ㆍ]|\s및\s|;|/'))) {
      it = it.replaceAll(RegExp(r'[(（\[][^)）\]]*[)）\]]'), '');
      it = it.replaceAll(RegExp(r'\s*(?:각\s*)?\d+\s*(?:부|매|개|건|종)\s*$'), '');
      it = _stripChars(it.replaceAll(RegExp(r'\s*(?:을|를|등)\s*$'), ''), ' .:-');
      if (it.replaceAll(RegExp(r'[^가-힣A-Za-z0-9]'), '').length >= 2 && !out.contains(it)) out.add(it);
    }
    if (out.isNotEmpty) {
      return out.take(3).join('·') + (out.length > 3 ? ' 외 ${out.length - 3}' : '');
    }
  }
  return '';
}

/// 마감 날짜가 들어 있는 문장에서 '무엇을' 내라는 건지 찾는다. lo~hi 는 이 날짜의 문장 범위(앞뒤 다른 날짜 사이)
String whatToSubmit(String body, int lo, int s0, int e0, int hi) {
  final before = body.substring(lo, s0), after = body.substring(e0, hi);
  final vb = _verb.allMatches(before).toList();
  String window;
  if (vb.isNotEmpty) {
    window = before.substring(0, vb.last.start);
  } else {
    final va = _verb.firstMatch(after);
    window = '$before ${va != null ? after.substring(0, va.start) : after.substring(0, after.length < 40 ? after.length : 40)}';
  }
  var best = '';
  for (final m in _obj.allMatches(window)) {
    final obj = _cleanObj(m.group(1)!);
    if (obj.replaceAll(RegExp(r'[^가-힣A-Za-z0-9]'), '').length >= 3) best = obj;
  }
  return best;
}

/// 반환: (제목, 일정 후보 목록). withTopic: 일정 이름 앞에 공문 주제를 붙인다 ('[공유재산 실태조사] 조사 기간')
final _refBefore = RegExp(r'[가-힣A-Za-z]-\s*\d+\s*\(\s*$'); // '회계과-991(' — 문서번호 바로 뒤 괄호
final _refAfter = RegExp(r'^\s*\)\s*호'); // ')호'

/// '행정안전부 공유재산정책과-991(2026.3.16.)호' 처럼 관련 문서의 '발송 날짜' 는 일정이 아니다
bool _isDocRefDate(String body, int s0, int e0) {
  final before = body.substring(s0 - 30 < 0 ? 0 : s0 - 30, s0);
  final after = body.substring(e0, e0 + 6 > body.length ? body.length : e0 + 6);
  return _refBefore.hasMatch(before) && _refAfter.hasMatch(after);
}

/// 공문 제목 자체가 '… 제출(요청)' 이면, 연도와 '요청' 만 뺀 그 제목이 곧 일정 이름이다. 제출 공문이 아니면 빈 글자
String submitTitle(String title) {
  var t = title.replaceAll(RegExp('[「」『』“”"\'‘’]'), '').trim();
  t = t.replaceFirst(RegExp(r'^20\d{2}\s*(년도|년|학년도)?\s*'), '').trim();
  t = t.replaceFirst(RegExp(r'\s*(?:협조\s*)?(?:요청|바랍니다)\s*$'), '').trim();
  return t.contains('제출') && t.length >= 4 ? t : '';
}

(String, List<GongmunItem>) parseGongmun(String text, DateTime today, {bool withTopic = true}) {
  final paras = paragraphs(text);
  final title = findTitle(text, paras);
  final topic = withTopic ? topicOf(title) : '';
  final listed = submitList(paras); // '제출 서류: …' 가 있으면 모든 제출기한에 그 서류를 붙인다
  var year = today.year;
  for (final p in paras) {
    final m = _date.firstMatch(p);
    if (m != null && (m.namedGroup('y') != null || m.namedGroup('y2') != null)) {
      final d0 = _dateOf(m, year);
      if (d0 != null) year = d0.year;
      break;
    }
  }

  final found = <GongmunItem>[];
  final submitIdx = <int>[]; // 제출기한 일정의 위치
  final seen = <String>{};
  for (final p in paras) {
    if (_skip.hasMatch(p) || p.startsWith('제목')) continue;
    final body = p.replaceFirst(_mark, '');
    if (_skip.hasMatch(body)) continue;
    final spans = <_Span>[];
    var pos = 0;
    for (final m in _date.allMatches(body)) {
      if (m.start < pos) continue;
      final d = _dateOf(m, year);
      final tailText = body.substring(m.end, m.end + 8 > body.length ? body.length : m.end + 8);
      if (d == null || _asof.hasMatch(tailText) || _isDocRefDate(body, m.start, m.end)) continue;
      final rest = body.substring(m.end);
      final when = _timeAfter(rest);
      DateTime? end;
      var stop = m.end;
      final r = _range.firstMatch(rest);
      if (r != null) {
        final at = m.end + r.end;
        final m2 = _date.matchAsPrefix(body, at) as RegExpMatch?;
        DateTime? e2;
        int? e2end;
        if (m2 != null) {
          e2 = _dateOf(m2, d.year);
          e2end = m2.end;
        } else {
          final m3 = _rangeEnd.matchAsPrefix(body, at);
          if (m3 != null) {
            e2 = _mkDate(d.year, int.parse(m3.group(1)!), int.parse(m3.group(2)!));
            e2end = m3.end;
          }
        }
        if (e2end != null && e2 != null && e2.isAfter(d)) {
          end = e2;
          stop = e2end;
        }
      }
      pos = stop;
      spans.add(_Span(m.start, stop, d, end, when));
    }
    for (final m in _mrange.allMatches(body)) {
      if (spans.any((s) => s.start < m.end && m.start < s.stop)) continue;
      final m1 = int.parse(m.group(1)!), m2 = int.parse(m.group(2)!);
      if (m1 >= 1 && m1 <= 12 && m2 >= 1 && m2 <= 12 && m1 <= m2) {
        spans.add(_Span(m.start, m.end, DateTime(year, m1, 1), DateTime(year, m2 + 1, 0), ''));
      }
    }
    spans.sort((a, b) => a.start.compareTo(b.start));
    for (var k = 0; k < spans.length; k++) {
      final s = spans[k];
      final core = _coreText(body, spans, k);
      if (!_meaningful(core)) continue;
      final key = '${_iso(s.d)}|${s.end == null ? '' : _iso(s.end!)}|$core';
      if (!seen.add(key)) continue;
      final ctx = body.substring(s.start - 30 < 0 ? 0 : s.start - 30, s.stop + 14 > body.length ? body.length : s.stop + 14);
      final pa = body.substring(s.start - 40 < 0 ? 0 : s.start - 40, s.stop + 20 > body.length ? body.length : s.stop + 20);
      var label = core;
      if (topic.isNotEmpty) {
        final kind = kindOf(core);
        label = topic.contains(kind) ? topic : (kind.contains(topic) ? kind : '$topic $kind');
        if (kind == '제출기한') {
          final lo = k > 0 ? spans[k - 1].stop : 0;
          final hi = k + 1 < spans.length ? spans[k + 1].start : body.length;
          final what = listed.isNotEmpty ? listed : whatToSubmit(body, lo, s.start, s.stop, hi);
          if (what.isNotEmpty) label = '$what 제출 · $topic';
        }
      }
      if (topic.isNotEmpty && kindOf(core) == '제출기한') submitIdx.add(found.length);
      found.add(GongmunItem(s.d, s.end, s.when, label, title, s.end != null && _period.hasMatch(pa), _urgent.hasMatch(ctx) ? 1 : 2,
          detail: core));
    }
  }
  // 제출 공문에 제출기한이 하나면 제목 그대로: '제10차 ○○위원회 심의안건 제출'
  final st = submitTitle(title);
  if (withTopic && submitIdx.length == 1 && st.isNotEmpty) {
    final o = found[submitIdx.first];
    found[submitIdx.first] = GongmunItem(o.date, o.end, o.time, st, o.doc, o.period, o.pri, detail: o.detail);
  }
  // 안정 정렬: 날짜 → 시간(없으면 맨 뒤) → 원래 순서
  final idx = {for (var i = 0; i < found.length; i++) found[i]: i};
  found.sort((a, b) {
    final c = a.date.compareTo(b.date);
    if (c != 0) return c;
    final t = (a.time.isEmpty ? '99:99' : a.time).compareTo(b.time.isEmpty ? '99:99' : b.time);
    return t != 0 ? t : idx[a]!.compareTo(idx[b]!);
  });
  return (title, found);
}
