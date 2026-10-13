# -*- coding: utf-8 -*-
"""인터넷 동기화: 암호화한 일정 데이터를 GitHub 비공개 저장소의 파일 하나로 주고받는다.
 - 데이터는 AES-256-GCM 으로 암호화되어 저장소에는 알아볼 수 없는 바이트만 올라간다.
 - 암호 키는 PC 와 폰의 QR 연결 때만 오가며 GitHub 에는 올라가지 않는다.
 - 두 기기가 동시에 올려도 항목별 '나중에 수정한 쪽이 이김' 규칙(core.merge_state)으로 합쳐진다."""
import base64
import json
import os
import urllib.error
import urllib.request

from cryptography.hazmat.primitives.ciphers.aead import AESGCM

MAGIC = b"DC1"
API = "https://api.github.com"


class Conflict(Exception):
    pass


class SyncError(Exception):
    pass


def new_key():
    return base64.b64encode(os.urandom(32)).decode()


def encrypt(key_b64, obj):
    key = base64.b64decode(key_b64)
    nonce = os.urandom(12)
    raw = json.dumps(obj, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    return MAGIC + nonce + AESGCM(key).encrypt(nonce, raw, None)   # 암호문 뒤에 인증 태그 16바이트


def decrypt(key_b64, blob):
    if blob[:3] != MAGIC:
        raise SyncError("알 수 없는 데이터 형식")
    key = base64.b64decode(key_b64)
    try:
        return json.loads(AESGCM(key).decrypt(blob[3:15], blob[15:], None).decode("utf-8"))
    except Exception:
        raise SyncError("복호화 실패: 암호 키가 다릅니다 (QR 로 다시 연결하세요)")


def canonical(state):
    """두 상태가 같은지 비교하기 위한 문자열"""
    s = {"items": sorted(state.get("items", []), key=lambda r: r.get("id", "")),
         "memos": state.get("memos", {}), "deleted": state.get("deleted", {})}
    return json.dumps(s, sort_keys=True, ensure_ascii=False)


class GitHubStore:
    def __init__(self, repo, token, path="deskcalendar.enc"):
        self.repo, self.token, self.path = repo.strip().strip("/"), token.strip(), path

    def _req(self, method, body=None):
        url = f"{API}/repos/{self.repo}/contents/{self.path}"
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(url, data=data, method=method, headers={
            "Authorization": f"Bearer {self.token}", "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28", "User-Agent": "DeskCalendar"})
        try:
            with urllib.request.urlopen(req, timeout=20) as r:
                return json.loads(r.read().decode("utf-8"))
        except urllib.error.HTTPError as e:
            if e.code == 404 and method == "GET":
                return None
            if e.code in (409, 422) and method == "PUT":
                raise Conflict()
            msg = {401: "토큰이 올바르지 않거나 만료됐어요", 403: "토큰 권한이 부족해요 (Contents: Read and write 필요)",
                   404: "저장소를 찾을 수 없어요 (이름·토큰 권한 확인)"}.get(e.code, f"GitHub 오류 {e.code}")
            raise SyncError(msg)
        except (urllib.error.URLError, TimeoutError, OSError):
            raise SyncError("인터넷에 연결할 수 없어요")

    def get(self):
        """(암호화된 바이트 또는 None, sha 또는 None)"""
        j = self._req("GET")
        if not j:
            return None, None
        return base64.b64decode(j.get("content", "")), j.get("sha")

    def put(self, blob, sha):
        body = {"message": "sync", "content": base64.b64encode(blob).decode()}
        if sha:
            body["sha"] = sha
        return self._req("PUT", body)["content"]["sha"]


def pull(store, key_b64):
    """원격 상태 가져오기 → (state 또는 None, sha)"""
    blob, sha = store.get()
    return (decrypt(key_b64, blob) if blob else None), sha


def push(store, key_b64, state, sha):
    return store.put(encrypt(key_b64, state), sha)
