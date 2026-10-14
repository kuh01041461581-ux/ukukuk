# -*- coding: utf-8 -*-
"""HWPX 이미지 압축 — 사진·미리보기 썸네일을 줄이고 BMP/GIF/TIFF 는 PNG 로 바꾼다(매니페스트 자동 갱신)"""
import io
import re
from dataclasses import dataclass, field
from PIL import Image, ImageOps

from ...core import hwpx as H

SRC_CONVERT = {"bmp", "gif", "tif", "tiff"}


@dataclass
class Row:
    name: str
    before: int
    after: int

    @property
    def pct(self) -> int:
        return round((1 - self.after / self.before) * 100) if self.before else 0


@dataclass
class Report:
    rows: list = field(default_factory=list)
    original_size: int = 0
    new_size: int = 0
    converted: int = 0
    convert_failed: int = 0
    convert_failed_bytes: int = 0
    other_bin: int = 0
    other_bin_bytes: int = 0
    data: bytes = b""

    @property
    def pct(self) -> int:
        return round((1 - self.new_size / self.original_size) * 100) if self.original_size else 0


def _shrink(img: Image.Image, max_dim: int) -> Image.Image:
    w, h = img.size
    scale = min(1.0, max_dim / max(w, h))
    if scale < 1.0:
        img = img.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)
    return img


def compress_image(data: bytes, as_jpeg: bool, max_dim: int, quality: int) -> bytes:
    im = Image.open(io.BytesIO(data))
    im = ImageOps.exif_transpose(im)
    im.load()
    im = _shrink(im, max_dim)
    out = io.BytesIO()
    if as_jpeg:
        if im.mode in ("RGBA", "LA", "P"):
            bg = Image.new("RGB", im.size, (255, 255, 255))
            im = im.convert("RGBA")
            bg.paste(im, mask=im.split()[-1])
            im = bg
        elif im.mode != "RGB":
            im = im.convert("RGB")
        im.save(out, "JPEG", quality=quality, optimize=True)
    else:
        if im.mode not in ("RGB", "RGBA", "L", "LA", "P"):
            im = im.convert("RGBA" if "A" in im.getbands() else "RGB")
        im.save(out, "PNG", optimize=True)
    return out.getvalue()


def _manifest_convert_map(z, names: list) -> tuple:
    hpf = next((n for n in names if re.search(r"(^|/)content\.hpf$", n, re.I)), None)
    cmap: dict = {}
    text = None
    if not hpf:
        return hpf, text, cmap
    text = z.read(hpf).decode("utf-8", errors="replace")
    lower = {n.lower(): n for n in names}
    for m in re.finditer(r"<[^>]*\bhref=\"(bindata/[^\"]+\.(\w+))\"[^>]*>", text, re.I):
        href, ext = m.group(1), m.group(2).lower()
        if ext in SRC_CONVERT and href.lower() in lower:
            actual = lower[href.lower()]
            cmap[actual.lower()] = {"actual": actual, "href": href, "new_href": re.sub(r"\.\w+$", ".png", href)}
    return hpf, text, cmap


def _replace_item_attrs(text: str, old_href: str, new_href: str, media_type: str) -> str:
    m = re.search(r"<[^>]*\shref=\"" + re.escape(old_href) + r"\"[^>]*/?>", text)
    if not m:
        return text
    tag = m.group(0)
    new = tag.replace(f'href="{old_href}"', f'href="{new_href}"')
    if re.search(r"\smedia-type=\"[^\"]*\"", new):
        new = re.sub(r"(\smedia-type=\")[^\"]*(\")", rf"\g<1>{media_type}\g<2>", new)
    return text[:m.start()] + new + text[m.end():]


def compress_hwpx(src_path: str, max_dim: int = 1600, quality: int = 70, progress=lambda *a: None) -> Report:
    z = H.open_hwpx(src_path)
    names = [i.filename for i in z.infolist() if not i.is_dir()]
    rep = Report()
    import os
    rep.original_size = os.path.getsize(src_path)
    hpf, hpf_text, cmap = _manifest_convert_map(z, names)
    entries = []
    succeeded: dict = {}
    total = len(names)
    for k, name in enumerate(names):
        progress(int(k / max(total, 1) * 100), name)
        if hpf and name == hpf:
            continue
        low = name.lower()
        data = z.read(name)
        info = cmap.get(low)
        if info:
            try:
                out = compress_image(data, False, max_dim, quality)
                entries.append((info["new_href"], out))
                succeeded[low] = info
                rep.converted += 1
                rep.rows.append(Row(f"{name.split('/')[-1]} → {info['new_href'].split('/')[-1]}", len(data), len(out)))
            except Exception:
                entries.append((name, data))
                rep.convert_failed += 1
                rep.convert_failed_bytes += len(data)
        elif H.is_bindata_image(name) or H.is_preview_image(name):
            is_jpeg = bool(re.search(r"\.jpe?g$", low))
            out = data
            try:
                c = compress_image(data, is_jpeg, max_dim, quality)
                if len(c) < len(data):
                    out = c
            except Exception:
                pass
            entries.append((name, out))
            label = ("[미리보기] " if H.is_preview_image(name) else "") + name.split("/")[-1]
            rep.rows.append(Row(label, len(data), len(out)))
        else:
            entries.append((name, data))
            if low.startswith("bindata/"):
                rep.other_bin += 1
                rep.other_bin_bytes += len(data)
    if hpf:
        text = hpf_text
        for info in succeeded.values():
            text = _replace_item_attrs(text, info["href"], info["new_href"], "image/png")
        entries.insert(min(len(entries), next((i for i, (n, _) in enumerate(entries) if n == "mimetype"), -1) + 1), (hpf, text.encode("utf-8")))
    rep.data = H.write_hwpx(entries)
    rep.new_size = len(rep.data)
    progress(100, "완료")
    return rep
