# -*- coding: utf-8 -*-
"""도구 목록 — 새 도구는 여기에 한 항목만 추가하면 사이드바·홈·빠른 이동에 모두 나타난다"""
from dataclasses import dataclass, field


@dataclass
class ToolInfo:
    id: str
    group: str
    label: str
    icon: str
    title: str
    desc: str
    tags: list = field(default_factory=list)
    steps: list = field(default_factory=list)
    factory: str = ""          # "패키지.모듈:클래스"
    fill: bool = False         # 화면을 가득 채우는 편집형 도구(제목 머리글 생략)


GROUPS = ["여비", "정산", "문서", "라벨"]

TOOLS = [
    ToolInfo("travel", "여비", "관내여비", "wallet", "관내여비 지급명세서",
             "출장 원본 집계표(엑셀)를 올리면 인원별 산정 규칙에 따라 지급액을 자동 계산하고, 지급명세서 양식으로 바로 내려받을 수 있습니다.",
             ["엑셀 입력", "엑셀 출력"], ["출장 집계표 올리기", "대상자·규칙 확인", "지급명세서 내려받기"],
             "seomu.tools.travel:TravelPage"),
    ToolInfo("outstation", "여비", "관외출장", "route", "관외출장 여비지출결의서",
             "부서원 계좌와 출장 내역을 입력하면 여비를 계산하고, 여비지출결의서(.hwpx)와 지급·상세입력 엑셀 양식을 만들어 줍니다.",
             ["엑셀·PDF 입력", "HWPX 출력", "거리 계산"], ["부서원 계좌 등록", "출장 내역 입력", "거리·여비 계산", "결의서·엑셀 내려받기"],
             "seomu.tools.outstation:OutstationPage"),
    ToolInfo("meal", "정산", "급식비", "fork", "급식비(매식비) 정산",
             "인사랑 초과근무 내역 엑셀에서 평일 20시 이후 야간근무자를 뽑아 급식확인서·급식지급 상세내역·초과근무내역 정산 엑셀을 만듭니다.",
             ["엑셀 입력", "엑셀 출력"], ["초과근무 엑셀 올리기", "급식처·인원 입력", "정산 엑셀 내려받기"],
             "seomu.tools.meal:MealPage"),
    ToolInfo("utility", "정산", "공과금 고지서", "bolt", "공과금 고지서 인식",
             "전기·상하수도·통신요금 고지서(이미지/PDF)를 AI로 읽어 거래처입력 양식 엑셀로 만들어 줍니다.",
             ["이미지·PDF 입력", "AI 인식", "엑셀 출력"], ["고지서 종류 선택", "이미지·PDF 올리기", "표 확인·수정", "엑셀 내려받기"],
             "seomu.tools.utility:UtilityPage"),
    ToolInfo("blur", "문서", "얼굴 블러", "face", "HWPX 얼굴 자동 블러",
             "HWPX 문서 안의 사진에서 얼굴을 자동으로 찾아 모자이크·블러 처리한 새 HWPX 파일을 만들어 줍니다.",
             ["HWPX 입력", "HWPX 출력", "PC 안에서 처리"], ["HWPX 파일 선택", "얼굴 자동 인식", "필요하면 보정", "HWPX 내려받기"],
             "seomu.tools.blur:BlurPage"),
    ToolInfo("compress", "문서", "이미지 압축", "zip", "HWPX 이미지 압축",
             "HWPX 문서 안의 사진과 미리보기 썸네일을 지정한 크기·품질로 줄여 파일 용량을 낮춥니다. 서식과 내용은 그대로 유지됩니다.",
             ["HWPX 입력", "HWPX 출력", "PC 안에서 처리"], ["HWPX 파일 선택", "크기·품질 정하기", "압축 후 내려받기"],
             "seomu.tools.compress:CompressPage"),
    ToolInfo("labels", "라벨", "우편 라벨지", "tag", "우편 라벨지 자동화",
             "엑셀 주소 목록으로 우편 라벨을 만들어 인쇄하고, 사진·문구가 들어가는 일반 라벨도 자유롭게 편집합니다.",
             ["엑셀 입력", "라벨 편집", "인쇄"], ["데이터 불러오기", "라벨 편집", "출력(인쇄)"],
             "seomu.tools.labels:LabelsPage", fill=True),
]


def by_id(tool_id: str) -> ToolInfo | None:
    for t in TOOLS:
        if t.id == tool_id:
            return t
    return None
