# -*- coding: utf-8 -*-
"""유리(데스크탑) 달력용: 음력·24절기·기념일, 바탕화면 이미지를 흐리게 깔아 만드는 반투명 배경"""
import datetime as dt
import math
import os
import sys

from holidays_kr import holiday_name

# ── 음력 ──
_lunar_cache = {}
_cal = None


def lunar_text(d):
    """'(음)9.3' 형태. 변환을 못 하면 빈 문자열"""
    global _cal
    if d in _lunar_cache:
        return _lunar_cache[d]
    out = ""
    try:
        if _cal is None:
            from korean_lunar_calendar import KoreanLunarCalendar
            _cal = KoreanLunarCalendar()
        if _cal.setSolarDate(d.year, d.month, d.day):
            out = f"(음){_cal.lunarMonth}.{_cal.lunarDay}" if not _cal.isIntercalation else f"(음)윤{_cal.lunarMonth}.{_cal.lunarDay}"
    except Exception:
        out = ""
    _lunar_cache[d] = out
    return out


# ── 24절기 (2001~2100년 근사식: 하루 정도 어긋날 수 있음) ──
_TERMS = [(1, "소한", 5.4055), (1, "대한", 20.12), (2, "입춘", 3.87), (2, "우수", 18.73), (3, "경칩", 5.63),
          (3, "춘분", 20.646), (4, "청명", 4.81), (4, "곡우", 20.1), (5, "입하", 5.52), (5, "소만", 21.04),
          (6, "망종", 5.678), (6, "하지", 21.37), (7, "소서", 7.108), (7, "대서", 22.83), (8, "입추", 7.5),
          (8, "처서", 23.13), (9, "백로", 7.646), (9, "추분", 23.042), (10, "한로", 8.318), (10, "상강", 23.438),
          (11, "입동", 7.438), (11, "소설", 22.36), (12, "대설", 7.18), (12, "동지", 21.94)]
_term_cache = {}


def solar_terms(year):
    if year in _term_cache:
        return _term_cache[year]
    out = {}
    y = year - 2000
    for m, name, c in _TERMS:
        leap = (y - 1) // 4 if m <= 2 else y // 4
        out[(m, int(y * 0.2422 + c) - leap)] = name
    _term_cache[year] = out
    return out


# ── 기념일 (공휴일이 아닌 것) ──
_ANNIV = {(2, 14): "발렌타인", (3, 14): "화이트데이", (4, 5): "식목일", (5, 8): "어버이날", (5, 15): "스승의날",
          (5, 21): "부부의날", (6, 25): "6.25", (7, 17): "제헌절", (10, 1): "국군의날", (10, 31): "만성절",
          (11, 11): "빼빼로데이", (12, 24): "이브", (12, 31): "제야"}


def special_day(d):
    """(표시 글자, 종류) 종류: holiday(공휴일) | anniv(기념일) | term(절기) | None"""
    h = holiday_name(d)
    if h:
        return h, "holiday"
    a = _ANNIV.get((d.month, d.day))
    if a:
        return a, "anniv"
    t = solar_terms(d.year).get((d.month, d.day))
    if t:
        return t, "term"
    return "", None


# ── 바탕화면 이미지 ──
_wp_cache = {}


def _screen_size():
    import ctypes
    u = ctypes.windll.user32
    return u.GetSystemMetrics(0), u.GetSystemMetrics(1)


def wallpaper(sw, sh):
    """현재 바탕화면을 화면 크기(sw x sh)로 맞춘 PIL 이미지. 못 가져오면 None"""
    if sys.platform != "win32":
        return None
    try:
        import ctypes
        import winreg
        from PIL import Image
        buf = ctypes.create_unicode_buffer(520)
        ctypes.windll.user32.SystemParametersInfoW(0x73, 520, buf, 0)   # SPI_GETDESKWALLPAPER
        path = buf.value
        if not path or not os.path.exists(path):
            path = os.path.join(os.environ.get("APPDATA", ""), r"Microsoft\Windows\Themes\TranscodedWallpaper")
        if not os.path.exists(path):
            return None
        style, tile = "10", "0"
        try:
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Control Panel\Desktop") as k:
                style = str(winreg.QueryValueEx(k, "WallpaperStyle")[0])
                tile = str(winreg.QueryValueEx(k, "TileWallpaper")[0])
        except OSError:
            pass
        key = (path, os.path.getmtime(path), style, tile, sw, sh)
        if key in _wp_cache:
            return _wp_cache[key]
        im = Image.open(path).convert("RGB")
        canvas = Image.new("RGB", (sw, sh), (0, 0, 0))
        w, h = im.size
        if tile == "1":                                  # 바둑판식
            for x in range(0, sw, w):
                for y in range(0, sh, h):
                    canvas.paste(im, (x, y))
        elif style == "2":                               # 채우기(늘이기)
            canvas = im.resize((sw, sh))
        elif style == "6":                               # 맞춤
            s = min(sw / w, sh / h)
            r = im.resize((max(1, int(w * s)), max(1, int(h * s))))
            canvas.paste(r, ((sw - r.width) // 2, (sh - r.height) // 2))
        elif style == "0":                               # 가운데
            canvas.paste(im, ((sw - w) // 2, (sh - h) // 2))
        else:                                            # 10 채우기, 22 스팬 등 → 화면을 덮게 확대 후 가운데 자르기
            s = max(sw / w, sh / h)
            r = im.resize((max(1, int(w * s)), max(1, int(h * s))))
            canvas = r.crop(((r.width - sw) // 2, (r.height - sh) // 2, (r.width - sw) // 2 + sw, (r.height - sh) // 2 + sh))
        _wp_cache.clear()
        _wp_cache[key] = canvas
        return canvas
    except Exception:
        return None


def fallback_wallpaper(sw, sh, base=(18, 60, 110)):
    """바탕화면을 못 읽을 때 쓰는 부드러운 그라데이션"""
    from PIL import Image
    small = Image.new("RGB", (64, 36))
    px = small.load()
    for y in range(36):
        for x in range(64):
            t = (x / 63 * 0.6 + y / 35 * 0.4)
            px[x, y] = (int(base[0] + 90 * t), int(base[1] + 80 * (1 - t)), int(base[2] + 90 * (1 - t) ** 0.5))
    return small.resize((sw, sh))


# ── 유리 배경 만들기 (윈도우 11 아크릴 느낌: 은은한 흐림 + 낮은 대비 + 둥근 모서리) ──
TINT_ORDER = ["그래파이트", "남색", "보라", "초록", "장미", "밝게"]
TINTS = {"그래파이트": (17, 19, 27), "남색": (20, 32, 66), "보라": (40, 28, 76), "초록": (16, 48, 42),
         "장미": (66, 26, 44), "밝게": (236, 240, 247), "청록": (22, 60, 82)}


def _mix(img, color, a):
    from PIL import Image
    return Image.blend(img, Image.new("RGB", img.size, color), a)


def _rmask(w, h, r, inset=0):
    """부드러운 모서리의 둥근 사각형 마스크 (2배로 그려 줄여서 계단 현상 제거)"""
    from PIL import Image, ImageDraw
    ss = 3
    m = Image.new("L", (w * ss, h * ss), 0)
    ImageDraw.Draw(m).rounded_rectangle((inset * ss, inset * ss, w * ss - 1 - inset * ss, h * ss - 1 - inset * ss),
                                        radius=max(0, (r - inset)) * ss, fill=255)
    return m.resize((w, h), Image.LANCZOS)


def render_bg(wp, ox, oy, W, H, panel, cells, tint=(17, 19, 27), strength=0.55, blur=20, radius=14,
              cell_radius=9, light=False, accent=None):
    """wp: 화면 크기 배경 PIL 이미지, (ox,oy): 창이 화면에서 시작하는 위치
    panel: 유리 판 사각형 (x1,y1,x2,y2), cells: 칸 사각형 목록 [(x1,y1,x2,y2,weekend)]
    반환: W x H 의 PIL RGB 이미지"""
    from PIL import Image, ImageChops, ImageEnhance, ImageFilter, ImageStat
    base = Image.new("RGB", (W, H), (0, 0, 0))
    base.paste(wp, (-ox, -oy))
    px1, py1, px2, py2 = panel
    pw, ph = px2 - px1, py2 - py1
    # 부드러운 그림자
    sh = Image.new("L", (W, H), 0)
    sh.paste(_rmask(pw, ph, radius), (px1, py1 + 6))
    sh = sh.filter(ImageFilter.GaussianBlur(14)).point(lambda v: int(v * 0.38))
    out = Image.composite(Image.new("RGB", (W, H), (0, 0, 0)), base, sh)
    # 유리 판: 흐린 바탕화면 + 색 입히기
    blurred = base.crop(panel).filter(ImageFilter.GaussianBlur(blur))
    glass = _mix(blurred, tint, strength)
    if not light:
        lum = ImageStat.Stat(glass.convert("L")).mean[0]
        if lum > 60:                          # 밝은 바탕화면이어도 글자가 잘 읽히게 어둡게
            glass = ImageEnhance.Brightness(glass).enhance(60 / lum)
    out.paste(glass, (px1, py1), _rmask(pw, ph, radius))
    # 칸: 판보다 아주 조금만 밝게 + 거의 안 보이는 얇은 선 (윈도우 11 위젯처럼 조용하게)
    over, a_fill, a_week = ((255, 255, 255), 0.55, 0.30) if light else ((255, 255, 255), 0.05, 0.022)
    ring_c, ring_a = ((0, 0, 0), 0.05) if light else ((255, 255, 255), 0.032)
    for cell in cells:
        x1, y1, x2, y2, weekend = cell[:5]
        kind = cell[5] if len(cell) > 5 else None
        w, h = x2 - x1, y2 - y1
        if w < 4 or h < 4:
            continue
        m = _rmask(w, h, cell_radius)
        region = out.crop((x1, y1, x2, y2))
        a = a_week if weekend else a_fill
        col, ra, rc = over, ring_a, ring_c
        if kind == "out":                    # 이번 달이 아닌 날: 더 흐리게
            a, ra = a * 0.35, ring_a * 0.5
        elif kind == "today" and accent:     # 오늘: 강조색이 아주 살짝 비치게
            col, a, ra, rc = accent, (0.16 if light else 0.13), 0.32, accent
        out.paste(Image.blend(region, Image.new("RGB", (w, h), col), a), (x1, y1), m)
        ring = ImageChops.subtract(m, _rmask(w, h, cell_radius, inset=1)).point(lambda v, ra=ra: int(v * ra * 3.2))
        out.paste(Image.new("RGB", (w, h), rc), (x1, y1), ring)
    return out

def wallpaper_sig():
    """바탕화면이 바뀌었는지 알아보는 표식 (경로, 수정시각)"""
    if sys.platform != "win32":
        return None
    try:
        import ctypes
        buf = ctypes.create_unicode_buffer(520)
        ctypes.windll.user32.SystemParametersInfoW(0x73, 520, buf, 0)
        path = buf.value or os.path.join(os.environ.get("APPDATA", ""), r"Microsoft\Windows\Themes\TranscodedWallpaper")
        return (path, os.path.getmtime(path))
    except Exception:
        return None
