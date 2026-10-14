// 분석한 일정/할 일 후보를 보여 주고, 체크한 것만 추가한다 (붙여넣기·공유·캡처 공통)
import 'package:flutter/material.dart';

import 'analyze.dart';
import 'core.dart';
import 'main.dart' show cAccent, cBorder, cCard, cDim, cSub, cText, kCurve, priColors, weekdayKo;
import 'store.dart';

Future<void> showCandidates(BuildContext context, AppStore store, String text, {String heading = '붙여넣은 내용에서 만들기'}) async {
  final today = dayOnly(DateTime.now());
  final items = analyzeText(text, today);
  final messenger = ScaffoldMessenger.of(context);
  if (items.isEmpty) {
    messenger.showSnackBar(const SnackBar(content: Text('일정이나 할 일로 보이는 내용을 찾지 못했어요 (날짜나 \'제출·회신·확인\' 같은 요청이 있어야 해요)')));
    return;
  }
  final added = await showModalBottomSheet<List<Json>>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    sheetAnimationStyle: AnimationStyle(duration: const Duration(milliseconds: 380), curve: kCurve),
    builder: (ctx) => _CandidateSheet(store: store, items: items, heading: heading),
  );
  if (added == null || added.isEmpty) return;
  messenger.showSnackBar(SnackBar(
    duration: const Duration(seconds: 8),
    content: Text('${added.length}개를 추가했어요'),
    action: SnackBarAction(
        label: '실행 취소',
        onPressed: () {
          for (final t in added) {
            store.remove(t);
          }
        }),
  ));
}

class _CandidateSheet extends StatefulWidget {
  final AppStore store;
  final List<AnalyzedItem> items;
  final String heading;
  const _CandidateSheet({required this.store, required this.items, required this.heading});
  @override
  State<_CandidateSheet> createState() => _CandidateSheetState();
}

class _CandidateSheetState extends State<_CandidateSheet> {
  late final List<bool> on = List.filled(widget.items.length, true);
  late final List<TextEditingController> texts = [for (final i in widget.items) TextEditingController(text: i.text)];

  @override
  void dispose() {
    for (final c in texts) {
      c.dispose();
    }
    super.dispose();
  }

  String _when(AnalyzedItem i) {
    if (i.date == null) return '보관함 · 할 일';
    final d = i.date!;
    var s = '${d.month}/${d.day}(${weekdayKo[d.weekday - 1]})';
    if (i.time.isNotEmpty) s += ' ${i.time}';
    if (i.end != null) s += ' ~ ${i.end!.month}/${i.end!.day}';
    return s;
  }

  void _confirm() {
    final added = <Json>[];
    for (var k = 0; k < widget.items.length; k++) {
      if (!on[k]) continue;
      final it = widget.items[k];
      final text = texts[k].text.trim();
      if (text.isEmpty) continue;
      var d = it.date;
      DateTime? end = it.end;
      String? note = (it.detail.isNotEmpty && it.detail != text) ? it.detail : null; // 짧은 이름 뒤의 원래 문장
      if (d != null && end != null && end.isAfter(d) && !it.period) {
        note = [if (note != null) note, '기간 ${d.month}/${d.day} ~ ${end.month}/${end.day}'].join(' · '); // 마감형 기간은 종료일 하루에만
        d = end;
        end = null;
      }
      if (d != null && dayItems(widget.store.data, d).any((r) => r.t['text'] == text)) continue; // 같은 날 같은 내용은 건너뜀
      final extra = <String, dynamic>{'pri': it.pri == 0 ? 1 : it.pri};
      if (it.time.isNotEmpty) extra['time'] = it.time;
      if (end != null && d != null && end.isAfter(d)) extra['end'] = iso(end);
      if (note != null) extra['note'] = note;
      final t = newItem(text, extra: extra);
      widget.store.add(d == null ? '_inbox' : iso(d), t);
      added.add(t);
    }
    Navigator.pop(context, added);
  }

  @override
  Widget build(BuildContext context) {
    final n = on.where((e) => e).length;
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Container(width: 36, height: 4, margin: const EdgeInsets.only(top: 8), decoration: BoxDecoration(color: cBorder, borderRadius: BorderRadius.circular(2))),
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 14, 20, 4),
            child: Align(alignment: Alignment.centerLeft, child: Text(widget.heading, style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w800))),
          ),
          const Padding(
            padding: EdgeInsets.fromLTRB(20, 0, 20, 8),
            child: Align(
                alignment: Alignment.centerLeft,
                child: Text('추가할 것만 체크하세요. 글자는 눌러서 고칠 수 있어요.', style: TextStyle(fontSize: 12.5, color: cSub))),
          ),
          Flexible(
            child: ListView.builder(
              shrinkWrap: true,
              itemCount: widget.items.length,
              itemBuilder: (context, k) {
                final it = widget.items[k];
                return Container(
                  margin: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
                  padding: const EdgeInsets.fromLTRB(4, 6, 12, 8),
                  decoration: BoxDecoration(color: cCard, borderRadius: BorderRadius.circular(16), border: Border.all(color: cBorder.withOpacity(0.7))),
                  child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Checkbox(value: on[k], onChanged: (v) => setState(() => on[k] = v ?? false), shape: const CircleBorder(), activeColor: cAccent),
                    Expanded(
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        TextField(
                          controller: texts[k],
                          minLines: 1,
                          maxLines: 3,
                          style: const TextStyle(fontSize: 14.5, color: cText),
                          decoration: const InputDecoration(isDense: true, filled: false, contentPadding: EdgeInsets.fromLTRB(0, 8, 0, 4), border: InputBorder.none, enabledBorder: InputBorder.none, focusedBorder: InputBorder.none),
                        ),
                        Row(children: [
                          Icon(it.date == null ? Icons.inbox_outlined : Icons.event_rounded, size: 14, color: cDim),
                          const SizedBox(width: 4),
                          Text(_when(it), style: TextStyle(fontSize: 12.5, color: it.date == null ? cDim : cAccent, fontWeight: FontWeight.w600)),
                        ]),
                      ]),
                    ),
                    Padding(
                      padding: const EdgeInsets.only(top: 14, left: 6),
                      child: Container(width: 10, height: 10, decoration: BoxDecoration(color: priColors[it.pri.clamp(0, 3)], shape: BoxShape.circle)),
                    ),
                  ]),
                );
              },
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 10, 20, 14),
            child: Row(children: [
              TextButton(onPressed: () => Navigator.pop(context), child: const Text('취소')),
              const Spacer(),
              FilledButton(onPressed: n == 0 ? null : _confirm, child: Text('$n개 추가')),
            ]),
          ),
        ]),
      ),
    );
  }
}
