# -*- coding: utf-8 -*-
"""서무 업무 자동화 — 실행 파일(exe)용 런처

하는 일은 단순합니다.
  1) 안에 들어 있는 서무업무자동화(index.html)를 내 PC의 프로그램 폴더에 풀어 놓고
  2) Microsoft Edge(없으면 Chrome)를 '앱 창' 모드로 열어 그 화면을 보여 줍니다.
앱 창은 주소창·탭 없이 독립 프로그램처럼 보이고, 작업 내용(부서원 명단 등)은
%APPDATA%\\SeomuSuite\\profile 에 따로 저장됩니다. 브라우저 방문 기록과는 섞이지 않습니다.

  SUITE_BROWSER=경로   브라우저를 직접 지정 (시험용)
  --selftest           창을 열지 않고 준비 상태만 점검해서 출력
"""
import os
import pathlib
import shutil
import subprocess
import sys
import webbrowser

APP_NAME = "서무 업무 자동화"
DATA_NAME = "SeomuSuite"


def resource(name):
    base = getattr(sys, "_MEIPASS", None) or os.path.dirname(os.path.abspath(__file__))
    return os.path.join(base, name)


def data_dir():
    root = os.environ.get("APPDATA") or os.environ.get("XDG_CONFIG_HOME") or os.path.join(os.path.expanduser("~"), ".config")
    d = os.path.join(root, DATA_NAME)
    os.makedirs(d, exist_ok=True)
    return d


def find_browser():
    env = os.environ.get("SUITE_BROWSER")
    if env and os.path.exists(env):
        return env
    roots = [os.environ.get(k) for k in ("PROGRAMFILES(X86)", "PROGRAMFILES", "LOCALAPPDATA")]
    rels = [r"Microsoft\Edge\Application\msedge.exe", r"Google\Chrome\Application\chrome.exe"]
    for rel in rels:                      # Edge 먼저, 그다음 Chrome
        for root in roots:
            if root and os.path.exists(os.path.join(root, rel)):
                return os.path.join(root, rel)
    for name in ("msedge", "chrome", "chromium"):
        p = shutil.which(name)
        if p:
            return p
    return None


def alert(msg):
    try:
        import ctypes
        ctypes.windll.user32.MessageBoxW(0, msg, APP_NAME, 0x10)
    except Exception:
        print(msg, file=sys.stderr)


def prepare():
    """index.html 을 프로그램 폴더에 풀고 (주소, 프로필 폴더, 브라우저)를 돌려준다"""
    d = data_dir()
    app_dir = os.path.join(d, "app")
    os.makedirs(app_dir, exist_ok=True)
    index = os.path.join(app_dir, "index.html")
    shutil.copyfile(resource("index.html"), index)      # 새 버전 exe 로 바꾸면 화면도 함께 갱신
    profile = os.path.join(d, "profile")
    os.makedirs(profile, exist_ok=True)
    return pathlib.Path(index).as_uri(), profile, find_browser()


def main():
    url, profile, browser = prepare()
    if "--selftest" in sys.argv:
        print("url     :", url)
        print("profile :", profile)
        print("browser :", browser)
        sys.exit(0 if browser else 1)

    if not browser:                                        # 앱 창을 못 열면 기본 브라우저로라도
        webbrowser.open(url)
        return
    cmd = [
        browser,
        "--app=" + url,
        "--user-data-dir=" + profile,
        "--window-size=1440,920",
        "--no-first-run",
        "--no-default-browser-check",
        "--disable-features=Translate,msEdgeSidebarV2",
        "--lang=ko",
    ]
    try:
        subprocess.Popen(cmd, close_fds=True)
    except OSError as e:
        alert("프로그램 창을 열지 못했습니다.\n\n" + str(e))
        webbrowser.open(url)


if __name__ == "__main__":
    main()
