// 붙여넣은 글(메신저·메일·캡처에서 읽은 글자)에서 일정과 할 일 후보를 뽑는다 (PC 의 analyze.py 와 같은 규칙)
import 'gongmun.dart';
import 'quickadd.dart';

class AnalyzedItem {
  final DateTime? date, end;
  final String time, text, kind; // kind: event | todo
  final int pri;
  final bool period;
  AnalyzedItem(this.date, this.end, this.time, this.text, this.pri, this.kind, this.period);
}

final _chatPrefix = RegExp(r'^\s*\[[^\]]{1,20}\]\s*\[(?:오전|오후)\s*\d{1,2}:\d{2}\]\s*');
final _speaker = RegExp(r'^\s*[가-힣A-Za-z0-9_·\s]{1,12}\s*[:：]\s+(?=\S)');
final _dateSep = RegExp(r'^\s*20\d{2}\s*년\s*\d{1,2}\s*월\s*\d{1,2}\s*일\s*[월화수목금토일]요일\s*$');
final _tasky = RegExp(r'해\s*주|바랍니다|바람|부탁|제출|회신|확인|보고|준비|작성|검토|요청|신청|참석|송부|전달|공유|취합|점검|완료');
final _marked = RegExp(r'^\s*(?:\d+[.)]|[가-하][.)]|[○ㅇ□■※•▪◦·❍◾◇▶►➢●∙⇒▷◆\-–—])\s');
// 업무 메신저(시 자체 메신저·쪽지) 복사본의 머리줄: "홍길동(도시재생과) 2026-10-13 오후 2:30:15", "보낸 사람: …"
final _meta = RegExp(r'^\s*(?:보낸\s*사람|받는\s*사람|보낸\s*시간|받은\s*시간|보낸\s*날짜|받은\s*날짜|발신자|수신자|발신\s*일시|'
    r'수신\s*일시|일시|시간|참조|숨은\s*참조|읽음|안\s*읽음|첨부(?:\s*파일)?|파일)\s*[:：]');
final _subject = RegExp(r'^\s*(?:제목|쪽지\s*제목)\s*[:：]\s*');
final _stampDate = RegExp(r'\(?20\d{2}\s*[.\-/년]\s*\d{1,2}\s*[.\-/월]\s*\d{1,2}\s*일?\.?\)?(?:\s*\(?[월화수목금토일](?:요일)?\)?)?');
final _stampTime = RegExp(r'\(?(?:(오전|오후|AM|PM|am|pm)\s*)?\d{1,2}:\d{2}(:\d{2})?(?:\s*(AM|PM|am|pm))?\)?');
final _nameOnly = RegExp(r'^[가-힣]{2,4}(?:\s*(?:주무관|팀장|과장|계장|국장|실장|소장|동장|주임|담당|담당자|대리|사원|님|선생님|부장|차장))?'
    r'(?:\s*[(\[<][^)\]>]{1,20}[)\]>])?\s*(?:님)?$');
final _eventy = RegExp(r'회의|설명회|교육|점검|행사|출장|보고|제출|마감|감사|면담|방문|간담회|워크숍|연수|훈련|시험|심사|회식|휴가|당직|발표|정산');
final _timeLine = RegExp(r'^\s*[(\[]?\s*(?:(?:오전|오후)\s*)?\d{1,2}:\d{2}(?::\d{2})?\s*[)\]]?\s*$');

/// 메신저 머리줄이면 그 줄에 적힌 날짜(없으면 true), 아니면 null
Object? stampOf(String line) {
  final t = _stampTime.firstMatch(line);
  if (t == null) return null;
  final dm = _stampDate.firstMatch(line);
  if (dm == null && t.group(1) == null && t.group(2) == null && t.group(3) == null) return null;
  var rest = line.replaceAll(_stampDate, ' ').replaceAll(_stampTime, ' ');
  rest = rest.replaceAll(RegExp(r'[\[\]|·•\-:：,]+'), ' ').replaceAll(RegExp(r'\s+'), ' ').trim();
  if (rest.isNotEmpty && (!_nameOnly.hasMatch(rest) || _eventy.hasMatch(rest))) return null;
  if (dm != null) {
    final nums = RegExp(r'\d+').allMatches(dm.group(0)!).map((m) => int.parse(m.group(0)!)).toList();
    if (nums.length >= 3) {
      final d = DateTime(nums[0], nums[1], nums[2]);
      if (d.month == nums[1] && d.day == nums[2]) return d;
    }
  }
  return true;
}

final _greet = RegExp(r'^\s*(?:안녕하세요|수고하십니다|수고 많으십니다|감사합니다)[.,!]?\s*');
final _past = RegExp(r'(?:했습니다|했어요|했어|하였습니다|됐습니다|드렸습니다|보냈습니다|받았습니다|알겠습니다|네|넵)[.!]?\s*$');
final _askTail = RegExp(r'\s*(?:해\s*주세요|해주세요|부탁드려요|부탁드립니다|부탁해요|부탁합니다|바랍니다|바람|요망)\s*[.!]?\s*$');
final _urgentWord = RegExp(r'긴급|급히|꼭|반드시');

String _finish(String t) {
  t = t.replaceFirst(_greet, '');
  t = t.replaceAll(RegExp(r'\(\s*[월화수목금토일]\s*\)'), ' ');
  t = t.replaceAll(RegExp(r'(?<![가-힣])(?:꼭|반드시|급히)(?![가-힣])'), ' ');
  t = t.replaceAll(RegExp(r'\s*(?:있으니까|있으니|있습니다|있어요)\s*'), ' ');
  t = t.trim().replaceFirst(RegExp(r'^(?:까지|부터|에는|에서|에)\s+'), '');
  t = t.trim().replaceFirst(_askTail, '');
  t = t.replaceFirstMapped(RegExp(r'^([가-힣]{2,})(?:은|는)\s'), (m) => '${m.group(1)} '); // '자료는 제출' → '자료 제출'
  return tidyText(t);
}

/// [(줄, 기준일)] — 메신저 머리줄은 빼고, 머리줄에 적힌 날짜를 아래 글의 '내일·모레' 기준일로 쓴다
List<(String, DateTime)> _lines(String text, DateTime today) {
  final out = <(String, DateTime)>[];
  var ref = today;
  for (final raw in text.replaceAll('\r', '').split('\n')) {
    var s = raw.replaceFirst(_chatPrefix, '');
    if (_dateSep.hasMatch(s) || _meta.hasMatch(s) || _timeLine.hasMatch(s)) continue;
    final st = stampOf(s);
    if (st != null) {
      if (st is DateTime) {
        final gap = DateTime.utc(today.year, today.month, today.day).difference(DateTime.utc(st.year, st.month, st.day)).inDays;
        if (gap >= 0 && gap <= 30) ref = st;
      }
      continue;
    }
    s = s.replaceFirst(_subject, '');
    s = s.replaceAll(RegExp(r'\s+'), ' ').trim();
    if (s.replaceAll(RegExp(r'[^0-9A-Za-z가-힣]'), '').length >= 4) out.add((s, ref));
  }
  return out;
}

bool isDocument(String text) {
  final ls = text.replaceAll('\r', '').split('\n').where((l) => l.trim().isNotEmpty);
  final marked = ls.where((l) => _marked.hasMatch(l)).length;
  return RegExp(r'^\s*제목\s', multiLine: true).hasMatch(text) || marked >= 3;
}

List<AnalyzedItem> analyzeText(String text, DateTime today) {
  today = DateTime(today.year, today.month, today.day);
  if (isDocument(text)) {
    final (_, items) = parseGongmun(text, today);
    return [for (final i in items) AnalyzedItem(i.date, i.end, i.time, i.text, i.pri, 'event', i.period)];
  }
  final lines = _lines(text, today);
  final numbered = lines.map((l) => '1. ${l.$1.replaceFirst(_speaker, '')}').join('\n');
  final (_, g) = parseGongmun('제목 메모\n$numbered', today, withTopic: false);
  final out = <AnalyzedItem>[];
  final seen = <String>{};
  final covered = <String>[];
  String k(DateTime? d, String t) => '${d == null ? '' : '${d.year}-${d.month}-${d.day}'}|$t';
  for (final i in g) {
    if (!seen.add(k(i.date, i.text))) continue;
    final f = _finish(i.text);
    out.add(AnalyzedItem(i.date, i.end, i.time, f.isEmpty ? i.text : f, i.pri, 'event', i.period));
    covered.add(i.text);
  }
  for (final (l, ref) in lines) {
    final body = l.replaceFirst(_speaker, '');
    if (covered.any((c) => c.isNotEmpty && body.contains(c)) || _past.hasMatch(body)) continue;
    final r = parseQuick(body, ref);
    final cleaned = _finish(r.text);
    final rt = cleaned.isEmpty ? r.text : cleaned;
    final d = r.date;
    final isToday = d != null && d.year == ref.year && d.month == ref.month && d.day == ref.day;
    if (d != null && (!isToday || body.contains('오늘'))) {
      if (!seen.add(k(d, rt))) continue;
      out.add(AnalyzedItem(d, null, r.time ?? '', rt, r.pri ?? (_urgentWord.hasMatch(body) ? 1 : 2), 'event', false));
    } else if (_tasky.hasMatch(body)) {
      final c2 = _finish(parseQuick(body, ref).text);
      final tt = c2.isEmpty ? body : c2;
      if (!seen.add(k(null, tt))) continue;
      out.add(AnalyzedItem(null, null, '', tt, _urgentWord.hasMatch(body) ? 1 : 2, 'todo', false));
    }
  }
  // 안정 정렬: 날짜 있는 것 먼저(날짜 → 시간), 날짜 없는 할 일은 뒤로
  final idx = {for (var i = 0; i < out.length; i++) out[i]: i};
  out.sort((a, b) {
    if ((a.date == null) != (b.date == null)) return a.date == null ? 1 : -1;
    if (a.date != null) {
      final c = a.date!.compareTo(b.date!);
      if (c != 0) return c;
    }
    final t = (a.time.isEmpty ? '99:99' : a.time).compareTo(b.time.isEmpty ? '99:99' : b.time);
    return t != 0 ? t : idx[a]!.compareTo(idx[b]!);
  });
  return out;
}
