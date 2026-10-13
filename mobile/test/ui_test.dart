import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:desk_calendar_mobile/core.dart';
import 'package:desk_calendar_mobile/main.dart';
import 'package:desk_calendar_mobile/store.dart';

void main() {
  testWidgets('달력 → 날짜 누르면 시트 → 일정 추가 → 탭/월 이동이 오류 없이 동작', (tester) async {
    SharedPreferences.setMockInitialValues({});
    final store = AppStore();
    await store.load();
    final today = dayOnly(DateTime.now());
    store.add(iso(today), newItem('통합전수관 당직 근무 길게 길게 쓰는 일정'));
    store.add(iso(today), newItem('두번째', extra: {'pri': 0}));
    await tester.binding.setSurfaceSize(const Size(393, 852));
    await tester.pumpWidget(DeskApp(store));
    await tester.pump(const Duration(milliseconds: 600));
    expect(find.text('${today.day}'), findsWidgets);

    await tester.tap(find.text('${today.day}').last);
    await tester.pump(const Duration(milliseconds: 600));
    expect(find.text('새 일정 입력 (예: 14:30 회의)'), findsOneWidget);
    await tester.enterText(find.byType(TextField).first, '14:30 회의');
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pump(const Duration(milliseconds: 600));
    expect(find.text('회의'), findsWidgets);
    await tester.tapAt(const Offset(200, 40)); // 시트 밖을 눌러 닫기
    await tester.pump(const Duration(milliseconds: 600));

    await tester.tap(find.byIcon(Icons.chevron_right_rounded));
    await tester.pump(const Duration(milliseconds: 150));
    await tester.pump(const Duration(milliseconds: 600));
    await tester.tap(find.text('예정'));
    await tester.pump(const Duration(milliseconds: 600));
    await tester.tap(find.text('보관함'));
    await tester.pump(const Duration(milliseconds: 600));
    await tester.tap(find.text('설정'));
    await tester.pump(const Duration(milliseconds: 600));
    expect(find.text('화면 크기'), findsOneWidget);
    await tester.drag(find.byType(Slider).first, const Offset(60, 0));
    await tester.pump(const Duration(milliseconds: 400));
    expect(tester.takeException(), isNull);
  });
}
