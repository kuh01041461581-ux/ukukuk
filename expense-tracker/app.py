"""일상경비·공공요금 관리 — 윈도우 프로그램(exe) 본체.

화면(ui.html)을 pywebview 창에 띄우고, 데이터는 %APPDATA%\\일상경비관리\\data.json 에 저장합니다.
엑셀 가져오기·백업 저장 같은 파일 작업만 이 파일이 맡습니다.
"""
import base64
import datetime
import json
import os
import shutil
import sys
import tempfile
from pathlib import Path

APP_NAME = "일상경비관리"


def data_dir() -> Path:
    base = os.environ.get("APPDATA") or os.environ.get("XDG_DATA_HOME") or str(Path.home() / ".local" / "share")
    d = Path(base) / APP_NAME
    d.mkdir(parents=True, exist_ok=True)
    return d


def resource(name: str) -> Path:
    base = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
    p = base / name
    return p if p.exists() else Path(__file__).resolve().parent / name


class Api:
    """화면(JS)에서 window.pywebview.api.* 로 부르는 함수들."""

    def __init__(self, root: Path | None = None):
        self.root = root or data_dir()
        self.window = None
        self.file = self.root / "data.json"

    # --- 저장 / 불러오기 ---
    def load_state(self):
        try:
            return self.file.read_text(encoding="utf-8") if self.file.exists() else ""
        except Exception as e:  # noqa
            bak = sorted((self.root / "backups").glob("data-*.json"))
            if bak:
                return bak[-1].read_text(encoding="utf-8")
            raise e

    def save_state(self, text):
        try:
            json.loads(text)  # 깨진 내용으로 덮어쓰지 않기
            tmp = self.file.with_suffix(".tmp")
            tmp.write_text(text, encoding="utf-8")
            if self.file.exists():
                self._daily_backup()
            os.replace(tmp, self.file)
            return {"ok": True}
        except Exception as e:  # noqa
            return {"ok": False, "error": str(e)}

    def _daily_backup(self):
        bdir = self.root / "backups"
        bdir.mkdir(exist_ok=True)
        dst = bdir / f"data-{datetime.date.today().isoformat()}.json"
        if not dst.exists():
            shutil.copy2(self.file, dst)
            for old in sorted(bdir.glob("data-*.json"))[:-30]:
                old.unlink(missing_ok=True)

    # --- 파일 대화상자 ---
    def _dialog(self, kind, **kw):
        import webview
        fd = getattr(webview, "FileDialog", None)
        mode = getattr(fd, kind) if fd else getattr(webview, kind + "_DIALOG")
        return self.window.create_file_dialog(mode, **kw)

    def save_text(self, name, text):
        """백업 파일 저장: 저장 대화상자를 띄우고, 안 뜨면 '내보내기' 폴더에 저장한 뒤 탐색기로 엽니다."""
        try:
            r = self._dialog("SAVE", save_filename=name)
            if r:
                path = r if isinstance(r, str) else r[0]
                Path(path).write_text(text, encoding="utf-8")
                return {"ok": True, "path": str(path)}
            return {"ok": False}
        except Exception as e:  # noqa
            self.log("save_text dialog", e)
        try:
            d = self.root / "내보내기"
            d.mkdir(exist_ok=True)
            path = d / name
            path.write_text(text, encoding="utf-8")
            if sys.platform.startswith("win"):
                os.startfile(str(d))  # type: ignore[attr-defined]
            return {"ok": True, "path": str(path)}
        except Exception as e:  # noqa
            self.log("save_text fallback", e)
            return {"ok": False, "error": str(e)}

    def import_xlsx_data(self, name, b64, year=2026, mode="auto"):
        """화면에서 읽어 보낸 엑셀 파일(base64)을 풀어 일상경비/공공요금을 자동으로 구분해 읽습니다."""
        tmp = None
        try:
            data = base64.b64decode(b64)
            with tempfile.NamedTemporaryFile(suffix=".xlsx", delete=False) as f:
                f.write(data)
                tmp = f.name
            return self.read_workbook(tmp, int(year), mode)
        except Exception as e:  # noqa
            self.log("import_xlsx_data", e)
            return {"error": f"엑셀 파일을 읽지 못했습니다 ({name}).\n{e}"}
        finally:
            if tmp:
                try:
                    os.unlink(tmp)
                except OSError:
                    pass

    @staticmethod
    def read_workbook(path, year=2026, mode="auto"):
        import openpyxl
        import seedlib
        wb = openpyxl.load_workbook(path)
        names = list(wb.sheetnames)
        if mode == "form":
            return {"form": seedlib.extract_form_rows(wb)}
        out, errs = {}, []
        try:
            led = seedlib.parse_ledger_xlsx(wb)
        except Exception as e:  # noqa
            led = None
            errs.append(str(e))
        if led:
            out["ledger"] = led
            return out
        daily_ws = seedlib.find_daily_sheet(wb)
        if daily_ws is not None:
            try:
                lines, memo, asof = seedlib.parse_daily(path)
                if lines:
                    out.update({"lines": lines, "memo": memo, "asOf": asof})
            except Exception as e:  # noqa
                errs.append(str(e))
        if str(year) in names:
            try:
                out["util"] = seedlib.parse_util(path, year)
            except Exception as e:  # noqa
                errs.append(str(e))
        if not out:
            rows = seedlib.extract_form_rows(wb)
            if rows:
                return {"form": rows}
            msg = "이 엑셀에서 일상경비('사무관리비' 탭)·공공요금('%s' 탭)·통계목별 조회('집행누계액' 열)를 찾지 못했습니다.\n탭 이름: %s" % (year, ", ".join(names[:10]))
            if errs:
                msg += "\n" + "\n".join(errs)
            return {"error": msg}
        return out

    def log(self, where, err):
        try:
            with open(self.root / "app.log", "a", encoding="utf-8") as f:
                f.write(f"{datetime.datetime.now().isoformat()} {where}: {err!r}\n")
        except Exception:  # noqa
            pass

    def open_data_dir(self):
        try:
            if sys.platform.startswith("win"):
                os.startfile(str(self.root))  # type: ignore[attr-defined]
            return {"ok": True}
        except Exception as e:  # noqa
            return {"ok": False, "error": str(e)}


def main():
    import webview
    root = data_dir()
    api = Api(root)
    # 화면 파일을 데이터 폴더에 풀어 file:// 로 연다(PDF 읽기용 작업자 스레드가 정상 동작)
    ui = root / "ui.html"
    shutil.copyfile(resource("app/ui.html"), ui)
    win = webview.create_window("일상경비·공공요금 관리", url=ui.as_uri(), js_api=api, width=1480, height=940, min_size=(1000, 650))
    api.window = win
    webview.start(storage_path=str(root / "webview"))


if __name__ == "__main__":
    main()
