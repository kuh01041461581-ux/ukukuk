import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:desk_calendar_mobile/memo_rich.dart';

TextEditingValue v(String t, [int? caret]) => TextEditingValue(text: t, selection: TextSelection.collapsed(offset: caret ?? t.length));

void main() {
  test('저장 글자 ↔ 화면 글자: 표시 글자를 감추고 그대로 되돌린다', () {
    const text = '**굵게** ~~취소~~ {빨강|색} {파랑|**색+굵게**}\n1. 가\n- [x] 나\n- [ ] 다\n- 글머리\n그냥 글';
    final c = RichMemoController(text);
    expect(c.markup, text);
    expect(c.text, '굵게 취소 색 색+굵게\n1. 가\n☑ 나\n☐ 다\n• 글머리\n그냥 글');
  });

  test('달력·검색용: 표시 글자 빼기', () {
    expect(stripMemoMarkup('**a** {빨강|b}\n- [x] c\n- d'), 'a b\n☑ c\n• d');
  });

  test('B 를 누르고 치면 이어서 치는 글자가 굵게, 다시 누르면 보통', () {
    final c = RichMemoController('가');
    c.value = c.value.copyWith(selection: const TextSelection.collapsed(offset: 1));
    c.toggleBold();
    c.value = v('가나다');
    expect(c.markup, '가**나다**');
    c.toggleBold();
    c.value = v('가나다라');
    expect(c.markup, '가**나다**라');
  });

  test('글자를 골라 굵게·취소선·색을 켜고 끈다', () {
    final c = RichMemoController('중요');
    c.value = c.value.copyWith(selection: const TextSelection(baseOffset: 0, extentOffset: 2));
    c.toggleBold();
    expect(c.markup, '**중요**');
    c.toggleBold();
    expect(c.markup, '중요');
    c.setColor('빨강');
    expect(c.markup, '{빨강|중요}');
    c.setColor(null);
    expect(c.markup, '중요');
  });

  test('한글 조합처럼 마지막 글자를 바꿔 써도 서식이 유지된다', () {
    final c = RichMemoController('');
    c.toggleBold();
    c.value = v('ㅎ');
    c.value = v('하');
    c.value = v('한');
    expect(c.markup, '**한**');
  });

  test('Enter: 번호·체크·글머리를 이어 쓰고 빈 줄에서 끝낸다', () {
    final c = RichMemoController('1. 가\n2. 나');
    c.value = v('1. 가\n2. 나\n');
    expect(c.text, '1. 가\n2. 나\n3. ');
    c.value = v('1. 가\n2. 나\n3. \n');
    expect(c.text, '1. 가\n2. 나\n');

    final k = RichMemoController('- [x] 가');
    k.value = v('☑ 가\n');
    expect(k.text, '☑ 가\n☐ ');
    expect(k.markup, '- [x] 가\n- [ ] ');

    final b = RichMemoController('- 가');
    b.value = v('• 가\n');
    expect(b.text, '• 가\n• ');
  });

  test('손으로 친 줄 머리가 목록으로 바뀐다', () {
    final c = RichMemoController('');
    c.value = v('-'); // 키보드에서는 한 글자씩 들어온다
    c.value = v('- ');
    expect(c.text, '• ');
    final d = RichMemoController('');
    d.value = v('[]');
    d.value = v('[] ');
    expect(d.text, '☐ ');
  });

  test('줄 머리 바로 뒤에서 지우면 줄 머리가 통째로 사라진다', () {
    final c = RichMemoController('- [ ] ');
    expect(c.text, '☐ ');
    c.value = v('☐', 1);
    expect(c.text, '');
  });

  test('목록 버튼: 여러 줄에 붙이고 한 번 더 누르면 뺀다', () {
    final c = RichMemoController('가\n나\n다');
    c.value = c.value.copyWith(selection: const TextSelection(baseOffset: 0, extentOffset: 5));
    c.prefix('num');
    expect(c.text, '1. 가\n2. 나\n3. 다');
    c.value = c.value.copyWith(selection: const TextSelection(baseOffset: 0, extentOffset: 14));
    c.prefix('num');
    expect(c.text, '가\n나\n다');
    c.value = c.value.copyWith(selection: const TextSelection(baseOffset: 0, extentOffset: 3)); // 앞의 두 줄만
    c.prefix('check');
    expect(c.markup, '- [ ] 가\n- [ ] 나\n다');
  });

  test('체크 칸을 누르면 ☐ ↔ ☑', () {
    final c = RichMemoController('- [ ] 가\n- [x] 나');
    expect(c.toggleCheckAt(0), true);
    expect(c.markup.split('\n').first, '- [x] 가');
    expect(c.toggleCheckAt(2), false); // 글자 쪽을 누른 건 무시
    expect(c.toggleCheckAt(c.text.indexOf('나') - 1), true); // 두 번째 줄의 칸 뒤쪽 (공백)
    expect(c.markup.split('\n').last, '- [ ] 나');
  });

  testWidgets('메모 칸: 서식 버튼이 보이고, 굵게·체크가 화면에서 동작한다', (tester) async {
    final c = RichMemoController('');
    var changed = 0;
    await tester.pumpWidget(MaterialApp(
        home: Scaffold(body: SingleChildScrollView(child: MemoEditor(controller: c, onChanged: () => changed++)))));
    expect(find.text('B'), findsOneWidget);
    expect(find.text('취소'), findsOneWidget);
    await tester.tap(find.text('☐'));
    await tester.pump();
    expect(c.text, '☐ '); // 빈 칸에서 ☐ 버튼 → 줄 머리
    await tester.enterText(find.byType(TextField), '☐ 할 일');
    await tester.pump();
    expect(c.markup, '- [ ] 할 일');
    expect(find.byType(TextField), findsOneWidget);
    // 글자 모양: 체크한 줄은 흐리게 취소선
    c.setMarkup('- [x] 끝\n**굵게**');
    await tester.pump();
    final editable = tester.widget<EditableText>(find.byType(EditableText));
    final span = c.buildTextSpan(context: tester.element(find.byType(TextField)), style: editable.style, withComposing: false);
    expect(span.toPlainText(), '☑ 끝\n굵게');
    expect(changed, greaterThan(0));
  });
}
