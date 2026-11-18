# -*- coding: utf-8 -*-
"""모든 양식의 '만드는 순서'(레시피)를 JSON 으로 뽑아 본다 — 한글 명령으로 직접 만들 때 쓰는 순서를 눈으로 확인·고칠 수 있다.
   사용: python tools/make_recipes.py [저장 폴더]   (기본: ./recipes)"""
import json
import os
import sys

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
from hwpgen import recipe, registry  # noqa: E402


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "recipes")
    os.makedirs(out, exist_ok=True)
    n = 0
    for fid, _folder, _meta in registry.list_forms():
        try:
            f = registry.load_form(fid)
            vals = {x["key"]: "{{%s}}" % x["key"] for x in getattr(f, "fields", [])}
            pkg = f.render(vals, f.meta.get("starter", "")) if "weekly" not in fid else None
            if pkg is None:
                continue
            rc, imgs = recipe.extract(pkg)
        except Exception as e:                           # noqa: BLE001
            print("건너뜀", fid, e)
            continue
        with open(os.path.join(out, fid + ".json"), "w", encoding="utf-8") as fh:
            json.dump(rc, fh, ensure_ascii=False, indent=1)
        n += 1
    print(n, "개 →", out)


if __name__ == "__main__":
    main()
