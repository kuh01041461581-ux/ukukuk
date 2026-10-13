import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:desk_calendar_mobile/candidates.dart';
import 'package:desk_calendar_mobile/core.dart';
import 'package:desk_calendar_mobile/store.dart';

void main() {
  testWidgets('붙여넣은 글 → 후보 시트 → 체크한 것만 추가', (tester) async {
    SharedPreferences.setMockInitialValues({});
    final store = AppStore();
    await store.load();
    await tester.binding.setSurfaceSize(const Size(393, 852));
    const text = '[김과장] [오후 2:31] 10/27(화)까지 자료 제출 부탁드립니다\n회의록 작성 부탁드려요';
    await tester.pumpWidget(MaterialApp(
        home: Scaffold(
            body: Builder(
                builder: (c) => TextButton(onPressed: () => showCandidates(c, store, text), child: const Text('열기'))))));
    await tester.tap(find.text('열기'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 600));
    expect(find.text('2개 추가'), findsOneWidget);
    expect(find.text('보관함 · 할 일'), findsOneWidget);
    await tester.tap(find.byType(Checkbox).last); // 보관함 항목 해제
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('1개 추가'), findsOneWidget);
    await tester.tap(find.text('1개 추가'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 600));
    final y = dayOnly(DateTime.now()).year;
    expect(store.data.keys.any((k) => k.startsWith('$y-') || k.startsWith('${y + 1}-')), true);
    expect((store.data['_inbox'] as Map?)?['todos'] ?? [], isEmpty);
    expect(tester.takeException(), isNull);
  });
}
