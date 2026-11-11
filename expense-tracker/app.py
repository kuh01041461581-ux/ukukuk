"""일상경비·공공요금 관리 — 윈도우 프로그램(exe) 본체.

화면(ui.html)을 pywebview 창에 띄우고, 데이터는 %APPDATA%\\일상경비관리\\data.json 에 저장합니다.
엑셀 가져오기·백업 저장 같은 파일 작업만 이 파일이 맡습니다.
"""
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
        try:
            r = self._dialog("SAVE", save_filename=name)
            if not r:
                return {"ok": False}
            path = r if isinstance(r, str) else r[0]
            Path(path).write_text(text, encoding="utf-8")
            return {"ok": True, "path": str(path)}
        except Exception as e:  # noqa
            return {"ok": False, "error": str(e)}

    def import_xlsx(self, kind, year=2026):
        """kind='daily' 일상경비 엑셀 / 'util' 공공요금 엑셀 → 화면이 쓸 데이터"""
        try:
            r = self._dialog("OPEN", allow_multiple=False, file_types=("엑셀 파일 (*.xlsx;*.xlsm)", "모든 파일 (*.*)"))
            if not r:
                return {"cancel": True}
            return self.read_xlsx(kind, r[0] if not isinstance(r, str) else r, int(year))
        except Exception as e:  # noqa
            return {"error": f"엑셀을 읽지 못했습니다.\n{e}"}

    @staticmethod
    def read_xlsx(kind, path, year=2026):
        import seedlib
        if kind == "daily":
            lines, memo, asof = seedlib.parse_daily(path)
            if not lines:
                return {"error": "일상경비 표를 찾지 못했습니다. '사무관리비' 탭(연번·사업명·총예산액·지출액 열)이 있는 파일인지 확인하세요."}
            return {"lines": lines, "memo": memo, "asOf": asof}
        return {"util": seedlib.parse_util(path, year)}

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
