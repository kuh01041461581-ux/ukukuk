# -*- coding: utf-8 -*-
"""무거운 일을 화면 멈춤 없이 뒤에서 돌리는 도우미"""
import traceback
from PySide6.QtCore import QObject, QRunnable, QThreadPool, Signal


class _Sig(QObject):
    done = Signal(object)
    failed = Signal(str)
    progress = Signal(int, str)


class _Job(QRunnable):
    def __init__(self, fn, args, kwargs):
        super().__init__()
        self.fn, self.args, self.kwargs = fn, args, kwargs
        self.sig = _Sig()
        self.setAutoDelete(True)

    def run(self):
        try:
            res = self.fn(*self.args, progress=self.sig.progress.emit, **self.kwargs)
        except Exception as e:  # noqa
            traceback.print_exc()
            self.sig.failed.emit(f"{type(e).__name__}: {e}")
            return
        self.sig.done.emit(res)


_keep: set = set()


def run_async(fn, *args, on_done=None, on_error=None, on_progress=None, **kwargs):
    """fn(*args, progress=콜백(퍼센트, 문구), **kwargs) 를 별도 스레드에서 실행"""
    job = _Job(fn, args, kwargs)
    _keep.add(job.sig)

    def _fin(*_):
        _keep.discard(job.sig)
    if on_done:
        job.sig.done.connect(on_done)
    if on_error:
        job.sig.failed.connect(on_error)
    if on_progress:
        job.sig.progress.connect(on_progress)
    job.sig.done.connect(_fin)
    job.sig.failed.connect(_fin)
    QThreadPool.globalInstance().start(job)
    return job


def run_sync(fn, *args, **kwargs):
    """시험용: 같은 스레드에서 바로 실행"""
    return fn(*args, progress=lambda *a: None, **kwargs)
