# -*- coding: utf-8 -*-
"""프로그램을 하나만 실행: 이미 떠 있으면 새로 띄우지 않고 기존 창을 앞으로 불러온다.
127.0.0.1 의 작은 포트를 '자리 표시'로 쓴다(첫 실행이 포트를 잡고, 두 번째 실행은 거기에 '열어 줘'라고 알리고 끝낸다)."""
import queue
import socket
import sys
import threading

PORT = 47831


class SingleInstance:
    def __init__(self, port=PORT):
        self.port = port
        self.q = queue.Queue()
        self.sock = None

    def acquire(self):
        """첫 실행이면 True(이후 다른 실행의 신호를 q 로 받음), 이미 실행 중이면 신호를 보내고 False"""
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        if sys.platform != "win32":              # 리눅스: 방금 닫은 연결의 대기 상태 때문에 바로 다시 못 잡는 일 방지(윈도우에선 켜면 중복 실행이 가능해져 쓰지 않음)
            s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        try:
            s.bind(("127.0.0.1", self.port))
            s.listen(4)
        except OSError:
            s.close()
            try:
                with socket.create_connection(("127.0.0.1", self.port), timeout=1.5) as c:
                    c.sendall(b"show")
            except OSError:
                pass
            return False
        self.sock = s
        threading.Thread(target=self._serve, daemon=True).start()
        return True

    def _serve(self):
        while True:
            try:
                conn, _ = self.sock.accept()
            except OSError:
                return
            try:
                conn.settimeout(1.0)
                data = conn.recv(16)
                if data.startswith(b"show"):
                    self.q.put("show")
            except OSError:
                pass
            finally:
                conn.close()

    def close(self):
        try:
            self.sock.close()
        except Exception:                                  # noqa: BLE001
            pass
