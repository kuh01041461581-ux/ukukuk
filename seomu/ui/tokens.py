# -*- coding: utf-8 -*-
"""디자인 토큰 — 색·모서리·간격·글꼴을 여기 한 곳에서만 정한다.

기준: Notion(차분한 글자·여백) × Linear(또렷한 1px 선) × Windows 11(둥근 모서리·부드러운 질감)
포인트색은 인디고→바이올렛 그라데이션. 모든 화면 부품은 이 값만 읽는다.
"""

LIGHT = {
    "bg": "#F5F6FB", "surface": "#FFFFFF", "surface2": "#F8F9FD", "surface3": "#EDEFF7",
    "hover": "#F0F2FA", "border": "#E4E7F1", "border_strong": "#CFD4E4",
    "text": "#11131C", "text2": "#4D576B", "text3": "#6E7890",
    "primary": "#4F5DF0", "primary_hover": "#3E4CDF",
    "primary_tint": "#EEF0FE", "primary_tint2": "#E0E4FD", "primary_border": "#C4CBFA",
    "g1": "#5B6DF8", "g2": "#7557F5", "g1h": "#4E60EF", "g2h": "#6745EA",
    "soft1": "#EEF1FF", "soft2": "#F4EEFF",
    "success": "#0E9F6E", "success_tint": "#E5F7EF", "success_g1": "#12B981", "success_g2": "#0E9F6E",
    "warning": "#C26A00", "warning_tint": "#FFF3DD",
    "danger": "#E03E4A", "danger_hover": "#C42F3B", "danger_tint": "#FDECEE",
    "shadow": "rgba(40,52,130,0.16)", "scrim": "rgba(10,12,24,0.45)",
    "card_top": "#FFFFFF", "card_bottom": "#FBFBFE",
    "sidebar_top": "#FFFFFF", "sidebar_bottom": "#F4F5FB",
}

DARK = {
    "bg": "#0B0D13", "surface": "#141720", "surface2": "#181C27", "surface3": "#212634",
    "hover": "#1D2230", "border": "#232938", "border_strong": "#323A4E",
    "text": "#ECEEF5", "text2": "#A3ACC0", "text3": "#7C869C",
    "primary": "#7B8BFF", "primary_hover": "#98A5FF",
    "primary_tint": "#1A2042", "primary_tint2": "#232B5A", "primary_border": "#343F82",
    "g1": "#6577FB", "g2": "#8A63F8", "g1h": "#7585FF", "g2h": "#9872FF",
    "soft1": "#181D3C", "soft2": "#211A3E",
    "success": "#34D399", "success_tint": "#0F2A20", "success_g1": "#12B981", "success_g2": "#0E9F6E",
    "warning": "#F5A524", "warning_tint": "#2E2410",
    "danger": "#F4606B", "danger_hover": "#FF8089", "danger_tint": "#34171B",
    "shadow": "rgba(0,0,0,0.55)", "scrim": "rgba(0,0,0,0.6)",
    "card_top": "#161A24", "card_bottom": "#131620",
    "sidebar_top": "#12151D", "sidebar_bottom": "#0E1017",
}

# 모서리 (px)
R_SM, R_BTN, R_INPUT, R_CARD, R_MODAL = 6, 8, 8, 14, 16

# 글자 크기 (px) — 카드 제목 15 / 본문 13.5 / 설명 13 / 보조 12
FS = {"title": 25, "h2": 15, "body": 13.5, "desc": 13, "small": 12, "tiny": 11}

FONT_FAMILY = "Noto Sans KR"
FONT_FALLBACK = '"Noto Sans KR", "Malgun Gothic", "Segoe UI", sans-serif'
