#!/usr/bin/env python3
"""웹앱 빌드 뒤처리: (1) Flutter 서비스 워커가 '/ukukuk/' 같은 하위 주소에서도 오프라인으로 동작하게 고치고,
(2) HTML 렌더러가 쓰지 않는 canvaskit 폴더(약 25MB)를 지운다.  사용: python3 tools/patch_sw.py build/web"""
import os
import re
import shutil
import sys

root = sys.argv[1] if len(sys.argv) > 1 else "build/web"
p = os.path.join(root, "flutter_service_worker.js")
s = open(p, encoding="utf-8").read()


def sub(old, new, count=1):
    global s
    if old not in s:
        raise SystemExit(f"패치할 부분을 찾지 못함: {old[:60]}")
    s = s.replace(old, new, count)


# 요청 주소를 서비스 워커가 맡은 범위(scope) 기준으로 읽는다 — 앱이 사이트 맨 위가 아닌 하위 폴더에 있어도 맞는다
sub("""  var origin = self.location.origin;
  var key = event.request.url.substring(origin.length + 1);
  // Redirect URLs to the index.html""", """  var origin = self.location.origin;
  var scope = self.registration.scope;
  var key = event.request.url.startsWith(scope) ? event.request.url.substring(scope.length) : '\\u0000';
  // Redirect URLs to the index.html""")
sub("if (event.request.url == origin || event.request.url.startsWith(origin + '/#') || key == '') {",
    "if (event.request.url == origin || event.request.url == scope.slice(0, -1) || event.request.url.startsWith(origin + '/#') || key == '') {")
sub("""      var origin = self.location.origin;
      for (var request of await contentCache.keys()) {
        var key = request.url.substring(origin.length + 1);""", """      var scope = self.registration.scope;
      for (var request of await contentCache.keys()) {
        var key = request.url.startsWith(scope) ? request.url.substring(scope.length) : request.url;""")
# 인터넷이 없을 때: 처음 주소가 보관돼 있지 않아도 index.html 로 연다
sub("""          if (response != null) {
            return response;
          }
          throw error;""", """          if (response != null) {
            return response;
          }
          return cache.match(self.registration.scope + 'index.html').then((r) => {
            if (r != null) return r;
            throw error;
          });""")
# canvaskit 은 쓰지 않는다
s = re.sub(r'"canvaskit/[^"]*": "[0-9a-f]+",?\n', "", s)
s = s.replace(',\n};', '\n};')
open(p, "w", encoding="utf-8").write(s)
shutil.rmtree(os.path.join(root, "canvaskit"), ignore_errors=True)
print("서비스 워커 패치 완료")
