[exe 만들기 - Windows PC]
1) Python 3.10 이상 설치 (설치 화면에서 "Add python.exe to PATH" 체크)
2) 이 폴더(hwp_form_generator) 전체를 PC에 복사
3) build.bat 더블클릭 -> dist\한글양식생성기.exe 생성
   (exe 단독 실행 가능, templates 폴더는 exe 안에 포함됨)

[GitHub로 만들기] 이 폴더를 GitHub 저장소에 올리면
Actions 탭 > build-exe > Run workflow > Artifacts에서 exe 다운로드

[Python으로 바로 실행] python app.py   (pip install lxml 필요)
