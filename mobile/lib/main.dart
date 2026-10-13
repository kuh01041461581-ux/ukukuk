import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import 'core.dart';
import 'holidays.dart';
import 'notify.dart';
import 'store.dart';

const cBg = Color(0xFF111218);
const cSurface = Color(0xFF181A22);
const cCard = Color(0xFF1D1F29);
const cBorder = Color(0xFF2A2D3A);
const cText = Color(0xFFECEFF5);
const cSub = Color(0xFF98A2B3);
const cDim = Color(0xFF6A7387);
const cAccent = Color(0xFF7C6CFF);
const cSun = Color(0xFFF08A94);
const cSat = Color(0xFF8FB4F2);
const priColors = [Color(0xFFFF8F9B), Color(0xFFF2C374), Color(0xFFDFE4EE), Color(0xFF82D9A8)];
const priNames = ['매우 중요', '중요', '보통', '낮음'];
const appVersion = String.fromEnvironment('APP_VERSION', defaultValue: 'dev');
const weekdayKo = ['월', '화', '수', '목', '금', '토', '일'];

int priOf(Json t) => (((baseOf(t)['pri'] as num?)?.toInt() ?? 2)).clamp(0, 3);
Color priColor(Json t) => priColors[priOf(t)];
String dateLabel(DateTime d) => '${d.month}월 ${d.day}일 (${weekdayKo[d.weekday - 1]})';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Notifier.init();
  final store = AppStore();
  await store.load();
  runApp(DeskApp(store));
  store.syncNow();
  Notifier.reschedule(store);
}

class DeskApp extends StatelessWidget {
  final AppStore store;
  const DeskApp(this.store, {super.key});

  @override
  Widget build(BuildContext context) {
    final scheme = ColorScheme.fromSeed(seedColor: cAccent, brightness: Brightness.dark).copyWith(
        primary: cAccent, surface: cSurface, onSurface: cText, surfaceContainerHighest: cCard);
    return MaterialApp(
      title: 'DeskCalendar',
      debugShowCheckedModeBanner: false,
      locale: const Locale('ko'),
      supportedLocales: const [Locale('ko')],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      theme: ThemeData(
        useMaterial3: true,
        brightness: Brightness.dark,
        colorScheme: scheme,
        scaffoldBackgroundColor: cBg,
        appBarTheme: const AppBarTheme(backgroundColor: cBg, foregroundColor: cText, elevation: 0),
        navigationBarTheme: NavigationBarThemeData(
            backgroundColor: cSurface, indicatorColor: cAccent.withOpacity(0.25)),
        dividerColor: cBorder,
      ),
      home: Home(store),
    );
  }
}

class Home extends StatefulWidget {
  final AppStore store;
  const Home(this.store, {super.key});
  @override
  State<Home> createState() => _HomeState();
}

class _HomeState extends State<Home> with WidgetsBindingObserver {
  int tab = 0;
  DateTime selected = dayOnly(DateTime.now());
  late DateTime month = DateTime(selected.year, selected.month);

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState s) {
    if (s == AppLifecycleState.resumed) widget.store.syncNow();
  }

  void jump(DateTime d) => setState(() {
        selected = d;
        month = DateTime(d.year, d.month);
        tab = 0;
      });

  Future<void> search() async {
    final r = await showSearch<Object?>(context: context, delegate: ItemSearch(widget.store));
    if (r is DateTime) {
      jump(r);
    } else if (r == 'inbox') {
      setState(() => tab = 2);
    }
  }

  @override
  Widget build(BuildContext context) {
    final store = widget.store;
    return ListenableBuilder(
      listenable: store,
      builder: (context, _) {
        final titles = ['달력', '예정', '보관함', '연결 · 설정'];
        return Scaffold(
          appBar: AppBar(
            title: Text(titles[tab], style: const TextStyle(fontWeight: FontWeight.w700)),
            actions: [
              if (store.syncing)
                const Padding(
                    padding: EdgeInsets.symmetric(horizontal: 14),
                    child: SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))),
              IconButton(icon: const Icon(Icons.search), onPressed: search),
            ],
          ),
          body: IndexedStack(index: tab, children: [
            CalendarPage(
              store: store,
              month: month,
              selected: selected,
              onMonth: (m) => setState(() => month = m),
              onSelect: (d) => setState(() {
                selected = d;
                if (d.month != month.month) month = DateTime(d.year, d.month);
              }),
            ),
            AgendaPage(store: store, onJump: jump),
            InboxPage(store: store),
            ConnectPage(store: store),
          ]),
          floatingActionButton: tab == 2
                  ? FloatingActionButton(
                      backgroundColor: cAccent,
                      foregroundColor: Colors.white,
                      onPressed: () => showItemEditor(context, store, inbox: true),
                      child: const Icon(Icons.add))
                  : null,
          bottomNavigationBar: NavigationBar(
            selectedIndex: tab,
            onDestinationSelected: (i) => setState(() => tab = i),
            destinations: const [
              NavigationDestination(icon: Icon(Icons.calendar_month_outlined), label: '달력'),
              NavigationDestination(icon: Icon(Icons.view_agenda_outlined), label: '예정'),
              NavigationDestination(icon: Icon(Icons.inbox_outlined), label: '보관함'),
              NavigationDestination(icon: Icon(Icons.sync), label: '연결'),
            ],
          ),
        );
      },
    );
  }
}

// ───────────────────────── 달력 ─────────────────────────
class CalendarPage extends StatelessWidget {
  final AppStore store;
  final DateTime month, selected;
  final ValueChanged<DateTime> onMonth, onSelect;
  const CalendarPage(
      {super.key,
      required this.store,
      required this.month,
      required this.selected,
      required this.onMonth,
      required this.onSelect});

  @override
  Widget build(BuildContext context) {
    final today = dayOnly(DateTime.now());
    final first = DateTime(month.year, month.month, 1);
    final gridStart = addDays(first, -(first.weekday % 7)); // 일요일 시작
    return Column(children: [
      Padding(
        padding: const EdgeInsets.fromLTRB(8, 0, 8, 4),
        child: Row(children: [
          IconButton(onPressed: () => onMonth(DateTime(month.year, month.month - 1)), icon: const Icon(Icons.chevron_left)),
          Expanded(
              child: Text('${month.year}년 ${month.month}월',
                  textAlign: TextAlign.center, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700))),
          IconButton(onPressed: () => onMonth(DateTime(month.year, month.month + 1)), icon: const Icon(Icons.chevron_right)),
          TextButton(
              onPressed: () {
                onMonth(DateTime(today.year, today.month));
                onSelect(today);
              },
              child: const Text('오늘')),
        ]),
      ),
      Row(children: [
        for (var i = 0; i < 7; i++)
          Expanded(
              child: Center(
                  child: Text('일월화수목금토'[i],
                      style: TextStyle(fontSize: 12, color: i == 0 ? cSun : (i == 6 ? cSat : cSub))))),
      ]),
      Expanded(
        child: GestureDetector(
          onHorizontalDragEnd: (d) {
            final v = d.primaryVelocity ?? 0;
            if (v < -200) onMonth(DateTime(month.year, month.month + 1));
            if (v > 200) onMonth(DateTime(month.year, month.month - 1));
          },
          child: Column(children: [
            for (var w = 0; w < 6; w++)
              Expanded(
                child: Row(children: [
                  for (var c = 0; c < 7; c++) Expanded(child: _cell(context, addDays(gridStart, w * 7 + c), today)),
                ]),
              ),
          ]),
        ),
      ),
    ]);
  }

  Widget _cell(BuildContext context, DateTime d, DateTime today) {
    final inMonth = d.month == month.month;
    final isToday = sameDay(d, today);
    final sel = sameDay(d, selected);
    final h = holidayName(d);
    final rows = dayItems(store.data, d);
    final wd = d.weekday % 7; // 0=일
    Color numColor = !inMonth ? cDim : (h != null || wd == 0) ? cSun : (wd == 6 ? cSat : cText);
    if (isToday) numColor = Colors.white;
    return GestureDetector(
      onTap: () {
        onSelect(d);
        showDaySheet(context, store, d);
      },
      child: Container(
        margin: const EdgeInsets.all(1.5),
        decoration: BoxDecoration(
          color: sel ? cAccent.withOpacity(0.14) : (inMonth ? Colors.white.withOpacity(0.035) : Colors.transparent),
          borderRadius: BorderRadius.circular(9),
          border: Border.all(color: sel ? cAccent : Colors.white.withOpacity(inMonth ? 0.08 : 0.04), width: sel ? 1.4 : 1),
        ),
        child: LayoutBuilder(builder: (context, c) {
          final slots = ((c.maxHeight - 24) / 11.5).floor().clamp(1, 7);
          final entries = <(String, Color, bool, bool)>[]; // 글자, 색, 완료, 공휴일
          if (h != null) entries.add((h, inMonth ? cSun : cDim, false, true));
          for (final r in rows) {
            final done = r.t['done'] == true;
            entries.add((r.t['text'] as String? ?? '', done ? cDim : priColor(r.t), done, false));
          }
          final over = entries.length > slots;
          final show = over ? slots - 1 : entries.length;
          return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            const SizedBox(height: 3),
            Center(
              child: Container(
                width: 20,
                height: 18,
                alignment: Alignment.center,
                decoration: isToday ? BoxDecoration(color: cAccent, borderRadius: BorderRadius.circular(9)) : null,
                child: Text('${d.day}', style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: numColor)),
              ),
            ),
            const SizedBox(height: 2),
            for (var i = 0; i < show; i++)
              entries[i].$4
                  ? Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 3),
                      child: Text(entries[i].$1,
                          maxLines: 1, softWrap: false, overflow: TextOverflow.clip, style: TextStyle(fontSize: 8, color: entries[i].$2)))
                  : Padding(
                      padding: const EdgeInsets.fromLTRB(3, 0, 2, 1.5),
                      child: Row(children: [
                        Container(
                            width: 3.5,
                            height: 3.5,
                            margin: const EdgeInsets.only(right: 2),
                            decoration: BoxDecoration(color: entries[i].$2, shape: BoxShape.circle)),
                        Expanded(
                            child: Text(entries[i].$1,
                                maxLines: 1,
                                softWrap: false,
                                overflow: TextOverflow.clip,
                                style: TextStyle(
                                    fontSize: 8.5,
                                    height: 1.1,
                                    color: entries[i].$3 ? cDim : cText.withOpacity(0.92),
                                    decoration: entries[i].$3 ? TextDecoration.lineThrough : null))),
                      ])),
            if (over)
              Padding(
                  padding: const EdgeInsets.only(left: 4, top: 0),
                  child: Text('+${entries.length - show}', style: const TextStyle(fontSize: 8.5, color: cSub))),
          ]);
        }),
      ),
    );
  }
}

// 날짜를 누르면 아래에서 올라오는 메모 앱 같은 시트: 입력칸이 맨 위, 일정 목록, 메모
void showDaySheet(BuildContext context, AppStore store, DateTime d) {
  showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    backgroundColor: cSurface,
    shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(20))),
    builder: (ctx) => DaySheet(store: store, date: d),
  );
}

class DaySheet extends StatefulWidget {
  final AppStore store;
  final DateTime date;
  const DaySheet({super.key, required this.store, required this.date});
  @override
  State<DaySheet> createState() => _DaySheetState();
}

class _DaySheetState extends State<DaySheet> {
  late DateTime date = widget.date;
  final entry = TextEditingController();
  late final TextEditingController memo = TextEditingController(text: widget.store.memoOf(widget.date));
  int pri = 2;

  @override
  void dispose() {
    _saveMemo();
    entry.dispose();
    memo.dispose();
    super.dispose();
  }

  void _saveMemo() {
    if (memo.text != widget.store.memoOf(date)) widget.store.setMemo(date, memo.text);
  }

  void _go(int n) {
    _saveMemo();
    setState(() {
      date = addDays(date, n);
      memo.text = widget.store.memoOf(date);
    });
  }

  void _add() {
    final text = entry.text.trim();
    if (text.isEmpty) return;
    final t = newItem(text, extra: {'pri': pri});
    final m = RegExp(r'^(\d{1,2}):(\d{2})\s+(.+)$').firstMatch(text);
    if (m != null && int.parse(m.group(1)!) < 24 && int.parse(m.group(2)!) < 60) {
      t['text'] = m.group(3)!.trim();
      t['time'] = '${m.group(1)!.padLeft(2, '0')}:${m.group(2)}';
    }
    widget.store.add(iso(date), t);
    entry.clear();
    setState(() => pri = 2);
  }

  @override
  Widget build(BuildContext context) {
    final store = widget.store;
    final kb = MediaQuery.of(context).viewInsets.bottom;
    return ListenableBuilder(
      listenable: store,
      builder: (context, _) {
        final rows = dayItems(store.data, date);
        final h = holidayName(date);
        final today = dayOnly(DateTime.now());
        return Padding(
          padding: EdgeInsets.only(bottom: kb),
          child: SizedBox(
            height: MediaQuery.of(context).size.height * 0.78,
            child: Column(children: [
              Container(
                  width: 36, height: 4, margin: const EdgeInsets.only(top: 8),
                  decoration: BoxDecoration(color: cBorder, borderRadius: BorderRadius.circular(2))),
              Padding(
                padding: const EdgeInsets.fromLTRB(4, 6, 8, 0),
                child: Row(children: [
                  IconButton(onPressed: () => _go(-1), icon: const Icon(Icons.chevron_left)),
                  Expanded(
                      child: Column(children: [
                    Text(dateLabel(date), style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
                    if (h != null || sameDay(date, today))
                      Text([if (sameDay(date, today)) '오늘', if (h != null) h].join(' · '),
                          style: TextStyle(fontSize: 11, color: h != null ? cSun : cAccent)),
                  ])),
                  IconButton(onPressed: () => _go(1), icon: const Icon(Icons.chevron_right)),
                ]),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 4, 16, 0),
                child: Row(children: [
                  Expanded(
                    child: TextField(
                      controller: entry,
                      autofocus: true,
                      textInputAction: TextInputAction.done,
                      onSubmitted: (_) => _add(),
                      style: const TextStyle(fontSize: 14),
                      decoration: InputDecoration(
                        hintText: '새 일정 입력 (예: 14:30 회의)',
                        hintStyle: const TextStyle(fontSize: 13, color: cDim),
                        isDense: true,
                        filled: true,
                        fillColor: cCard,
                        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide.none),
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  GestureDetector(
                    onTap: () => setState(() => pri = (pri + 3) % 4), // 보통 → 낮음 → … 돌려가며 선택
                    child: Container(
                        width: 28, height: 28, alignment: Alignment.center,
                        child: Container(width: 14, height: 14, decoration: BoxDecoration(color: priColors[pri], shape: BoxShape.circle))),
                  ),
                  IconButton.filled(
                      style: IconButton.styleFrom(backgroundColor: cAccent, minimumSize: const Size(36, 36)),
                      onPressed: _add,
                      icon: const Icon(Icons.arrow_upward, size: 18)),
                ]),
              ),
              Expanded(
                child: ListView(padding: const EdgeInsets.only(top: 6, bottom: 8), children: [
                  if (rows.isEmpty)
                    const Padding(
                        padding: EdgeInsets.all(22),
                        child: Center(child: Text('이 날의 일정이 없어요', style: TextStyle(color: cDim, fontSize: 13)))),
                  for (final r in rows) ItemTile(store: store, row: r),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 10, 16, 4),
                    child: TextField(
                      controller: memo,
                      minLines: 3,
                      maxLines: 6,
                      onChanged: (_) => _saveMemo(),
                      style: const TextStyle(fontSize: 13),
                      decoration: InputDecoration(
                        hintText: '메모',
                        hintStyle: const TextStyle(color: cDim, fontSize: 13),
                        filled: true,
                        fillColor: cCard,
                        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10), borderSide: BorderSide.none),
                      ),
                    ),
                  ),
                ]),
              ),
            ]),
          ),
        );
      },
    );
  }
}

class DayPanel extends StatelessWidget {
  final AppStore store;
  final DateTime date;
  const DayPanel({super.key, required this.store, required this.date});

  @override
  Widget build(BuildContext context) {
    final rows = dayItems(store.data, date);
    final h = holidayName(date);
    final memo = store.memoOf(date);
    final today = dayOnly(DateTime.now());
    return RefreshIndicator(
      onRefresh: store.syncNow,
      child: ListView(padding: const EdgeInsets.only(bottom: 90), children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 10, 8, 2),
          child: Row(children: [
            Expanded(
                child: Text(
                    '${dateLabel(date)}${sameDay(date, today) ? '  · 오늘' : ''}${h != null ? '  · $h' : ''}',
                    style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700))),
            IconButton(
                tooltip: '메모',
                onPressed: () => _editMemo(context),
                icon: Icon(memo.isEmpty ? Icons.edit_note : Icons.sticky_note_2, color: memo.isEmpty ? cSub : cAccent)),
          ]),
        ),
        if (memo.isNotEmpty)
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 6),
            child: GestureDetector(
                onTap: () => _editMemo(context),
                child: Text(memo, maxLines: 3, overflow: TextOverflow.ellipsis, style: const TextStyle(color: cSub, fontSize: 13))),
          ),
        if (rows.isEmpty)
          const Padding(
              padding: EdgeInsets.all(24),
              child: Center(child: Text('일정이 없어요\n아래 + 로 추가하세요', textAlign: TextAlign.center, style: TextStyle(color: cDim)))),
        for (final r in rows) ItemTile(store: store, row: r),
      ]),
    );
  }

  Future<void> _editMemo(BuildContext context) async {
    final c = TextEditingController(text: store.memoOf(date));
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('${dateLabel(date)} 메모'),
        content: TextField(controller: c, maxLines: 6, autofocus: true),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('취소')),
          FilledButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('저장')),
        ],
      ),
    );
    if (ok == true) store.setMemo(date, c.text);
  }
}

class ItemTile extends StatelessWidget {
  final AppStore store;
  final ItemRow row;
  final bool inbox;
  const ItemTile({super.key, required this.store, required this.row, this.inbox = false});

  @override
  Widget build(BuildContext context) {
    final t = row.t;
    final done = t['done'] == true;
    final col = priColor(t);
    final tags = <String>[];
    final rep = t['repeat'] as Json?;
    if (rep != null) {
      tags.add('반복 ${repeatLabels.firstWhere((e) => e[0] == rep['f'], orElse: () => ['', ''])[1]}');
    } else {
      final e = inbox ? null : itemEnd(t, row.start);
      if (e != null) tags.add('기간 ${row.start.month}/${row.start.day} ~ ${e.month}/${e.day}');
    }
    if ((t['time'] as String?)?.isNotEmpty == true && !inbox) tags.add(t['time'] as String);
    final note = (t['note'] as String?) ?? '';
    final key = ValueKey('${t['id']}-${t['_occ'] ?? ''}-${inbox ? 'i' : 'd'}');
    return Dismissible(
      key: key,
      direction: DismissDirection.endToStart,
      background: Container(
          color: const Color(0xFF8A2A3A),
          alignment: Alignment.centerRight,
          padding: const EdgeInsets.only(right: 20),
          child: const Icon(Icons.delete_outline)),
      onDismissed: (_) {
        final (ownerKey, _) = findOwner(store.data, t);
        final base = baseOf(t);
        store.remove(t);
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: const Text('일정을 삭제했어요'),
          action: SnackBarAction(
              label: '실행 취소',
              onPressed: () {
                touch(base);
                store.add(ownerKey ?? (inbox ? '_inbox' : iso(row.start)), base);
              }),
        ));
      },
      child: ListTile(
        dense: true,
        leading: GestureDetector(
          onTap: () => store.toggle(t),
          child: Icon(done ? Icons.check_circle : Icons.radio_button_unchecked, color: done ? cDim : col, size: 26),
        ),
        title: Text(t['text'] as String? ?? '',
            style: TextStyle(
                fontSize: 14,
                color: done ? cDim : cText,
                decoration: done ? TextDecoration.lineThrough : null)),
        subtitle: (tags.isEmpty && note.isEmpty)
            ? null
            : Text([...tags, if (note.isNotEmpty) note.split('\n').first].join('  ·  '),
                maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: done ? cDim : cSub)),
        trailing: inbox
            ? TextButton(
                onPressed: () async {
                  final d = await showDatePicker(
                      context: context,
                      initialDate: dayOnly(DateTime.now()),
                      firstDate: DateTime(2020),
                      lastDate: DateTime(2035));
                  if (d != null) store.moveTo(t, d);
                },
                child: const Text('꺼내기'))
            : GestureDetector(
                onTap: () => store.cyclePri(t),
                child: Container(
                    width: 10, height: 10, decoration: BoxDecoration(color: done ? cDim : col, shape: BoxShape.circle))),
        onTap: () => showItemEditor(context, store, item: t),
      ),
    );
  }
}

// ───────────────────────── 예정 / 보관함 ─────────────────────────
class AgendaPage extends StatelessWidget {
  final AppStore store;
  final ValueChanged<DateTime> onJump;
  const AgendaPage({super.key, required this.store, required this.onJump});

  @override
  Widget build(BuildContext context) {
    final today = dayOnly(DateTime.now());
    final children = <Widget>[];
    Widget header(String text, Color col, DateTime? d) => InkWell(
          onTap: d == null ? null : () => onJump(d),
          child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 14, 16, 2),
              child: Text(text, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: col))),
        );
    // 지난 미완료
    final over = <ItemRow>[];
    store.data.forEach((key, v) {
      if (nonDateKeys.contains(key) || v is! Map) return;
      final s = parseDate(key);
      if (s == null || !s.isBefore(today)) return;
      final list = (v['todos'] as List?) ?? const [];
      for (final t0 in list) {
        final t = t0 as Json;
        if (t['done'] == true || t['repeat'] != null) continue;
        if ((itemEnd(t, s) ?? s).isBefore(today)) over.add(ItemRow(list, t, s));
      }
    });
    over.sort((a, b) => a.start.compareTo(b.start));
    if (over.isNotEmpty) {
      children.add(header('지난 일정 · 아직 안 끝난 것', cSun, null));
      children.addAll(over.map((r) => ItemTile(store: store, row: r)));
    }
    for (var k = 0; k < 14; k++) {
      final d = addDays(today, k);
      final rows = dayItems(store.data, d);
      final h = holidayName(d);
      if (rows.isEmpty && k != 0 && h == null) continue;
      final name = k == 0 ? '오늘' : k == 1 ? '내일' : '${d.month}/${d.day} (${weekdayKo[d.weekday - 1]})';
      children.add(header(name + (h != null ? ' · $h' : ''), k == 0 ? cAccent : cSub, d));
      if (rows.isEmpty) {
        children.add(const Padding(padding: EdgeInsets.fromLTRB(16, 4, 16, 4), child: Text('일정 없음', style: TextStyle(color: cDim, fontSize: 13))));
      }
      children.addAll(rows.map((r) => ItemTile(store: store, row: r)));
    }
    return RefreshIndicator(onRefresh: store.syncNow, child: ListView(padding: const EdgeInsets.only(bottom: 24), children: children));
  }
}

class InboxPage extends StatelessWidget {
  final AppStore store;
  const InboxPage({super.key, required this.store});

  @override
  Widget build(BuildContext context) {
    final list = store.inbox();
    final raw = ((store.data['_inbox'] as Map?)?['todos'] as List?) ?? <dynamic>[];
    return RefreshIndicator(
      onRefresh: store.syncNow,
      child: ListView(padding: const EdgeInsets.only(bottom: 90), children: [
        const Padding(
            padding: EdgeInsets.fromLTRB(16, 10, 16, 6),
            child: Text('날짜를 정하기 전의 할 일을 적어 두는 곳이에요. 날짜가 정해지면 "꺼내기"로 옮기세요.',
                style: TextStyle(color: cSub, fontSize: 13))),
        if (list.isEmpty)
          const Padding(padding: EdgeInsets.all(32), child: Center(child: Text('보관함이 비어 있어요', style: TextStyle(color: cDim)))),
        for (final t in list) ItemTile(store: store, row: ItemRow(raw, t, dayOnly(DateTime.now())), inbox: true),
      ]),
    );
  }
}

// ───────────────────────── 검색 ─────────────────────────
class ItemSearch extends SearchDelegate<Object?> {
  final AppStore store;
  ItemSearch(this.store) : super(searchFieldLabel: '일정·메모 검색');

  @override
  List<Widget>? buildActions(BuildContext context) =>
      [IconButton(icon: const Icon(Icons.clear), onPressed: () => query = '')];

  @override
  Widget? buildLeading(BuildContext context) =>
      IconButton(icon: const Icon(Icons.arrow_back), onPressed: () => close(context, null));

  @override
  Widget buildResults(BuildContext context) => _list(context);

  @override
  Widget buildSuggestions(BuildContext context) => _list(context);

  Widget _list(BuildContext context) {
    final q = query.trim().toLowerCase();
    if (q.isEmpty) return const Center(child: Text('찾을 말을 입력하세요', style: TextStyle(color: cDim)));
    final hits = <(DateTime?, String, String)>[];
    store.data.forEach((key, v) {
      if (key == '_deleted' || v is! Map) return;
      final d = parseDate(key);
      for (final t in (v['todos'] as List? ?? const [])) {
        final text = (t['text'] as String?) ?? '', note = (t['note'] as String?) ?? '';
        if (text.toLowerCase().contains(q) || note.toLowerCase().contains(q)) hits.add((d, text, d == null ? '보관함' : dateLabel(d)));
      }
      final memo = (v['memo'] as String?) ?? '';
      if (d != null && memo.toLowerCase().contains(q)) hits.add((d, memo.split('\n').first, '${dateLabel(d)} · 메모'));
    });
    hits.sort((a, b) => a.$1 == null ? 1 : b.$1 == null ? -1 : a.$1!.compareTo(b.$1!));
    if (hits.isEmpty) return const Center(child: Text('결과가 없어요', style: TextStyle(color: cDim)));
    return ListView(children: [
      for (final h in hits.take(100))
        ListTile(title: Text(h.$2, maxLines: 2, overflow: TextOverflow.ellipsis), subtitle: Text(h.$3), onTap: () => close(context, h.$1 ?? 'inbox')),
    ]);
  }
}

// ───────────────────────── 일정 입력 / 수정 ─────────────────────────
Future<void> showItemEditor(BuildContext context, AppStore store, {Json? item, DateTime? date, bool inbox = false}) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    backgroundColor: cSurface,
    shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(18))),
    builder: (ctx) => Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(ctx).viewInsets.bottom),
      child: ItemEditor(store: store, item: item, date: date, inbox: inbox),
    ),
  );
}

class ItemEditor extends StatefulWidget {
  final AppStore store;
  final Json? item;
  final DateTime? date;
  final bool inbox;
  const ItemEditor({super.key, required this.store, this.item, this.date, this.inbox = false});
  @override
  State<ItemEditor> createState() => _ItemEditorState();
}

class _ItemEditorState extends State<ItemEditor> {
  late final TextEditingController text, note;
  DateTime? start, end, until;
  String? time;
  int pri = 2;
  String rep = '';
  String? ownerKey;

  @override
  void initState() {
    super.initState();
    final it = widget.item == null ? null : baseOf(widget.item!);
    text = TextEditingController(text: it?['text'] as String? ?? '');
    note = TextEditingController(text: it?['note'] as String? ?? '');
    if (it != null) {
      final (k, _) = findOwner(widget.store.data, it);
      ownerKey = k;
      start = k == null || k == '_inbox' ? null : parseDate(k);
      end = parseDate(it['end'] as String?);
      time = (it['time'] as String?)?.isNotEmpty == true ? it['time'] as String : null;
      pri = ((it['pri'] as num?)?.toInt() ?? 2).clamp(0, 3);
      final r = it['repeat'] as Json?;
      rep = (r?['f'] as String?) ?? '';
      until = parseDate(r?['until'] as String?);
    } else {
      start = widget.inbox ? null : (widget.date ?? dayOnly(DateTime.now()));
    }
  }

  @override
  void dispose() {
    text.dispose();
    note.dispose();
    super.dispose();
  }

  Future<DateTime?> _pick(DateTime? init) => showDatePicker(
      context: context, initialDate: init ?? dayOnly(DateTime.now()), firstDate: DateTime(2020), lastDate: DateTime(2035));

  void _save() {
    final s = text.text.trim();
    if (s.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('내용을 입력해 주세요')));
      return;
    }
    final store = widget.store;
    final t = widget.item == null ? newItem(s) : baseOf(widget.item!);
    t['text'] = s;
    t['pri'] = pri;
    void setOrRemove(String k, dynamic v) => (v == null || v == '') ? t.remove(k) : t[k] = v;
    setOrRemove('time', time);
    setOrRemove('end', (start != null && end != null && end!.isAfter(start!) && rep.isEmpty) ? iso(end!) : null);
    setOrRemove('note', note.text.trim());
    if (rep.isNotEmpty) {
      t['repeat'] = {'f': rep, 'n': 1, if (until != null) 'until': iso(until!)};
    } else {
      t.remove('repeat');
      t.remove('done_dates');
    }
    touch(t);
    final newKey = start == null ? '_inbox' : iso(start!);
    if (widget.item == null) {
      store.add(newKey, t);
    } else {
      if (newKey != ownerKey) {
        removeItem(store.data, t);
        addItem(store.data, newKey, t);
      }
      store.changed();
    }
    Navigator.pop(context);
  }

  @override
  Widget build(BuildContext context) {
    const chipStyle = TextStyle(fontSize: 13);
    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(18, 16, 18, 12),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisSize: MainAxisSize.min, children: [
          Text(widget.item == null ? '일정 추가' : '일정 수정', style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700)),
          const SizedBox(height: 10),
          TextField(
              controller: text,
              autofocus: widget.item == null,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(hintText: '무엇을 할까요?', border: OutlineInputBorder())),
          const SizedBox(height: 10),
          Wrap(spacing: 8, runSpacing: 4, children: [
            OutlinedButton.icon(
                onPressed: () async {
                  final d = await _pick(start);
                  if (d != null) setState(() => start = d);
                },
                icon: const Icon(Icons.event, size: 18),
                label: Text(start == null ? '날짜 없음 (보관함)' : dateLabel(start!))),
            if (start != null && rep.isEmpty)
              OutlinedButton.icon(
                  onPressed: () async {
                    final d = await _pick(end ?? start);
                    if (d != null) setState(() => end = d);
                  },
                  icon: const Icon(Icons.date_range, size: 18),
                  label: Text(end == null ? '기간 (종료일)' : '~ ${end!.month}/${end!.day}')),
            if (end != null && rep.isEmpty) TextButton(onPressed: () => setState(() => end = null), child: const Text('기간 해제')),
            OutlinedButton.icon(
                onPressed: () async {
                  final tt = await showTimePicker(context: context, initialTime: const TimeOfDay(hour: 9, minute: 0));
                  if (tt != null) setState(() => time = '${two(tt.hour)}:${two(tt.minute)}');
                },
                icon: const Icon(Icons.schedule, size: 18),
                label: Text(time ?? '시간')),
            if (time != null) TextButton(onPressed: () => setState(() => time = null), child: const Text('시간 해제')),
          ]),
          const SizedBox(height: 10),
          const Text('중요도', style: TextStyle(color: cSub, fontSize: 12)),
          Wrap(spacing: 8, children: [
            for (var i = 0; i < 4; i++)
              ChoiceChip(
                  selected: pri == i,
                  showCheckmark: false,
                  avatar: Icon(Icons.circle, size: 12, color: priColors[i]),
                  label: Text(priNames[i], style: chipStyle),
                  onSelected: (_) => setState(() => pri = i)),
          ]),
          const SizedBox(height: 6),
          const Text('반복', style: TextStyle(color: cSub, fontSize: 12)),
          Wrap(spacing: 8, children: [
            for (final r in repeatLabels)
              ChoiceChip(
                  selected: rep == r[0],
                  showCheckmark: false,
                  label: Text(r[1], style: chipStyle),
                  onSelected: (_) => setState(() => rep = r[0])),
          ]),
          if (rep.isNotEmpty)
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                  onPressed: () async {
                    final d = await _pick(until ?? start);
                    if (d != null) setState(() => until = d);
                  },
                  icon: const Icon(Icons.event_busy, size: 18),
                  label: Text(until == null ? '반복 종료일 (없으면 계속)' : '${iso(until!)} 까지')),
            ),
          const SizedBox(height: 6),
          TextField(
              controller: note,
              maxLines: 3,
              minLines: 1,
              decoration: const InputDecoration(hintText: '메모', border: OutlineInputBorder())),
          const SizedBox(height: 14),
          Row(children: [
            if (widget.item != null)
              TextButton(
                  onPressed: () {
                    widget.store.remove(widget.item!);
                    Navigator.pop(context);
                  },
                  child: const Text('삭제', style: TextStyle(color: cSun))),
            const Spacer(),
            TextButton(onPressed: () => Navigator.pop(context), child: const Text('취소')),
            const SizedBox(width: 8),
            FilledButton(onPressed: _save, child: const Text('저장')),
          ]),
        ]),
      ),
    );
  }
}

// ───────────────────────── 연결 · 설정 ─────────────────────────
class ConnectPage extends StatelessWidget {
  final AppStore store;
  const ConnectPage({super.key, required this.store});

  Future<void> _scan(BuildContext context) async {
    final cfg = await Navigator.push<Map<String, dynamic>>(context, MaterialPageRoute(builder: (_) => const ScanPage()));
    if (cfg != null) await store.connect(cfg);
  }

  @override
  Widget build(BuildContext context) {
    final cfg = store.syncCfg;
    Widget card(List<Widget> children) => Card(
        color: cCard,
        margin: const EdgeInsets.fromLTRB(14, 8, 14, 6),
        child: Padding(padding: const EdgeInsets.all(16), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: children)));
    return ListView(children: [
      card([
        const Text('PC와 연결', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
        const SizedBox(height: 8),
        if (cfg == null) ...[
          const Text('PC 프로그램의 [휴대폰 연결] 화면에 나오는 QR 코드를 스캔하세요.\n집·사무실 밖에서도 인터넷만 되면 동기화돼요.',
              style: TextStyle(color: cSub, fontSize: 13)),
          const SizedBox(height: 12),
          FilledButton.icon(onPressed: () => _scan(context), icon: const Icon(Icons.qr_code_scanner), label: const Text('QR 스캔해서 연결')),
        ] else ...[
          Text('연결됨 · ${cfg['repo']}', style: const TextStyle(color: cAccent)),
          const SizedBox(height: 4),
          Text(store.syncStatus.isEmpty ? '대기 중' : store.syncStatus, style: const TextStyle(color: cSub, fontSize: 13)),
          const SizedBox(height: 12),
          Row(children: [
            FilledButton.tonal(onPressed: store.syncing ? null : store.syncNow, child: const Text('지금 동기화')),
            const SizedBox(width: 8),
            TextButton(
                onPressed: () async {
                  final ok = await showDialog<bool>(
                      context: context,
                      builder: (ctx) => AlertDialog(
                            title: const Text('연결 해제'),
                            content: const Text('이 폰의 동기화를 끕니다. 폰 안의 일정은 그대로 남아요.'),
                            actions: [
                              TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('취소')),
                              TextButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('해제')),
                            ],
                          ));
                  if (ok == true) await store.disconnect();
                },
                child: const Text('연결 해제')),
          ]),
        ],
      ]),
      card([
        const Text('알림', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
        SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('일정 알림'),
            subtitle: const Text('시간이 있는 일정을 미리 알려줘요'),
            value: store.notify,
            onChanged: (v) {
              store.notify = v;
              store.saveSettings();
            }),
        Row(children: [
          const Text('미리 알림'),
          const Spacer(),
          DropdownButton<int>(
              value: store.lead,
              items: const [
                DropdownMenuItem(value: 0, child: Text('정시')),
                DropdownMenuItem(value: 5, child: Text('5분 전')),
                DropdownMenuItem(value: 10, child: Text('10분 전')),
                DropdownMenuItem(value: 30, child: Text('30분 전')),
                DropdownMenuItem(value: 60, child: Text('1시간 전')),
              ],
              onChanged: (v) {
                store.lead = v ?? 10;
                store.saveSettings();
              }),
        ]),
        Row(children: [
          const Text('아침 요약'),
          const Spacer(),
          DropdownButton<String>(
              value: store.brief,
              items: const [
                DropdownMenuItem(value: '', child: Text('끔')),
                DropdownMenuItem(value: '08:00', child: Text('오전 8시')),
                DropdownMenuItem(value: '09:00', child: Text('오전 9시')),
                DropdownMenuItem(value: '10:00', child: Text('오전 10시')),
              ],
              onChanged: (v) {
                store.brief = v ?? '';
                store.saveSettings();
              }),
        ]),
        TextButton.icon(
            onPressed: () async {
              final ok = await Notifier.requestPermission();
              if (context.mounted) {
                ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(ok ? '알림이 허용됐어요' : '설정에서 알림을 허용해 주세요')));
              }
            },
            icon: const Icon(Icons.notifications_active_outlined),
            label: const Text('알림 권한 허용')),
      ]),
      const Padding(
          padding: EdgeInsets.fromLTRB(20, 8, 20, 24),
          child: Text('일정은 암호화되어 내 GitHub 비공개 저장소를 거쳐 PC와 주고받아요.\n암호 키는 이 폰과 PC에만 있어요.\n\nDeskCalendar $appVersion',
              style: TextStyle(color: cDim, fontSize: 12))),
    ]);
  }
}

class ScanPage extends StatefulWidget {
  const ScanPage({super.key});
  @override
  State<ScanPage> createState() => _ScanPageState();
}

class _ScanPageState extends State<ScanPage> {
  bool _done = false;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('PC의 QR 코드를 비추세요')),
      body: MobileScanner(
        onDetect: (capture) {
          if (_done) return;
          for (final b in capture.barcodes) {
            final raw = b.rawValue;
            if (raw == null) continue;
            try {
              final j = jsonDecode(raw) as Map<String, dynamic>;
              if (j['repo'] != null && j['tok'] != null && j['key'] != null) {
                _done = true;
                Navigator.pop(context, j);
                return;
              }
            } catch (_) {}
          }
        },
      ),
    );
  }
}
