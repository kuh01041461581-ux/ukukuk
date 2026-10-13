# -*- coding: utf-8 -*-
"""캡처 이미지 → 글자: 윈도우 10/11 에 내장된 글자 인식(Windows.Media.Ocr)을 PowerShell 로 호출한다.
별도 설치가 필요 없지만, 윈도우에 '한국어 OCR' 언어 기능이 있어야 한다 (한국어 윈도우는 기본 포함)."""
import os
import subprocess
import sys
import tempfile

_PS = r"""
param([string]$path)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics, ContentType = WindowsRuntime]
$null = [Windows.Globalization.Language, Windows.Globalization, ContentType = WindowsRuntime]
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($op, $type) {
    $t = $asTask.MakeGenericMethod($type).Invoke($null, @($op))
    $t.Wait(-1) | Out-Null
    $t.Result
}
$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync($path)) ([Windows.Storage.StorageFile])
$stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
$decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
$bmp = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage((New-Object Windows.Globalization.Language 'ko'))
if ($engine -eq $null) { $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages() }
if ($engine -eq $null) { Write-Output '__NO_ENGINE__'; exit 0 }
$res = Await ($engine.RecognizeAsync($bmp)) ([Windows.Media.Ocr.OcrResult])
foreach ($l in $res.Lines) { Write-Output $l.Text }
"""


class OcrError(Exception):
    pass


def available():
    return sys.platform == "win32"


def read_image(path):
    """이미지 파일 → 글자. 실패하면 OcrError (사람이 읽을 수 있는 이유)"""
    if not available():
        raise OcrError("글자 인식은 윈도우에서만 쓸 수 있어요")
    ps1 = os.path.join(tempfile.gettempdir(), "deskcal_ocr.ps1")
    with open(ps1, "w", encoding="utf-8-sig") as f:        # PowerShell 5 가 한글을 읽도록 BOM 포함
        f.write(_PS)
    try:
        r = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ps1, os.path.abspath(path)],
                           capture_output=True, timeout=90, creationflags=0x08000000)
    except (OSError, subprocess.TimeoutExpired):
        raise OcrError("글자 인식을 실행하지 못했어요")
    out = r.stdout.decode("utf-8", "replace").strip()
    if "__NO_ENGINE__" in out:
        raise OcrError("이 PC에 한국어 글자 인식 기능이 없어요\n(설정 → 시간 및 언어 → 언어 및 지역 → 한국어 → 언어 옵션 → 'OCR' 설치)")
    if r.returncode != 0 or not out:
        raise OcrError("이미지에서 글자를 읽지 못했어요")
    return out


def read_pil(img):
    """PIL 이미지(클립보드 캡처) → 글자"""
    p = os.path.join(tempfile.gettempdir(), "deskcal_clip.png")
    img.convert("RGB").save(p)
    try:
        return read_image(p)
    finally:
        try:
            os.remove(p)
        except OSError:
            pass
