import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart' show TargetPlatform, defaultTargetPlatform, kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import 'core.dart';
import 'holidays.dart';
import 'notify.dart';
import 'pairing.dart';
import 'quickadd.dart';
import 'package:home_widget/home_widget.dart';

import 'bgsync.dart';
import 'candidates.dart';
import 'ocr_mobile.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';
import 'share_import.dart';
import 'widget_sync.dart';
import 'store.dart';
import 'work_page.dart';

const cBg = Color(0xFF0F1015);
const cSurface = Color(0xFF16171F);
const cCard = Color(0xFF1C1E28);
const cBorder = Color(0xFF292C3A);
const cText = Color(0xFFF1F3F8);
const cSub = Color(0xFF9AA4B8);
const cDim = Color(0xFF687187);
const cAccent = Color(0xFF7C6CFF);
const cSun = Color(0xFFF08A94);
const cSat = Color(0xFF8FB4F2);
// 중요도는 3단계(중요/보통/낮음). 번호 0 은 예전 데이터용(= 중요)
const priColors = [Color(0xFFFF8F9B), Color(0xFFFF8F9B), Color(0xFFDFE4EE), Color(0xFF82D9A8)];
const priNames = ['중요', '중요', '보통', '낮음'];
const fontFamily = 'Pretendard'; // 빌드 때 내려받아 넣으면 적용되고, 없으면 기기 기본 글꼴
const appVersion = String.fromEnvironment('APP_VERSION', defaultValue: 'dev');
const kFast = Duration(milliseconds: 180);
const kMid = Duration(milliseconds: 320);
const kCurve = Curves.easeOutCubic;
const weekdayKo = ['월', '화', '수', '목', '금', '토', '일'];

int priOf(Json t) {
  final p = (((baseOf(t)['pri'] as num?)?.toInt() ?? 2)).clamp(0, 3);
  return p == 0 ? 1 : p;
}

int nextPri(int p) => p <= 1 ? 3 : p - 1; // 중요 → 낮음 → 보통 → 중요 …
Color priColor(Json t) => priColors[priOf(t)];
String dateLabel(DateTime d) => '${d.month}월 ${d.day}일 (${weekdayKo[d.weekday - 1]})';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  if (!kIsWeb) await Notifier.init(); // 웹(아이폰 홈 화면 앱)에서는 폰 알림 예약을 쓰지 않는다
  if (kIsWeb) MobileScannerPlatform.instance.setBarcodeLibraryScriptUrl('zxing-library-0.19.1.min.js'); // QR 해석 부품을 외부 서버 대신 앱에 포함한 것으로 쓴다
  final store = AppStore();
  await store.load();
  runApp(DeskApp(store));
  store.syncNow();
  Notifier.reschedule(store);
  updateHomeWidget(store);
  registerBackgroundSync();
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
        fontFamily: fontFamily,
        colorScheme: scheme,
        scaffoldBackgroundColor: Colors.transparent,
        splashFactory: InkRipple.splashFactory,
        highlightColor: cAccent.withOpacity(0.06),
        splashColor: cAccent.withOpacity(0.12),
        hoverColor: cAccent.withOpacity(0.07),
        pageTransitionsTheme: const PageTransitionsTheme(builders: {
          TargetPlatform.android: CupertinoPageTransitionsBuilder(),
          TargetPlatform.iOS: CupertinoPageTransitionsBuilder(),
        }),
        appBarTheme: const AppBarTheme(
            backgroundColor: Colors.transparent,
            foregroundColor: cText,
            elevation: 0,
            scrolledUnderElevation: 0,
            centerTitle: false,
            titleTextStyle: TextStyle(fontFamily: fontFamily, fontSize: 20, fontWeight: FontWeight.w700, color: cText)),
        navigationBarTheme: NavigationBarThemeData(
            backgroundColor: Colors.transparent,
            height: 64,
            indicatorColor: cAccent.withOpacity(0.22),
            indicatorShape: const StadiumBorder(),
            labelTextStyle: WidgetStateProperty.all(const TextStyle(fontSize: 11.5, fontWeight: FontWeight.w600))),
        cardTheme: CardTheme(
            color: cCard,
            elevation: 0,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(22))),
        dialogTheme: DialogTheme(
            backgroundColor: cSurface, shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(26))),
        bottomSheetTheme: const BottomSheetThemeData(
            backgroundColor: cSurface,
            modalBackgroundColor: cSurface,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(28)))),
        snackBarTheme: SnackBarThemeData(
            behavior: SnackBarBehavior.floating,
            backgroundColor: cCard,
            contentTextStyle: const TextStyle(color: cText, fontSize: 13.5),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16))),
        filledButtonTheme: FilledButtonThemeData(
            style: FilledButton.styleFrom(
                shape: const StadiumBorder(), padding: const EdgeInsets.symmetric(horizontal: 22, vertical: 12))),
        outlinedButtonTheme: OutlinedButtonThemeData(
            style: OutlinedButton.styleFrom(
                shape: const StadiumBorder(), side: BorderSide.none, backgroundColor: Colors.white.withOpacity(0.07), foregroundColor: cText)),
        textButtonTheme: TextButtonThemeData(style: TextButton.styleFrom(shape: const StadiumBorder())),
        chipTheme: ChipThemeData(
            shape: const StadiumBorder(),
            side: BorderSide.none,
            pressElevation: 0,
            backgroundColor: Colors.white.withOpacity(0.06),
            selectedColor: cAccent.withOpacity(0.24)),
        inputDecorationTheme: InputDecorationTheme(
            filled: true,
            fillColor: cCard,
            contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 13),
            border: OutlineInputBorder(borderRadius: BorderRadius.circular(16), borderSide: BorderSide.none),
            enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(16), borderSide: BorderSide.none),
            focusedBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(16), borderSide: const BorderSide(color: cAccent, width: 1.4))),
        sliderTheme: SliderThemeData(
            trackHeight: 4, thumbShape: const RoundSliderThumbShape(enabledThumbRadius: 9), overlayShape: SliderComponentShape.noOverlay),
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

  Timer? _poll;
  StreamSubscription<Uri?>? _widgetSub;
  ShareImporter? _share;

  // 앱이 화면에 떠 있는 동안 30초마다 PC 의 변경을 받아온다 (내 변경은 입력 후 4초 안에 자동으로 올라감)
  void _startPoll() {
    _poll?.cancel();
    _poll = Timer.periodic(const Duration(seconds: 30), (_) => widget.store.syncNow());
  }

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _startPoll();
    _share = ShareImporter(widget.store, () => context)..start();
    if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) {
      HomeWidget.initiallyLaunchedFromHomeWidget().then(_widgetUri).catchError((_) {});
      _widgetSub = HomeWidget.widgetClicked.listen(_widgetUri, onError: (_) {});
    }
  }

  /// 홈 화면 위젯을 눌러 들어왔을 때: 날짜를 누르면 그 날짜 시트, + 는 오늘에 추가, 제목은 보관함
  void _widgetUri(Uri? u) {
    if (u == null || !mounted) return;
    if (u.host == 'date' && u.pathSegments.isNotEmpty) {
      final d = parseDate(u.pathSegments.first);
      if (d != null) {
        jump(d);
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (mounted) showDaySheet(context, widget.store, d);
        });
      }
    } else if (u.host == 'add') {
      final d = dayOnly(DateTime.now());
      jump(d);
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) showDaySheet(context, widget.store, d);
      });
    } else if (u.host == 'tasks') {
      setState(() => tab = 3);
    } else if (u.host == 'work') {
      setState(() => tab = 2);
    }
  }

  @override
  void dispose() {
    _poll?.cancel();
    _share?.dispose();
    _widgetSub?.cancel();
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState s) {
    if (s == AppLifecycleState.resumed) {
      widget.store.syncNow();
      _startPoll();
    } else if (s == AppLifecycleState.paused) {
      _poll?.cancel();
    }
  }

  void jump(DateTime d) => setState(() {
        selected = d;
        month = DateTime(d.year, d.month);
        tab = 0;
      });

  /// 복사해 둔 글(메신저·메일) 또는 캡처·사진 속 글자를 분석해서 일정/할 일 후보로 만든다
  Future<void> _smartAdd(String kind) async {
    final store = widget.store;
    if (kind == 'clip') {
      final data = await Clipboard.getData('text/plain');
      final text = data?.text ?? '';
      if (!mounted) return;
      if (text.trim().length < 6) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('먼저 메신저·메일의 내용을 복사해 주세요')));
        return;
      }
      await showCandidates(context, store, text);
    } else {
      final img = await ImagePicker().pickImage(source: ImageSource.gallery);
      if (img == null || !mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('이미지에서 글자를 읽는 중…'), duration: Duration(seconds: 3)));
      try {
        final text = await ocrImage(img.path);
        if (!mounted) return;
        await showCandidates(context, store, text, heading: '캡처한 이미지에서 만들기');
      } catch (_) {
        if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('이미지에서 글자를 읽지 못했어요')));
      }
    }
  }

  Future<void> search() async {
    final r = await showSearch<Object?>(context: context, delegate: ItemSearch(widget.store));
    if (r is DateTime) {
      jump(r);
    } else if (r == 'inbox') {
      setState(() => tab = 3);
    }
  }

  @override
  Widget build(BuildContext context) {
    final store = widget.store;
    return ListenableBuilder(
      listenable: store,
      builder: (context, _) {
        final titles = ['', '예정', '업무', '보관함', '연결 · 설정'];
        final now = DateTime.now();
        final pages = <Widget>[
          CalendarPage(
            key: const ValueKey('cal'),
            store: store,
            month: month,
            selected: selected,
            onMonth: (m) => setState(() => month = m),
            onAgenda: () => setState(() => tab = 1),
            onSelect: (d) => setState(() {
              selected = d;
              if (d.month != month.month) month = DateTime(d.year, d.month);
            }),
          ),
          AgendaPage(key: const ValueKey('agenda'), store: store, onJump: jump),
          WorkPage(key: const ValueKey('work'), store: store, onJump: jump),
          InboxPage(key: const ValueKey('inbox'), store: store),
          ConnectPage(key: const ValueKey('connect'), store: store),
        ];
        return DecoratedBox(
          decoration: const BoxDecoration(
              gradient: LinearGradient(
                  begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Color(0xFF181730), cBg, cBg], stops: [0, 0.38, 1])),
          child: Scaffold(
            extendBody: true,
            appBar: AppBar(
              toolbarHeight: 56,
              title: tab == 0
                  ? Text('${now.month}월 ${now.day}일 ${weekdayKo[now.weekday - 1]}요일',
                      style: const TextStyle(fontSize: 19, fontWeight: FontWeight.w800))
                  : Text(titles[tab], style: const TextStyle(fontSize: 19, fontWeight: FontWeight.w800)),
              actions: [
                AnimatedSwitcher(
                    duration: kFast,
                    child: store.syncing
                        ? const Padding(
                            key: ValueKey('spin'),
                            padding: EdgeInsets.symmetric(horizontal: 14),
                            child: SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2)))
                        : const SizedBox(key: ValueKey('nospin'), width: 4)),
                PopupMenuButton<String>(
                  icon: const Icon(Icons.auto_awesome_rounded),
                  tooltip: '복사한 글·캡처로 일정 만들기',
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
                  color: cSurface,
                  onSelected: (v) => _smartAdd(v),
                  itemBuilder: (_) => [
                    const PopupMenuItem(value: 'clip', child: ListTile(dense: true, leading: Icon(Icons.content_paste_rounded), title: Text('복사한 글에서 만들기'))),
                    if (!kIsWeb) const PopupMenuItem(value: 'img', child: ListTile(dense: true, leading: Icon(Icons.image_search_rounded), title: Text('캡처·사진에서 읽기'))),
                  ],
                ),
                IconButton(icon: const Icon(Icons.search_rounded), onPressed: search),
                const SizedBox(width: 4),
              ],
            ),
            body: AnimatedSwitcher(
              duration: kMid,
              switchInCurve: kCurve,
              switchOutCurve: Curves.easeIn,
              transitionBuilder: (child, anim) => FadeTransition(
                  opacity: anim,
                  child: SlideTransition(
                      position: Tween(begin: const Offset(0, 0.03), end: Offset.zero).animate(anim), child: child)),
              child: KeyedSubtree(key: ValueKey(tab), child: pages[tab]),
            ),
            floatingActionButton: tab == 3
                ? FloatingActionButton.extended(
                    backgroundColor: cAccent,
                    foregroundColor: Colors.white,
                    elevation: 2,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
                    onPressed: () => showItemEditor(context, store, inbox: true),
                    icon: const Icon(Icons.add_rounded),
                    label: const Text('새 할 일', style: TextStyle(fontWeight: FontWeight.w700)))
                : null,
            bottomNavigationBar: Padding(
              padding: const EdgeInsets.fromLTRB(14, 0, 14, 10),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(28),
                child: DecoratedBox(
                  decoration: BoxDecoration(
                      color: cSurface.withOpacity(0.96),
                      borderRadius: BorderRadius.circular(28)),
                  child: NavigationBar(
                    selectedIndex: tab,
                    animationDuration: kMid,
                    onDestinationSelected: (i) {
                      if (i != tab) HapticFeedback.selectionClick();
                      setState(() => tab = i);
                    },
                    destinations: const [
                      NavigationDestination(
                          icon: Icon(Icons.calendar_month_outlined),
                          selectedIcon: Icon(Icons.calendar_month_rounded),
                          label: '달력'),
                      NavigationDestination(
                          icon: Icon(Icons.view_agenda_outlined), selectedIcon: Icon(Icons.view_agenda_rounded), label: '예정'),
                      NavigationDestination(
                          icon: Icon(Icons.work_outline_rounded), selectedIcon: Icon(Icons.work_rounded), label: '업무'),
                      NavigationDestination(
                          icon: Icon(Icons.inbox_outlined), selectedIcon: Icon(Icons.inbox_rounded), label: '보관함'),
                      NavigationDestination(icon: Icon(Icons.tune_rounded), selectedIcon: Icon(Icons.tune_rounded), label: '설정'),
                    ],
                  ),
                ),
              ),
            ),
          ),
        );
      },
    );
  }
}

// ───────────────────────── 달력 ─────────────────────────
class Pressable extends StatefulWidget {
  final Widget child;
  final VoidCallback onTap;
  const Pressable({super.key, required this.child, required this.onTap});
  @override
  State<Pressable> createState() => _PressableState();
}

class _PressableState extends State<Pressable> {
  bool down = false;
  @override
  Widget build(BuildContext context) => GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTapDown: (_) => setState(() => down = true),
        onTapCancel: () => setState(() => down = false),
        onTapUp: (_) => setState(() => down = false),
        onTap: () {
          HapticFeedback.selectionClick();
          widget.onTap();
        },
        child: AnimatedScale(scale: down ? 0.94 : 1, duration: const Duration(milliseconds: 110), curve: kCurve, child: widget.child),
      );
}

class CalendarPage extends StatefulWidget {
  final AppStore store;
  final DateTime month, selected;
  final ValueChanged<DateTime> onMonth, onSelect;
  final VoidCallback? onAgenda;
  const CalendarPage(
      {super.key,
      required this.store,
      required this.month,
      required this.selected,
      required this.onMonth,
      required this.onSelect,
      this.onAgenda});
  @override
  State<CalendarPage> createState() => _CalendarPageState();
}

class _CalendarPageState extends State<CalendarPage> {
  int dir = 1; // 달이 바뀔 때 미끄러지는 방향

  AppStore get store => widget.store;
  DateTime get month => widget.month;
  DateTime get selected => widget.selected;

  @override
  void didUpdateWidget(CalendarPage old) {
    super.didUpdateWidget(old);
    if (old.month != widget.month) dir = widget.month.isAfter(old.month) ? 1 : -1;
  }

  void _go(int n) => widget.onMonth(DateTime(month.year, month.month + n));

  @override
  Widget build(BuildContext context) {
    final today = dayOnly(DateTime.now());
    final first = DateTime(month.year, month.month, 1);
    final gridStart = addDays(first, -(first.weekday % 7)); // 일요일 시작
    final key = ValueKey('${month.year}-${month.month}');
    Widget navBtn(IconData i, VoidCallback f) => Pressable(
        onTap: f,
        child: Container(
            width: 38,
            height: 38,
            decoration: BoxDecoration(color: Colors.white.withOpacity(0.06), shape: BoxShape.circle),
            child: Icon(i, size: 22, color: cText)));
    return Column(children: [
      Padding(
        padding: const EdgeInsets.fromLTRB(20, 2, 16, 8),
        child: Row(children: [
          Expanded(
              child: AnimatedSwitcher(
                  duration: kMid,
                  switchInCurve: kCurve,
                  transitionBuilder: (c, a) => FadeTransition(
                      opacity: a,
                      child: SlideTransition(
                          position: Tween(begin: Offset(0, 0.25 * dir), end: Offset.zero).animate(a), child: c)),
                  layoutBuilder: (cur, prev) => Stack(alignment: Alignment.centerLeft, children: [...prev, if (cur != null) cur]),
                  child: Text('${month.year}년 ${month.month}월',
                      key: key, style: const TextStyle(fontSize: 26, fontWeight: FontWeight.w800, letterSpacing: -0.5)))),
          Pressable(
              onTap: () {
                widget.onMonth(DateTime(today.year, today.month));
                widget.onSelect(today);
              },
              child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 9),
                  decoration: BoxDecoration(color: cAccent.withOpacity(0.18), borderRadius: BorderRadius.circular(20)),
                  child: const Text('오늘', style: TextStyle(color: cAccent, fontWeight: FontWeight.w700, fontSize: 13)))),
          const SizedBox(width: 8),
          navBtn(Icons.chevron_left_rounded, () => _go(-1)),
          const SizedBox(width: 6),
          navBtn(Icons.chevron_right_rounded, () => _go(1)),
        ]),
      ),
      Builder(builder: (context) {
        final (nToday, nLate, nSoon) = summaryCounts(store.data);
        Widget chip(String t, Color c) => Container(
            margin: const EdgeInsets.only(right: 6),
            padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 5),
            decoration: BoxDecoration(color: Colors.white.withOpacity(0.06), borderRadius: BorderRadius.circular(14)),
            child: Text(t, style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: c)));
        return Padding(
            padding: const EdgeInsets.fromLTRB(20, 0, 16, 8),
            child: GestureDetector(
                onTap: widget.onAgenda,
                child: Row(children: [
                  chip('오늘 $nToday', cText),
                  if (nLate > 0) chip('지연 $nLate', cSun),
                  chip('3일 내 $nSoon', nSoon > 0 ? cAccent : cDim),
                  const Spacer(),
                  const Icon(Icons.chevron_right_rounded, size: 18, color: cDim),
                ])));
      }),
      Padding(
        padding: const EdgeInsets.symmetric(horizontal: 8),
        child: Row(children: [
          for (var i = 0; i < 7; i++)
            Expanded(
                child: Center(
                    child: Text('일월화수목금토'[i],
                        style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w600, color: i == 0 ? cSun : (i == 6 ? cSat : cSub))))),
        ]),
      ),
      const SizedBox(height: 4),
      Expanded(
        child: GestureDetector(
          onHorizontalDragEnd: (d) {
            final v = d.primaryVelocity ?? 0;
            if (v < -200) _go(1);
            if (v > 200) _go(-1);
          },
          child: Padding(
            padding: const EdgeInsets.fromLTRB(6, 0, 6, 92),
            child: AnimatedSwitcher(
              duration: kMid,
              switchInCurve: kCurve,
              switchOutCurve: kCurve,
              transitionBuilder: (c, a) {
                final incoming = c.key == key;
                final begin = Offset(incoming ? 0.18 * dir : -0.18 * dir, 0);
                return FadeTransition(opacity: a, child: SlideTransition(position: Tween(begin: begin, end: Offset.zero).animate(a), child: c));
              },
              layoutBuilder: (cur, prev) => Stack(children: [...prev, if (cur != null) cur]),
              child: Column(key: key, children: [
                for (var w = 0; w < 6; w++)
                  Expanded(
                    child: Row(children: [
                      for (var c = 0; c < 7; c++) Expanded(child: _cell(context, addDays(gridStart, w * 7 + c), today)),
                    ]),
                  ),
              ]),
            ),
          ),
        ),
      ),
    ]);
  }

  Widget _cell(BuildContext context, DateTime d, DateTime today) {
    final k = store.calScale;
    final inMonth = d.month == month.month;
    final isToday = sameDay(d, today);
    final sel = sameDay(d, selected);
    final h = holidayName(d);
    final rows = dayItems(store.data, d);
    final wd = d.weekday % 7; // 0=일
    Color numColor = !inMonth ? cDim : (h != null || wd == 0) ? cSun : (wd == 6 ? cSat : cText);
    if (isToday) numColor = Colors.white;
    return Pressable(
      onTap: () {
        widget.onSelect(d);
        showDaySheet(context, store, d);
      },
      child: AnimatedContainer(
        duration: kFast,
        curve: kCurve,
        margin: const EdgeInsets.all(2),
        decoration: BoxDecoration(
          color: sel ? cAccent.withOpacity(0.16) : (inMonth ? Colors.white.withOpacity(0.045) : Colors.white.withOpacity(0.015)),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: sel ? cAccent.withOpacity(0.9) : Colors.transparent, width: 1.4),
        ),
        child: LayoutBuilder(builder: (context, c) {
          final slots = ((c.maxHeight - 28) / (12.5 * k)).floor().clamp(1, 8);
          final entries = <(String, Color, bool, bool)>[]; // 글자, 색, 완료, 공휴일
          if (h != null) entries.add((h, inMonth ? cSun : cDim, false, true));
          for (final r in rows) {
            final done = r.t['done'] == true;
            final late = !done && d.isBefore(today) && r.t['repeat'] == null && (itemEnd(r.t, r.start) ?? d).isBefore(today);
            entries.add((r.t['text'] as String? ?? '', done ? cDim : (late ? cSun : priColor(r.t)), done, false));
          }
          final over = entries.length > slots;
          final show = over ? slots - 1 : entries.length;
          return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            const SizedBox(height: 4),
            Center(
              child: Container(
                width: 22,
                height: 22,
                alignment: Alignment.center,
                decoration: isToday ? const BoxDecoration(color: cAccent, shape: BoxShape.circle) : null,
                child: Text('${d.day}', style: TextStyle(fontSize: 12 * k, fontWeight: FontWeight.w700, color: numColor)),
              ),
            ),
            const SizedBox(height: 3),
            for (var i = 0; i < show; i++)
              entries[i].$4
                  ? Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 4),
                      child: Text(entries[i].$1,
                          maxLines: 1, softWrap: false, overflow: TextOverflow.clip, style: TextStyle(fontSize: 9 * k, fontWeight: FontWeight.w600, color: entries[i].$2)))
                  : Padding(
                      padding: const EdgeInsets.fromLTRB(4, 0, 2, 2),
                      child: Row(children: [
                        Container(
                            width: 4,
                            height: 4,
                            margin: const EdgeInsets.only(right: 3),
                            decoration: BoxDecoration(color: entries[i].$2, shape: BoxShape.circle)),
                        Expanded(
                            child: Text(entries[i].$1,
                                maxLines: 1,
                                softWrap: false,
                                overflow: TextOverflow.clip,
                                style: TextStyle(
                                    fontSize: 9.5 * k,
                                    height: 1.1,
                                    color: entries[i].$3 ? cDim : cText.withOpacity(0.94),
                                    decoration: entries[i].$3 ? TextDecoration.lineThrough : null))),
                      ])),
            if (over)
              Padding(
                  padding: const EdgeInsets.only(left: 5),
                  child: Text('+${entries.length - show}', style: TextStyle(fontSize: 9.5 * k, fontWeight: FontWeight.w600, color: cSub))),
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
    sheetAnimationStyle: AnimationStyle(duration: const Duration(milliseconds: 380), reverseDuration: const Duration(milliseconds: 240), curve: kCurve),
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
    final q = parseQuick(text, dayOnly(DateTime.now()));
    final t = newItem(q.text, extra: {'pri': q.pri ?? pri});
    if (q.time != null) t['time'] = q.time;
    if (q.repeat != null) t['repeat'] = {'f': q.repeat, 'n': 1};
    final target = q.date ?? date;
    widget.store.add(iso(target), t);
    entry.clear();
    setState(() => pri = 2);
    if (q.date != null && !sameDay(q.date!, date)) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('${target.month}월 ${target.day}일에 추가했어요')));
    }
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
                      onChanged: (_) => setState(() {}),
                      style: const TextStyle(fontSize: 15),
                      decoration: const InputDecoration(
                        hintText: '예) 내일 오후 3시 회의 !중요',
                        hintStyle: TextStyle(fontSize: 14, color: cDim),
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  GestureDetector(
                    onTap: () => setState(() => pri = nextPri(pri)), // 중요/보통/낮음을 돌려가며 선택
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
              if (entry.text.trim().isNotEmpty)
                Builder(builder: (context) {
                  final q = parseQuick(entry.text.trim(), dayOnly(DateTime.now()));
                  final d = q.describe(dayOnly(DateTime.now()));
                  return Padding(
                      padding: const EdgeInsets.fromLTRB(20, 6, 20, 0),
                      child: Align(
                          alignment: Alignment.centerLeft,
                          child: Text('→ ${d.isEmpty ? '이 날짜에 추가' : d}  ·  ${q.text}',
                              maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 12.5, color: cAccent))));
                }),
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
                      decoration: const InputDecoration(hintText: '메모', hintStyle: TextStyle(color: cDim, fontSize: 14)),
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
      tags.add('반복 ${repeatLabel(rep, row.start)}');
    } else {
      final e = inbox ? null : itemEnd(t, row.start);
      if (e != null) tags.add('기간 ${row.start.month}/${row.start.day} ~ ${e.month}/${e.day}');
    }
    if ((t['time'] as String?)?.isNotEmpty == true && !inbox) tags.add(t['time'] as String);
    final note = (t['note'] as String?) ?? '';
    final key = ValueKey('${t['id']}-${t['_occ'] ?? ''}-${inbox ? 'i' : 'd'}');
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: kMid,
      curve: kCurve,
      builder: (context, v, child) => Opacity(opacity: v, child: Transform.translate(offset: Offset(0, 10 * (1 - v)), child: child)),
      child: Dismissible(
        key: key,
        direction: DismissDirection.endToStart,
        background: Container(
            margin: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
            decoration: BoxDecoration(color: const Color(0xFF7A2536), borderRadius: BorderRadius.circular(18)),
            alignment: Alignment.centerRight,
            padding: const EdgeInsets.only(right: 22),
            child: const Icon(Icons.delete_outline_rounded)),
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
        child: Container(
          margin: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
          decoration: BoxDecoration(
              color: cCard, borderRadius: BorderRadius.circular(18)),
          child: Material(
            color: Colors.transparent,
            borderRadius: BorderRadius.circular(18),
            child: InkWell(
              borderRadius: BorderRadius.circular(18),
              onTap: () => showItemEditor(context, store, item: t),
              child: Padding(
                padding: const EdgeInsets.fromLTRB(8, 8, 14, 8),
                child: Row(children: [
                  Pressable(
                    onTap: () => store.toggle(t),
                    child: Padding(
                      padding: const EdgeInsets.all(6),
                      child: AnimatedSwitcher(
                        duration: kFast,
                        switchInCurve: Curves.easeOutBack,
                        transitionBuilder: (c, a) => ScaleTransition(scale: a, child: FadeTransition(opacity: a, child: c)),
                        child: Icon(done ? Icons.check_circle_rounded : Icons.circle_outlined,
                            key: ValueKey(done), color: done ? cDim : col, size: 26),
                      ),
                    ),
                  ),
                  const SizedBox(width: 6),
                  Expanded(
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      AnimatedDefaultTextStyle(
                        duration: kMid,
                        style: TextStyle(
                            fontFamily: fontFamily,
                            fontSize: 15,
                            height: 1.35,
                            fontWeight: FontWeight.w500,
                            color: done ? cDim : cText,
                            decoration: done ? TextDecoration.lineThrough : null,
                            decorationColor: cDim),
                        child: Text(t['text'] as String? ?? ''),
                      ),
                      if (tags.isNotEmpty || note.isNotEmpty)
                        Padding(
                          padding: const EdgeInsets.only(top: 3),
                          child: Text([...tags, if (note.isNotEmpty) note.split('\n').first].join('  ·  '),
                              maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12.5, color: done ? cDim : cSub)),
                        ),
                    ]),
                  ),
                  if (inbox)
                    TextButton(
                        onPressed: () async {
                          final d = await showDatePicker(
                              context: context, initialDate: dayOnly(DateTime.now()), firstDate: DateTime(2020), lastDate: DateTime(2035));
                          if (d != null) store.moveTo(t, d);
                        },
                        child: const Text('꺼내기'))
                  else
                    Pressable(
                        onTap: () => store.cyclePri(t),
                        child: Padding(
                            padding: const EdgeInsets.all(8),
                            child: AnimatedContainer(
                                duration: kMid,
                                width: 11,
                                height: 11,
                                decoration: BoxDecoration(color: done ? cDim : col, shape: BoxShape.circle)))),
                ]),
              ),
            ),
          ),
        ),
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
    sheetAnimationStyle: AnimationStyle(duration: const Duration(milliseconds: 380), reverseDuration: const Duration(milliseconds: 240), curve: kCurve),
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
      rep = repeatCode(r);
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
      t['repeat'] = repeatFromCode(rep, start, until == null ? null : iso(until!));
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
            for (var i = 1; i < 4; i++)
              ChoiceChip(
                  selected: priOf({'pri': pri}) == i,
                  showCheckmark: false,
                  avatar: Icon(Icons.circle, size: 12, color: priColors[i]),
                  label: Text(priNames[i], style: chipStyle),
                  onSelected: (_) {
                    HapticFeedback.selectionClick();
                    setState(() => pri = i);
                  }),
          ]),
          const SizedBox(height: 6),
          const Text('반복', style: TextStyle(color: cSub, fontSize: 12)),
          Wrap(spacing: 8, children: [
            for (final r in repeatLabels)
              ChoiceChip(
                  selected: rep == r[0],
                  showCheckmark: false,
                  label: Text(r[1], style: chipStyle),
                  onSelected: (_) {
                    HapticFeedback.selectionClick();
                    setState(() => rep = r[0]);
                  }),
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
    Widget slider(String label, double v, double lo, double hi, void Function(double) on) => Row(children: [
          SizedBox(width: 104, child: Text(label, style: const TextStyle(fontSize: 15.5))),
          Expanded(child: Slider(value: v, min: lo, max: hi, divisions: ((hi - lo) / 0.05).round(), onChanged: on)),
          SizedBox(width: 48, child: Text('${(v * 100).round()}%', style: const TextStyle(fontSize: 14.5, color: cSub))),
        ]);
    return ListView(children: [
      card([
        const Text('화면 크기', style: TextStyle(fontSize: 19, fontWeight: FontWeight.w800)),
        slider('전체 글자', store.textScale, 0.8, 1.4, (v) => store.setScales(text: v)),
        slider('달력 칸 글자', store.calScale, 0.8, 1.6, (v) => store.setScales(cal: v)),
        Align(
            alignment: Alignment.centerRight,
            child: TextButton(onPressed: () => store.setScales(text: 1.0, cal: 1.0), child: const Text('기본값'))),
      ]),
      card([
        const Text('PC와 연결', style: TextStyle(fontSize: 19, fontWeight: FontWeight.w800)),
        const SizedBox(height: 8),
        if (cfg == null) ...[
          const Text('PC 프로그램에서 [폰 연결] → [QR 코드 만들기]를 누르고, 나온 QR 코드를 스캔하세요.\n집·사무실 밖에서도 인터넷만 되면 동기화돼요.',
              style: TextStyle(color: cSub, fontSize: 15, height: 1.45)),
          const SizedBox(height: 12),
          FilledButton.icon(onPressed: () => _scan(context), icon: const Icon(Icons.qr_code_scanner), label: const Text('QR 스캔해서 연결')),
          const SizedBox(height: 14),
          PasteConnect(store: store),
        ] else ...[
          const Text('PC와 연결됨 ✓', style: TextStyle(color: cAccent, fontWeight: FontWeight.w700)),
          const SizedBox(height: 4),
          Text(store.syncStatus.isEmpty ? '대기 중' : store.syncStatus, style: const TextStyle(color: cSub, fontSize: 15, height: 1.45)),
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
      if (!kIsWeb) // 웹앱(아이폰)에서는 폰 알림 예약을 쓰지 않는다
      card([
        const Text('알림', style: TextStyle(fontSize: 19, fontWeight: FontWeight.w800)),
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
          child: Text('일정은 암호화되어 인터넷을 거쳐 PC와 주고받아요.\n암호 키는 이 기기와 PC에만 있어요.\n\nDeskCalendar $appVersion',
              style: TextStyle(color: cDim, fontSize: 14, height: 1.4))),
    ]);
  }
}

/// QR 대신 PC 의 [연결 코드 복사] 로 복사한 코드를 붙여 넣어 연결한다 (아이폰 웹앱 등)
class PasteConnect extends StatefulWidget {
  final AppStore store;
  const PasteConnect({super.key, required this.store});
  @override
  State<PasteConnect> createState() => _PasteConnectState();
}

class _PasteConnectState extends State<PasteConnect> {
  final c = TextEditingController();
  String msg = '';

  Future<void> _go() async {
    final cfg = parsePairing(c.text);
    if (cfg == null) {
      setState(() => msg = '올바른 연결 코드가 아니에요. PC 의 [휴대폰 연결]에서 [연결 코드 복사]를 눌러 복사해 주세요.');
      return;
    }
    await widget.store.connect(cfg);
    if (mounted) setState(() => msg = '');
  }

  @override
  Widget build(BuildContext context) {
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      const Text('QR 이 어렵다면 연결 코드로', style: TextStyle(fontSize: 12.5, color: cSub)),
      const SizedBox(height: 6),
      Row(children: [
        Expanded(child: TextField(controller: c, style: const TextStyle(fontSize: 13), decoration: const InputDecoration(hintText: 'PC 에서 복사한 연결 코드 붙여넣기', isDense: true))),
        const SizedBox(width: 8),
        FilledButton.tonal(onPressed: _go, child: const Text('연결')),
      ]),
      if (msg.isNotEmpty) Padding(padding: const EdgeInsets.only(top: 6), child: Text(msg, style: const TextStyle(fontSize: 12, color: cSun))),
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
              if (j['key'] != null && ((j['relay'] != null && j['id'] != null) || (j['repo'] != null && j['tok'] != null))) {
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
