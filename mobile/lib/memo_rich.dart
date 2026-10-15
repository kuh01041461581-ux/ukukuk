// 메모 서식: PC 스티커와 같은 규칙.
// 저장은 '그냥 글자'(**굵게** ~~취소선~~ {빨강|글} / 1. 번호 / - 글머리 / - [ ] 체크)라서 PC·폰·검색·동기화가 모두 같은 글을 본다.
// 화면에서는 표시 글자를 감추고 진짜 서식으로 보여 준다: 굵게는 그냥 굵게, 체크는 ☐/☑(누르면 바뀜), 글머리는 •.
import 'package:flutter/material.dart';

const memoColorNames = ['빨강', '주황', '초록', '파랑', '보라'];
// 어두운 바탕에서 잘 읽히는 색
const memoColors = [Color(0xFFFF8F9B), Color(0xFFFFB86B), Color(0xFF82D9A8), Color(0xFF8FB4F2), Color(0xFFC3A6FF)];
const _doneColor = Color(0xFF7D869C);

const _bold = 1, _strike = 2; // 색은 4 · 8 · 16 자리에 (0 = 없음, 1~5 = memoColorNames 순서)

final _pat = RegExp(r'\*\*(.+?)\*\*|~~(.+?)~~|\{(빨강|주황|초록|파랑|보라)\|(.+?)\}');
final _rxCheck = RegExp(r'^(\s*)- \[( |x|X)\] ?(.*)$');
final _rxBul = RegExp(r'^(\s*)- (.*)$');
final _dPre = RegExp(r'^(\s*)(☐ |☑ |• |\d+\. )'); // 화면에 보이는 줄 머리
final _rxAuto = RegExp(r'^(\s*)(- \[ \] |- \[[xX]\] |\[\] |\[ \] |\[[xX]\] |- |\* )$'); // 줄 머리를 손으로 치면 바꿔 준다
final _rxBareHead = RegExp(r'^(\s*)([☐☑•]|\d+\.)$'); // 줄 머리에서 빈칸만 지운 모양

/// 달력·검색에서 한 줄로 보여 줄 때: 서식 표시 글자를 빼고 글자만 (체크는 ☐/☑, 글머리는 •)
String stripMemoMarkup(String text) {
  final out = <String>[];
  for (var line in text.split('\n')) {
    final c = _rxCheck.firstMatch(line);
    if (c != null) {
      line = '${c.group(1)}${'xX'.contains(c.group(2)!) ? '☑ ' : '☐ '}${c.group(3)}';
    } else {
      final b = _rxBul.firstMatch(line);
      if (b != null) line = '${b.group(1)}• ${b.group(2)}';
    }
    String prev;
    do {
      prev = line;
      line = line.replaceAllMapped(_pat, (m) => m.group(1) ?? m.group(2) ?? m.group(4)!);
    } while (prev != line);
    out.add(line);
  }
  return out.join('\n');
}

class _Diff {
  final int p, oldEnd, newEnd;
  const _Diff(this.p, this.oldEnd, this.newEnd);
  int get removed => oldEnd - p;
  int get inserted => newEnd - p;
}

int _lineStart(String t, int i) => i <= 0 ? 0 : t.lastIndexOf('\n', i - 1) + 1;
int _lineEnd(String t, int i) {
  final e = t.indexOf('\n', i);
  return e < 0 ? t.length : e;
}

/// 글자마다 서식(굵게·취소선·색)을 따로 들고 있는 메모 입력 컨트롤러. text 는 '화면에 보이는 글자'이고,
/// markup 으로 저장 글자를 꺼내고 setMarkup 으로 저장 글자를 넣는다.
class RichMemoController extends TextEditingController {
  List<int> _fmt = [];
  int? _pending; // 글자를 고르지 않고 눌러 둔 서식: 이어서 칠 글자에 적용

  RichMemoController([String markup = '']) {
    setMarkup(markup);
  }

  // ── 저장 글자 ↔ 화면 ──
  void setMarkup(String s) {
    final out = StringBuffer();
    final f = <int>[];
    void put(String x, int fm) {
      out.write(x);
      f.addAll(List.filled(x.length, fm));
    }

    void emit(String text, int fm) {
      var pos = 0;
      for (final m in _pat.allMatches(text)) {
        if (m.start > pos) put(text.substring(pos, m.start), fm);
        if (m.group(1) != null) {
          emit(m.group(1)!, fm | _bold);
        } else if (m.group(2) != null) {
          emit(m.group(2)!, fm | _strike);
        } else {
          emit(m.group(4)!, (fm & 3) | ((memoColorNames.indexOf(m.group(3)!) + 1) << 2));
        }
        pos = m.end;
      }
      if (pos < text.length) put(text.substring(pos), fm);
    }

    final lines = s.split('\n');
    for (var i = 0; i < lines.length; i++) {
      if (i > 0) put('\n', 0);
      var line = lines[i];
      final c = _rxCheck.firstMatch(line);
      if (c != null) {
        put('${c.group(1)}${'xX'.contains(c.group(2)!) ? '☑ ' : '☐ '}', 0);
        line = c.group(3)!;
      } else {
        final b = _rxBul.firstMatch(line);
        if (b != null) {
          put('${b.group(1)}• ', 0);
          line = b.group(2)!;
        }
      }
      emit(line, 0);
    }
    _fmt = f;
    _pending = null;
    super.value = TextEditingValue(text: out.toString(), selection: TextSelection.collapsed(offset: out.length));
  }

  String get markup {
    final t = text;
    final f = _fmt.length == t.length ? _fmt : List.filled(t.length, 0);
    final lines = <String>[];
    var ls = 0;
    while (true) {
      final nl = t.indexOf('\n', ls);
      final le = nl < 0 ? t.length : nl;
      var head = '';
      var a = ls;
      final m = _dPre.firstMatch(t.substring(ls, le));
      if (m != null) {
        final g = m.group(2)!;
        head = m.group(1)! + (g == '☐ ' ? '- [ ] ' : g == '☑ ' ? '- [x] ' : g == '• ' ? '- ' : g);
        a = ls + m.end;
      }
      final sb = StringBuffer(head);
      var i = a;
      while (i < le) {
        var j = i + 1;
        while (j < le && f[j] == f[i]) {
          j++;
        }
        sb.write(_wrap(t.substring(i, j), f[i]));
        i = j;
      }
      lines.add(sb.toString());
      if (nl < 0) break;
      ls = nl + 1;
    }
    return lines.join('\n');
  }

  static String _wrap(String s, int fm) {
    var r = s;
    if (fm & _strike != 0) r = '~~$r~~';
    if (fm & _bold != 0) r = '**$r**';
    final ci = (fm >> 2) & 7;
    if (ci > 0) r = '{${memoColorNames[ci - 1]}|$r}';
    return r;
  }

  // ── 입력이 들어올 때: 서식 배열을 글자에 맞추고, 목록 이어쓰기·자동 변환 ──
  @override
  set value(TextEditingValue v) {
    final old = text;
    if (v.text != old) {
      if (_fmt.length != old.length) _fmt = List.filled(old.length, 0);
      final d = _diff(old, v.text);
      _adjust(old, d);
      v = _smart(old, v, d);
      _pending = null;
    } else if (v.selection.baseOffset != value.selection.baseOffset) {
      _pending = null; // 커서를 옮기면 미리 눌러 둔 서식은 취소
    }
    super.value = v;
  }

  static _Diff _diff(String a, String b) {
    final m = a.length < b.length ? a.length : b.length;
    var p = 0;
    while (p < m && a.codeUnitAt(p) == b.codeUnitAt(p)) {
      p++;
    }
    var s = 0;
    while (s < m - p && a.codeUnitAt(a.length - 1 - s) == b.codeUnitAt(b.length - 1 - s)) {
      s++;
    }
    return _Diff(p, a.length - s, b.length - s);
  }

  void _adjust(String old, _Diff d) {
    final ins = d.inserted;
    int fm;
    if (ins == 0) {
      fm = 0;
    } else if (d.removed > 0) {
      fm = _fmt[d.p]; // 바꿔 쓰는 글자(한글 조합 등)는 원래 글자의 서식을 이어받는다
    } else if (_pending != null) {
      fm = _pending!;
    } else {
      fm = d.p > 0 && old[d.p - 1] != '\n' ? _fmt[d.p - 1] : 0; // 앞 글자의 서식을 이어서
    }
    _fmt = [..._fmt.sublist(0, d.p), ...List.filled(ins, fm), ..._fmt.sublist(d.oldEnd)];
  }

  TextEditingValue _smart(String old, TextEditingValue v, _Diff d) {
    final t = v.text;
    final sel = v.selection;
    if (!sel.isValid || !sel.isCollapsed) return v;
    // Enter: 번호는 다음 번호로, 체크·글머리는 같은 모양으로 이어 쓰고, 빈 목록 줄에서 Enter 면 목록 끝내기
    if (d.removed == 0 && d.inserted == 1 && t[d.p] == '\n' && sel.baseOffset == d.p + 1) {
      final ls = _lineStart(t, d.p);
      final m = _dPre.firstMatch(t.substring(ls, d.p));
      if (m != null && ls + m.end <= d.p) {
        final after = t.substring(d.p + 1);
        final restEmpty = after.isEmpty || after.startsWith('\n');
        if (restEmpty && ls + m.end == d.p) {
          _fmt = [..._fmt.sublist(0, ls + m.group(1)!.length), ..._fmt.sublist(d.p + 1)];
          final nt = t.substring(0, ls + m.group(1)!.length) + t.substring(d.p + 1);
          return TextEditingValue(text: nt, selection: TextSelection.collapsed(offset: ls + m.group(1)!.length));
        }
        final g = m.group(2)!;
        final nxt = (g == '☐ ' || g == '☑ ') ? '☐ ' : g == '• ' ? '• ' : '${int.parse(g.substring(0, g.length - 2)) + 1}. ';
        final pre = m.group(1)! + nxt;
        _fmt = [..._fmt.sublist(0, d.p + 1), ...List.filled(pre.length, 0), ..._fmt.sublist(d.p + 1)];
        return TextEditingValue(
            text: t.substring(0, d.p + 1) + pre + t.substring(d.p + 1),
            selection: TextSelection.collapsed(offset: d.p + 1 + pre.length));
      }
    }
    // 손으로 친 줄 머리('- ', '[] ')를 • / ☐ 로
    if (d.removed == 0 && d.inserted == 1 && t[d.p] != '\n' && sel.baseOffset == d.p + 1) {
      final ls = _lineStart(t, d.p + 1);
      final m = _rxAuto.firstMatch(t.substring(ls, d.p + 1));
      if (m != null) {
        final g = m.group(2)!;
        final glyph = g.contains('[') ? (g.toLowerCase().contains('x') ? '☑ ' : '☐ ') : '• ';
        final a = ls + m.group(1)!.length;
        _fmt = [..._fmt.sublist(0, a), ...List.filled(glyph.length, 0), ..._fmt.sublist(d.p + 1)];
        return TextEditingValue(
            text: t.substring(0, a) + glyph + t.substring(d.p + 1), selection: TextSelection.collapsed(offset: a + glyph.length));
      }
    }
    // 줄 머리 바로 뒤에서 지우기: 줄 머리를 통째로
    if (d.removed == 1 && d.inserted == 0 && old[d.p] == ' ' && sel.baseOffset == d.p) {
      final ls = _lineStart(t, d.p);
      final m = _rxBareHead.firstMatch(t.substring(ls, d.p));
      if (m != null) {
        final a = ls + m.group(1)!.length;
        _fmt = [..._fmt.sublist(0, a), ..._fmt.sublist(d.p)];
        return TextEditingValue(text: t.substring(0, a) + t.substring(d.p), selection: TextSelection.collapsed(offset: a));
      }
    }
    return v;
  }

  // ── 서식 버튼 ──
  bool get _hasSel => selection.isValid && !selection.isCollapsed;

  int _baseAtCaret() {
    final i = selection.isValid ? selection.baseOffset : text.length;
    return i > 0 && i <= _fmt.length && text[i - 1] != '\n' ? _fmt[i - 1] : 0;
  }

  void _guard() {
    if (_fmt.length != text.length) _fmt = List.filled(text.length, 0);
  }

  /// 굵게(1) · 취소선(2): 고른 글자에 켜고 끄기, 고른 글자가 없으면 이어서 칠 글자에
  void toggleFlag(int flag) {
    _guard();
    if (_hasSel) {
      final a = selection.start, b = selection.end;
      var all = true;
      for (var i = a; i < b; i++) {
        if (text[i] != '\n' && _fmt[i] & flag == 0) {
          all = false;
          break;
        }
      }
      for (var i = a; i < b; i++) {
        _fmt[i] = all ? _fmt[i] & ~flag : _fmt[i] | flag;
      }
      notifyListeners();
    } else {
      _pending = (_pending ?? _baseAtCaret()) ^ flag;
    }
  }

  void toggleBold() => toggleFlag(_bold);
  void toggleStrike() => toggleFlag(_strike);

  /// 글자색 (name 이 null 이면 색 지우기)
  void setColor(String? name) {
    _guard();
    final idx = name == null ? 0 : memoColorNames.indexOf(name) + 1;
    if (_hasSel) {
      for (var i = selection.start; i < selection.end; i++) {
        _fmt[i] = (_fmt[i] & 3) | (idx << 2);
      }
      notifyListeners();
    } else {
      _pending = ((_pending ?? _baseAtCaret()) & 3) | (idx << 2);
    }
  }

  /// 줄 머리: 'check' ☐ · 'num' 1. 2. · 'bul' • (고른 줄 전체, 이미 같은 종류면 뺀다)
  void prefix(String kind) {
    _guard();
    final t = text;
    final s = selection.isValid ? selection : TextSelection.collapsed(offset: t.length);
    final a = _lineStart(t, s.start);
    var endAt = s.end;
    if (!s.isCollapsed && endAt > s.start && endAt > 0 && t[endAt - 1] == '\n') endAt -= 1;
    final b = _lineEnd(t, endAt);
    final lines = t.substring(a, b).split('\n');
    String? kindOf(String line) {
      final m = _dPre.firstMatch(line);
      if (m == null) return null;
      final g = m.group(2)!;
      return (g == '☐ ' || g == '☑ ') ? 'check' : g == '• ' ? 'bul' : 'num';
    }

    final allSame = lines.where((x) => x.trim().isNotEmpty).every((x) => kindOf(x) == kind);
    final sb = StringBuffer();
    final f = <int>[];
    var pos = a, n = 0;
    for (var i = 0; i < lines.length; i++) {
      final line = lines[i];
      final m = _dPre.firstMatch(line);
      final ind = m != null ? m.group(1)!.length : RegExp(r'^\s*').firstMatch(line)!.end;
      final cut = m != null ? m.end : ind; // 지울 줄 머리의 끝
      sb.write(line.substring(0, ind));
      f.addAll(_fmt.sublist(pos, pos + ind));
      if (!(allSame && line.trim().isNotEmpty)) {
        n++;
        final pre = kind == 'num' ? '$n. ' : kind == 'check' ? '☐ ' : '• ';
        sb.write(pre);
        f.addAll(List.filled(pre.length, 0));
      }
      sb.write(line.substring(cut));
      f.addAll(_fmt.sublist(pos + cut, pos + line.length));
      pos += line.length + 1;
      if (i < lines.length - 1) {
        sb.write('\n');
        f.add(0);
      }
    }
    final nt = t.substring(0, a) + sb.toString() + t.substring(b);
    _fmt = [..._fmt.sublist(0, a), ...f, ..._fmt.sublist(b)];
    _pending = null;
    super.value = TextEditingValue(text: nt, selection: TextSelection.collapsed(offset: a + sb.length));
  }

  /// 체크 칸(☐/☑)을 눌렀으면 바꾸고 true
  bool toggleCheckAt(int offset) {
    final t = text;
    if (offset < 0 || offset > t.length) return false;
    final ls = _lineStart(t, offset);
    final m = _dPre.firstMatch(t.substring(ls, _lineEnd(t, ls)));
    if (m == null || (m.group(2) != '☐ ' && m.group(2) != '☑ ')) return false;
    final g = ls + m.group(1)!.length;
    if (offset < g || offset > g + 1) return false;
    final nt = t.replaceRange(g, g + 1, t[g] == '☑' ? '☐' : '☑');
    super.value = value.copyWith(text: nt);
    return true;
  }

  // ── 그리기 ──
  @override
  TextSpan buildTextSpan({required BuildContext context, TextStyle? style, required bool withComposing}) {
    final t = text;
    if (_fmt.length != t.length) _fmt = List.filled(t.length, 0);
    if (t.isEmpty) return TextSpan(style: style, text: '');
    final keys = List<int>.from(_fmt);
    // 체크한 줄(☑)은 취소선·흐리게
    var ls = 0;
    while (true) {
      final le = _lineEnd(t, ls);
      final m = _dPre.firstMatch(t.substring(ls, le));
      if (m != null && m.group(2) == '☑ ') {
        for (var i = ls + m.end; i < le; i++) {
          keys[i] |= 64;
        }
      }
      if (le >= t.length) break;
      ls = le + 1;
    }
    final comp = value.composing;
    if (withComposing && comp.isValid && !comp.isCollapsed) {
      for (var i = comp.start; i < comp.end && i < keys.length; i++) {
        keys[i] |= 128;
      }
    }
    final kids = <InlineSpan>[];
    var i = 0;
    while (i < t.length) {
      var j = i + 1;
      while (j < t.length && keys[j] == keys[i]) {
        j++;
      }
      kids.add(TextSpan(text: t.substring(i, j), style: _styleOf(keys[i])));
      i = j;
    }
    return TextSpan(style: style, children: kids);
  }

  static TextStyle _styleOf(int k) {
    final deco = <TextDecoration>[];
    if (k & _strike != 0 || k & 64 != 0) deco.add(TextDecoration.lineThrough);
    if (k & 128 != 0) deco.add(TextDecoration.underline);
    final ci = (k >> 2) & 7;
    return TextStyle(
      fontWeight: k & _bold != 0 ? FontWeight.w700 : null,
      color: k & 64 != 0 ? _doneColor : (ci > 0 && ci <= memoColors.length ? memoColors[ci - 1] : null),
      decoration: deco.isEmpty ? null : TextDecoration.combine(deco),
    );
  }
}

/// 서식 버튼 줄 + 입력 칸. 컨트롤러는 RichMemoController.
class MemoEditor extends StatelessWidget {
  final RichMemoController controller;
  final int minLines, maxLines;
  final bool autofocus;
  final double fontSize;
  final String hint;
  final VoidCallback? onChanged;
  const MemoEditor(
      {super.key,
      required this.controller,
      this.minLines = 3,
      this.maxLines = 6,
      this.autofocus = false,
      this.fontSize = 14,
      this.hint = '메모',
      this.onChanged});

  Widget _btn(BuildContext context, String label, VoidCallback onTap, {FontWeight? weight, TextDecoration? deco}) {
    return Padding(
      padding: const EdgeInsets.only(right: 6),
      child: Material(
        color: Colors.white.withOpacity(0.08),
        borderRadius: BorderRadius.circular(16),
        child: InkWell(
          borderRadius: BorderRadius.circular(16),
          onTap: () {
            onTap();
            onChanged?.call();
          },
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 7),
            child: Text(label, style: TextStyle(fontSize: 14, fontWeight: weight, decoration: deco)),
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisSize: MainAxisSize.min, children: [
      SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(children: [
          _btn(context, 'B', controller.toggleBold, weight: FontWeight.w800),
          _btn(context, '취소', controller.toggleStrike, deco: TextDecoration.lineThrough),
          PopupMenuButton<String>(
            tooltip: '글자색',
            onSelected: (v) {
              controller.setColor(v.isEmpty ? null : v); // 빈 값 = 색 지우기
              onChanged?.call();
            },
            itemBuilder: (_) => [
              for (var i = 0; i < memoColorNames.length; i++)
                PopupMenuItem(value: memoColorNames[i], child: Text('● ${memoColorNames[i]}', style: TextStyle(color: memoColors[i]))),
              const PopupMenuItem(value: '', child: Text('색 지우기')),
            ],
            child: Padding(
              padding: const EdgeInsets.only(right: 6),
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 7),
                decoration: BoxDecoration(color: Colors.white.withOpacity(0.08), borderRadius: BorderRadius.circular(16)),
                child: const Text('색', style: TextStyle(fontSize: 14)),
              ),
            ),
          ),
          _btn(context, '☐', () => controller.prefix('check')),
          _btn(context, '1.', () => controller.prefix('num')),
          _btn(context, '•', () => controller.prefix('bul')),
        ]),
      ),
      TextField(
        controller: controller,
        minLines: minLines,
        maxLines: maxLines,
        autofocus: autofocus,
        keyboardType: TextInputType.multiline,
        onChanged: (_) => onChanged?.call(),
        onTap: () {
          if (controller.selection.isValid && controller.selection.isCollapsed && controller.toggleCheckAt(controller.selection.baseOffset)) {
            onChanged?.call();
          }
        },
        style: TextStyle(fontSize: fontSize),
        decoration: InputDecoration(hintText: hint, hintStyle: TextStyle(color: Colors.white38, fontSize: fontSize)),
      ),
    ]);
  }
}
