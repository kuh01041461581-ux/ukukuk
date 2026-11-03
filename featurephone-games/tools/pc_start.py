#!/usr/bin/env python3
"""PC 실행기: 이 파일이 있는 폴더의 'www' 를 로컬 서버(127.0.0.1)로 열고 브라우저를 띄운다.
외부 인터넷에는 접속하지 않는다. 종료는 창을 닫거나 Ctrl+C."""
import http.server, os, socketserver, sys, threading, webbrowser

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'www')
PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8765

class Handler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map, '.wasm': 'application/wasm', '.js': 'text/javascript'}
    def __init__(self, *a, **k): super().__init__(*a, directory=ROOT, **k)
    def log_message(self, *a): pass
    def end_headers(self):
        self.send_header('Cache-Control', 'no-cache'); super().end_headers()

socketserver.ThreadingTCPServer.allow_reuse_address = True
with socketserver.ThreadingTCPServer(('127.0.0.1', PORT), Handler) as srv:
    url = f'http://127.0.0.1:{PORT}/index.html'
    print('피처폰 게임 실행 중:', url, '\n(이 창을 닫으면 종료됩니다)')
    threading.Timer(0.8, lambda: webbrowser.open(url)).start()
    try: srv.serve_forever()
    except KeyboardInterrupt: pass
