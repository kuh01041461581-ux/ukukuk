# -*- coding: utf-8 -*-
"""한글 양식 생성기.  인자 없이 실행하면 옆 창 화면, 인자가 있으면 명령줄(python app.py --help)."""
import sys

if __name__ == "__main__":
    if len(sys.argv) > 1:
        from hwpgen.cli import main
        sys.exit(main())
    from hwpgen.gui import run
    run()
