# -*- coding: utf-8 -*-
"""HWPX(= zip) 읽기/쓰기 도우미.  mimetype 은 항상 맨 앞에 무압축으로 쓴다(한글이 요구하는 규칙)."""
import io
import re
import zipfile

IMG_EXT = re.compile(r"\.(png|jpe?g|bmp|gif|tiff?)$", re.I)


class HwpxError(Exception):
    pass


def open_hwpx(path_or_bytes) -> zipfile.ZipFile:
    try:
        src = io.BytesIO(path_or_bytes) if isinstance(path_or_bytes, (bytes, bytearray)) else path_or_bytes
        return zipfile.ZipFile(src)
    except zipfile.BadZipFile:
        raise HwpxError("HWPX 파일이 아닙니다. (.hwp 구형 파일은 한글에서 \"다른 이름으로 저장 → HWPX\"로 바꿔 주세요)")


def write_hwpx(entries: list, out_path: str | None = None, level: int = 9) -> bytes | None:
    """entries: [(이름, bytes)] 순서 그대로 기록. mimetype 이 있으면 무압축으로."""
    buf = io.BytesIO() if out_path is None else None
    with zipfile.ZipFile(out_path or buf, "w") as z:
        for name, data in entries:
            if name == "mimetype":
                z.writestr(zipfile.ZipInfo("mimetype"), data, compress_type=zipfile.ZIP_STORED)
            else:
                z.writestr(zipfile.ZipInfo(name, date_time=(2020, 1, 1, 0, 0, 0)), data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=level)
    return buf.getvalue() if buf is not None else None


def is_bindata_image(name: str) -> bool:
    low = name.lower()
    return low.startswith("bindata/") and bool(IMG_EXT.search(low))


def is_preview_image(name: str) -> bool:
    low = name.lower()
    return low.startswith("preview/") and bool(IMG_EXT.search(low))
