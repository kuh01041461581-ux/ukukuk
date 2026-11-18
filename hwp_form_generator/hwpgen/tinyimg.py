# -*- coding: utf-8 -*-
"""양식에서 쓰지 않는 그림을 대신할 아주 작은 빈 그림 (개인 정보가 든 그림이 양식에 남지 않게)."""
import base64
import os

_PNG = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4//8/AAX+Av4N70a4AAAAAElFTkSuQmCC")
_JPG = base64.b64decode(
    "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5"
    "PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAAR"
    "CAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEG"
    "E1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWG"
    "h4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEB"
    "AQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYk"
    "NOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0"
    "tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD3+iiigD//2Q==")
_BMP = base64.b64decode("Qk06AAAAAAAAADYAAAAoAAAAAQAAAAEAAAABABgAAAAAAAQAAADEDgAAxA4AAAAAAAAAAAAA////AA==")


def blank_like(name):
    ext = os.path.splitext(name)[1].lower()
    if ext in (".jpg", ".jpeg"):
        return _JPG
    if ext == ".bmp":
        return _BMP
    return _PNG


def shrink(data, name, max_w=1200, min_bytes=60000):
    """큰 그림을 가볍게: 가로 max_w 이하로 줄이고 PNG 는 256색(투명 유지), JPG 는 품질 82, BMP 는 256색. 더 작아질 때만 바꾼다.
    (양식은 그림 자리 크기를 따로 가지고 있어 그림의 픽셀 수가 줄어도 모양은 그대로다)"""
    if len(data) < min_bytes:
        return data
    import io
    from PIL import Image
    ext = os.path.splitext(name)[1].lower()
    try:
        im = Image.open(io.BytesIO(data))
        im.load()
    except Exception:                                  # noqa: BLE001
        return data
    if im.width > max_w:
        im = im.resize((max_w, max(1, round(im.height * max_w / im.width))), Image.LANCZOS)
    out = io.BytesIO()
    if ext in (".jpg", ".jpeg"):
        im.convert("RGB").save(out, "JPEG", quality=82, optimize=True)
    elif ext == ".bmp":
        im.convert("RGB").quantize(256, dither=Image.FLOYDSTEINBERG).save(out, "BMP")
    else:
        rgba = im.convert("RGBA")
        if rgba.getchannel("A").getextrema()[0] == 255:
            q = rgba.convert("RGB").quantize(256, method=Image.MEDIANCUT, dither=Image.FLOYDSTEINBERG)
        else:
            q = rgba.quantize(256, method=Image.FASTOCTREE, dither=Image.FLOYDSTEINBERG)
        q.save(out, "PNG", optimize=True)
    res = out.getvalue()
    return res if len(res) < len(data) else data
