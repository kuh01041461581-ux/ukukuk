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

    @property
    def hwp_version(self):
        """연결된 한글 버전 글자(예: 10.0.0.xxxx) — 검증 기록에 남겨 버전별 차이를 알 수 있게"""
        try:
            return str(self.hwp.Version)
        except Exception:                                  # noqa: BLE001
            return ""

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
        try:                                               # 참고 프로그램이 쓰는 방식: 동작 만들기 → 항목 하나(FileName)만 지정
            act = hwp.CreateAction("InsertFile")
            st = act.CreateSet()
            act.GetDefault(st)
            st.SetItem("FileName", path)
            if act.Execute(st):
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

    def get_selection_text(self):
        """지금 선택(블록)한 글자. 선택이 없으면 빈 글"""
        hwp = self._need()
        try:
            t = hwp.GetTextFile("TEXT", "saveblock")
            if t:
                return t
        except Exception:                                  # noqa: BLE001
            pass
        return self._scan_selection()

    def _scan_selection(self):
        """선택한 글을 한글의 '훑기'(InitScan/GetText)로 읽는다 — 참고 프로그램이 쓰는 방식. 상태 0·1 이면 끝, 2 이어짐, 3 문단 끝"""
        hwp = self._need()
        parts = []
        try:
            hwp.InitScan(1, 255)
            for _ in range(5000):
                r = hwp.GetText()
                st, tx = (r[0], r[1]) if isinstance(r, (tuple, list)) and len(r) >= 2 else (0, "")
                if tx:
                    parts.append(tx + ("\r\n" if st == 3 else ""))
                if st in (0, 1):
                    break
        except Exception:                                  # noqa: BLE001
            return ""
        finally:
            try:
                hwp.ReleaseScan()
            except Exception:                              # noqa: BLE001
                pass
        return "".join(parts)

    def get_all_text(self):
        """문서 전체 글자(줄바꿈 유지)"""
        hwp = self._need()
        try:
            return hwp.GetTextFile("TEXT", "") or ""
        except Exception:                                  # noqa: BLE001
            return self.get_text()

    def clear_target(self, whole):
        """바꿔 넣기 전에 지우기: whole=True 면 문서 전체, 아니면 지금 선택한 부분"""
        hwp = self._need()
        try:
            if whole:
                hwp.HAction.Run("SelectAll")
            hwp.HAction.Run("Delete")
            return True
        except Exception:                                  # noqa: BLE001
            return False

    def replace_selection(self, text):
        """지금 선택한 글을 text 로 바꾼다(줄바꿈은 문단 나눔으로). 선택한 부분이 없으면 오류"""
        if not self.get_selection_text().strip():
            raise BridgeError("바꿀 글을 먼저 선택(블록)해 주세요")
        if not self.clear_target(False):
            raise BridgeError("선택한 글을 지우지 못했어요")
        return self.insert_text(text.replace("\r", "").replace("\n", "\r\n"))

    def run_action(self, *names):
        """한글 명령을 이름으로 실행 — 이름 후보를 차례로 시도해 처음 되는 것을 쓴다. 하나도 안 되면 오류(어떤 명령이었는지 알림)"""
        hwp = self._need()
        last = None
        for nm in names:
            try:
                if hwp.HAction.Run(nm) is not False:
                    return nm
            except Exception as e:                         # noqa: BLE001
                last = e
        raise BridgeError("이 한글 버전에서는 안 되는 명령이에요 (%s)%s" % ("/".join(names), (": %s" % last) if last else ""))

    _LANGS = ("Hangul", "Latin", "Hanja", "Japanese", "Other", "Symbol", "User")

    def char_shape(self, **kw):
        """선택한(또는 커서 자리의) 글자 모양 바꾸기 — 참고 프로그램과 같은 방식(동작 만들기 → 항목 지정 → 실행).
        Spacing(자간 %), Ratio(장평 %), Height(pt), TextColor((r,g,b)), Font(글꼴 이름)"""
        hwp = self._need()
        try:
            act = hwp.CreateAction("CharShape")
            st = act.CreateSet()
            act.GetDefault(st)
            for k, v in kw.items():
                if k == "Spacing":
                    for lg in self._LANGS:
                        st.SetItem("Spacing" + lg, v)
                elif k == "Ratio":
                    for lg in self._LANGS:
                        st.SetItem("Ratio" + lg, v)
                elif k == "Height":
                    st.SetItem("Height", int(v * 100))
                elif k == "TextColor":
                    st.SetItem("TextColor", hwp.RGBColor(*v))
                elif k == "Font":
                    for lg in self._LANGS:
                        st.SetItem("FaceName" + lg, v)
                        st.SetItem("FontType" + lg, 1)
                else:
                    st.SetItem(k, v)
            if act.Execute(st) is False:
                raise BridgeError("글자 모양을 바꾸지 못했어요")
            return True
        except BridgeError:
            raise
        except Exception as e:                             # noqa: BLE001
            raise BridgeError("글자 모양을 바꾸지 못했어요: %s" % e)

    def cell_fill(self, r, g, b):
        """블록으로 잡은 표 칸의 배경색(참고 프로그램과 같은 CellFill 방식)"""
        hwp = self._need()
        try:
            ps = hwp.HParameterSet.HCellBorderFill
            hwp.HAction.GetDefault("CellFill", ps.HSet)
            ps.FillAttr.type = hwp.BrushType("NullBrush|WinBrush")
            ps.FillAttr.WinBrushFaceColor = hwp.RGBColor(r, g, b)
            ps.FillAttr.WinBrushHatchColor = hwp.RGBColor(r, g, b)
            ps.FillAttr.WinBrushFaceStyle = hwp.HatchStyle("None")
            ps.FillAttr.WindowsBrush = 1
            if hwp.HAction.Execute("CellFill", ps.HSet) is False:
                raise BridgeError("칸 배경색을 바꾸지 못했어요")
            return True
        except BridgeError:
            raise
        except Exception as e:                             # noqa: BLE001
            raise BridgeError("칸 배경색을 바꾸지 못했어요: %s" % e)

    def split_cell(self, rows, cols):
        """표 칸 나누기(대화상자 없이 줄·칸 수를 직접 지정)"""
        hwp = self._need()
        try:
            ps = hwp.HParameterSet.HTableSplitCell
            hwp.HAction.GetDefault("TableSplitCell", ps.HSet)
            ps.Rows, ps.Cols = int(rows), int(cols)
            if hwp.HAction.Execute("TableSplitCell", ps.HSet) is False:
                raise BridgeError("칸을 나누지 못했어요")
            return True
        except BridgeError:
            raise
        except Exception as e:                             # noqa: BLE001
            raise BridgeError("칸을 나누지 못했어요: %s" % e)

    def page_number(self, pos=5):
        """쪽 번호 넣기. pos: 1 왼쪽 위 · 2 가운데 위 · 3 오른쪽 위 · 4 왼쪽 아래 · 5 가운데 아래 · 6 오른쪽 아래, 0 없음"""
        hwp = self._need()
        try:
            ps = hwp.HParameterSet.HPageNumPos
            hwp.HAction.GetDefault("PageNumPos", ps.HSet)
            ps.DrawPos = pos
            if hwp.HAction.Execute("PageNumPos", ps.HSet) is False:
                raise BridgeError("쪽 번호를 넣지 못했어요")
            return True
        except BridgeError:
            raise
        except Exception as e:                             # noqa: BLE001
            raise BridgeError("쪽 번호를 넣지 못했어요: %s" % e)

    # ------------------------------------------------------------------ 레시피 재생용 기본 동작
    # 참고 프로그램(범정부 오피스)이 쓰는 한글 명령·항목 이름을 그대로 따른다(동작 만들기 → 항목 지정 → 실행).
    _ALIGN = {"JUSTIFY": "ParagraphShapeAlignJustify", "LEFT": "ParagraphShapeAlignLeft", "RIGHT": "ParagraphShapeAlignRight",
              "CENTER": "ParagraphShapeAlignCenter", "DISTRIBUTE": "ParagraphShapeAlignDistribute",
              "DISTRIBUTE_SPACE": "ParagraphShapeAlignDistribute"}

    def _act(self, name, items):
        hwp = self._need()
        act = hwp.CreateAction(name)
        st = act.CreateSet()
        act.GetDefault(st)
        for k, v in items:
            st.SetItem(k, v)
        return act.Execute(st)

    def rp_char(self, cs):
        """글자 모양: 글꼴·크기(1/100pt)·색·자간·장평 → 굵게·기울임·밑줄"""
        hwp = self._need()
        items = []
        for lg in self._LANGS:
            if cs.get("font"):
                items += [("FaceName" + lg, cs["font"]), ("FontType" + lg, 1)]
            items += [("Spacing" + lg, int(cs.get("spacing", 0))), ("Ratio" + lg, int(cs.get("ratio", 100)))]
        if cs.get("h"):
            items.append(("Height", int(cs["h"])))
        c = cs.get("color")
        if c:
            items.append(("TextColor", hwp.RGBColor(int(c[1:3], 16), int(c[3:5], 16), int(c[5:7], 16))))
        self._act("CharShape", items)
        hwp.HAction.Run("CharShapeNormal")
        for flag, act in (("bold", "CharShapeBold"), ("italic", "CharShapeItalic"), ("ul", "CharShapeUnderline")):
            if cs.get(flag):
                hwp.HAction.Run(act)

    def rp_para(self, ps):
        """문단 모양: 정렬(명령) + 왼쪽·오른쪽 여백·들여쓰기·문단 위 아래·줄 간격(항목, 값은 HWPUNIT×2)"""
        hwp = self._need()
        hwp.HAction.Run(self._ALIGN.get(ps.get("align", "JUSTIFY"), "ParagraphShapeAlignJustify"))
        self._act("ParagraphShape", [
            ("LeftMargin", int(ps.get("left", 0)) * 2), ("RightMargin", int(ps.get("right", 0)) * 2),
            ("Indentation", int(ps.get("indent", 0)) * 2), ("PrevSpacing", int(ps.get("prev", 0)) * 2),
            ("NextSpacing", int(ps.get("next", 0)) * 2), ("LineSpacingType", 0), ("LineSpacing", int(ps.get("ls", 160)))])

    def rp_table(self, rows, cols, colw, rowh):
        """표 만들기: 칸 너비·줄 높이(HWPUNIT) 지정, 글자처럼 취급. 만든 뒤 커서는 첫 칸"""
        hwp = self._need()
        ps = hwp.HParameterSet.HTableCreation
        hwp.HAction.GetDefault("TableCreate", ps.HSet)
        ps.Rows, ps.Cols = int(rows), int(cols)
        ps.WidthType, ps.HeightType = 2, 1
        ps.CreateItemArray("ColWidth", int(cols))
        for i in range(int(cols)):
            ps.ColWidth.SetItem(i, int(colw[i]))
        ps.CreateItemArray("RowHeight", int(rows))
        for i in range(int(rows)):
            ps.RowHeight.SetItem(i, int(rowh[i]))
        ps.TableProperties.TreatAsChar = 1
        if hwp.HAction.Execute("TableCreate", ps.HSet) is False:
            raise BridgeError("표를 만들지 못했어요")

    def rp_merge(self, rs, cs):
        """지금 칸에서 오른쪽 cs-1칸, 아래 rs-1칸까지 블록을 잡아 합친다(안 되면 그냥 둔다)"""
        try:
            self.run_action("TableCellBlock")
            for _ in range(cs - 1):
                self.run_action("TableCellBlockRight", "MoveRight")
            for _ in range(rs - 1):
                self.run_action("TableCellBlockDown", "MoveDown")
            self.run_action("TableMergeCell")
        except Exception:                                  # noqa: BLE001
            try:
                self.run_action("Cancel")
            except Exception:                              # noqa: BLE001
                pass

    def rp_cell_format(self, c):
        """칸 하나의 배경색·테두리(없음/굵기)·세로 맞춤 — 칸을 블록으로 잡고 지정한 뒤 선택을 푼다"""
        hwp = self._need()
        try:
            hwp.HAction.Run("TableCellBlock")
        except Exception:                                  # noqa: BLE001
            return
        try:
            fill = c.get("fill")
            if fill and fill.upper() != "#FFFFFF":
                self.cell_fill(int(fill[1:3], 16), int(fill[3:5], 16), int(fill[5:7], 16))
        except Exception:                                  # noqa: BLE001
            pass
        try:
            from .recipe import bd_index
            none_code = hwp.HwpLineType("None") if hasattr(hwp, "HwpLineType") else 0
            names = {"l": "Left", "r": "Right", "t": "Top", "b": "Bottom"}
            items = []
            for k, nm in names.items():
                typ, mm = (c.get("bd") or {}).get(k, ["SOLID", 0.12])
                if typ == "NONE":
                    items.append(("BorderType" + nm, none_code))
                else:
                    items.append(("BorderWidth" + nm, bd_index(mm)))
            self._act("CellBorderFill", items)
        except Exception:                                  # noqa: BLE001
            pass
        try:
            va = {"TOP": 0, "CENTER": 1, "BOTTOM": 2}.get(c.get("va", "TOP"), 0)
            ps = hwp.HParameterSet.HShapeObject
            hwp.HAction.GetDefault("TablePropertyDialog", ps.HSet)
            ps.HSet.SetItem("ShapeType", 3)
            ps.HSet.SetItem("ShapeCellSize", 0)
            ps.ShapeTableCell.VertAlign = va
            hwp.HAction.Execute("TablePropertyDialog", ps.HSet)
        except Exception:                                  # noqa: BLE001
            pass
        try:                                               # 칸 안 여백(참고 프로그램의 '셀여백지정' 방식)
            mg = c.get("margin")
            if mg:
                ps = hwp.HParameterSet.HShapeObject
                hwp.HAction.GetDefault("TablePropertyDialog", ps.HSet)
                ps.HSet.SetItem("ShapeType", 3)
                ps.HSet.SetItem("ShapeCellSize", 0)
                ps.ShapeTableCell.HasMargin = 1
                ps.ShapeTableCell.MarginLeft, ps.ShapeTableCell.MarginRight = int(mg[0]), int(mg[1])
                ps.ShapeTableCell.MarginTop, ps.ShapeTableCell.MarginBottom = int(mg[2]), int(mg[3])
                hwp.HAction.Execute("TablePropertyDialog", ps.HSet)
        except Exception:                                  # noqa: BLE001
            pass
        try:
            hwp.HAction.Run("Cancel")
        except Exception:                                  # noqa: BLE001
            pass

    def rp_table_leave(self):
        """표에서 나와 그 표가 든 문단 끝으로(참고 프로그램의 '표나가기')"""
        hwp = self._need()
        hwp.HAction.Run("CloseEx")
        hwp.HAction.Run("MoveLineEnd")

    def rp_picture(self, path):
        hwp = self._need()
        hwp.InsertPicture(path, 1, 3, 0, 0, 0)
        hwp.HAction.Run("ParagraphShapeAlignCenter")

    # ------------------------------------------------------------------ 엔진 검증용
    def save_hwpx(self, path):
        """지금 문서를 hwpx 로 저장(참고 프로그램의 FileSaveAs_S 방식, 안 되면 SaveAs)"""
        hwp = self._need()
        path = os.path.abspath(path)
        try:
            act = hwp.CreateAction("FileSaveAs_S")
            st = act.CreateSet()
            act.GetDefault(st)
            st.SetItem("FileName", path)
            st.SetItem("Format", "HWPX")
            if act.Execute(st) is not False and os.path.exists(path):
                return True
        except Exception:                                  # noqa: BLE001
            pass
        try:
            hwp.SaveAs(path, "HWPX", "")
            return os.path.exists(path)
        except Exception as e:                             # noqa: BLE001
            raise BridgeError("hwpx 로 저장하지 못했어요: %s" % e)

    def scratch_build(self, recipe_, images, page, out_path):
        """새 문서를 열어 레시피를 명령으로 만들고 out_path(hwpx)로 저장한 뒤 그 문서를 닫는다 — 지금 열어 둔 문서는 건드리지 않는다"""
        from .recipe import Player
        hwp = self._need()
        hwp.HAction.Run("FileNew")
        try:
            if page:
                self.apply_page_setup(page)
            Player(self, recipe_, images or {}, fresh=True, first_page=True).run()
            if not self.save_hwpx(out_path):
                raise BridgeError("검증용 파일을 저장하지 못했어요")
        finally:
            try:
                hwp.Clear(1)                               # 저장 여부를 묻지 않고 비운 뒤 닫는다
            except Exception:                              # noqa: BLE001
                pass
            try:
                hwp.HAction.Run("FileClose")
            except Exception:                              # noqa: BLE001
                pass

    def insert_table(self, rows, cols):
        """커서 위치에 rows × cols 표 만들기(한글 표 만들기 명령)"""
        hwp = self._need()
        try:
            ps = hwp.HParameterSet.HTableCreation
            hwp.HAction.GetDefault("TableCreate", ps.HSet)
            ps.Rows, ps.Cols = int(rows), int(cols)
            for name, val in (("WidthType", 0), ("HeightType", 0)):      # 단 너비에 맞춤·내용에 맞는 높이(안 되면 기본값)
                try:
                    setattr(ps, name, val)
                except Exception:                          # noqa: BLE001
                    pass
            if not hwp.HAction.Execute("TableCreate", ps.HSet):
                raise BridgeError("표를 만들지 못했어요")
            return True
        except BridgeError:
            raise
        except Exception as e:                             # noqa: BLE001
            raise BridgeError("표를 만들지 못했어요: %s" % e)

    def doc_key(self):
        """지금 열려 있는 문서를 가리키는 값(문서가 바뀌었는지 알아보는 용). 알 수 없으면 None"""
        try:
            return str(self._need().XHwpDocuments.Active_XHwpDocument.DocumentID)
        except Exception:                                  # noqa: BLE001
            return None

    def at_page_top(self):
        """커서가 어떤 쪽의 맨 위에 있는지. '쪽 처음으로'(MovePageBegin)를 해 보고 위치가 그대로면 맨 위다(바로 원래 자리로 되돌린다).
        앞 양식이 쪽을 꽉 채워 빈 줄이 새 쪽으로 넘어간 경우, 다음 양식의 '쪽 나눔'이 빈 쪽을 하나 더 만드는 것을 막는 데 쓴다."""
        hwp = self._need()
        try:
            p0 = hwp.GetPos()
            if not p0:
                return False
            ok = hwp.HAction.Run("MovePageBegin")
            p1 = hwp.GetPos()
            try:
                hwp.SetPos(*p0)
            except Exception:                              # noqa: BLE001
                pass
            return ok is not False and tuple(p0) == tuple(p1)
        except Exception:                                  # noqa: BLE001
            return False

    def break_page(self):
        hwp = self._need()
        try:
            hwp.HAction.Run("BreakPage")
            return True
        except Exception as e:                             # noqa: BLE001
            raise BridgeError("쪽 나누기를 하지 못했어요: %s" % e)

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
        """커서 뒤에 글자가 하나도 없는지(= 사실상 문서 끝). 한글은 끼워 넣은 뒤 커서 바로 뒤에 빈 문단이 남아 있어서
        '커서 위치 == 문서 맨 끝 위치' 로 비교하면 두 번째 양식부터 늘 아니라고 나온다 → 커서부터 끝까지 선택해 글자를 본다."""
        hwp = self._need()
        try:
            p0 = hwp.GetPos()
            try:
                hwp.HAction.Run("MoveSelDocEnd")
                rest = hwp.GetTextFile("TEXT", "saveblock") or ""
            finally:
                try:
                    hwp.HAction.Run("Cancel")
                except Exception:                          # noqa: BLE001
                    pass
                hwp.SetPos(*p0)
            return not rest.strip()
        except Exception:                                  # noqa: BLE001
            pass
        try:                                               # 위가 안 되면 예전 방식(위치 비교)
            p0 = hwp.GetPos()
            hwp.HAction.Run("MoveDocEnd")
            p1 = hwp.GetPos()
            hwp.SetPos(*p0)
            return tuple(p0) == tuple(p1)
        except Exception:                                  # noqa: BLE001
            return False

    def apply_page_setup(self, d):
        """용지 크기·여백(HWPUNIT)을 지금 문서에 적용. 항목마다 따로 지정해서, 일부(용지 크기 등)가 안 먹어도 여백은 적용되게 한다
        (참고 프로그램의 PageSetup 방식: HSecDef.PageDef 여백 + ApplyTo=3). 하나라도 적용하면 True"""
        hwp = self._need()
        try:
            ps = hwp.HParameterSet.HSecDef
            hwp.HAction.GetDefault("PageSetup", ps.HSet)
            pd = ps.PageDef
        except Exception:                                  # noqa: BLE001
            return False
        n = 0
        for attr, key in (("LeftMargin", "left"), ("RightMargin", "right"), ("TopMargin", "top"), ("BottomMargin", "bottom"),
                          ("HeaderLen", "header"), ("FooterLen", "footer"), ("GutterLen", "gutter"),
                          ("PaperWidth", "width"), ("PaperHeight", "height")):
            try:
                setattr(pd, attr, int(d[key]))
                n += 1
            except Exception:                              # noqa: BLE001
                pass
        try:
            pd.Landscape = 1 if d.get("landscape") else 0
        except Exception:                                  # noqa: BLE001
            pass
        for k, v in (("ApplyClass", 24), ("ApplyTo", 3)):
            try:
                ps.HSet.SetItem(k, v)
            except Exception:                              # noqa: BLE001
                pass
        try:
            return bool(hwp.HAction.Execute("PageSetup", ps.HSet)) and n > 0
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
        """글자 겹치기(네모·원 안의 글자). 참고 프로그램과 같은 방식: 글자를 먼저 입력해 그 글자를 블록으로 잡고
        ComposeChars(CircleType=모양, CheckCompose=0)를 실행한다. 이 방식이 안 되면 예전 방식들을 차례로 시도한다."""
        hwp = self._need()
        errs = []
        try:                                               # 1) 입력 → 방금 쓴 글자 블록 → 겹치기 (참고 프로그램 방식)
            self.insert_text(text)
            for _ in range(len(text)):
                hwp.HAction.Run("MoveSelLeft")
            act = hwp.CreateAction("ComposeChars")
            st = act.CreateSet()
            act.GetDefault(st)
            st.SetItem("CircleType", shape_no)
            st.SetItem("CheckCompose", 0)
            if act.Execute(st) is not False:
                try:
                    hwp.HAction.Run("MoveRight")           # 블록을 풀고 겹친 글자 뒤로
                except Exception:                          # noqa: BLE001
                    pass
                return True
            errs.append("ComposeChars 가 거절했어요")
            try:                                           # 되지 않았다면 입력해 둔 글자를 지운다
                hwp.HAction.Run("Delete")
            except Exception:                              # noqa: BLE001
                pass
        except Exception as e:                             # noqa: BLE001
            errs.append(str(e))
            try:
                hwp.HAction.Run("Cancel")
            except Exception:                              # noqa: BLE001
                pass
        for use_hset in (True, False):                     # 2) 예전 방식: 글자·크기를 직접 지정
            try:
                if use_hset:
                    ps = hwp.HParameterSet.HChCompose
                    hwp.HAction.GetDefault("ComposeChars", ps.HSet)
                    ps.Chars = text
                    ps.CharSize = char_size
                    ps.CircleType = shape_no
                    return bool(hwp.HAction.Execute("ComposeChars", ps.HSet))
                act = hwp.CreateAction("ComposeChars")
                st = act.CreateSet()
                act.GetDefault(st)
                st.SetItem("Chars", text)
                st.SetItem("CharSize", char_size)
                st.SetItem("CircleType", shape_no)
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
