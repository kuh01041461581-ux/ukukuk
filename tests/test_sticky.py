"""스티커 메모 편집 동작 시험 (Tk 가 있는 PC 에서만: 화면이 없으면 건너뜀)
실행: python tests/test_sticky.py"""
import os
import sys
import tkinter as tk
import tkinter.font as tkfont

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import sticky  # noqa: E402


class _App:
    pass


def make(text):
    root = tk.Tk()
    root.withdraw()
    app = _App()
    app.f_small = tkfont.Font(root=root, family="Malgun Gothic", size=10)
    t = tk.Text(root)
    t.insert("1.0", text)
    changes = []
    f = sticky.MemoFormat(t, app, lambda: changes.append(1))
    return root, t, f, changes


def body(t):
    return t.get("1.0", "end-1c")


def press_return(t, f, at):
    t.mark_set("insert", at)
    return f._return(None)


def test_numbering():
    root, t, f, _ = make("1. 가\n2. 나")
    assert press_return(t, f, "2.end") == "break"
    assert body(t) == "1. 가\n2. 나\n3. "
    assert press_return(t, f, "3.end") == "break"          # 빈 번호 줄에서 Enter → 목록 끝
    assert body(t) == "1. 가\n2. 나\n"
    root.destroy()


def test_check_and_bullet_continue():
    root, t, f, _ = make("- [ ] 가")
    press_return(t, f, "1.end")
    assert body(t) == "- [ ] 가\n- [ ] "
    root.destroy()
    root, t, f, _ = make("- 가")
    press_return(t, f, "1.end")
    assert body(t) == "- 가\n- "
    root.destroy()


def test_plain_line_untouched():
    root, t, f, _ = make("그냥 글")
    assert press_return(t, f, "1.end") is None             # 일반 줄은 기본 Enter 동작
    root.destroy()


def test_prefix_toggle():
    root, t, f, _ = make("가\n나\n다")
    t.tag_add("sel", "1.0", "3.end")
    f.prefix("num")
    assert body(t) == "1. 가\n2. 나\n3. 다"
    f.prefix("num")                                        # 한 번 더 누르면 뺀다
    assert body(t) == "가\n나\n다"
    f.prefix("check")
    assert body(t) == "- [ ] 가\n- [ ] 나\n- [ ] 다"
    root.destroy()


def test_wrap_and_unwrap():
    root, t, f, _ = make("중요")
    t.tag_add("sel", "1.0", "1.end")
    f.wrap("**", "**")
    assert body(t) == "**중요**"
    t.tag_add("sel", "1.0", "1.end")
    f.wrap("**", "**")
    assert body(t) == "중요"
    t.tag_add("sel", "1.0", "1.end")
    f.color("빨강")
    assert body(t) == "{빨강|중요}"
    t.tag_add("sel", "1.0", "1.end")
    f.color(None)
    assert body(t) == "중요"
    root.destroy()


def test_checkbox_click_toggle():
    root, t, f, _ = make("- [ ] 가\n- [x] 나")

    class E:
        pass
    # 직접 인덱스를 넘길 수 없어 _click 이 쓰는 같은 규칙으로 토글을 확인: 1줄의 [ ] → [x]
    t.index = (lambda orig: (lambda i: "1.2" if str(i).startswith("@") else orig(i)))(t.index)
    e = E()
    e.x = e.y = 0
    assert f._click(e) == "break"
    assert body(t).split("\n")[0] == "- [x] 가"
    root.destroy()


def test_render_does_not_change_text():
    text = "**굵게** ~~취소~~ {빨강|색}\n1. 가\n- [x] 나"
    root, t, f, _ = make(text)
    f.render()
    assert body(t) == text                                 # 서식은 그리기만 하고 저장되는 글자는 그대로
    assert t.tag_ranges("bold") and t.tag_ranges("strike") and t.tag_ranges("c_빨강")
    root.destroy()


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
