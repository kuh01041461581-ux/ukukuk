// 업무(공문 한 건 = 업무 하나): 기한(D-day)과 연결된 일정 (PC work.py 와 같은 데이터)
// 업무 폴더는 파일을 다루는 PC 에서 만들고, 폰에서는 기한·일정 확인과 완료 표시를 한다.
import 'core.dart';

Map<String, dynamic> workOf(Json data) => (data['_work'] as Map?)?.cast<String, dynamic>() ?? <String, dynamic>{};

List<Json> sortedCases(Json data, {bool includeDone = false}) {
  final out = workOf(data).values.cast<Json>().where((c) => includeDone || c['done'] != true).toList();
  int k(Json c) => c['done'] == true ? 1 : 0;
  out.sort((a, b) {
    final d = k(a) - k(b);
    if (d != 0) return d;
    final x = ((a['due'] as String?) ?? '9999').compareTo((b['due'] as String?) ?? '9999');
    if (x != 0) return x;
    return ((a['created'] as num?) ?? 0).compareTo((b['created'] as num?) ?? 0);
  });
  return out;
}

int? dday(Json c, DateTime today) {
  final d = parseDate(c['due'] as String?);
  return d == null ? null : daysBetween(dayOnly(today), d);
}

String ddayLabel(int? n) => n == null ? '' : (n == 0 ? 'D-DAY' : (n > 0 ? 'D-$n' : 'D+${-n}'));

String fmtDate(DateTime? d, {bool wd = true}) =>
    d == null ? '' : '${d.month}.${d.day}.${wd ? '(${wdKo[d.weekday - 1]})' : ''}';

/// 업무에 묶인 일정 [(날짜키, 항목)]
List<(String, Json)> caseItems(Json data, String cid) {
  final out = <(String, Json)>[];
  data.forEach((key, v) {
    if (key == '_deleted' || key == '_work' || v is! Map) return;
    for (final t in (v['todos'] as List? ?? const [])) {
      if ((t as Map)['case'] == cid) out.add((key, t as Json));
    }
  });
  out.sort((a, b) => a.$1.compareTo(b.$1));
  return out;
}
