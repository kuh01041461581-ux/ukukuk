"""스티커 메모 서식 편집 시험 (Tk 가 있는 PC 에서만: 화면이 없으면 건너뜀)
화면에는 진짜 서식(굵게·☐/☑·•)으로 보이고, 저장되는 글자는 **굵게** / - [ ] / - 표시 글자로 오간다.
실행: python tests/test_sticky.py"""
import os
import sys
import tkinter as tk

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import sticky  # noqa: E402


class _App:
    pass


def make(markup):
    root = tk.Tk()
    root.withdraw()
    t = tk.Text(root)
    changes = []
    f = sticky.MemoFormat(t, _App(), lambda: changes.append(1))
    f.set_markup(markup)
    return root, t, f, changes


def shown(t):
    return t.get("1.0", "end-1c")


def press_return(t, f, at):
    t.mark_set("insert", at)
    return f._return(None)


class _Click:
    pass


def test_roundtrip_keeps_stored_text():
    text = "**굵게** ~~취소~~ {빨강|색} {파랑|**색+굵게**}\n1. 가\n- [x] 나\n- [ ] 다\n- 글머리\n그냥 글"
    root, t, f, _ = make(text)
    assert f.get_markup() == text, f.get_markup()
    root.destroy()


def test_markers_are_hidden_and_real_formatting_shown():
    root, t, f, _ = make("**굵게** {빨강|색}\n- [x] 끝\n- [ ] 할일\n- 점")
    assert shown(t).split("\n") == ["굵게 색", "☑ 끝", "☐ 할일", "• 점"], shown(t).split("\n")
    assert "**" not in shown(t) and "{" not in shown(t)
    assert t.tag_ranges("bold") and t.tag_ranges("c_빨강")
    assert t.tag_ranges("done")                              # 체크한 줄은 취소선·흐리게
    root.destroy()


def test_numbering():
    root, t, f, _ = make("1. 가\n2. 나")
    assert press_return(t, f, "2.end") == "break"
    assert shown(t) == "1. 가\n2. 나\n3. "
    assert press_return(t, f, "3.end") == "break"            # 빈 번호 줄에서 Enter → 목록 끝
    assert shown(t) == "1. 가\n2. 나\n"
    root.destroy()


def test_check_and_bullet_continue():
    root, t, f, _ = make("- [x] 가")
    press_return(t, f, "1.end")
    assert shown(t) == "☑ 가\n☐ " and f.get_markup() == "- [x] 가\n- [ ] "
    root.destroy()
    root, t, f, _ = make("- 가")
    press_return(t, f, "1.end")
    assert shown(t) == "• 가\n• "
    root.destroy()


def test_plain_line_untouched():
    root, t, f, _ = make("그냥 글")
    assert press_return(t, f, "1.end") is None               # 일반 줄은 기본 Enter 동작
    root.destroy()


def test_prefix_toggle():
    root, t, f, _ = make("가\n나\n다")
    t.tag_add("sel", "1.0", "3.end")
    f.prefix("num")
    assert shown(t) == "1. 가\n2. 나\n3. 다"
    f.prefix("num")                                          # 한 번 더 누르면 뺀다
    assert shown(t) == "가\n나\n다"
    f.prefix("check")
    assert shown(t) == "☐ 가\n☐ 나\n☐ 다" and f.get_markup() == "- [ ] 가\n- [ ] 나\n- [ ] 다"
    f.prefix("bul")                                          # 다른 종류로 바꾸기
    assert shown(t) == "• 가\n• 나\n• 다"
    root.destroy()


def test_bold_strike_color_on_selection_and_undo_toggle():
    root, t, f, _ = make("중요")
    t.tag_add("sel", "1.0", "1.end")
    f.toggle("bold")
    assert shown(t) == "중요" and f.get_markup() == "**중요**"
    f.toggle("strike")
    assert f.get_markup() == "**~~중요~~**" or f.get_markup() == "~~**중요**~~", f.get_markup()
    f.toggle("strike"); f.toggle("bold")                     # 한 번 더 누르면 푼다
    assert f.get_markup() == "중요"
    f.color("빨강")
    assert f.get_markup() == "{빨강|중요}"
    f.color(None)
    assert f.get_markup() == "중요"
    root.destroy()


def test_bold_for_text_typed_next():
    root, t, f, _ = make("가")
    t.mark_set("insert", "1.end")
    f.toggle("bold")                                         # 글자를 고르지 않고 B → 이어서 치는 글자가 굵게
    t.insert("insert", "나다")
    f.on_key(None)
    assert f.get_markup() == "가**나다**", f.get_markup()
    f.toggle("bold")                                         # 다시 누르면 보통 글자로
    t.insert("insert", "라")
    f.on_key(None)
    assert f.get_markup() == "가**나다**라", f.get_markup()
    root.destroy()


def test_checkbox_click_toggle():
    root, t, f, _ = make("- [ ] 가\n- [x] 나")
    t.index = (lambda orig: (lambda i: "1.0" if str(i).startswith("@") else orig(i)))(t.index)
    e = _Click()
    e.x = e.y = 0
    assert f._click(e) == "break"
    assert f.get_markup().split("\n")[0] == "- [x] 가"
    assert t.tag_ranges("done")
    root.destroy()


def test_typed_prefix_becomes_list():
    root, t, f, _ = make("")
    t.insert("1.0", "- ")
    t.mark_set("insert", "1.end")
    f.on_key(None)
    assert shown(t) == "• ", shown(t)
    root.destroy()
    root, t, f, _ = make("")
    t.insert("1.0", "[] ")
    t.mark_set("insert", "1.end")
    f.on_key(None)
    assert shown(t) == "☐ ", shown(t)
    root.destroy()


def test_backspace_removes_prefix():
    root, t, f, _ = make("- [ ] ")
    t.mark_set("insert", "1.end")
    assert f._back(None) == "break"
    assert shown(t) == ""
    root.destroy()


def test_strip_markup_for_preview():
    assert sticky.strip_markup("**a** {빨강|b}\n- [x] c\n- d") == "a b\n☑ c\n• d"


if __name__ == "__main__":
    try:
        tk.Tk().destroy()
    except tk.TclError:
        print("화면이 없어 건너뜀")
        sys.exit(0)
    n = 0
    for name, fn in list(globals().items()):
        if name.startswith("test_"):
            fn()
            print("ok", name)
            n += 1
    print(n, "sticky tests passed")
