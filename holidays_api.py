# -*- coding: utf-8 -*-
"""공공데이터포털(한국천문연구원 특일 정보)에서 해당 연도의 공휴일을 받아 온다.
 - 임시공휴일·선거일·대체공휴일까지 정확히 반영하려는 용도. 키가 없거나 실패하면 내장 규칙(holidays_kr)을 쓴다.
 - 서비스키는 코드에 넣지 않고 사용자가 설정에서 입력한 값(config.json)만 쓴다."""
import datetime as dt
import json
import os
import re
import time
import urllib.error
import urllib.parse
import urllib.request

URL = "https://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService/getRestDeInfo"
_NAMES = {"1월1일": "신정", "1월 1일": "신정"}


class ApiError(Exception):
    pass


def parse(text):
    """응답 본문 → {'YYYY-MM-DD': 이름} (공휴일만)"""
    t = text.strip()
    if t.startswith("<"):                       # 키 오류 등은 XML 로 온다
        m = re.search(r"<returnAuthMsg>(.*?)</returnAuthMsg>", t, re.S) or re.search(r"<resultMsg>(.*?)</resultMsg>", t, re.S)
        raise ApiError(m.group(1).strip() if m else "공공데이터 서버 응답을 읽을 수 없어요")
    try:
        j = json.loads(t)
        resp = j["response"]
        head = resp.get("header", {})
        if str(head.get("resultCode", "00")) not in ("00", "0"):
            raise ApiError(head.get("resultMsg", "오류"))
        items = resp.get("body", {}).get("items") or {}
        items = items.get("item", []) if isinstance(items, dict) else []
        if isinstance(items, dict):
            items = [items]
    except ApiError:
        raise
    except Exception:
        raise ApiError("공공데이터 서버 응답을 읽을 수 없어요")
    out = {}
    for it in items:
        if str(it.get("isHoliday", "Y")).upper() != "Y":
            continue
        s = str(it.get("locdate", ""))
        if len(s) != 8:
            continue
        name = str(it.get("dateName", "")).strip()
        out[f"{s[:4]}-{s[4:6]}-{s[6:]}"] = _NAMES.get(name, name)
    return out


def fetch_year(key, year, timeout=20):
    q = urllib.parse.urlencode({"solYear": year, "numOfRows": 100, "_type": "json"})
    # 키는 이미 인코딩된 값일 수 있으므로 그대로 붙인다 (인코딩 키 / 디코딩 키 모두 허용)
    k = key if "%" in key else urllib.parse.quote(key, safe="")
    req = urllib.request.Request(f"{URL}?{q}&ServiceKey={k}", headers={"User-Agent": "DeskCalendar"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            body = r.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as e:
        raise ApiError({401: "서비스키가 올바르지 않아요", 403: "서비스키 권한이 없어요(활용 신청 승인 확인)"}.get(e.code, f"서버 오류 {e.code}"))
    except (urllib.error.URLError, TimeoutError, OSError):
        raise ApiError("인터넷에 연결할 수 없어요")
    return parse(body)


# ── 캐시 ──
def load_cache(path):
    try:
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return {}


def save_cache(path, cache):
    try:
        tmp = path + ".tmp"
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(cache, f, ensure_ascii=False)
        os.replace(tmp, path)
    except Exception:
        pass


def is_fresh(entry, days=30):
    return bool(entry) and time.time() - entry.get("fetched", 0) < days * 86400
