# -*- coding: utf-8 -*-
"""보도자료를 AI(Claude)로 쓰기 — 사용자가 프롬프트처럼 적은 글을 보도자료 문체의 초안으로 만들어 준다.
인터넷과 사용자의 API 키가 필요하다(키는 이 PC 의 설정 파일에만 저장). 적은 글은 Anthropic 서버로 전송된다."""
import json
import re
import urllib.error
import urllib.request

API_URL = "https://api.anthropic.com/v1/messages"
DEFAULT_MODEL = "claude-sonnet-5-5"

SYSTEM = """당신은 대한민국 지방자치단체 홍보 담당 공무원입니다. 사용자가 적은 요청·메모를 바탕으로 언론 배포용 보도자료 초안을 씁니다.

규칙
- 사용자가 제공한 사실(날짜·시각·장소·금액·인원·기관명)은 그대로 쓰고 바꾸거나 새로 지어내지 않습니다. 모르는 사실은 [  ] 로 비워 둡니다.
- 문체는 보도자료체: '~한다', '~밝혔다', '~될 예정이다', '~기대하고 있다'. 과장·광고 문구는 피하고 담백하게 씁니다.
- 첫 문단은 리드문(누가·언제·어디서·무엇을)이고 제목은 핵심을 담아 간결하게 씁니다.
- 이어서 추진 배경·취지, 주요 내용(여러 항목은 △ 로 나열), 참여 대상·신청 방법·사업비(있을 때), 기대효과 순으로 구성합니다.
- 인용문은 '시 관계자는' 또는 요청에 적힌 인물이 한 말로, 사실을 해치지 않는 무난한 내용으로 한 문단만 씁니다. 요청에 인용 대상이 없으면 '[   ]' 로 이름을 비웁니다.
- 마지막 문단은 문의처(요청에 담당부서·전화가 있으면 그것)로 맺습니다.
- 각 문단은 2~3문장, 전체 6~10문단.

출력은 JSON 한 덩어리만 냅니다(설명·코드블록 없이):
{"title": "제목", "sub1": "부제1(– … –)", "sub2": "부제2(– … –)", "paragraphs": ["문단1", "문단2", ...]}"""


class AIError(Exception):
    pass


def _extract_json(text):
    t = (text or "").strip()
    t = re.sub(r"^```(?:json)?\s*|\s*```$", "", t, flags=re.S).strip()
    try:
        return json.loads(t)
    except ValueError:
        m = re.search(r"\{.*\}", t, re.S)
        if m:
            try:
                return json.loads(m.group(0))
            except ValueError:
                pass
    raise AIError("AI 응답을 읽지 못했어요")


def generate_press(prompt, org, dept, tel, key, model=None, timeout=90, opener=None):
    """→ {"title","sub1","sub2","paras"}. 실패하면 AIError"""
    if not key:
        raise AIError("API 키가 없어요 (더보기 → AI 작성에서 입력)")
    user = "기관: %s\n담당부서: %s\n전화: %s\n\n요청:\n%s" % (org or "", dept or "", tel or "", prompt.strip())
    body = json.dumps({"model": model or DEFAULT_MODEL, "max_tokens": 2500, "system": SYSTEM,
                       "messages": [{"role": "user", "content": user}]}).encode("utf-8")
    req = urllib.request.Request(API_URL, data=body, method="POST", headers={
        "x-api-key": key.strip(), "anthropic-version": "2023-06-01", "content-type": "application/json"})
    try:
        with (opener or urllib.request.urlopen)(req, timeout=timeout) as r:
            data = json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        detail = ""
        try:
            detail = json.loads(e.read().decode("utf-8")).get("error", {}).get("message", "")
        except Exception:                                  # noqa: BLE001
            pass
        hint = {401: "API 키가 맞지 않아요", 403: "이 키로는 쓸 수 없어요", 429: "요청이 너무 많아요. 잠시 뒤 다시"}.get(e.code, "서버 오류")
        raise AIError("%s (%s) %s" % (hint, e.code, detail))
    except (urllib.error.URLError, OSError) as e:
        raise AIError("인터넷에 연결하지 못했어요: %s" % e)
    text = "".join(b.get("text", "") for b in data.get("content", []) if b.get("type") == "text")
    j = _extract_json(text)
    paras = [str(x).strip() for x in j.get("paragraphs", []) if str(x).strip()]
    if not paras:
        raise AIError("AI가 본문을 쓰지 못했어요")
    return {"title": str(j.get("title", "")).strip(), "sub1": str(j.get("sub1", "")).strip(),
            "sub2": str(j.get("sub2", "")).strip(), "paras": paras}
