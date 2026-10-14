// 폰의 '업무' 탭: PC 에서 만든 업무(공문)의 기한(D-day)과 연결된 일정을 보고 완료 표시를 한다
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'core.dart';
import 'main.dart' show Pressable;
import 'store.dart';
import 'work.dart';

const _bg = Color(0xFF16171F);
const _card = Color(0xFF1C1E28);
const _border = Color(0xFF292C3A);
const _text = Color(0xFFF1F3F8);
const _sub = Color(0xFF9AA4B8);
const _dim = Color(0xFF687187);
const _accent = Color(0xFF7C6CFF);
const _red = Color(0xFFF08A94);
const _amber = Color(0xFFF2C374);

Color ddayColor(int? n) => n == null ? _dim : (n < 0 ? _red : (n <= 2 ? _amber : _accent));

class WorkPage extends StatefulWidget {
  final AppStore store;
  final ValueChanged<DateTime> onJump;
  const WorkPage({super.key, required this.store, required this.onJump});
  @override
  State<WorkPage> createState() => _WorkPageState();
}

class _WorkPageState extends State<WorkPage> {
  bool showDone = false;

  @override
  Widget build(BuildContext context) {
    final store = widget.store;
    final today = dayOnly(DateTime.now());
    final all = sortedCases(store.data, includeDone: true);
    final list = all.where((c) => (c['done'] == true) == showDone).toList();
    final nOn = all.where((c) => c['done'] != true).length;
    return RefreshIndicator(
      onRefresh: store.syncNow,
      child: ListView(padding: const EdgeInsets.fromLTRB(16, 6, 16, 110), children: [
        Row(children: [
          for (final (lab, v) in [('진행 중 $nOn', false), ('완료 ${all.length - nOn}', true)])
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: ChoiceChip(
                  label: Text(lab, style: const TextStyle(fontSize: 13)),
                  selected: showDone == v,
                  showCheckmark: false,
                  onSelected: (_) {
                    HapticFeedback.selectionClick();
                    setState(() => showDone = v);
                  }),
            ),
        ]),
        const SizedBox(height: 6),
        if (list.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 48, horizontal: 8),
            child: Text(
                showDone
                    ? '완료한 업무가 없어요'
                    : 'PC 달력에 공문(PDF·한글)을 끌어다 놓으면\n업무가 생기고 여기서 함께 볼 수 있어요.\n\n'
                        '공문마다 기한(D-day)과 연결된 일정을\n한눈에 보고 끝나면 완료로 표시하세요.',
                textAlign: TextAlign.center,
                style: const TextStyle(color: _dim, height: 1.6)),
          ),
        for (final c in list) _CaseCard(c: c, today: today, onTap: () => _open(c)),
      ]),
    );
  }

  void _open(Json c) => Navigator.of(context).push(MaterialPageRoute(
      builder: (_) => WorkDetail(store: widget.store, id: c['id'] as String, onJump: widget.onJump)));
}

class _CaseCard extends StatelessWidget {
  final Json c;
  final DateTime today;
  final VoidCallback onTap;
  const _CaseCard({required this.c, required this.today, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final n = dday(c, today);
    final done = c['done'] == true;
    final col = done ? _dim : ddayColor(n);
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Pressable(
        onTap: onTap,
        child: Material(
          color: _card,
          borderRadius: BorderRadius.circular(22),
          child: Container(
            padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
            decoration: BoxDecoration(borderRadius: BorderRadius.circular(22), border: Border.all(color: _border)),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Expanded(
                    child: Text((c['title'] as String?) ?? '',
                        style: TextStyle(
                            fontSize: 15.5, fontWeight: FontWeight.w700, color: done ? _dim : _text, height: 1.35))),
                if (n != null) ...[
                  const SizedBox(width: 10),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                    decoration: BoxDecoration(color: col.withOpacity(0.16), borderRadius: BorderRadius.circular(99)),
                    child: Text(ddayLabel(n), style: TextStyle(color: col, fontWeight: FontWeight.w800, fontSize: 12.5)),
                  ),
                ],
              ]),
              const SizedBox(height: 8),
              Row(children: [
                if (n != null)
                  Text('${fmtDate(parseDate(c['due'] as String?))} 기한', style: const TextStyle(color: _sub, fontSize: 12.5)),
              ]),
            ]),
          ),
        ),
      ),
    );
  }
}

class WorkDetail extends StatelessWidget {
  final AppStore store;
  final String id;
  final ValueChanged<DateTime> onJump;
  const WorkDetail({super.key, required this.store, required this.id, required this.onJump});

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: store,
      builder: (context, _) {
        final c = workOf(store.data)[id] as Json?;
        if (c == null) {
          return Scaffold(
              backgroundColor: _bg, appBar: AppBar(), body: const Center(child: Text('지워진 업무예요', style: TextStyle(color: _dim))));
        }
        final today = dayOnly(DateTime.now());
        final n = dday(c, today);
        final items = caseItems(store.data, id);
        final due = parseDate(c['due'] as String?);
        return Scaffold(
          backgroundColor: _bg,
          appBar: AppBar(
            backgroundColor: _bg,
            actions: [
              IconButton(
                tooltip: c['done'] == true ? '다시 진행' : '완료로 표시',
                icon: Icon(c['done'] == true ? Icons.replay_rounded : Icons.task_alt_rounded),
                onPressed: () {
                  c['done'] = c['done'] != true;
                  c['updated'] = nowMs();
                  store.changed();
                },
              ),
            ],
          ),
          body: ListView(padding: const EdgeInsets.fromLTRB(20, 0, 20, 40), children: [
            Text((c['title'] as String?) ?? '',
                style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800, color: _text, height: 1.3)),
            const SizedBox(height: 10),
            Wrap(spacing: 8, runSpacing: 8, crossAxisAlignment: WrapCrossAlignment.center, children: [
              if (n != null)
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  decoration: BoxDecoration(color: ddayColor(n).withOpacity(0.16), borderRadius: BorderRadius.circular(99)),
                  child: Text('${ddayLabel(n)} · ${fmtDate(due)}까지',
                      style: TextStyle(color: ddayColor(n), fontWeight: FontWeight.w700)),
                ),
              if ((c['doc_no'] as String?)?.isNotEmpty == true)
                Text('문서 ${c['doc_no']}', style: const TextStyle(color: _dim, fontSize: 12.5)),
            ]),
            const SizedBox(height: 26),
            const Text('연결된 일정', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w800, color: _text)),
            const SizedBox(height: 8),
            if (items.isEmpty) const Text('없음', style: TextStyle(color: _dim)),
            for (final (key, t) in items)
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: Icon(t['done'] == true ? Icons.check_circle_rounded : Icons.circle_outlined,
                    color: t['done'] == true ? _dim : _accent, size: 22),
                title: Text((t['text'] as String?) ?? '',
                    style: TextStyle(color: t['done'] == true ? _dim : _text, fontSize: 14.5)),
                subtitle: Text(parseDate(key) == null ? '보관함' : fmtDate(parseDate(key)),
                    style: const TextStyle(color: _sub, fontSize: 12.5)),
                onTap: () {
                  final d = parseDate(key);
                  if (d != null) {
                    Navigator.of(context).pop();
                    onJump(d);
                  }
                },
              ),
            if ((c['note'] as String?)?.isNotEmpty == true) ...[
              const SizedBox(height: 20),
              const Text('메모', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w800, color: _text)),
              const SizedBox(height: 8),
              Text(c['note'] as String, style: const TextStyle(color: _sub, height: 1.5)),
            ],
            const SizedBox(height: 24),
            const Text('업무 폴더(공문·작성 자료·제출본)는 PC의 업무 창에서 열 수 있어요.',
                style: TextStyle(color: _dim, fontSize: 12)),
          ]),
        );
      },
    );
  }
}
