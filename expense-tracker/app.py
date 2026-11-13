"""일상경비·공공요금 관리 — 윈도우 프로그램(exe) 본체.

화면(ui.html)을 pywebview 창에 띄우고, 데이터는 %APPDATA%\\일상경비관리\\data.json 에 저장합니다.
창이 안 뜨거나 응답이 없으면 자동으로 '브라우저 모드'(내 PC 안에서만 도는 작은 서버 + 기본 브라우저)로 바꿔 실행합니다.

  ExpenseManager.exe              일반 실행 (창)
  ExpenseManager.exe --browser    브라우저 모드로 실행
  ExpenseManager.exe --selftest   서버·화면 파일 점검 후 종료 (빌드 점검용)
  ExpenseManager.exe --selftest-gui  창을 실제로 띄워 화면이 연결되는지 점검 후 종료 (빌드 점검용)
"""
import base64
import datetime
import json
import os
import secrets
import shutil
import sys
import tempfile
import threading
import time
from pathlib import Path

APP_NAME = "일상경비관리"
T0 = time.time()


def data_dir() -> Path:
    base = os.environ.get("APPDATA") or os.environ.get("XDG_DATA_HOME") or str(Path.home() / ".local" / "share")
    d = Path(base) / APP_NAME
    d.mkdir(parents=True, exist_ok=True)
    return d


def ascii_dir() -> Path:
    """WebView2 프로필·화면 파일을 둘 영문 경로 (한글 경로에서 생기는 문제를 피함)."""
    for key in ("LOCALAPPDATA", "PROGRAMDATA", "APPDATA", "TEMP"):
        base = os.environ.get(key)
        if base and base.isascii():
            try:
                d = Path(base) / "ExpenseManager"
                d.mkdir(parents=True, exist_ok=True)
                return d
            except Exception:  # noqa
                continue
    return data_dir()


def resource(name: str) -> Path:
    base = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
    p = base / name
    return p if p.exists() else Path(__file__).resolve().parent / name


def log(msg):
    """시작·오류 기록 (멈췄을 때 원인을 찾기 위한 app.log)"""
    try:
        with open(data_dir() / "app.log", "a", encoding="utf-8") as f:
            f.write(f"{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')} +{time.time() - T0:5.1f}s {msg}\n")
    except Exception:  # noqa
        pass


class Api:
    """화면(JS)에서 window.pywebview.api.* 로 부르는 함수들.
    주의: 밑줄(_)로 시작하지 않는 속성은 pywebview 가 시작할 때 전부 따라 들어가 살펴보므로(창·경로 객체가
    있으면 멈출 수 있음) 내부 값은 모두 밑줄로 시작시킨다."""

    def __init__(self, root: Path | None = None):
        self._root = root or data_dir()
        self._window = None
        self._file = self._root / "data.json"
        self._loaded = False
        self._last_ping = 0.0

    # --- 저장 / 불러오기 ---
    def load_state(self):
        if not self._loaded:
            log("화면이 연결됨 (load_state)")
        self._loaded = True
        try:
            return self._file.read_text(encoding="utf-8") if self._file.exists() else ""
        except Exception as e:  # noqa
            bak = sorted((self._root / "backups").glob("data-*.json"))
            if bak:
                return bak[-1].read_text(encoding="utf-8")
            raise e

    def save_state(self, text):
        try:
            json.loads(text)  # 깨진 내용으로 덮어쓰지 않기
            tmp = self._file.with_suffix(".tmp")
            tmp.write_text(text, encoding="utf-8")
            if self._file.exists():
                self._daily_backup()
            os.replace(tmp, self._file)
            return {"ok": True}
        except Exception as e:  # noqa
            return {"ok": False, "error": str(e)}

    def _daily_backup(self):
        bdir = self._root / "backups"
        bdir.mkdir(exist_ok=True)
        dst = bdir / f"data-{datetime.date.today().isoformat()}.json"
        if not dst.exists():
            shutil.copy2(self._file, dst)
            for old in sorted(bdir.glob("data-*.json"))[:-30]:
                old.unlink(missing_ok=True)

    def ping(self):
        self._last_ping = time.time()
        return {"ok": True}

    # --- 파일 대화상자 (창 모드에서만) ---
    def _dialog(self, kind, **kw):
        import webview
        fd = getattr(webview, "FileDialog", None)
        mode = getattr(fd, kind) if fd else getattr(webview, kind + "_DIALOG")
        return self._window.create_file_dialog(mode, **kw)

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
            self._log("save_text dialog", e)
        try:
            d = self._root / "내보내기"
            d.mkdir(exist_ok=True)
            path = d / name
            path.write_text(text, encoding="utf-8")
            if sys.platform.startswith("win"):
                os.startfile(str(d))  # type: ignore[attr-defined]
            return {"ok": True, "path": str(path)}
        except Exception as e:  # noqa
            self._log("save_text fallback", e)
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
            self._log("import_xlsx_data", e)
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

    def _log(self, where, err):
        log(f"{where}: {err!r}")

    def snapshot(self, label="전환전"):
        """부서(과)를 바꾸기 전 등에 현재 데이터를 backups 폴더에 따로 복사해 둡니다."""
        try:
            if not self._file.exists():
                return {"ok": True, "path": ""}
            bdir = self._root / "backups"
            bdir.mkdir(exist_ok=True)
            stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
            dst = bdir / f"data-{stamp}-{label}.json"
            shutil.copy2(self._file, dst)
            return {"ok": True, "path": str(dst)}
        except Exception as e:  # noqa
            self._log("snapshot", e)
            return {"ok": False, "error": str(e)}

    def open_data_dir(self):
        try:
            if sys.platform.startswith("win"):
                os.startfile(str(self._root))  # type: ignore[attr-defined]
            return {"ok": True}
        except Exception as e:  # noqa
            return {"ok": False, "error": str(e)}


# ---------------------------------------------------------------- 브라우저 모드 (작은 로컬 서버)
HTTP_METHODS = {"load_state", "save_state", "import_xlsx_data", "snapshot", "open_data_dir", "ping"}
SHIM = """window.__HTTP__=true;window.__TOKEN__=%s;
(function(){var call=function(n,a){return fetch('/api/'+n,{method:'POST',headers:{'X-Token':window.__TOKEN__,'Content-Type':'application/json'},body:JSON.stringify(a)}).then(function(r){return r.json();});};
window.pywebview={api:{load_state:function(){return call('load_state',[]);},save_state:function(t){return call('save_state',[t]);},
import_xlsx_data:function(n,b,y,m){return call('import_xlsx_data',[n,b,y,m]);},snapshot:function(l){return call('snapshot',[l]);},
open_data_dir:function(){return call('open_data_dir',[]);},ping:function(){return call('ping',[]);}}};
setInterval(function(){window.pywebview.api.ping().catch(function(){});},4000);})();"""


def start_http(api, ui_path: Path, idle_exit=True):
    """127.0.0.1 에서만 듣는 작은 서버를 띄운다. (서버 객체, 주소) 반환"""
    import http.server
    import socketserver

    token = secrets.token_urlsafe(16)

    class H(http.server.BaseHTTPRequestHandler):
        def log_message(self, *a, **k):  # 창 없는 프로그램: 콘솔 출력 금지
            pass

        def _send(self, code, body, ctype):
            self.send_response(code)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):  # noqa
            if self.path.split("?")[0] in ("/", "/index.html", "/ui.html"):
                html = ui_path.read_text(encoding="utf-8")
                html = html.replace("window.__EXE__=true;", "window.__EXE__=true;" + SHIM % json.dumps(token), 1)
                self._send(200, html.encode("utf-8"), "text/html; charset=utf-8")
            else:
                self._send(404, b"not found", "text/plain")

        def do_POST(self):  # noqa
            name = self.path.rsplit("/", 1)[-1]
            if self.headers.get("X-Token") != token or name not in HTTP_METHODS:
                self._send(403, b'{"error":"forbidden"}', "application/json")
                return
            try:
                n = int(self.headers.get("Content-Length") or 0)
                args = json.loads(self.rfile.read(n) or b"[]")
                res = getattr(api, name)(*args)
                self._send(200, json.dumps(res, ensure_ascii=False).encode("utf-8"), "application/json")
            except Exception as e:  # noqa
                log(f"http {name}: {e!r}")
                self._send(500, json.dumps({"error": str(e)}).encode("utf-8"), "application/json")

    class S(socketserver.ThreadingMixIn, http.server.HTTPServer):
        daemon_threads = True

    srv = S(("127.0.0.1", 0), H)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    url = f"http://127.0.0.1:{srv.server_address[1]}/"
    log(f"브라우저 모드 서버 시작 {url}")
    if idle_exit:
        def watch():
            while True:
                time.sleep(5)
                started = api._last_ping > 0
                if (started and time.time() - api._last_ping > 45) or (not started and time.time() - T0 > 180):
                    log("브라우저 화면이 닫혀 종료")
                    srv.shutdown()
                    os._exit(0)
        threading.Thread(target=watch, daemon=True).start()
    return srv, url


def run_browser(api, ui_path):
    import webbrowser
    srv, url = start_http(api, ui_path, idle_exit=True)
    if not os.environ.get("EXPENSE_NO_BROWSER"):
        webbrowser.open(url)
    while True:
        time.sleep(3600)


# ---------------------------------------------------------------- 창 모드
def webview2_present() -> bool:
    if not sys.platform.startswith("win"):
        return True
    try:
        import winreg
        guid = r"{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
        for hive, sub in ((winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\\" + guid),
                          (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Microsoft\EdgeUpdate\Clients\\" + guid),
                          (winreg.HKEY_CURRENT_USER, r"Software\Microsoft\EdgeUpdate\Clients\\" + guid)):
            try:
                with winreg.OpenKey(hive, sub) as k:
                    pv, _ = winreg.QueryValueEx(k, "pv")
                    if pv and pv != "0.0.0.0":
                        return True
            except OSError:
                continue
    except Exception as e:  # noqa
        log(f"WebView2 확인 실패(있는 것으로 가정): {e!r}")
        return True
    return False


def prepare_ui(prof: Path) -> Path:
    """화면 파일을 영문 경로에 풀어 둔다 (바뀐 때만 복사)."""
    src = resource("app/ui.html")
    ui = prof / "ui.html"
    if not ui.exists() or ui.stat().st_size != src.stat().st_size or ui.stat().st_mtime < src.stat().st_mtime - 1:
        shutil.copyfile(src, ui)
    return ui


def run_window(api, prof: Path, ui: Path, selftest=False):
    import webview
    win = webview.create_window("일상경비·공공요금 관리", url=ui.as_uri(), js_api=api, width=1480, height=940, min_size=(1000, 650), background_color="#f3f4f6")
    api._window = win
    log("창 만듦, 시작")

    def watchdog():
        for _ in range(60 if selftest else 40):     # 최대 40초(점검 때 60초) 기다림
            time.sleep(1)
            if api._loaded:
                if selftest:
                    log("점검: 화면 연결 확인 → 종료")
                    os._exit(0)
                return
        log("화면이 제한 시간 안에 연결되지 않음")
        if selftest:
            os._exit(2)
        try:   # 멈춘 창 대신 브라우저로 이어서 실행
            import webbrowser
            _, url = start_http(api, ui, idle_exit=False)
            if not os.environ.get("EXPENSE_NO_BROWSER"):
                webbrowser.open(url)
            if sys.platform.startswith("win"):
                import ctypes
                ctypes.windll.user32.MessageBoxW(0, "프로그램 창이 응답하지 않아 브라우저로 열었습니다.\n\n창이 멈춰 있으면 닫아 주세요. 다음부터는 'Run-in-Browser.bat'으로 실행하면 됩니다.", "일상경비·공공요금 관리", 0x40)
        except Exception as e:  # noqa
            log(f"브라우저 전환 실패: {e!r}")

    threading.Thread(target=watchdog, daemon=True).start()
    webview.start(storage_path=str(prof / "webview"), private_mode=False, gui="edgechromium" if sys.platform.startswith("win") else None)


def main():
    argv = sys.argv[1:]
    log(f"시작 argv={argv} frozen={getattr(sys, 'frozen', False)}")
    sys.excepthook = lambda t, v, tb: log(f"처리 안 된 오류: {t.__name__}: {v}")
    try:
        threading.excepthook = lambda a: log(f"스레드 오류: {a.exc_type.__name__}: {a.exc_value}")
    except Exception:  # noqa
        pass
    api = Api()
    prof = ascii_dir()
    ui = prepare_ui(prof)
    if "--selftest" in argv:                       # 빌드 점검: 서버와 화면 파일이 정상인지
        import urllib.request
        srv, url = start_http(api, ui, idle_exit=False)
        html = urllib.request.urlopen(url, timeout=15).read().decode("utf-8")
        ok = "window.__HTTP__=true" in html and "일상경비" in html
        tok = html.split("window.__TOKEN__=")[1].split(";")[0].strip('"')
        req = urllib.request.Request(url + "api/ping", data=b"[]", headers={"X-Token": tok, "Content-Type": "application/json"})
        ok = ok and json.loads(urllib.request.urlopen(req, timeout=15).read()).get("ok") is True
        log(f"점검(서버): {'통과' if ok else '실패'}")
        sys.exit(0 if ok else 1)
    if "--browser" in argv:
        run_browser(api, ui)
        return
    if not webview2_present():
        log("WebView2 런타임이 없어 브라우저 모드로 실행")
        run_browser(api, ui)
        return
    try:
        run_window(api, prof, ui, selftest="--selftest-gui" in argv)
    except Exception as e:  # noqa
        log(f"창 모드 실패 → 브라우저 모드: {e!r}")
        run_browser(api, ui)


if __name__ == "__main__":
    main()
