# -*- coding: utf-8 -*-
"""부서 공유 달력: 작성자(이름·색)와 '내가 쓴 일정만 고칠 수 있다' 규칙 (화면과 무관한 순수 로직)

- 일정마다 작성자 id('au')가 붙고, 부서원 명단은 data["_team"] = {id: {"name", "color", "updated"}} 로 함께 동기화된다.
- 부서 공유를 켜면 새 일정에는 내 id 가 붙고, 달력 글자는 작성자 색으로 보인다.
- 내가 쓴 일정(또는 작성자가 없는 예전 일정)만 고칠 수 있다. 모두가 같은 암호 키로 동기화하는 구조라서 이 잠금은
  '앱이 막는' 수준이다(부서 안에서 실수로 남의 일정을 지우는 일을 막는 용도, 일부러 우회하는 것까지는 못 막는다)."""
import time
import uuid

# 어두운 바탕·밝은 바탕 모두에서 구분이 잘 되는 12색
PALETTE = ["#ff6b81", "#ff9f43", "#f7c948", "#2ed573", "#1dd1a1", "#48dbfb",
           "#54a0ff", "#7d6bff", "#c56cf0", "#ff9ff3", "#a4b0be", "#ee5253"]


def new_uid():
    return uuid.uuid4().hex[:8]


def members(data):
    return data.get("_team") or {}


def pick_color(data, prefer=None):
    """명단에서 아직 쓰지 않은 색 (prefer 가 비어 있으면 그것)"""
    used = {m.get("color") for m in members(data).values()}
    if prefer and prefer not in used:
        return prefer
    for c in PALETTE:
        if c not in used:
            return c
    return PALETTE[len(used) % len(PALETTE)]


def register(data, uid, name, color):
    """내 이름·색을 명단에 올린다 (바뀐 게 없으면 시각을 갱신하지 않는다). 바뀌었으면 True"""
    team = data.setdefault("_team", {})
    cur = team.get(uid)
    if cur and cur.get("name") == name and cur.get("color") == color:
        return False
    team[uid] = {"name": name, "color": color, "updated": int(time.time() * 1000)}
    return True


def author_of(item):
    return (item.get("_src", item)).get("au") or None


def name_of(data, uid):
    return (members(data).get(uid) or {}).get("name", "")


def color_of(data, uid):
    return (members(data).get(uid) or {}).get("color", "")


def can_edit(item, me, on):
    """부서 공유가 꺼져 있거나, 작성자가 없는 예전 일정이거나, 내가 쓴 일정이면 고칠 수 있다"""
    if not on:
        return True
    au = author_of(item)
    return not au or au == me


def claim_legacy(data, uid):
    """작성자가 없는 예전 일정을 모두 내 것으로 (동기화되도록 수정 시각도 올린다). 바꾼 개수를 돌려준다"""
    n = 0
    now = int(time.time() * 1000)
    for key, v in data.items():
        if key in ("_deleted", "_work", "_team") or not isinstance(v, dict):
            continue
        for t in v.get("todos", []):
            if not t.get("au"):
                t["au"] = uid
                t["updated"] = now
                n += 1
    return n
