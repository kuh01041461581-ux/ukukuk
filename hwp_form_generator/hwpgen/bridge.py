# -*- coding: utf-8 -*-
"""실행 중인 한글(HWP)에 붙어서 문서를 넣는 다리 (Windows 전용, pywin32 필요).

 - connect()       : 실행 중인 한글에 연결 (없으면 새로 띄움)
 - insert_file()   : 열려 있는 문서의 커서 위치에 hwpx 내용을 서식째 끼워 넣음
 - open_file()     : 새 문서(창)로 열기
 - get_text()      : 지금 열린 문서 전체 글자 (주간업무계획에서 일정 발췌용)
 - dock_beside()   : 한글 창을 줄이고 이 프로그램 창을 오른쪽에 붙임 (범정부 오피스 같은 옆 창)

한글이 보안 승인 창('접근을 허용하시겠습니까')을 띄우면 '모두 허용'을 누른다.
(한컴 자동화용 FilePathCheckerModule DLL 을 레지스트리에 등록해 두면 창이 뜨지 않는다 — README 참고)
"""
import os
import sys

IS_WIN = sys.platform == "win32"


class BridgeError(Exception):
    pass


class HwpBridge:
    def __init__(self):
        self.hwp = None

    @property
    def connected(self):
        if self.hwp is None:
            return False
        try:
            _ = self.hwp.Version
            return True
        except Exception:
            self.hwp = None
            return False

    def connect(self, launch=True):
        if not IS_WIN:
            raise BridgeError("한글 연결은 Windows 에서만 돼요")
        try:
            import pythoncom
            import win32com.client as wc
        except ImportError:
            raise BridgeError("pywin32 가 없어요 (pip install pywin32)")
        hwp = None
        try:                                              # 실행 중인 한글을 먼저 찾는다
            rot = pythoncom.GetRunningObjectTable()
            ctx = pythoncom.CreateBindCtx(0)
            for mk in rot.EnumRunning():
                if mk.GetDisplayName(ctx, None).startswith("!HwpObject"):
                    obj = rot.GetObject(mk).QueryInterface(pythoncom.IID_IDispatch)
                    hwp = wc.Dispatch(obj)
                    break
        except Exception:
            hwp = None
        if hwp is None:
            if not launch:
                raise BridgeError("실행 중인 한글을 찾지 못했어요")
            try:
                hwp = wc.Dispatch("HWPFrame.HwpObject")
            except Exception as e:
                raise BridgeError("한글을 연결하지 못했어요: %s" % e)
        for name in ("FilePathCheckerModule", "FilePathCheckerModuleExample"):
            try:
                if hwp.RegisterModule("FilePathCheckerModuleExample", name):
                    break
            except Exception:
                pass
        try:
            hwp.XHwpWindows.Item(0).Visible = True
        except Exception:
            pass
        self.hwp = hwp
        return True

    def _need(self):
        if not self.connected:
            raise BridgeError("한글에 연결되어 있지 않아요")
        return self.hwp

    def insert_file(self, path, keep_section=False):
        """열린 문서의 커서 위치에 파일을 끼워 넣는다. 글자·문단 모양·스타일을 원본 그대로 유지(InsertFile 동작).
        keep_section=True 면 파일의 쪽 설정(용지·여백)도 가져온다(빈 문서에 첫 양식을 넣을 때). 한 번만 넣는다(성공을 거짓값으로 알려 주는 경우가 있어 재시도하지 않음)."""
        hwp = self._need()
        path = os.path.abspath(path)
        err = None
        try:
            ps = hwp.HParameterSet.HInsertFile
            hwp.HAction.GetDefault("InsertFile", ps.HSet)
            ps.filename = path
            ps.KeepSection = 1 if keep_section else 0
            ps.KeepCharshape = 1
            ps.KeepParashape = 1
            ps.KeepStyle = 1
            if hwp.HAction.Execute("InsertFile", ps.HSet):
                return True
        except Exception as e:                             # noqa: BLE001
            err = e
        try:                                               # 위가 안 될 때만 한 번: 옛 방식 Insert (오류 없으면 성공으로 본다)
            hwp.Insert(path, "HWPX", "")
            return True
        except Exception as e:                             # noqa: BLE001
            err = e
        raise BridgeError("한글에 끼워 넣지 못했어요%s" % (" (%s)" % err if err else ""))

    def move_doc_end(self):
        """커서를 문서 맨 끝으로 — 조각을 차례로 넣을 때 순서가 뒤집히지 않게 매번 끝에 붙인다"""
        hwp = self._need()
        try:
            hwp.HAction.Run("MoveDocEnd")
            return True
        except Exception:                                  # noqa: BLE001
            return False

    def prepare_insert(self, at_end):
        """내용이 있는 문서에 넣기 전: (문서 끝이면 끝으로 가서) 커서가 문단 중간이면 문단을 나눠 새 줄에서 시작하게 한다"""
        hwp = self._need()
        try:
            if at_end:
                hwp.HAction.Run("MoveDocEnd")
            pos = hwp.GetPos()
            if pos and pos[2] != 0:
                hwp.HAction.Run("BreakPara")
            return True
        except Exception:                                  # noqa: BLE001
            return False

    def break_para(self):
        """문단 나눔(Enter) — 조각을 이어 넣을 때 앞 조각 끝에 다음 조각이 붙지 않게"""
        hwp = self._need()
        try:
            hwp.HAction.Run("BreakPara")
        except Exception:                                  # noqa: BLE001
            pass

    def is_empty_doc(self):
        """지금 문서가 비어 있는지(새 문서) — 그럴 때만 샘플의 용지·여백을 적용한다"""
        try:
            return not self.get_text().strip()
        except Exception:                                  # noqa: BLE001
            return False

    def at_doc_end(self):
        """커서가 문서 맨 끝에 있는지 (그렇다면 조각을 차례로 이어 붙여도 순서가 뒤집힐 수 없다)"""
        hwp = self._need()
        try:
            p0 = hwp.GetPos()
            hwp.HAction.Run("MoveDocEnd")
            p1 = hwp.GetPos()
            hwp.SetPos(*p0)
            return tuple(p0) == tuple(p1)
        except Exception:                                  # noqa: BLE001
            return False

    def apply_page_setup(self, d):
        """용지 크기·여백(HWPUNIT)을 지금 문서에 적용. 안 되면 조용히 넘어간다."""
        hwp = self._need()
        try:
            ps = hwp.HParameterSet.HSecDef
            hwp.HAction.GetDefault("PageSetup", ps.HSet)
            pd = ps.PageDef
            pd.PaperWidth, pd.PaperHeight = d["width"], d["height"]
            pd.Landscape = 1 if d.get("landscape") else 0
            pd.LeftMargin, pd.RightMargin = d["left"], d["right"]
            pd.TopMargin, pd.BottomMargin = d["top"], d["bottom"]
            pd.HeaderLen, pd.FooterLen, pd.GutterLen = d["header"], d["footer"], d["gutter"]
            ps.HSet.SetItem("ApplyClass", 24)
            ps.HSet.SetItem("ApplyTo", 3)                 # 문서 전체
            return bool(hwp.HAction.Execute("PageSetup", ps.HSet))
        except Exception:                                  # noqa: BLE001
            return False

    def insert_text(self, text):
        """커서 위치에 글자(기호) 넣기. 지금 글자 모양을 그대로 따른다."""
        hwp = self._need()
        try:
            act = hwp.CreateAction("InsertText")
            ps = act.CreateSet()
            act.GetDefault(ps)
            ps.SetItem("Text", text)
            if act.Execute(ps):
                return True
        except Exception:
            pass
        try:
            hwp.HAction.GetDefault("InsertText", hwp.HParameterSet.HInsertText.HSet)
            hwp.HParameterSet.HInsertText.Text = text
            hwp.HAction.Execute("InsertText", hwp.HParameterSet.HInsertText.HSet)
            return True
        except Exception as e:
            raise BridgeError("기호를 넣지 못했어요: %s" % e)

    def compose_chars(self, text, shape_no, char_size=-3):
        """한글 명령(ComposeChars)으로 글자 겹치기를 직접 넣는다 (hwpx 끼워 넣기가 안 될 때)."""
        hwp = self._need()
        errs = []
        for use_hset in (True, False):
            try:
                if use_hset:
                    ps = hwp.HParameterSet.HChCompose
                    hwp.HAction.GetDefault("ComposeChars", ps.HSet)
                    ps.Chars = text
                    ps.CharSize = char_size
                    ps.CheckBorder = shape_no
                    return bool(hwp.HAction.Execute("ComposeChars", ps.HSet))
                act = hwp.CreateAction("ComposeChars")
                st = act.CreateSet()
                act.GetDefault(st)
                st.SetItem("Chars", text)
                st.SetItem("CharSize", char_size)
                st.SetItem("CheckBorder", shape_no)
                return bool(act.Execute(st))
            except Exception as e:                         # noqa: BLE001
                errs.append(str(e))
        raise BridgeError("글자 겹치기 명령을 쓰지 못했어요: %s" % "; ".join(errs))

    def convert_to_hwpx(self, src, dst):
        """옛 형식(.hwp)을 한글이 직접 열어 .hwpx 로 저장한다(서식이 가장 정확). 열었던 문서는 닫는다."""
        hwp = self._need()
        src, dst = os.path.abspath(src), os.path.abspath(dst)
        try:
            if not hwp.Open(src, "HWP", "forceopen:true"):
                raise BridgeError("한글에서 파일을 열지 못했어요")
            ok = hwp.SaveAs(dst, "HWPX", "")
            try:
                hwp.XHwpDocuments.Active_XHwpDocument.Close(False)
            except Exception:                              # noqa: BLE001
                pass
            if not ok or not os.path.exists(dst):
                raise BridgeError("hwpx 로 저장하지 못했어요")
            return dst
        except BridgeError:
            raise
        except Exception as e:                             # noqa: BLE001
            raise BridgeError("한글로 변환하지 못했어요: %s" % e)

    def open_file(self, path):
        hwp = self._need()
        path = os.path.abspath(path)
        last = None
        for fmt in ("HWPX", ""):
            try:
                if hwp.Open(path, fmt, "forceopen:true"):
                    return True
            except Exception as e:
                last = e
        raise BridgeError("한글에서 열지 못했어요%s" % (" (%s)" % last if last else ""))

    def get_text(self):
        hwp = self._need()
        try:
            hwp.InitScan(0x0000, 0x0077, 0, 0, 0, 0)
            out = []
            while True:
                state, text = hwp.GetText()
                if state in (0, 1):
                    break
                out.append(text)
            hwp.ReleaseScan()
            return "".join(out)
        except Exception as e:
            raise BridgeError("한글 문서 글자를 읽지 못했어요: %s" % e)


# ---------------------------------------------------------------- 창 배치 (ctypes, Windows 전용)
def _user32():
    import ctypes
    return ctypes.windll.user32


def find_hwp_window():
    """보이는 한글 본창 핸들 (창 종류 이름이 HwpApp 으로 시작하는 것)"""
    if not IS_WIN:
        return None
    import ctypes
    from ctypes import wintypes
    u = _user32()
    found = []

    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def cb(hwnd, _):
        if u.IsWindowVisible(hwnd):
            buf = ctypes.create_unicode_buffer(64)
            u.GetClassNameW(hwnd, buf, 64)
            if buf.value.startswith("HwpApp") or buf.value.startswith("Hwp"):
                found.append(hwnd)
        return True

    u.EnumWindows(cb, 0)
    return found[0] if found else None


def work_area():
    import ctypes
    from ctypes import wintypes
    r = wintypes.RECT()
    ctypes.windll.user32.SystemParametersInfoW(0x0030, 0, ctypes.byref(r), 0)    # SPI_GETWORKAREA
    return r.left, r.top, r.right, r.bottom


def dock_beside(panel_width):
    """한글 창을 작업 영역 왼쪽으로 줄이고, 오른쪽 panel_width 만큼을 비워 (x, y, w, h) 로 돌려준다."""
    l, t, r, b = work_area()
    hwnd = find_hwp_window()
    if hwnd:
        _user32().MoveWindow(hwnd, l, t, max(400, r - l - panel_width), b - t, True)
    return r - panel_width, t, panel_width, b - t
