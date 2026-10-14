# -*- coding: utf-8 -*-
"""PyInstaller 시작 파일"""
import multiprocessing

from seomu.app import main

if __name__ == "__main__":
    multiprocessing.freeze_support()
    main()
