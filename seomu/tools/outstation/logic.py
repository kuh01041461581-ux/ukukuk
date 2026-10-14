# -*- coding: utf-8 -*-
"""관외출장 여비지출결의서 — 순수 계산·파일 처리 코드 (화면과 무관, Qt 없이 시험 가능)

웹 버전(suite/tools/outstation.html)의 계산 엔진·표 인식기·거리 계산·HWPX 채우기를 그대로 옮겼다.
 · 출장/직원 기록은 웹과 같은 키(camelCase)의 dict 로 다룬다 → 웹에서 저장한 백업과 호환
 · 네트워크는 http_get() 한 곳만 거치므로 시험에서는 이 함수만 바꿔 끼우면 된다
"""
import datetime as dt
import io
import json
import math
import re
import time
import urllib.parse
import urllib.request
import urllib.error
import uuid

from openpyxl import Workbook

from ...core import hwpx as H

# ─────────────────────────── 상수 (웹 원문 그대로) ───────────────────────────
MAP_API_KEY = "a1e32e809202e84389f8a7913da38fe1"      # 카카오 REST API 키 (설정에 입력한 키가 있으면 그쪽이 우선)
OPINET_API_KEY = "F260915135"                          # 오피넷 인증키 (설정에 입력한 키가 있으면 그쪽이 우선)
OPINET_PROXY_URL = ""                                  # 직접 만든 오피넷 중계 주소(비워 두면 직접 호출)
DEFAULT_ORIGIN_ADDR = "전북특별자치도 익산시 익산시청"
DEFAULT_DEPT = "문화유산과"
DEFAULT_TEMPLATE_NAME = "기본 내장 서식(사리장엄관 정책세미나 결의서)"

CAR_FUEL_OPTIONS = [("GASOLINE", "휘발유"), ("DIESEL", "경유"), ("LPG", "LPG(가스)"), ("EV", "전기"), ("HYDROGEN", "수소")]
CAR_TYPE_OPTIONS = [(1, "경차"), (2, "소형(1,600cc 미만)"), (3, "중형(1,600~2,000cc)"), (4, "대형(2,000cc 초과)")]
FUEL_EFFICIENCY = {"GASOLINE": 11.97, "DIESEL": 12.52, "LPG": 8.83, "EV": 5.22, "HYDROGEN": 94.9}
FUEL_EFFICIENCY_BEFORE_2023 = {"GASOLINE": 13.3}
FUEL_EFFICIENCY_CHANGE_DATE = "2023-01-01"
FUEL_PRICE_UNIT = {"GASOLINE": "원/L", "DIESEL": "원/L", "LPG": "원/L", "EV": "원/kWh", "HYDROGEN": "원/kg"}
OPINET_PRODCD = {"GASOLINE": "B027", "DIESEL": "D047", "LPG": "K015"}
FALLBACK_FUEL_PRICE = {"GASOLINE": 1859, "DIESEL": 1844, "LPG": 1098, "EV": 324, "HYDROGEN": 9000}

TRANSPORTS = [("공무용차량", "공무용차량(관용차)"), ("자차", "자차"), ("기차", "기차/철도"), ("기타", "대중교통/기타(무료)")]
RETURN_TRANSPORTS = [("기차", "기차/철도"), ("공무용차량", "공무용차량(관용차, 무료)"), ("기타", "대중교통/기타(무료)")]
LODGE_REGIONS = [("서울", "서울 (1박 최대 100,000원)"), ("광역시", "광역시 (1박 최대 80,000원)"), ("기타", "그 밖의 지역 (1박 최대 70,000원)")]

MEAL_FEE_TABLE = {0: 0, 1: 9000, 2: 18000, 3: 25000}

PAYMENT_HEADER = ["거래처명\n(100자리이하)\n(필수입력)", "거래처구분\n(필수입력)", "사업자등록번호\n(10자리)", "생년월일\n(8자리)", "입금유형\n(선택)", "은행\n(선택)",
                  "예금주명\n(100자리이하)", "계좌번호\n(30자리이하)", "공급가액\n(17자리이하)\n(필수입력)", "부가세액\n(17자리이하)", "공제액\n(금액 0원으로 고정)",
                  "지출액\n(17자리이하)", "지로번호\n(19자리이하)", "CMS번호\n(20자리이하)", "대표채주\n(Y/N)", "대표자명\n(100자리)", "우편번호\n(5자리)",
                  "기본주소\n(75자이하)", "상세주소\n(75자이하)", "전화번호\n(30자리이하)", "휴대폰번호\n(20자리이하)", "입금명세(100자리)"]
DETAIL_HEADER = ["출장자명\n(100자리이하)", "계좌번호\n(30자리이하)", "직급명\n(100자리이하)", "출장목적\n(100자리이하)", "정산유형\n(선택)", "일자\n(8자리)",
                 "출발지\n(100자리이하)", "경유지\n(100자리이하)", "도착지\n(100자리이하)", "교통편\n(선택)", "종별\n(100자리이하)", "등급\n(100자리이하)",
                 "거리\n(10자리이하)", "요금\n(17자리이하)", "출장시작일\n(8자리)", "출장종료일\n(8자리)", "식비\n(17자리이하)", "숙박료\n(17자리이하)",
                 "일비\n(17자리이하)", "현지교통비\n(사용하지 않는 항목)", "기타\n(사용하지 않는 항목)", "계\n(17자리이하)", "청구 및 수령액\n(17자리이하)",
                 "실국명\n(100자리이하)", "부서명\n(100자리이하)"]


class ParseError(Exception):
    pass


# ─────────────────────────── 자바스크립트 흉내 도우미 ───────────────────────────
def js_round(x) -> int:
    """Math.round (0.5 는 올림)"""
    try:
        return int(math.floor(float(x) + 0.5))
    except (TypeError, ValueError, OverflowError):
        return 0


def num_str(x) -> str:
    """숫자를 자바스크립트 문자열화 방식으로 (342.0 → '342')"""
    try:
        f = float(x)
    except (TypeError, ValueError):
        return str(x)
    if f == int(f):
        return str(int(f))
    return repr(f)


def won(n) -> str:
    """fmt(): 반올림 + 천 단위 + '원'"""
    return f"{js_round(n):,}원"


def comma(n) -> str:
    """Number.toLocaleString() — 소수는 최대 3자리"""
    f = float(n)
    if f == int(f):
        return f"{int(f):,}"
    s = f"{f:,.3f}".rstrip("0").rstrip(".")
    return s


def parse_float(v, default=0.0) -> float:
    """parseFloat(v)||default"""
    m = re.match(r"\s*[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?", str(v if v is not None else ""))
    if not m:
        return default
    r = float(m.group(0))
    return r if r else default


def parse_int(v, default=0) -> int:
    m = re.match(r"\s*[-+]?\d+", str(v if v is not None else ""))
    if not m:
        return default
    r = int(m.group(0))
    return r if r else default


def escape_xml(s) -> str:
    return ("" if s is None else str(s)).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def spaced_name(name) -> str:
    return " ".join(str(name or ""))


def uid() -> str:
    return "id" + str(int(time.time() * 1000)) + str(uuid.uuid4().int % 10000)


def parse_dt(s) -> dt.datetime | None:
    s = str(s or "").strip()
    if not s:
        return None
    for fmt in ("%Y-%m-%dT%H:%M", "%Y-%m-%dT%H:%M:%S", "%Y-%m-%d %H:%M", "%Y-%m-%d %H:%M:%S", "%Y-%m-%d"):
        try:
            return dt.datetime.strptime(s, fmt)
        except ValueError:
            pass
    return None


def today_str() -> str:
    return dt.date.today().strftime("%Y%m%d")


# ─────────────────────────── 유종·단가 ───────────────────────────
def fuel_label(v) -> str:
    return dict(CAR_FUEL_OPTIONS).get(v, "")


def car_label(v) -> str:
    return dict(CAR_TYPE_OPTIONS).get(v, "")


def get_fuel_efficiency(fuel: str, date_str: str = "") -> float:
    d = (date_str or "")[:10]
    if d and d < FUEL_EFFICIENCY_CHANGE_DATE and fuel in FUEL_EFFICIENCY_BEFORE_2023:
        return FUEL_EFFICIENCY_BEFORE_2023[fuel]
    return FUEL_EFFICIENCY.get(fuel) or FUEL_EFFICIENCY["GASOLINE"]


def calc_rate_per_km(fuel: str, oil_price, date_str: str = "") -> int:
    return js_round((oil_price or 0) / get_fuel_efficiency(fuel, date_str))


def guess_lodge_region(dest: str) -> str:
    if not dest:
        return "기타"
    if "서울" in dest:
        return "서울"
    if any(m in dest for m in ["부산", "대구", "인천", "광주", "대전", "울산", "세종"]):
        return "광역시"
    return "기타"


# ─────────────────────────── 오피넷 유가 ───────────────────────────
def parse_opinet_text(text: str) -> list:
    try:
        data = json.loads(text)
    except Exception:
        m = re.search(r"\{[\s\S]*\}", str(text))
        if not m:
            raise ValueError("JSON 아님(응답: " + re.sub(r"\s+", " ", str(text)[:40]) + ")")
        try:
            data = json.loads(m.group(0))
        except Exception:
            raise ValueError("JSON 해석 실패")
    if isinstance(data, dict) and isinstance(data.get("contents"), str):
        return parse_opinet_text(data["contents"])
    lst = ((data or {}).get("RESULT") or {}).get("OIL") if isinstance(data, dict) else None
    if not isinstance(lst, list) or not lst:
        raise ValueError("유가 데이터 없음")
    return lst


def opinet_row_to_result(lst: list, prodcd: str):
    row = next((x for x in lst if x.get("PRODCD") == prodcd), None)
    if not row or not row.get("PRICE"):
        return None
    d = str(row.get("TRADE_DT") or "")
    return {"price": js_round(parse_float(row["PRICE"])), "date": f"{d[:4]}-{d[4:6]}-{d[6:8]}" if len(d) == 8 else ""}


def best_known_oil_price(fuel: str, cache: dict | None) -> int:
    prodcd = OPINET_PRODCD.get(fuel)
    r = opinet_row_to_result(cache["list"], prodcd) if (prodcd and cache and cache.get("list")) else None
    return r["price"] if r else FALLBACK_FUEL_PRICE[fuel]


def http_get(url: str, headers: dict | None = None, timeout: float = 15) -> tuple:
    """(상태코드, 본문). 시험에서는 이 함수를 바꿔 끼운다."""
    h = {"User-Agent": "SeomuSuite/1.0 (office-automation)"}
    h.update(headers or {})
    req = urllib.request.Request(url, headers=h)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.status, r.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        try:
            body = e.read().decode("utf-8", "replace")
        except Exception:
            body = ""
        return e.code, body


def opinet_url(key: str) -> str:
    k = urllib.parse.quote(key, safe="")
    return f"https://www.opinet.co.kr/api/avgAllPrice.do?out=json&certkey={k}&code={k}"


def fetch_opinet_avg_all(key: str, rounds: int = 2, sleep: float = 0.7) -> tuple:
    """(목록, 조회경로).  직접 호출(브라우저가 아니므로 CORS 중계는 필요 없음), 전 경로 실패 시 한 번 더."""
    routes = []
    if OPINET_PROXY_URL:
        routes.append(("내 중계서버", OPINET_PROXY_URL.rstrip("/") + "?k=" + urllib.parse.quote(key, safe="")))
    routes.append(("direct", opinet_url(key)))
    last = None
    for rnd in range(rounds):
        errs = []
        for name, url in routes:
            try:
                st, text = http_get(url, timeout=10)
                if not (200 <= st < 300):
                    raise ValueError(f"HTTP {st}")
                return parse_opinet_text(text), name
            except Exception as e:  # noqa
                errs.append(f"{name}: {e}")
        last = " / ".join(errs)
        if rnd < rounds - 1:
            time.sleep(sleep)
    raise RuntimeError(last or "알 수 없는 오류")


def fetch_oil_price(fuel: str, key: str, cache: dict | None) -> dict:
    """{price, source: opinet|cache|fallback, date, via, cachedAt, reason, manual, list}"""
    prodcd = OPINET_PRODCD.get(fuel)
    if not prodcd:
        return {"price": FALLBACK_FUEL_PRICE[fuel], "source": "fallback", "reason": "전기·수소는 오피넷 조회 대상이 아닙니다(참고값 사용)."}

    def from_cache(reason):
        r = opinet_row_to_result(cache["list"], prodcd) if (cache and cache.get("list")) else None
        if r:
            return {"price": r["price"], "source": "cache", "date": r["date"], "cachedAt": cache.get("ts"), "reason": reason, "manual": True}
        return {"price": FALLBACK_FUEL_PRICE[fuel], "source": "fallback", "reason": reason, "manual": True}
    if not key:
        return from_cache("오피넷 API 키가 없습니다.")
    try:
        lst, via = fetch_opinet_avg_all(key)
    except Exception as e:  # noqa
        return from_cache("오피넷 조회 실패 → " + str(e))
    r = opinet_row_to_result(lst, prodcd)
    if r:
        return {"price": r["price"], "source": "opinet", "date": r["date"], "via": via, "list": lst}
    return from_cache("오피넷 응답에 해당 유종이 없습니다.")


# ─────────────────────────── 주소·거리 ───────────────────────────
def _json(url, headers=None, timeout=15):
    st, text = http_get(url, headers, timeout)
    return st, (json.loads(text) if 200 <= st < 300 else None), text


def geocode_address(addr: str, key: str) -> dict:
    if key:
        try:
            st, data, _ = _json("https://dapi.kakao.com/v2/local/search/address.json?query=" + urllib.parse.quote(addr), {"Authorization": "KakaoAK " + key})
            if data and data.get("documents"):
                d = data["documents"][0]
                return {"lat": float(d["y"]), "lng": float(d["x"]), "roadAddr": (d.get("road_address") or {}).get("address_name") or d.get("address_name")}
        except Exception:
            pass   # 카카오 실패 → 무료 검색으로 대체
    st, data, _ = _json("https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=kr&q=" + urllib.parse.quote(addr))
    if data is None:
        raise ValueError(f"주소 검색에 실패했습니다 (오류 {st}).")
    if not data:
        raise ValueError("주소를 찾을 수 없습니다: " + addr)
    return {"lat": float(data[0]["lat"]), "lng": float(data[0]["lon"]), "roadAddr": data[0].get("display_name")}


def search_address_candidates(q: str, key: str) -> list:
    out = []
    if key:
        try:
            hdr = {"Authorization": "KakaoAK " + key}
            _, d, _ = _json("https://dapi.kakao.com/v2/local/search/address.json?size=10&query=" + urllib.parse.quote(q), hdr)
            for doc in (d or {}).get("documents", []):
                out.append({"title": doc.get("address_name"), "roadAddr": (doc.get("road_address") or {}).get("address_name") or doc.get("address_name"),
                            "lat": float(doc["y"]), "lng": float(doc["x"])})
            _, d2, _ = _json("https://dapi.kakao.com/v2/local/search/keyword.json?size=10&query=" + urllib.parse.quote(q), hdr)
            for doc in (d2 or {}).get("documents", []):
                out.append({"title": doc.get("place_name"), "roadAddr": doc.get("road_address_name") or doc.get("address_name"),
                            "lat": float(doc["y"]), "lng": float(doc["x"])})
            if out:
                return out
        except Exception:
            out = []
    st, data, _ = _json("https://nominatim.openstreetmap.org/search?format=json&limit=8&countrycodes=kr&q=" + urllib.parse.quote(q))
    if data is None:
        raise ValueError(f"주소 검색에 실패했습니다 (오류 {st}).")
    return [{"title": d.get("display_name"), "roadAddr": d.get("display_name"), "lat": float(d["lat"]), "lng": float(d["lon"])} for d in data]


def haversine_km(a: dict, b: dict) -> float:
    R = 6371
    rad = math.radians
    dlat, dlng = rad(b["lat"] - a["lat"]), rad(b["lng"] - a["lng"])
    s = math.sin(dlat / 2) ** 2 + math.cos(rad(a["lat"])) * math.cos(rad(b["lat"])) * math.sin(dlng / 2) ** 2
    return R * 2 * math.asin(math.sqrt(s))


def call_kakao_directions(origin: dict, dest: dict, key: str) -> tuple:
    url = f"https://apis-navi.kakaomobility.com/v1/directions?origin={origin['lng']},{origin['lat']}&destination={dest['lng']},{dest['lat']}"
    st, text = http_get(url, {"Authorization": "KakaoAK " + key}, 12)
    if not (200 <= st < 300):
        raise ValueError(f"HTTP {st} " + re.sub(r"\s+", " ", text[:80]))
    data = json.loads(text)
    routes = (data or {}).get("routes") or []
    route = routes[0] if routes else None
    if not route or (route.get("result_code") not in (0, None) and not route.get("summary")):
        raise ValueError(route.get("result_msg") if route and route.get("result_msg") else "경로 없음")
    summary = route.get("summary")
    if not summary or not summary.get("distance"):
        raise ValueError("거리 정보 없음")
    return route, summary


def fetch_driving_distance(origin: dict, dest: dict, key: str) -> dict:
    """통행료는 카카오맵 웹과 같은 기본 조건(차종·유종 미지정)으로 조회한다."""
    kakao_error = ""
    if key:
        try:
            route, summary = call_kakao_directions(origin, dest, key)
            fare = summary.get("fare") or {}
            roads = []
            for sec in route.get("sections") or []:
                for r in sec.get("roads") or []:
                    if r.get("name") and r["name"] not in roads:
                        roads.append(r["name"])
            return {"meters": summary["distance"], "tollFare": fare.get("toll") or 0, "tollKnown": fare.get("toll") is not None,
                    "taxiFare": fare.get("taxi") or 0, "duration": summary.get("duration") or 0, "roads": roads, "kakaoUsed": True}
        except Exception as e:  # noqa
            kakao_error = str(e)
    else:
        kakao_error = "카카오 API 키가 없습니다."
    return {"meters": haversine_km(origin, dest) * 1000 * 1.3, "tollFare": 0, "tollKnown": False, "taxiFare": 0, "duration": 0, "roads": [],
            "kakaoUsed": False, "kakaoError": kakao_error}


def kakao_map_route_url(origin_label: str, o: dict, dest_label: str, d: dict) -> str:
    enc = lambda s: urllib.parse.quote(s, safe="")  # noqa
    return f"https://map.kakao.com/link/by/car/{enc(origin_label)},{o['lat']},{o['lng']}/{enc(dest_label)},{d['lat']},{d['lng']}"


def run_distance_calc(origin_addr: str, dest_addr: str, fuel: str, car_type: int, oil_price: float, key: str) -> dict:
    origin_addr = origin_addr.strip() or DEFAULT_ORIGIN_ADDR
    o = geocode_address(origin_addr, key)
    d = geocode_address(dest_addr, key)
    r = fetch_driving_distance(o, d, key)
    meters = r["meters"]
    one_way = js_round(meters / 1000 * 10) / 10
    round_km = js_round(meters / 1000 * 2 * 10) / 10
    rate = calc_rate_per_km(fuel, oil_price)
    toll = r["tollFare"]
    last = {"oneWayKm": one_way, "roundKm": round_km, "originAddr": o.get("roadAddr") or origin_addr, "destAddr": d.get("roadAddr") or dest_addr,
            "fuelType": fuel, "carType": car_type, "oilPrice": oil_price, "rate": rate, "oneWayFare": js_round(one_way * rate),
            "roundFare": js_round(round_km * rate), "tollFareOneWay": toll, "tollFareRound": js_round(toll * 2), "kakaoUsed": r["kakaoUsed"],
            "kakaoError": r.get("kakaoError", ""), "oLat": o["lat"], "oLng": o["lng"], "dLat": d["lat"], "dLng": d["lng"]}
    last["mapUrl"] = kakao_map_route_url(last["originAddr"], o, last["destAddr"], d)
    return last


def format_dist_result(last: dict) -> str:
    """거리 계산 결과 안내문 (리치텍스트)"""
    fuel = last["fuelType"]
    unit = FUEL_PRICE_UNIT.get(fuel, "원/L")
    h = (f"편도 <b>{num_str(last['oneWayKm'])}km</b> · 왕복 <b>{num_str(last['roundKm'])}km</b> ({escape_xml(last['originAddr'])} → {escape_xml(last['destAddr'])})<br>"
         f"유종: {fuel_label(fuel)} · 유가(오피넷) {comma(last['oilPrice'])}{unit} ÷ 연비 {num_str(get_fuel_efficiency(fuel))} · 운임단가 <b>{last['rate']:,}원/km</b><br>"
         f"편도 운임비 <b>{last['oneWayFare']:,}원</b> · 왕복 운임비 <b>{last['roundFare']:,}원</b><br>")
    if not last["kakaoUsed"]:
        h += '통행료(카카오맵): <b style="color:#C0392B;">확인 못 함</b> — 영수증 금액을 통행료 칸에 직접 입력하세요.'
    elif last["tollFareOneWay"] > 0:
        h += f"통행료(카카오맵): 편도 {last['tollFareOneWay']:,}원 · 왕복 예상 <b>{last['tollFareRound']:,}원</b>(영수증 첨부 시 지급)"
    else:
        h += "통행료(카카오맵): 통행료 없는 경로(0원)"
    return h


def dist_warning(last: dict) -> str:
    if last["kakaoUsed"]:
        return ""
    err = f" (원인: {escape_xml(last['kakaoError'])})" if last.get("kakaoError") else ""
    return f"카카오 길찾기 결과를 받지 못해 직선거리×1.3 추정 거리를 사용했고 통행료는 반영되지 않았습니다.{err} 키 입력·권한(카카오내비 사용 설정)을 확인하고 다시 계산해주세요."


def apply_distance(trips: list, last: dict, driver_id, rider_ids: list) -> int:
    """선택한 운전자·동승자에게 왕복 거리 적용. 반환: 적용 인원"""
    ids = list(dict.fromkeys(([driver_id] if driver_id else []) + list(rider_ids)))
    n = 0
    for t in trips:
        if t["id"] not in ids:
            continue
        t["transport"] = "자차"
        t["distance"] = last["roundKm"]
        t["carRole"] = "driver" if t["id"] == driver_id else "passenger"
        t["fuelType"] = last["fuelType"]
        t["carType"] = last["carType"]
        t["oilPrice"] = last["oilPrice"]
        t["rate"] = last["rate"]
        if t["id"] == driver_id and last["tollFareRound"] > 0:
            t["toll"] = last["tollFareRound"]
        n += 1
    return n


# ─────────────────────────── 출장 기록 ───────────────────────────
def new_trip(f: dict, staff: list, default_dept: str = DEFAULT_DEPT, oil_price: float | None = None) -> dict:
    """makeTripRecord — 직원 명단에 있으면 직급/부서를 채운다"""
    s = next((x for x in staff if x.get("name") == f["name"]), {})
    op = oil_price if oil_price is not None else FALLBACK_FUEL_PRICE["GASOLINE"]
    return {
        "id": uid(), "name": f["name"], "position": f.get("position") or s.get("position") or "",
        "dept": f.get("dept") or s.get("dept") or default_dept,
        "start": f.get("start") or "", "end": f.get("end") or "", "dest": f.get("dest") or "", "purpose": f.get("purpose") or "",
        "grade": f.get("grade") or "제2호", "transport": f.get("transport") or "자차", "carRole": "driver",
        "distance": 0, "fuelType": "GASOLINE", "carType": 3, "rate": calc_rate_per_km("GASOLINE", op, f.get("start") or ""), "toll": 0, "tollReceipt": False,
        "trainFare": 0, "trainTicket": False, "nights": 0, "lodge": 0, "lodgeRegion": "기타", "notes": f.get("notes") or "",
        "mealOverride": None, "mealFeeOverride": None, "dailyOverride": None, "transOverride": None, "lodgeOverride": None,
    }


def blank_trip(default_dept: str = DEFAULT_DEPT, oil_price: float | None = None, now: dt.datetime | None = None) -> dict:
    now = now or dt.datetime.now()
    d = now.strftime("%Y-%m-%d")
    op = oil_price if oil_price is not None else FALLBACK_FUEL_PRICE["GASOLINE"]
    t = new_trip({"name": "", "dept": default_dept, "start": d + "T09:00", "end": d + "T18:00", "transport": "공무용차량"}, [], default_dept, op)
    t.update({"oilPrice": op, "rate": calc_rate_per_km("GASOLINE", op, d), "id": ""})
    return t


# ─────────────────────────── 계산 엔진 ───────────────────────────
def duration_hours(t: dict) -> float:
    s, e = parse_dt(t.get("start")), parse_dt(t.get("end"))
    if not s or not e:
        return 0
    return (e - s).total_seconds() / 3600


def calc_meal_count(t: dict) -> int:
    if t.get("mealOverride") is not None:
        return max(0, min(3, parse_int(t["mealOverride"])))
    s, e = parse_dt(t.get("start")), parse_dt(t.get("end"))
    if not (t.get("start") and t.get("end")) or not s or not e:
        return 0
    same_day = s.date() == e.date()
    s_min = s.hour * 60 + s.minute
    e_min = (e.hour * 60 + e.minute) if same_day else 24 * 60
    includes_lunch = s_min < 13 * 60 and e_min > 11 * 60
    beyond_core = (not same_day) or s_min < 9 * 60 or e_min > 18 * 60
    long_distance = (t.get("distance") or 0) >= 100
    count = 1
    if includes_lunch:
        count = 2
    if beyond_core or long_distance:
        count = 3
    return count


def calc_daily(t: dict):
    if t.get("dailyOverride") is not None:
        return t["dailyOverride"]
    if duration_hours(t) < 4:
        return 0
    return 12500 if t.get("transport") == "공무용차량" else 25000


def meal_fee_of_count(n) -> int:
    return MEAL_FEE_TABLE[max(0, min(3, parse_int(n)))]


def calc_meal_fee(t: dict):
    if t.get("mealFeeOverride") is not None:
        return t["mealFeeOverride"]
    return meal_fee_of_count(calc_meal_count(t))


def calc_transport_fee(t: dict):
    if t.get("transOverride") is not None:
        return t["transOverride"]
    tr = t.get("transport")
    if tr == "자차":
        if t.get("carRole") == "passenger":
            return 0
        mileage = (t.get("distance") or 0) * (t.get("rate") or 0)
        return mileage + ((t.get("toll") or 0) if t.get("tollReceipt") else 0)
    if tr == "기차":
        if t.get("trainOneWay"):
            fee = (t.get("trainFareGo") or 0) if t.get("trainTicketGo") else 0
            if (t.get("returnTransport") or "기차") == "기차":
                fee += (t.get("trainFareReturn") or 0) if t.get("trainTicketReturn") else 0
            return fee
        return (t.get("trainFare") or 0) if t.get("trainTicket") else 0
    return 0


def lodge_cap_per_night(region: str) -> int:
    return {"서울": 100000, "광역시": 80000}.get(region, 70000)


def calc_lodge_fee(t: dict):
    if t.get("lodgeOverride") is not None:
        return t["lodgeOverride"]
    n = t.get("nights") or 0
    if n <= 0:
        return 0
    return min(t.get("lodge") or 0, n * lodge_cap_per_night(t.get("lodgeRegion") or "기타"))


def calc_total(t: dict):
    return calc_daily(t) + calc_meal_fee(t) + calc_transport_fee(t) + calc_lodge_fee(t)


def find_account(staff: list, name: str) -> str:
    s = next((x for x in staff if x.get("name") == name), None)
    return s.get("account", "") if s else ""


def migrate_old_rates(trips: list, oil_price: float) -> bool:
    """예전 초기값(178/128원/km)이 그대로 저장된 기록은 현행 연비 기준 단가로 보정"""
    ch = False
    for t in trips:
        if t.get("rate") in (178, 128):
            fuel = t.get("fuelType") or "GASOLINE"
            t["rate"] = calc_rate_per_km(fuel, oil_price if fuel == "GASOLINE" else FALLBACK_FUEL_PRICE[fuel], t.get("start") or "")
            ch = True
    return ch


# ─────────────────────────── 표시용 문구 ───────────────────────────
def fmt_range(t: dict) -> str:
    if not t.get("start"):
        return "-"
    s = t["start"].replace("T", " ")
    e = (t.get("end") or "").replace("T", " ")
    return s + " ~ " + ((e.split(" ")[1] if " " in e else e))


def transport_badge(t: dict) -> tuple:
    """(문구, 종류) — 종류: pri(관용차) · ok(자차) · neutral · warn"""
    warn = (t.get("notes") or "").startswith("⚠")
    tr = t.get("transport")
    if tr == "공무용차량":
        return "관용차", "pri", warn
    if tr == "자차":
        return (f"자차 {num_str(t.get('distance') or 0)}km {fuel_label(t.get('fuelType'))}·{car_label(t.get('carType'))} "
                f"{'(동승)' if t.get('carRole') == 'passenger' else '(운전)'}"), "ok", warn
    if tr == "기차":
        if t.get("trainOneWay"):
            go = "승차권O" if t.get("trainTicketGo") else "승차권X"
            rt = t.get("returnTransport") or "기차"
            ret = ("승차권O" if t.get("trainTicketReturn") else "승차권X") if rt == "기차" else ("관용차" if rt == "공무용차량" else "기타")
            return f"기차(편도) 가는편 {go} / 오는편 {ret}", "neutral", warn
        return f"기차 {'(승차권O)' if t.get('trainTicket') else '(승차권X)'}", "neutral", warn
    return "기타", "neutral", warn


def detail_text(t: dict) -> str:
    tr = t.get("transport")
    if tr == "자차":
        if t.get("carRole") == "passenger":
            return "동승자 — 자차 운임료 미지급(일비만 지급)"
        return f"거리비 {won((t.get('distance') or 0) * (t.get('rate') or 0))} + 통행료 {won(t.get('toll') or 0) if t.get('tollReceipt') else '미첨부(0원)'}"
    if tr == "기차":
        if t.get("trainOneWay"):
            go = won(t.get("trainFareGo") or 0) if t.get("trainTicketGo") else "승차권 미첨부(0원)"
            rt = t.get("returnTransport") or "기차"
            if rt == "기차":
                ret = won(t.get("trainFareReturn") or 0) if t.get("trainTicketReturn") else "승차권 미첨부(0원)"
            else:
                ret = "관용차(무료)" if rt == "공무용차량" else "기타(무료)"
            return f"가는편 {go} / 오는편 {ret}"
        return f"운임 {won(t.get('trainFare') or 0) if t.get('trainTicket') else '승차권 미첨부(0원)'}"
    return "-"


# ─────────────────────────── 직원 명단 엑셀 ───────────────────────────
def parse_staff_rows(rows: list, default_dept: str = DEFAULT_DEPT) -> list:
    """'거래처입력' 양식 → 직원 목록 (성명=0열, 은행=5열 'code:name', 계좌=7열)"""
    out = []
    for r in rows[1:]:
        r = list(r) + [""] * (8 - len(r))
        if not r[0]:
            continue
        bank = _cell_str(r[5])
        if ":" in bank:
            code, name = bank.split(":")[0], bank.split(":")[1]     # 웹과 같이 앞 두 조각만
        else:
            code, name = "", bank
        out.append({"name": _cell_str(r[0]), "position": "", "dept": default_dept, "bankCode": code, "bankName": name, "account": _cell_str(r[7])})
    return out


def _cell_str(v) -> str:
    if v is None:
        return ""
    if isinstance(v, float) and v == int(v):
        return str(int(v))
    if isinstance(v, dt.datetime):
        return v.strftime("%Y-%m-%d %H:%M")
    if isinstance(v, dt.time):
        return v.strftime("%H:%M")
    if isinstance(v, dt.date):
        return v.strftime("%Y-%m-%d")
    return str(v)


def pick_staff_sheet(sheets: dict) -> list:
    name = next((n for n in sheets if "거래처입력" in n and "설명" not in n), None) or next(iter(sheets))
    return sheets[name]


def build_staff_workbook(staff: list) -> Workbook:
    wb = Workbook()
    ws = wb.active
    ws.title = "거래처입력"
    ws.append(PAYMENT_HEADER)
    for s in staff:
        bank = (s["bankCode"] + ":" if s.get("bankCode") else "") + (s.get("bankName") or "")
        ws.append([s.get("name"), "30:개인", "", "", "10:계좌이체", bank, s.get("name"), s.get("account"), "", "", "", "", "", "", "", "", "", "", "", "", "", ""])
    _style_sheet(ws)
    return wb


def build_payment_workbook(trips: list, staff: list) -> Workbook:
    wb = Workbook()
    ws = wb.active
    ws.title = "거래처입력"
    ws.append(PAYMENT_HEADER)
    for t in trips:
        s = next((x for x in staff if x.get("name") == t["name"]), {})
        total = js_round(calc_total(t))
        bank = (s["bankCode"] + ":" if s.get("bankCode") else "") + (s.get("bankName") or "")
        ws.append([t["name"], "30:개인", "", "", "10:계좌이체", bank, t["name"], s.get("account") or "", total, 0, 0, total, "", "", "", "", "", "", "", "", "", t.get("purpose") or ""])
    _style_sheet(ws)
    return wb


def build_detail_workbook(trips: list, staff: list) -> Workbook:
    wb = Workbook()
    ws = wb.active
    ws.title = "여비상세입력"
    ws.append(DETAIL_HEADER)
    for t in trips:
        s = next((x for x in staff if x.get("name") == t["name"]), {})
        daily, meal, trans, lodge = calc_daily(t), calc_meal_fee(t), calc_transport_fee(t), calc_lodge_fee(t)
        total = daily + meal + trans + lodge
        sd = (t.get("start") or "").split("T")[0].replace("-", "")
        ed = (t.get("end") or "").split("T")[0].replace("-", "")
        code = "002:철도" if t.get("transport") == "기차" else ("003:자가용" if t.get("transport") == "자차" else "001:없음")
        ws.append([t["name"], s.get("account") or "", t.get("position") or "", t.get("purpose") or "", "002:실비", sd, "익산", "", t.get("dest") or "", code, "",
                   t.get("grade") or "", t.get("distance") or 0, trans, sd, ed, meal, lodge, daily, "", "", total, total, "문화교육국", t.get("dept") or DEFAULT_DEPT])
    _style_sheet(ws)
    return wb


def _style_sheet(ws):
    from openpyxl.styles import Alignment
    for c in ws[1]:
        c.alignment = Alignment(wrap_text=True, vertical="center")
    ws.row_dimensions[1].height = 48
    for i in range(1, ws.max_column + 1):
        ws.column_dimensions[ws.cell(1, i).column_letter].width = 16


# ─────────────────────────── 출장 파일 읽기 (표 인식기) ───────────────────────────
HEADER_FIELD_SYNONYMS = {
    "name": ["성명", "이름", "출장자", "출장자명"],
    "deptPos": ["소속/직급", "소속·직급", "소속및직급", "부서/직급", "소속·직위"],
    "dept": ["소속", "부서"],
    "position": ["직급", "직위"],
    "gubun": ["구분", "근무구분"],
    "carUsed": ["공무용차량", "관용차량", "차량사용유무"],
    "purpose": ["출장목적", "목적", "용무"],
    "period": ["출장기간", "출장일정", "출장일시"],
    "startDT": ["출장시작일시", "시작일시", "출장시작", "출발일시"],
    "endDT": ["출장종료일시", "종료일시", "출장종료", "복귀일시"],
    "grade": ["여비등급", "여비구분", "등급"],
    "dest": ["출장지", "출장지역", "목적지"],
}


def norm_rows(rows: list) -> list:
    return [[_cell_str(c) for c in r] for r in rows]


def _norm_header(v) -> str:
    return re.sub(r"[\s\r\n]+", "", "" if v is None else str(v))


def is_header_row(row: list):
    col_map = {}
    match = 0
    for ci, cell in enumerate(row):
        norm = _norm_header(cell)
        if not norm:
            continue
        for field, syn in HEADER_FIELD_SYNONYMS.items():
            if field in col_map:
                continue
            if norm in syn:
                col_map[field] = ci
                match += 1
                break
    return col_map if ("name" in col_map and match >= 3) else None


def parse_datetime_cell(v) -> str:
    s = str("" if v is None else v).strip()
    m = re.search(r"(\d{4}-\d{2}-\d{2})[^\d]*?(\d{1,2}:\d{2})", s)
    if not m:
        return ""
    t = "0" + m.group(2) if len(m.group(2)) == 4 else m.group(2)
    return m.group(1) + "T" + t


def parse_period_cell(v) -> tuple:
    s = str("" if v is None else v).replace("_x000D_", "\n")
    m = re.search(r"(\d{4}-\d{2}-\d{2})[^\d]*?(\d{1,2}:\d{2})\s*[~\-]\s*(\d{4}-\d{2}-\d{2})[^\d]*?(\d{1,2}:\d{2})", s)
    if not m:
        return "", ""
    t1 = "0" + m.group(2) if len(m.group(2)) == 4 else m.group(2)
    t2 = "0" + m.group(4) if len(m.group(4)) == 4 else m.group(4)
    return m.group(1) + "T" + t1, m.group(3) + "T" + t2


def parse_generic_trip_rows(rows: list, mk) -> list:
    out = []
    col_map = None
    for row in rows:
        hdr = is_header_row(row)
        if hdr:
            col_map = hdr
            continue
        if not col_map:
            continue

        def get(field):
            i = col_map.get(field)
            return row[i] if (i is not None and i < len(row)) else None
        name_raw = str(get("name") or "").strip()
        if not name_raw:
            continue
        if re.match(r"^(소|총|합)\s*계$", re.sub(r"\s+", "", name_raw)):
            continue
        name = re.sub(r"\(.*?\)", "", name_raw, count=1).strip()
        if not re.search(r"[가-힣]{2,}", name):
            continue
        if "gubun" in col_map:
            g = str(get("gubun") or "").strip()
            if g and g != "근무지외":
                continue
        dept = position = ""
        if "deptPos" in col_map:
            parts = [p.strip() for p in re.split(r"\r\n|\r|\n|_x000D_", str(get("deptPos") or "")) if p.strip()]
            dept = parts[0] if parts else ""
            position = parts[-1] if parts else ""
        else:
            if "dept" in col_map:
                dept = str(get("dept") or "").strip()
            if "position" in col_map:
                position = str(get("position") or "").strip()
        purpose = str(get("purpose") or "").strip() if "purpose" in col_map else ""
        grade = (str(get("grade") or "").strip() if "grade" in col_map else "") or "제2호"
        dest = str(get("dest") or "").strip() if "dest" in col_map else ""
        # "관용차 미사용"은 자차를 썼다는 뜻이 아니므로 '기타/확인필요'로 둔다
        transport, needs = "기타", True
        if "carUsed" in col_map:
            cu = str(get("carUsed") or "").strip()
            if "미사용" in cu:
                transport, needs = "기타", True
            elif "사용" in cu:
                transport, needs = "공무용차량", False
        notes = "⚠ 이동수단 확인 필요(원본에는 관용차 미사용 여부만 있음 — 자차/기차/대중교통 중 실제 수단을 선택해주세요)" if needs else ""
        start = end = ""
        if "startDT" in col_map or "endDT" in col_map:
            start = parse_datetime_cell(get("startDT"))
            end = parse_datetime_cell(get("endDT"))
        elif "period" in col_map:
            start, end = parse_period_cell(get("period"))
        if not (start or end or purpose or dest or position or dept):
            continue
        out.append(mk({"name": name, "dept": dept, "position": position, "dest": dest, "purpose": purpose, "grade": grade, "start": start, "end": end,
                       "transport": transport, "notes": notes}))
    return out


def parse_aggregate_rows(rows: list, mk) -> list:
    out = []
    for r in rows:
        r = list(r) + [""] * (16 - len(r))
        if not re.match(r"^\d+$", str(r[1]).strip()):
            continue
        dp = re.split(r"\r|\n", str(r[2] or ""))
        position = dp[-1] if dp else ""
        name = re.sub(r"\(.*?\)", "", str(r[4] or ""), count=1).strip()
        m = re.search(r"(\d{4}-\d{2}-\d{2})\s+(\d{2}:\d{2})\s*\r?\n?\s*~\s*(\d{4}-\d{2}-\d{2})\s+(\d{2}:\d{2})", str(r[5] or ""))
        start = end = ""
        if m:
            start, end = m.group(1) + "T" + m.group(2), m.group(3) + "T" + m.group(4)
        car_used = "사용" in str(r[9] or "")
        if not name:
            continue
        out.append(mk({"name": name, "position": position, "dest": r[11] or "", "purpose": r[13] or "", "grade": r[15] or "제2호", "start": start, "end": end,
                       "transport": "공무용차량" if car_used else "자차"}))
    return out


def parse_trip_list_rows(rows: list, mk) -> list:
    out = []
    for r in rows:
        r = list(r) + [""] * (21 - len(r))
        if not re.match(r"^\d+$", str(r[0]).strip()):
            continue
        if str(r[5] or "").strip() != "근무지외":
            continue
        name = str(r[1] or "").strip()
        if not name:
            continue
        sd, stime, ed, etime = (str(r[i] or "") for i in (8, 9, 10, 11))
        out.append(mk({"name": name, "dept": str(r[3] or "") or DEFAULT_DEPT, "position": str(r[4] or ""), "dest": str(r[20] or ""), "purpose": str(r[7] or ""),
                       "grade": str(r[17] or "제2호"), "start": sd + "T" + stime if sd and stime else "", "end": ed + "T" + etime if ed and etime else "",
                       "transport": "공무용차량" if str(r[6] or "").strip() == "사용" else "자차"}))
    return out


def parse_draft_text(text: str, mk) -> list:
    norm = text.replace(" ", " ")

    def grab(label):
        m = re.search(label + r"\s*[:：]\s*([^\n\r]+)", norm)
        return m.group(1).strip() if m else ""
    actors = grab(r"출\s*장\s*자")
    dest_line = grab(r"출\s*장\s*지")
    purpose = grab("출장목적") or grab(r"출\s*장\s*목\s*적")
    date_line = grab("출장일시") or grab(r"출\s*장\s*일\s*시")
    if not actors and not date_line:
        return []
    names = [re.sub(r"\(.*?\)", "", n, count=1).strip() for n in re.split(r"[\s,、·]+", actors)]
    names = [n for n in names if re.match(r"^[가-힣]{2,4}$", n)]
    cm = re.search(r"공무용차량\s*\(?\s*(사용|미사용)\)?", dest_line)
    car_used = bool(cm and cm.group(1) == "사용")
    dest = re.split(r"[,、]", dest_line)[0].strip()
    dm = re.search(r"(\d{4})\D+(\d{1,2})\D+(\d{1,2})\D+(\d{1,2}):(\d{2})\s*~\s*(\d{4})\D+(\d{1,2})\D+(\d{1,2})\D+(\d{1,2}):(\d{2})", date_line)
    start = end = ""
    if dm:
        g = dm.groups()
        start = f"{g[0]}-{int(g[1]):02d}-{int(g[2]):02d}T{int(g[3]):02d}:{g[4]}"
        end = f"{g[5]}-{int(g[6]):02d}-{int(g[7]):02d}T{int(g[8]):02d}:{g[9]}"
    dm2 = re.search(r"([가-힣]{2,10}[시군구])\s+([가-힣]{2,10}(?:과|팀|국|실))", norm)
    dept = dm2.group(2) if dm2 else ""
    return [mk({"name": n, "dept": dept, "dest": dest, "purpose": purpose, "start": start, "end": end, "transport": "공무용차량" if car_used else "자차"}) for n in names]


def parse_from_plain_text(text: str, mk) -> list:
    draft = parse_draft_text(text, mk)
    if draft:
        return draft
    out = []
    line_re = re.compile(r"^\s*(\d+)\s+([가-힣]{2,4})\s+\S+\s+(\S+)\s+(\S+)\s+(근무지외|근무지내)\s+(사용|미사용)\s+(.+?)\s+(\d{4}-\d{2}-\d{2})\s+(\d{2}:\d{2})"
                         r"\s+(\d{4}-\d{2}-\d{2})\s+(\d{2}:\d{2}).*?(제\d호)\s+.*?\s+(\S+)\s+(결재완료|진행중)")
    for line in re.split(r"\r?\n", text):
        m = line_re.search(line)
        if not m:
            continue
        _, name, dept, position, gubun, car, purpose, sd, st, ed, et, grade, dest, _s = m.groups()
        if gubun != "근무지외":
            continue
        out.append(mk({"name": name, "dept": dept, "position": position, "purpose": purpose, "grade": grade, "dest": dest,
                       "start": sd + "T" + st, "end": ed + "T" + et, "transport": "공무용차량" if car == "사용" else "자차"}))
    return out


def detect_and_parse_rows(rows: list, mk) -> tuple:
    """(서식 이름, 출장 기록 목록)"""
    rows = norm_rows(rows)
    flat = "\n".join(" ".join(r) for r in rows)
    if re.search(r"기안상신\s*내역서|기안본문", flat):
        return "기안상신내역서", parse_draft_text("\n".join(" ".join(r) for r in rows), mk)
    generic = parse_generic_trip_rows(rows, mk)
    if generic:
        label = "출장내역(자동인식)"
        if re.search(r"출장\s*월별\s*집계", flat):
            label = "출장월별집계표"
        elif re.search(r"출\s*장\s*내\s*역\s*서", flat):
            label = "출장내역서"
        elif re.search(r"관외출장\s*집계표", flat):
            label = "관외출장 집계표"
        return label, generic
    title = (rows[0][0] if rows and rows[0] else "").strip()
    header_flat = " ".join(rows[1]) if len(rows) > 1 else ""
    if title == "출장내역서" or "식별번호" in header_flat:
        return "출장내역서", parse_trip_list_rows(rows, mk)
    return "관외출장 집계표", parse_aggregate_rows(rows, mk)


def extract_pdf_text(path: str) -> str:
    """PDF → 텍스트 (줄바꿈을 살려 한 줄에 한 항목이 오도록)"""
    try:
        from pypdf import PdfReader
        return "\n".join((p.extract_text() or "") for p in PdfReader(path).pages)
    except Exception:
        from pdfminer.high_level import extract_text
        return extract_text(path)


def import_trip_file(path: str, mk) -> tuple:
    """엑셀(첫 시트) 또는 PDF 에서 출장 기록을 읽는다. 반환: (서식 이름, 목록)"""
    if path.lower().endswith(".pdf"):
        return "PDF", parse_from_plain_text(extract_pdf_text(path), mk)
    from ...core import excel
    sheets = excel.read_sheets(path)
    return detect_and_parse_rows(next(iter(sheets.values())), mk)


# ─────────────────────────── 금액 → 한글, 날짜 ───────────────────────────
_KOR_NUM = ["", "일", "이", "삼", "사", "오", "육", "칠", "팔", "구"]
_KOR_U1 = ["", "십", "백", "천"]
_KOR_U2 = ["", "만", "억", "조"]


def number_to_korean(num) -> str:
    num = js_round(num)
    if num == 0:
        return "영"
    result, unit_pos, n = "", 0, num
    while n > 0:
        part = n % 10000
        if part > 0:
            ps, temp = "", part
            for i in range(4):
                digit = temp % 10
                if digit > 0:
                    ps = ("" if (digit == 1 and i > 0) else _KOR_NUM[digit]) + _KOR_U1[i] + ps
                temp //= 10
            result = ps + _KOR_U2[unit_pos] + result
        n //= 10000
        unit_pos += 1
    return result


def date_parts(s: str) -> dict:
    d = parse_dt(s)
    if not d:
        return {"y": "", "m": "", "d": "", "hm": ""}
    return {"y": d.year, "m": d.month, "d": d.day, "hm": d.strftime("%H:%M")}


# ─────────────────────────── 줄바꿈 ───────────────────────────
HWPX_WRAP_FIELDS = {44: 25, 58: 25}
HWPX_WRAP_CELL_ANCHORS = {44: '<hp:cellAddr colAddr="2" rowAddr="13"/>', 58: '<hp:cellAddr colAddr="2" rowAddr="16"/>'}
HWPX_CELL_PAD_HU = 282
HWPX_BODY_PT = 11


def wrap_text_by_chars(text, max_chars) -> list:
    s = "" if text is None else str(text)
    if not max_chars or len(s) <= max_chars:
        return [s]
    words = [w for w in s.split(" ") if w != ""]
    lines, cur = [], ""

    def forced(word):
        w = word
        while len(w) > max_chars:
            lines.append(w[:max_chars])
            w = w[max_chars:]
        return w
    for word in words:
        cand = word if cur == "" else cur + " " + word
        if len(cand) <= max_chars:
            cur = cand
        else:
            if cur != "":
                lines.append(cur)
            cur = word if len(word) <= max_chars else forced(word)
    lines.append(cur)
    return lines


def wrap_text_to_hwpx_runs(text, max_chars) -> tuple:
    lines = wrap_text_by_chars(text, max_chars)
    return "<hp:lineBreak/>".join(f"<hp:t>{escape_xml(l)}</hp:t>" for l in lines), len(lines)


def _char_w(ch: str) -> float:
    if ch.isdigit() and ch.isascii():
        return 0.55
    if ch in ",.":
        return 0.3
    if ch == " ":
        return 0.35
    if ch.isascii() and ch.isalpha():
        return 0.9 if ch in "mMWw" else 0.6
    return 1.0


def text_width_pt(s, pt) -> float:
    return sum((_char_w(ch) - 0.02) * pt for ch in str(s))


def wrap_text_by_width(text, avail_pt, pt=None) -> list:
    pt = pt or HWPX_BODY_PT
    s = "" if text is None else str(text)
    if text_width_pt(s, pt) <= avail_pt:
        return [s]
    segs = re.findall(r"[0-9][0-9,.]*|\s+|[^0-9\s]+", s) or [s]
    lines, cur = [], ""

    def push():
        nonlocal cur
        if cur.strip() != "":
            lines.append(cur.rstrip())
        cur = ""
    for seg in segs:
        if seg.isspace():
            if cur != "":
                cur += seg
            continue
        if text_width_pt(cur + seg, pt) <= avail_pt:
            cur += seg
            continue
        push()
        if text_width_pt(seg, pt) <= avail_pt:
            cur = seg
            continue
        for ch in seg:
            if cur != "" and text_width_pt(cur + ch, pt) > avail_pt:
                push()
            cur += ch
    push()
    return lines or [s]


# ─────────────────────────── 여행명세서 문구 ───────────────────────────
def build_travel_leg_texts(p: dict) -> dict:
    out = dict(dist1="", dist2="", fee1="", fee2="", distTotal="", feeTotal="", toll1="", toll2="", tollTotal="")

    def r1(n):
        return js_round(n * 10) / 10

    def km(n):
        s = f"{float(n):,.1f}"
        return (s[:-2] if s.endswith(".0") else s) + "km"

    def w(n):
        return f"{js_round(n):,}원"
    is_car = p["transport"] in ("자차", "공무용차량")
    dist = float(p.get("distance") or 0) if is_car else 0
    fee = js_round(float(p.get("transportFee") or 0))
    if dist > 0:
        d1 = r1(dist / 2)
        d2 = r1(dist - d1)
        out["dist1"], out["dist2"], out["distTotal"] = km(d1), km(d2), km(dist)
    if fee > 0:
        f1 = js_round(fee / 2)
        f2 = fee - f1
        legs = p.get("trainLegs")
        if p["transport"] == "기차" and legs and js_round(legs["go"] + legs["ret"]) == fee:
            f1, f2 = js_round(legs["go"]), js_round(legs["ret"])
        out["fee1"] = w(f1) if f1 > 0 else ""
        out["fee2"] = w(f2) if f2 > 0 else ""
        out["feeTotal"] = w(fee)
    toll = js_round(float(p.get("toll") or 0))
    if toll > 0:
        t1 = js_round(toll / 2)
        out["toll1"], out["toll2"], out["tollTotal"] = "통행료 " + w(t1), "통행료 " + w(toll - t1), "통행료 " + w(toll)
    return out


# ─────────────────────────── HWPX 채우기 (section0.xml 문자열 편집) ───────────────────────────
def build_person_data(t: dict, staff: list) -> dict:
    daily, meal, trans, lodge = calc_daily(t), calc_meal_fee(t), calc_transport_fee(t), calc_lodge_fee(t)
    total = js_round(daily + meal + trans + lodge)       # 문서에는 원 단위 정수만 (웹은 소수가 그대로 찍힐 수 있었음)
    s, e = date_parts(t.get("start")), date_parts(t.get("end"))
    si = next((x for x in staff if x.get("name") == t["name"]), {})
    one_way = t.get("transport") == "기차" and t.get("trainOneWay")
    return {
        "name": t["name"], "position": t.get("position") or "", "deptFull": "문화교육국 " + (t.get("dept") or DEFAULT_DEPT),
        "dest": t.get("dest") or "", "purpose": t.get("purpose") or "", "bankName": t["bankName"] if "bankName" in t else (si.get("bankName") or ""),
        "account": t["account"] if "account" in t else (si.get("account") or ""),
        "startY": s["y"], "startM": s["m"], "startD": s["d"], "endY": e["y"], "endM": e["m"], "endD": e["d"],
        "transport": t.get("transport"), "distance": t.get("distance") or 0,
        "transportFee": trans, "mealFee": meal, "mealCount": calc_meal_count(t), "dailyFee": daily, "lodgeFee": lodge,
        "nights": t.get("nights") or 0, "trainFare": trans if t.get("transport") == "기차" else 0,
        "trainLegs": ({"go": (t.get("trainFareGo") or 0) if t.get("trainTicketGo") else 0,
                       "ret": (t.get("trainFareReturn") or 0) if ((t.get("returnTransport") or "기차") == "기차" and t.get("trainTicketReturn")) else 0}
                      if one_way else None),
        "toll": (t.get("toll") or 0) if (t.get("transport") == "자차" and t.get("carRole") != "passenger" and t.get("tollReceipt")) else 0,
        "isHalf": daily == 12500, "total": total,
    }


def _transport_label(p) -> str:
    return "자가용" if p["transport"] == "자차" else ("관용차량" if p["transport"] == "공무용차량" else "-")


def build_field_transforms(p: dict) -> dict:
    t = {}
    t[42] = lambda: f"금{comma(p['total'])}원(금{number_to_korean(p['total'])}원)"
    t[44] = lambda: p["purpose"]
    t[47] = lambda: escape_xml(p["bankName"])
    t[48] = lambda: escape_xml(p["account"])
    t[50] = lambda: f" 근무처   {escape_xml(p['deptFull'])}"
    t[51] = lambda: f" 직  급   {escape_xml(p['position'])}"
    t[52] = lambda: f" 성  명   {escape_xml(spaced_name(p['name']))}    (인)"
    t[55] = lambda: f"                                                    {p['endY']}년   {p['endM']}월     일"
    t[58] = lambda: p["purpose"]
    t[60] = lambda: escape_xml(p["dest"])
    t[78] = lambda: f"{p['startM']}.{p['startD']}"
    t[80] = lambda: escape_xml(p["dest"])
    t[81] = lambda: escape_xml(p["dest"])
    train_shown = p["transport"] == "기차" and p["trainFare"] > 0
    if train_shown:
        t[92] = lambda: f"{comma(p['trainFare'])}원"
    t[101] = lambda: _transport_label(p)
    t[102] = lambda: f"왕복 {num_str(p['distance'])}km" if p["transport"] == "자차" else "왕복"
    if not train_shown:
        t[103] = lambda: f"{js_round(p['transportFee']):,}원"
    t[106] = lambda: f"{comma(p['mealFee'])}원"
    t[107] = lambda: f"{comma(p['mealFee'])}원"
    if p["nights"] > 0:
        t[112] = lambda: f" {p['nights']}야"
        t[114] = lambda: f"{comma(p['lodgeFee'])}원"
    t[120] = lambda: "(1/2)" if p["isHalf"] else ""
    t[122] = lambda: f"{comma(p['dailyFee'])}원"
    t[123] = lambda: f"{comma(p['dailyFee'])}원"
    t[132] = lambda: f"{comma(p['total'])}원"
    t[138] = lambda: f"{escape_xml(spaced_name(p['name']))}  (인)"
    return t


def _drop_lineseg(tc: str) -> str:
    return re.sub(r"<hp:linesegarray>[\s\S]*?</hp:linesegarray>", "", tc, count=1)


def sync_purpose_cell_layout(xml: str, anchor: str, line_count: int) -> str:
    if not anchor or not line_count or line_count <= 1:
        return xml
    pos = xml.find(anchor)
    if pos == -1:
        return xml
    s = xml.rfind("<hp:tc ", 0, pos)
    e = xml.find("</hp:tc>", pos)
    if s == -1 or e == -1:
        return xml
    return xml[:s] + _drop_lineseg(xml[s:e]) + xml[e:]


def locate_cell(xml: str, base: int, col: int, row: int):
    anchor = f'<hp:cellAddr colAddr="{col}" rowAddr="{row}"/>'
    a = xml.find(anchor, base)
    if a == -1:
        return None
    s = xml.rfind("<hp:tc ", 0, a)
    e = xml.find("</hp:tc>", a)
    if s == -1 or e == -1:
        return None
    tc = xml[s:e]
    m = re.search(r'<hp:cellSz width="(\d+)"', tc)
    avail = (int(m.group(1)) - HWPX_CELL_PAD_HU) / 100 if m else 50
    return s, e, tc, avail


def _runs_from_lines(lines) -> str:
    return "<hp:lineBreak/>".join(f"<hp:t>{escape_xml(l)}</hp:t>" for l in lines)


def fill_empty_cell(xml: str, base: int, col: int, row: int, text: str, pt=None) -> str:
    if not text:
        return xml
    c = locate_cell(xml, base, col, row)
    if not c:
        return xml
    s, e, tc, avail = c
    m = re.search(r'<hp:run charPrIDRef="(\d+)"/>', tc)
    if not m:
        return xml
    lines = wrap_text_by_width(text, avail, pt)
    tc = tc.replace(m.group(0), f'<hp:run charPrIDRef="{m.group(1)}">{_runs_from_lines(lines)}</hp:run>', 1)
    if len(lines) > 1:
        tc = _drop_lineseg(tc)
    return xml[:s] + tc + xml[e:]


def refit_cell(xml: str, base: int, col: int, row: int) -> str:
    c = locate_cell(xml, base, col, row)
    if not c:
        return xml
    s, e, tc, avail = c
    ts = re.findall(r"<hp:t>[\s\S]*?</hp:t>", tc)
    if len(ts) != 1:
        return xml
    inner = re.sub(r"^<hp:t>|</hp:t>$", "", ts[0])
    plain = inner.replace("&lt;", "<").replace("&gt;", ">").replace("&amp;", "&")
    lines = wrap_text_by_width(plain, avail)
    if len(lines) <= 1:
        return xml
    tc = _drop_lineseg(tc.replace(ts[0], _runs_from_lines(lines), 1))
    return xml[:s] + tc + xml[e:]


def fill_travel_statement_cells(block: str, p: dict) -> str:
    base = block.find("<hp:t>월 일</hp:t>")
    if base == -1:
        return block
    out = block
    L = build_travel_leg_texts(p)
    for c, r in ((1, 2), (5, 2), (1, 3), (5, 3)):
        out = refit_cell(out, base, c, r)
    out = fill_empty_cell(out, base, 10, 2, L["dist1"])
    out = fill_empty_cell(out, base, 12, 2, L["fee1"])
    out = fill_empty_cell(out, base, 10, 3, L["dist2"])
    out = fill_empty_cell(out, base, 12, 3, L["fee2"])
    out = fill_empty_cell(out, base, 10, 10, L["distTotal"])
    out = fill_empty_cell(out, base, 12, 10, L["feeTotal"])
    out = fill_empty_cell(out, base, 17, 2, L["toll1"], 9)
    out = fill_empty_cell(out, base, 17, 3, L["toll2"], 9)
    out = fill_empty_cell(out, base, 17, 10, L["tollTotal"], 9)
    if not (p["transport"] == "기차" and p["trainFare"] > 0):
        out = refit_cell(out, base, 7, 15)
    return out


def fill_remark_cells(block: str, p: dict) -> str:
    if p["transport"] != "공무용차량":
        return block
    return block.replace('<hp:run charPrIDRef="29"/>', '<hp:run charPrIDRef="29"><hp:t>관용차량</hp:t></hp:run>')


def fill_hwpx_block(block: str, p: dict) -> str:
    transforms = build_field_transforms(p)
    counter = [-1]
    wrap_counts = {}

    def rep(m):
        counter[0] += 1
        i = counter[0]
        if i not in transforms:
            return m.group(0)
        val = transforms[i]()
        mx = HWPX_WRAP_FIELDS.get(i)
        if mx:
            xml, n = wrap_text_to_hwpx_runs(val, mx)
            wrap_counts[i] = n
            return xml
        return f"<hp:t>{val}</hp:t>"
    out = re.sub(r"<hp:t>([\s\S]*?)</hp:t>", rep, block)
    for idx, n in wrap_counts.items():
        out = sync_purpose_cell_layout(out, HWPX_WRAP_CELL_ANCHORS[idx], n)
    out = fill_remark_cells(out, p)
    return fill_travel_statement_cells(out, p)


_MARKER = '<hp:p id="0" paraPrIDRef="15" styleIDRef="14" pageBreak="0" columnBreak="0" merged="0">'


def build_filled_section_xml(master: str, people: list) -> str:
    idxs, pos = [], 0
    while True:
        i = master.find(_MARKER, pos)
        if i == -1:
            break
        idxs.append(i)
        pos = i + len(_MARKER)
    if not idxs:
        raise ValueError("서식에서 결의서 블록을 찾을 수 없습니다.")
    tail = "</hs:sec>"
    content_end = len(master) - len(tail) if master.endswith(tail) else len(master)
    head_t = master[:idxs[1]] if len(idxs) > 1 else master[:content_end]
    unit_t = (master[idxs[1]:(idxs[2] if len(idxs) > 2 else content_end)]) if len(idxs) > 1 else head_t
    out = ""
    for i, p in enumerate(people):
        out += fill_hwpx_block(head_t if i == 0 else unit_t, p)
    return out + tail


def validate_template(data: bytes):
    z = H.open_hwpx(data)
    if "Contents/section0.xml" not in z.namelist():
        raise ValueError("section0.xml 없음")


def generate_hwpx(template: bytes, people: list, out_path: str | None = None):
    """템플릿 zip 의 Contents/section0.xml 만 바꿔 새 .hwpx 를 만든다. out_path 가 없으면 bytes 반환"""
    z = H.open_hwpx(template)
    master = z.read("Contents/section0.xml").decode("utf-8")
    filled = build_filled_section_xml(master, people)
    entries = []
    for n in z.namelist():
        entries.append((n, filled.encode("utf-8") if n == "Contents/section0.xml" else z.read(n)))
    return H.write_hwpx(entries, out_path)


# ─────────────────────────── 화면 미리보기 / PDF (결의서 두 쪽의 표를 HTML 로) ───────────────────────────
HX_COLS_P1 = [11.06, 3.69, 3.69, 3.69, 5.53, 4.66, 11.06, 11.33, 11.33, 5.57, 5.57, 5.57, 12.43, 4.85]
HX_COLS_P2 = [10.00, 5.34, 5.34, 5.34, 5.34, 3.56, 3.56, 3.56, 5.23, 5.23, 5.23, 5.23, 5.23, 5.23, 8.86, 4.43, 4.43, 8.86]


def _nb(s) -> str:
    """연속 공백을 보존 (Qt 리치텍스트는 공백을 접는다)"""
    return escape_xml(s).replace("  ", "&nbsp;&nbsp;").replace("\n", "<br>")


def build_receipt_html(p: dict, font_px: float = 12.5, pdf: bool = False) -> str:
    """결의서 1쪽(14열)·2쪽(18열) 표를 그대로 옮긴 HTML (Qt 리치텍스트 호환).  반환: 두 쪽을 이은 문서"""
    unit, fpx = ("pt", font_px * 0.62) if pdf else ("px", font_px)

    def tbl(cols, rows):
        head = "<tr>" + "".join(f'<td width="{w}%" style="padding:0; border:0; font-size:1px;"></td>' for w in cols) + "</tr>"
        return (f'<table width="100%" border="1" cellspacing="0" cellpadding="4" style="border-collapse:collapse; font-size:{fpx}{unit};">'
                f"{head}{''.join(rows)}</table>")

    def td(html, c=1, r=1, cls=""):
        attrs = (f' colspan="{c}"' if c > 1 else "") + (f' rowspan="{r}"' if r > 1 else "")
        al = "left" if cls == "left" else "center"
        extra = f"font-size:{fpx * 1.15:.1f}{unit}; font-weight:700;" if cls == "amt" else ""
        return f'<td{attrs} align="{al}" valign="middle" style="{extra}">{html if html != "" else "&nbsp;"}</td>'
    transport_label = "자가용" if p["transport"] == "자차" else ("관용차량" if p["transport"] == "공무용차량" else (p["transport"] or "-"))
    dist_label = f"왕복 {num_str(p['distance'])}km" if p["transport"] == "자차" else "왕복"
    nights_cell = f" {p['nights']}야" if p["nights"] > 0 else " 야"
    lodge_amt = comma(js_round(p["lodgeFee"])) + "원" if p["nights"] > 0 else "원"
    train_shown = p["transport"] == "기차" and p["trainFare"] > 0
    train_cell = f"{comma(p['trainFare'])}원" if train_shown else "원"
    L = build_travel_leg_texts(p)
    pur = "<br>".join(escape_xml(l) for l in wrap_text_by_chars(p["purpose"], HWPX_WRAP_FIELDS[44]))
    n = lambda s: _nb(s)  # noqa
    total_s = comma(p["total"])
    amt_text = "금" + total_s + "원(금" + number_to_korean(p["total"]) + "원)"
    who_text = n("근무처 " + p["deptFull"] + "   직  급 " + p["position"] + "   성  명 " + spaced_name(p["name"]) + "   (인)")
    rcpt_text = n("위 금액을 영수함.      " + str(p["endY"]) + "년 " + str(p["endM"]) + "월    일  성  명  김 욱 현   (인)")
    dp, mf, df = comma(p["dailyFee"]) + "원", comma(p["mealFee"]) + "원", comma(js_round(p["transportFee"])) + "원"
    sm = f"{p['startM']}.{p['startD']}"
    meal_n = str(p["mealCount"]) + "끼"
    r1 = [
        f"<tr>{td('증제 호', 6)}{td('', 8)}</tr>",
        f"<tr>{td('계장')}{td('', 3)}{td('분임재무관', 2)}{td('')}{td('2026년도 일반회계', 2)}{td('취급자', 3)}{td('지출원', 2)}</tr>",
        f"<tr>{td('')}{td('', 3)}{td('', 2)}{td('')}{td('세출과목', 2)}{td('', 3)}{td('', 2)}</tr>",
        f"<tr>{td('발 의', 3, 2)}{td('', 2, 2)}{td('인', 1, 2)}{td('조 직')}{td('문화유산과', 2)}{td('발 의', 2, 2)}{td('', 2, 2)}{td('인', 1, 2)}</tr>",
        f"<tr>{td('정 책')}{td('전통문화 보존 및 전승', 2)}</tr>",
        f"<tr>{td('원인행위부기재', 3, 2)}{td('', 2, 2)}{td('인', 1, 2)}{td('지출부기재', 2, 2)}{td('', 2, 2)}{td('인', 1, 2)}</tr>",
        f"<tr>{td('단 위')}{td('문화유산 보존 및 전승', 2)}</tr>",
        f"<tr>{td('', 3, 2)}{td('', 2, 2)}{td('', 1, 2)}{td('세 부')}{td('국가유산 운영', 2)}{td('지급명령발령부기재', 2, 2)}{td('', 2, 2)}{td('인', 1, 2)}</tr>",
        f"<tr>{td('편성목')}{td('여비', 2)}</tr>",
        f"<tr>{td('', 3, 2)}{td('', 2, 2)}{td('', 1, 2)}{td('지급명령번 호', 2, 2)}{td('제 호', 3, 2)}</tr>",
        f"<tr>{td('통계목')}{td('국내여비', 2)}</tr>",
        f"<tr>{td('개산급에대한정산', 3)}{td('개산액', 3)}{td('년 월 일', 3)}{td('정 산 액', 2)}{td('', 3)}</tr>",
        f"<tr>{td(amt_text, 14, 1, 'amt')}</tr>",
        f"<tr>{td('적 요', 2)}{td(pur, 7)}{td('거래은행<br>계좌번호', 2)}{td(escape_xml(p['bankName']) + '<br>' + escape_xml(p['account']), 3)}</tr>",
        f"<tr>{td('출장자', 2)}{td(who_text, 12)}</tr>",
        f"<tr>{td('영수', 2)}{td(rcpt_text, 12)}</tr>",
        f"<tr>{td('용무', 2)}{td(pur, 6)}{td('출장지', 2)}{td(escape_xml(p['dest']), 4)}</tr>",
        f"<tr>{td('주관과', 2)}{td('', 12, 2)}</tr>",
        f"<tr>{td('취급자인', 2)}</tr>",
    ]
    r2 = [
        f"<tr>{td('월 일', 1, 2)}{td('출발지', 2, 2)}{td('경유지', 2, 2)}{td('도착지', 3, 2)}{td('거 리 와 요 금', 6)}{td('일수', 1, 2)}{td('야수', 2, 2)}{td('비 고', 1, 2)}</tr>",
        f"<tr>{td('종 별', 2)}{td('거 리', 2)}{td('요 금', 2)}</tr>",
        f"<tr>{td(sm)}{td('익산', 2)}{td('', 2)}{td(escape_xml(p['dest']), 3)}{td('', 2)}{td(L['dist1'], 2)}{td(L['fee1'], 2)}{td('')}{td('', 2)}{td(L['toll1'])}</tr>",
        f"<tr>{td('')}{td(escape_xml(p['dest']), 2)}{td('', 2)}{td('익산', 3)}{td('', 2)}{td(L['dist2'], 2)}{td(L['fee2'], 2)}{td('')}{td('', 2)}{td(L['toll2'])}</tr>",
    ]
    for _ in range(6):
        r2.append(f"<tr>{td('')}{td('', 2)}{td('', 2)}{td('', 3)}{td('', 2)}{td('', 2)}{td('', 2)}{td('')}{td('', 2)}{td('')}</tr>")
    r2 += [
        f"<tr>{td('계')}{td('', 2)}{td('', 2)}{td('', 3)}{td('', 2)}{td(L['distTotal'], 2)}{td(L['feeTotal'], 2)}{td('')}{td('', 2)}{td(L['tollTotal'])}</tr>",
        f"<tr>{td('여 비 명 세 서', 18)}</tr>",
        f"<tr>{td('종 별', 2)}{td('', 2)}{td('', 3)}{td('원', 4)}{td('참가비', 2)}{td('', 3)}{td('원', 2)}</tr>",
        f"<tr>{td('철 도 임', 2)}{td('등실비', 2)}{td('회', 3)}{td(train_cell, 4)}{td('등실비', 2)}{td('', 3)}{td('', 2)}</tr>",
        f"<tr>{td('선 임', 2)}{td('등실비', 2)}{td('', 3)}{td('원', 4)}{td('등실비', 2)}{td('', 3)}{td('', 2)}</tr>",
        f"<tr>{td('차 마 임', 2)}{td(escape_xml(transport_label), 2)}{td(escape_xml(dist_label), 3)}{td('원' if train_shown else df, 4)}{td('', 2)}{td('', 3)}{td('', 2)}</tr>",
        f"<tr>{td('식 비', 2)}{td(meal_n, 2)}{td(mf, 3)}{td(mf, 4)}{td('', 2)}{td('1일당', 3)}{td('25,000원 한도', 2)}</tr>",
        f"<tr>{td('숙 박 료', 2)}{td(nights_cell, 2)}{td('1야당 원', 3)}{td(lodge_amt, 4)}{td('야', 2)}{td('1야당', 3)}{td('', 2)}</tr>",
        f"<tr>{td('일 비' + ('(1/2)' if p['isHalf'] else ''), 2)}{td('1일', 2)}{td(dp, 3)}{td(dp, 4)}{td('', 2)}{td('1일당', 3)}{td('', 2)}</tr>",
        f"<tr>{td('이 전 료', 2)}{td('', 2)}{td('부임수당', 3)}{td('', 4)}{td('', 2)}{td('가족이전표', 3)}{td('', 2)}</tr>",
        f"<tr>{td('합 계', 4)}{td('', 3)}{td(total_s + '원', 4)}{td('', 2)}{td('', 3)}{td('', 2)}</tr>",
        f"<tr>{td('출장명령부대 조 필', 2)}{td('김 욱 현 (인)', 4)}{td('청구금액 외에는 포기함', 3)}{td(escape_xml(spaced_name(p['name'])) + '  (인)', 4)}{td('비 고', 3)}{td('', 2)}</tr>",
    ]
    brk = " page-break-before:always;" if pdf else ""
    return (f'<p align="center" style="font-size:{fpx * 1.7:.1f}{unit}; font-weight:700; margin:6px 0 10px 0;">여 비 지 출 결 의 서</p>' + tbl(HX_COLS_P1, r1)
            + f'<p align="center" style="font-size:{fpx * 1.4:.1f}{unit}; font-weight:700; margin:22px 0 10px 0;{brk}">여 행 명 세 서</p>' + tbl(HX_COLS_P2, r2))
