# -*- coding: utf-8 -*-
"""사용 설명서(PDF·이미지)를 만든다: 실제 프로그램 화면을 찍어 빨간 박스·번호로 설명한다.
   사용: xvfb-run -a python tools/make_manual.py   (결과: docs/사용설명서.pdf, docs/page_XX.png)"""
import os
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
os.environ["APPDATA"] = tempfile.mkdtemp()
from PIL import Image, ImageDraw, ImageFont, ImageGrab          # noqa: E402

from hwpgen import gui                                           # noqa: E402

OUT = os.path.join(HERE, "docs")
FONT_R = os.path.join(HERE, "assets", "fonts", "NotoSansKR-Regular.otf")
FONT_B = os.path.join(HERE, "assets", "fonts", "NotoSansKR-Bold.otf")
RED = (226, 38, 38)
INK = (38, 39, 43)
SUB = (110, 112, 118)
ACC = (63, 74, 138)
PW, PH = 1240, 1754                                              # A4, 150dpi
S = 2                                                            # 화면 확대 배율


def font(size, bold=False):
    return ImageFont.truetype(FONT_B if bold else FONT_R, size)


class FB:
    connected = True

    def connect(self, launch=True):
        return True

    def get_selection_text(self):
        return ""

    def get_all_text(self):
        return ""


def pump(w, n=12):
    for _ in range(n):
        w.update()
        time.sleep(0.02)


def rel(widget, top):
    x = widget.winfo_rootx() - top.winfo_rootx()
    y = widget.winfo_rooty() - top.winfo_rooty()
    return (x, y, x + widget.winfo_width(), y + widget.winfo_height())


def find(widget, text, exact=False):
    """안쪽을 뒤져 글자(text 옵션)가 맞는 위젯 찾기"""
    out = []

    def walk(w):
        for c in w.winfo_children():
            t = None
            try:
                t = c.cget("text")
            except Exception:                                    # noqa: BLE001
                t = getattr(c, "text", None)
            t = t if isinstance(t, str) else getattr(c, "text", "")
            if isinstance(t, str) and ((t == text) if exact else (text in t)):
                out.append(c)
            walk(c)
    walk(widget)
    return out[0] if out else None


def shot(top, extra=(0, 0, 0, 0)):
    top.update_idletasks()
    x, y = top.winfo_rootx(), top.winfo_rooty()
    w, h = top.winfo_width() + extra[2], top.winfo_height() + extra[3]
    im = ImageGrab.grab(bbox=(x, y, x + w, y + h)).convert("RGB")
    return im.resize((im.width * S, im.height * S), Image.LANCZOS)


def annotate(im, boxes):
    """boxes: [(x0,y0,x1,y1) 화면 좌표(1배), 번호]"""
    d = ImageDraw.Draw(im)
    for (x0, y0, x1, y1), n in boxes:
        b = (x0 * S - 4, y0 * S - 4, x1 * S + 4, y1 * S + 4)
        d.rectangle(b, outline=RED, width=5)
        r = 17
        cx, cy = b[0] + 2, b[1] + 2
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=RED)
        d.text((cx, cy), str(n), fill="white", font=font(22, True), anchor="mm")
    return im


def wrap(d, text, fnt, width):
    lines = []
    for para in text.split("\n"):
        cur = ""
        for ch in para:
            if d.textlength(cur + ch, font=fnt) > width:
                lines.append(cur)
                cur = ch
            else:
                cur += ch
        lines.append(cur)
    return lines


def page(title, steps, img=None, tips=None, num=1, img_w=470):
    """한 쪽: 위 제목 띠 + 왼쪽 설명(번호 단계) + 오른쪽 화면"""
    pg = Image.new("RGB", (PW, PH), "white")
    d = ImageDraw.Draw(pg)
    d.rectangle((0, 0, PW, 120), fill=(249, 249, 247))
    d.rectangle((0, 116, PW, 120), fill=ACC)
    d.text((70, 60), title, fill=INK, font=font(46, True), anchor="lm")
    d.text((PW - 70, 60), "양식 도우미 사용 설명서", fill=SUB, font=font(24), anchor="rm")
    x_text = 70
    text_w = PW - 140 - (img_w + 50 if img is not None else 0)
    y = 170
    for n, (head, body) in enumerate(steps, 1):
        d.ellipse((x_text, y, x_text + 46, y + 46), fill=RED)
        d.text((x_text + 23, y + 23), str(n), fill="white", font=font(28, True), anchor="mm")
        d.text((x_text + 66, y + 23), head, fill=INK, font=font(31, True), anchor="lm")
        y += 62
        for ln in wrap(d, body, font(26), text_w - 66):
            d.text((x_text + 66, y), ln, fill=(70, 72, 78), font=font(26))
            y += 40
        y += 28
    if tips:
        y += 6
        box_h = 60 + sum(len(wrap(d, t, font(25), text_w - 60)) * 38 + 10 for t in tips)
        d.rounded_rectangle((x_text, y, x_text + text_w, y + box_h), radius=14, fill=(250, 244, 244), outline=(235, 190, 190), width=2)
        d.text((x_text + 24, y + 28), "참고", fill=RED, font=font(26, True), anchor="lm")
        ty = y + 56
        for t in tips:
            for i, ln in enumerate(wrap(d, t, font(25), text_w - 60)):
                d.text((x_text + 24, ty), ("• " if i == 0 else "  ") + ln, fill=(90, 70, 70), font=font(25))
                ty += 38
            ty += 10
    if img is not None:
        sc = min((PH - 330) / img.height, img_w / img.width)
        im2 = img.resize((int(img.width * sc), int(img.height * sc)), Image.LANCZOS)
        x0 = PW - 70 - im2.width
        d.rounded_rectangle((x0 - 10, 170, x0 + im2.width + 10, 170 + im2.height + 20), radius=14, outline=(214, 214, 208), width=3)
        pg.paste(im2, (x0, 180))
    d.text((PW // 2, PH - 50), "- %d -" % num, fill=SUB, font=font(24), anchor="mm")
    return pg


def main():
    os.makedirs(OUT, exist_ok=True)
    w = gui.Widget(FB())
    w.geometry("264x700+0+0")
    w.attributes("-topmost", False)
    w.update()
    pages = []

    # ---- 화면 찍기
    def tab(key):
        w.show_tab(key)
        pump(w)

    tab("report")
    s_report = shot(w)
    chip = w.head.bbox(w.head.chip.item)
    b_status = (chip[0] - 3, chip[1] - 2, chip[2] + 3, chip[3] + 2)
    tiles = w.head.tiles
    b_tabs = (tiles["report"]["x"] - 3, tiles["report"]["y"] - 3, tiles["__more"]["x"] + tiles["__more"]["w"] + 3, tiles["report"]["y"] + tiles["report"]["h"] + 3)
    ev = find(w.body, "행사 보고")
    b_event = rel(ev, w) if ev else (10, 190, 240, 232)
    sec1 = find(w.body, "문화교육국 보고")
    b_group = rel(sec1.master if sec1 is not None else w.body, w) if sec1 is not None else (10, 160, 240, 180)
    mayor = find(w.body, "시장님 보고", exact=True)
    b_mayor = rel(mayor, w) if mayor else None
    more_t = tiles["__more"]
    b_more = (more_t["x"] - 3, more_t["y"] - 3, more_t["x"] + more_t["w"] + 3, more_t["y"] + more_t["h"] + 3)
    pages.append(("cover", None))

    # 1. 시작하기
    img = annotate(s_report.copy(), [(b_status, 1), (b_tabs, 2), (b_event, 3)])
    pages.append(("시작하기", dict(steps=[
        ("한글을 먼저 엽니다", "한글(HWP)을 실행하고 문서를 엽니다. 새 문서도 됩니다. 양식은 한글에서 커서가 있는 곳에 들어갑니다."),
        ("양식 도우미를 실행합니다", "HwpFormGenerator.exe 를 실행하면 화면 오른쪽에 좁은 창이 뜹니다. 이미 실행 중이면 새로 뜨지 않고 기존 창이 앞으로 나옵니다."),
        ("연결 상태를 확인합니다 (①)", "초록 점과 '한글에 연결됨'이 보이면 준비가 끝났습니다. 빨간 점이면 그 글자를 눌러 다시 연결하세요."),
        ("분류를 고르고 양식을 누릅니다 (②③)", "위쪽 ② 에서 분류(보고·주간·예산·국회·감사)를 고르고, ③ 처럼 양식 버튼을 누르면 한글 문서에 양식이 차례로 입력됩니다."),
    ], tips=["창의 X 는 닫기가 아니라 트레이(알림 영역)로 숨기기입니다. 아이콘을 누르면 다시 나옵니다.",
             "완전히 끄려면 설정 → '프로그램 완전 종료' 또는 트레이 아이콘 메뉴의 '종료'를 누르세요."], img=img)))

    # 2. 분류와 색
    w.head._open_more()
    pump(w, 15)
    menu = w.head._menu
    s_menu = shot(w)
    mrect = (menu.winfo_rootx(), menu.winfo_rooty(), menu.winfo_rootx() + menu.winfo_width(), menu.winfo_rooty() + menu.winfo_height())
    w.head._close_more()
    pump(w)
    img = annotate(s_menu, [(b_tabs, 1), (b_more, 2), (mrect, 3)])
    pages.append(("분류와 색깔", dict(steps=[
        ("자주 쓰는 분류 (①)", "보고 · 주간 · 예산 · 국회 · 감사 다섯 개가 항상 보입니다. 분류마다 색이 달라서 어떤 문서인지 바로 구분됩니다."),
        ("그 밖의 분류 (②③)", "'⋯'을 누르면 보도자료 · 국가예산 신규사업 · 도구 · 기호/글자 겹치기 · 설정이 메뉴로 열립니다."),
        ("색 구분", "보고=코랄 / 주간·간부회의=보라 / 예산=청록 / 국회=파랑 / 감사=로즈. 각 묶음 제목 앞의 색 점과 버튼 왼쪽 아이콘 색이 같은 분류 색입니다."),
    ], tips=["양식 버튼에 마우스를 올리면 그 양식 설명이 말풍선으로 나옵니다."], img=img, img_w=560)))

    # 3. 보고 양식
    boxes = [(b_group, 1), (b_event, 2)]
    if b_mayor:
        boxes.append((b_mayor, 3))
    pages.append(("보고 양식", dict(steps=[
        ("문화교육국 보고 (①)", "행사 보고(식순·시간표 표 포함)와 사업 보고(개요·지난 실적·향후 계획)가 있습니다. 제목 상자부터 첫 줄에서 시작합니다."),
        ("양식 버튼 (②)", "누르면 제목 상자 · 담당자 줄 · □ 소제목 · ◦ ▸ 표시가 샘플과 같은 글꼴로 한글에 입력됩니다. 내용 칸은 비어 있어서 바로 쓰면 됩니다."),
        ("시장님 보고 (③)", "보고 내용은 1장으로 만들고, 참고자료가 필요하면 '참고자료 쪽 추가'로 뒤에 쪽을 붙입니다."),
    ], tips=["설정의 '내 정보'에 과 · 담당자를 적어 두면 담당자 줄이 자동으로 채워집니다. 비워 두면 빈칸으로 나옵니다.",
             "양식은 한글 2020에서도 열리는 파일 형식으로 만들어집니다."], img=annotate(s_report.copy(), boxes))))

    # 4. 주간
    tab("week")
    s_week = shot(w)
    bar = [c for c in w.body.winfo_children()][0]
    b_bar = rel(bar, w)
    b_cover = rel(find(w.body, "주간업무계획 표지"), w)
    b_plan = rel(find(w.body, "주간업무계획", exact=True), w)
    imp = find(w.body, "일정 입력")
    b_imp = rel(imp, w)
    pages.append(("주간업무계획", dict(steps=[
        ("작성할 주 고르기 (①)", "기본은 '다음 주'입니다. ◀ ▶ 로 주를 바꾸면 표지의 날짜 · 주요일정의 월~일 표가 그 주에 맞게 만들어집니다."),
        ("표지 · 계획 · 주요일정 (②)", "표지는 날짜 상자와 순서 목차, 주간업무계획은 ❶ 행사명(부서 담당자) + ◦ 일시 / 장소 / 주최 / 내용, 주요일정은 월~일 표입니다."),
        ("일정 입력 · 가져오기 (③)", "한 번 입력하면 계획과 주요일정이 같이 나옵니다. 지난 주 한글 문서에서 가져올 수도 있습니다."),
        ("글을 선택해서 바로 만들기", "한글에서 제목 · 이름 · 일시 · 장소 · 내용을 아무렇게나 적고 선택한 뒤 '주간업무계획'을 누르면, 제목을 기준으로 일시·장소·내용을 붙여 서식대로 바꿔 줍니다."),
    ], tips=["행사 제목과 담당자 줄만 파랑, 일시 · 장소 · 주최 · 내용은 검정으로 나옵니다.",
             "라벨은 '일시 :' 처럼 쌍점이 있어도 없어도 됩니다. 날짜만 적어도 일시로 읽습니다."],
        img=annotate(s_week.copy(), [(b_bar, 1), (b_plan, 2), (b_imp, 3)]))))

    # 5. 일정 입력 창
    w.open_detail("weekly")
    d_win = w.detail
    pump(w, 20)
    try:
        d_win.geometry("+300+20")
        pump(w, 20)
        s_det = shot(d_win)
        boxes = []
        for t_, n_ in (("가져오기", 1), ("추가", 2), ("만들기", 3), ("한글에 넣기", 3)):
            wd = find(d_win, t_)
            if wd is not None and n_ not in [b[1] for b in boxes]:
                boxes.append((rel(wd, d_win), n_))
        s_det = annotate(s_det, boxes[:3])
    except Exception:                                            # noqa: BLE001
        s_det = Image.new("RGB", (800, 600), (245, 245, 245))
    try:
        d_win.destroy()
    except Exception:                                            # noqa: BLE001
        pass
    w.detail = None
    pump(w, 20)
    pages.append(("일정 입력 창", dict(steps=[
        ("일정 가져오기 (①)", "열려 있는 한글 문서의 주간업무계획에서 우리 국 일정만 제목 기준으로 읽어 옵니다. 아래 '파일에서'는 .hwpx 한글 파일이나 .txt 글 파일을 골라 읽습니다."),
        ("일정 추가 · 고치기 (②)", "제목 · 담당자 · 날짜 · 시간 · 장소 · 주최 · 내용을 입력합니다. 같은 날짜 · 제목은 중복되지 않게 합칩니다."),
        ("문서 만들기 (③)", "'주간업무계획 → 한글에', '주간 주요일정 → 한글에'로 지금 열린 문서에 넣거나, '새 문서' · '파일 저장'으로 따로 만듭니다."),
    ], tips=["입력한 일정은 주 단위로 저장되어 다음에 열어도 남아 있습니다."], img=s_det, img_w=620)))

    # 6. 서식 바꾸기 + 설정
    tab("more")
    s_more = shot(w)
    conv = find(w.body, "서식 바꾸기")
    chk = find(w.body, "선택 없으면 문서 전체 변환")
    prof = find(w.body, "내 정보")
    logd = find(w.body, "오류 기록 폴더")
    recon = find(w.body, "한글에 다시 연결")
    b_chk = rel(chk, w) if chk else None
    settings_boxes = [(rel(wd, w), n) for wd, n in ((prof, 1), (recon, 2), (logd, 3)) if wd is not None]
    pages.append(("이미 쓴 글을 양식으로 바꾸기", dict(steps=[
        ("글을 선택합니다", "한글에서 서식 없이 적은 글(제목 · ○ ㅇ ◦ - 같은 표시 · 번호)을 마우스로 선택합니다."),
        ("원하는 양식 버튼을 누릅니다", "보고 · 예산 · 감사 등 어느 양식이든 누르면, 선택한 글이 □ ◦ ▸ - 단계로 정리되어 그 양식으로 바뀝니다. 글 맨 앞의 짧은 한 줄은 제목 칸으로 들어갑니다."),
        ("문서 전체를 바꾸려면 (①)", "설정 탭 '서식 바꾸기'의 ① 체크를 켜면, 선택이 없을 때 문서 전체를 바꿉니다(확인 창이 뜹니다)."),
    ], tips=["바꾼 뒤 한글에서 Ctrl+Z 로 되돌릴 수 있습니다.", "원래 있던 표는 표 모양이 아니라 | 칸 | 칸 | 글자로 옮겨집니다."],
        img=annotate(s_more.copy(), [(b_chk, 1)] if b_chk else []))))

    # 7. 보도자료 초안 대화창
    box_dlg = {}

    def grab_dlg():
        tops = [c for c in w.winfo_children() if isinstance(c, gui.tk.Toplevel)]
        if tops:
            t = tops[-1]
            t.update()
            box_dlg["img"] = shot(t)
            ta = find(t, "초안 만들기")
            tx = [c for c in t.winfo_children() if isinstance(c, gui.tk.Text)]
            box_dlg["b"] = [(rel(tx[0], t), 1)] if tx else []
            if ta is not None:
                box_dlg["b"].append((rel(ta, t), 2))
            t.destroy()
    w.after(500, grab_dlg)
    w.ask_rough("보도자료 초안", "제목, 일시·장소·주최·대상·내용을 대략 적으면 보도자료 문단으로 정리해 드려요.\n(비워 두면 틀만 넣어요)",
                "예)\n익산 푸드 페스티벌\n일시: 2026. 11. 20.(금) 14:00\n장소: 중앙체육관\n주최: 익산시\n대상: 시민 누구나\n내용: 먹거리 부스 운영")
    s_press = annotate(box_dlg["img"], box_dlg.get("b", [])) if "img" in box_dlg else None
    pages.append(("보도자료 초안", dict(steps=[
        ("보도자료 버튼을 누릅니다", "글을 선택해 두었으면 그 글로, 아니면 입력 창이 열립니다(①). 비워 두고 '틀만 넣기'를 누르면 상단 머리표와 제목 틀만 들어갑니다."),
        ("대략 적습니다 (①)", "제목과 일시 · 장소 · 주최 · 대상 · 내용 · 목적 · 신청 · 기대효과 · 사업비 · 문의를 줄마다 적습니다. 제목은 첫 줄에 적으세요."),
        ("초안 만들기 (②)", "리드문 → 추진 취지 → 일시 · 장소 · 주관 → 첫째 · 둘째 주요 내용 → 참여 대상 · 신청 → 기대효과 → 관계자 인용 → 문의 순서로 문단이 만들어집니다."),
    ], tips=["적지 않은 내용은 지어내지 않고 [대괄호]로 비워 둡니다. 한글에서 고쳐 쓰세요.",
             "리드문 예: 익산시는 11월 20일(금) 오후 2시 중앙체육관에서 ‘익산 푸드 페스티벌’을 개최한다고 밝혔다."], img=s_press, img_w=560)))

    # 8. 행정사무감사
    tab("audit")
    s_audit = shot(w)
    first = find(w.body, "표지", exact=True)
    sec = find(w.body, "감사자료 항목")
    b1 = rel(first, w) if first else (10, 150, 240, 190)
    b2 = rel(find(w.body, "1. 업무성과"), w) if find(w.body, "1. 업무성과") else (10, 400, 240, 440)
    pages.append(("행정사무감사", dict(steps=[
        ("표지 · 목차 · 공통사항 (①)", "표지(날짜·회기·국 이름), 목차, 과 공통사항 목록, 구분 쪽이 있습니다. 과 이름은 내 정보에서 자동으로 채워집니다."),
        ("감사자료 항목 1~37 (②)", "항목 제목 버튼을 누르면 그 항목의 쪽(제목 · 확인자/작성자 상자 · □ 소제목 · 표 머리)이 들어갑니다. 맨 끝에 '과 요구사항 사업 쪽'도 있습니다."),
        ("내용 쓰기", "표는 머리 줄과 빈 줄 두 개만 있습니다. 줄이 더 필요하면 한글에서 표 줄을 추가하세요."),
    ], tips=["감사 탭은 아래로 스크롤하면 항목 37개와 과 요구사항 쪽까지 보입니다.",
             "넣기가 실패하면 설정의 '오류 기록 폴더 열기'에서 failed_*.hwpx 와 error.log 를 확인하세요."],
        img=annotate(s_audit.copy(), [(b1, 1), (b2, 2)]))))

    # 9. 도구
    tab("tools")
    s_tools = shot(w)
    tb = rel(find(w.body, "표 넣기"), w)
    pages.append(("도구: 표 · 날짜 · 문구 · 금액", dict(steps=[
        ("표 만들기 (①)", "줄 · 칸 수를 − + 로 정하고 '표 넣기'를 누르면 커서 위치에 표가 만들어집니다."),
        ("오늘 날짜 · 쪽 나누기 · 자주 쓰는 문구", "날짜 표기 4가지(2026. 11. 13. / (요일) / 년월일 / ’26.), 쪽 나누기, 붙임 · 끝. · ※ · (단위 : 천원) 같은 문구를 한 번에 넣습니다."),
        ("금액 바꿔 쓰기", "원 단위 숫자를 쓰면 천원 · 백만원 · '3억 444만 1,020원' · 억원 표기가 나오고, 누르면 한글에 넣습니다."),
    ], tips=["기호 탭에는 ○ ● ◦ ▸ □ ■ 같은 기호와 원 숫자, 글자 겹치기(네모·원 안 숫자)가 있습니다."],
        img=annotate(s_tools.copy(), [(tb, 1)]))))

    # 10. 설정과 문제 해결
    pages.append(("설정과 문제 해결", dict(steps=[
        ("내 정보 (①)", "과 · 계 · 담당자 이름과 내선을 한 번 적어 두면 양식의 담당자 줄이 자동으로 채워집니다. 끄거나 비워 둘 수도 있습니다."),
        ("한글에 다시 연결 (②)", "'한글 연결 안 됨'일 때 한글을 열고 이 버튼(또는 위쪽 상태 글자)을 누르세요. 한글이 보안 승인 창을 띄우면 '모두 허용'을 누릅니다."),
        ("오류 기록 폴더 (③)", "양식 넣기가 실패하면 오류 내용(error.log)과 실패한 파일(failed_*.hwpx)이 저장됩니다. 문의할 때 함께 보내 주세요."),
        ("자주 겪는 일", "• '파일이 손상되었습니다'가 뜨면: 최신 exe 인지 확인하고 오류 기록을 보내 주세요.\n• 차례로 입력이 한 번에 나오면: 커서가 문서 중간(뒤에 글이 있는 곳)에 있을 때입니다. 순서를 지키려고 한 번에 넣습니다.\n• 내용이 있는 문서에 넣으면 새 쪽에서 시작합니다."),
    ], tips=["창 투명도와 '마우스를 올리면 선명하게'는 설정에서 바꿉니다.", "넣는 방식(차례대로 입력 / 한 번에)도 설정에서 고릅니다."],
        img=annotate(s_more.copy(), settings_boxes))))

    w.destroy()

    # ---- 쪽 만들기
    out_pages = []
    n = 0
    for title, kw in pages:
        n += 1
        if title == "cover":
            pg = Image.new("RGB", (PW, PH), "white")
            d = ImageDraw.Draw(pg)
            d.rectangle((0, 0, PW, PH), fill=(249, 249, 247))
            d.rectangle((90, 480, 140, 780), fill=ACC)
            d.text((180, 520), "양식 도우미", fill=INK, font=font(104, True), anchor="lm")
            d.text((180, 640), "사용 설명서", fill=INK, font=font(74, True), anchor="lm")
            d.text((180, 740), "한글 옆에서 클릭 한 번으로 보고 · 주간 · 예산 · 국회 · 감사 서식 만들기", fill=SUB, font=font(30), anchor="lm")
            logo = os.path.join(HERE, "assets", "app_256.png")
            if os.path.exists(logo):
                lg = Image.open(logo).convert("RGBA").resize((200, 200), Image.LANCZOS)
                pg.paste(lg, (180, 300), lg)
            toc = ["1  시작하기", "2  분류와 색깔", "3  보고 양식", "4  주간업무계획", "5  일정 입력 창", "6  이미 쓴 글을 양식으로 바꾸기",
                   "7  보도자료 초안", "8  행정사무감사", "9  도구: 표 · 날짜 · 문구 · 금액", "10  설정과 문제 해결"]
            for i, t in enumerate(toc):
                d.text((180, 940 + i * 56), t, fill=(70, 72, 78), font=font(30))
            d.text((180, 1560), "빨간 박스와 번호는 화면에서 누르거나 확인할 곳을 가리킵니다.", fill=RED, font=font(28, True), anchor="lm")
            out_pages.append(pg)
            continue
        out_pages.append(page(title, num=n, **kw))
    paths = []
    for i, pg in enumerate(out_pages, 1):
        p = os.path.join(OUT, "page_%02d.png" % i)
        pg.save(p)
        paths.append(p)
    pdf = os.path.join(OUT, "사용설명서.pdf")
    out_pages[0].save(pdf, "PDF", save_all=True, append_images=out_pages[1:], resolution=150.0)
    print("만들었어요:", pdf, len(out_pages), "쪽")


if __name__ == "__main__":
    main()
