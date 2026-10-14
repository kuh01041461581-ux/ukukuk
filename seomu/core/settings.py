# -*- coding: utf-8 -*-
"""설정·작업 데이터 저장 (JSON 파일). Windows: %APPDATA%\\SeomuSuite, 그 밖: ~/.config/SeomuSuite"""
import json
import os
import pathlib
import time


def data_dir() -> pathlib.Path:
    root = os.environ.get("SEOMU_HOME") or os.environ.get("APPDATA") or os.environ.get("XDG_CONFIG_HOME") or str(pathlib.Path.home() / ".config")
    d = pathlib.Path(root) / ("" if os.environ.get("SEOMU_HOME") else "SeomuSuite")
    d.mkdir(parents=True, exist_ok=True)
    return d


class Settings:
    """키-값 저장소. 값은 JSON 으로 직렬화 가능해야 한다."""

    def __init__(self, path: pathlib.Path | None = None):
        self.path = path or (data_dir() / "settings.json")
        self.data: dict = {}
        self.load()

    def load(self):
        try:
            self.data = json.loads(self.path.read_text(encoding="utf-8"))
        except Exception:
            self.data = {}

    def save(self):
        tmp = self.path.with_suffix(".tmp")
        tmp.write_text(json.dumps(self.data, ensure_ascii=False, indent=1), encoding="utf-8")
        tmp.replace(self.path)

    def get(self, key: str, default=None):
        return self.data.get(key, default)

    def set(self, key: str, value, save: bool = True):
        self.data[key] = value
        if save:
            self.save()

    # --- 하위 키 -------------------------------------------------
    def sub(self, group: str, key: str, default=""):
        return (self.data.get(group) or {}).get(key, default)

    def set_sub(self, group: str, key: str, value):
        g = self.data.setdefault(group, {})
        if value in ("", None):
            g.pop(key, None)
        else:
            g[key] = value
        self.save()

    # --- 백업/복원 -----------------------------------------------
    BACKUP_KEYS = ("staff_db", "trip_db", "info")

    def export_backup(self) -> dict:
        return {"__seomu": 1, "saved_at": time.strftime("%Y-%m-%dT%H:%M:%S"),
                "data": {k: self.data[k] for k in self.BACKUP_KEYS if k in self.data}}

    def import_backup(self, obj: dict):
        if not isinstance(obj, dict) or not obj.get("__seomu"):
            raise ValueError("이 프로그램에서 저장한 백업 파일이 아닙니다.")
        for k in self.BACKUP_KEYS:
            if k in obj.get("data", {}):
                self.data[k] = obj["data"][k]
        self.save()
