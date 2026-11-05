# wie-android: 32비트 안드로이드 게임을 64비트 폰에서 돌리기 (실험)

갤럭시 S24 같은 **64비트 전용 폰**은 32비트(armeabi) 네이티브 코드를 실행하지 못해서 `검은방` 같은 구형 APK 가 설치되지 않습니다.
이 실험은 게임의 32비트 라이브러리(`libroomofprey1.so`)를 **WIE 의 ARM 에뮬레이터 위에서 대신 실행**합니다.

## 현재 상태 (검은방 1)
- ELF 로더(재배치 포함), 시스템 함수 82개(libc/libm/liblog) 호스트 구현, JNI(자바 쪽 56개 기능) 구현
- 게임 시작 순서(`nativeInitKernel → InitVFS → InitFileStorage → InitAudio → InitDisplay → StartApp → nativeLoop`) 성공
- OpenGL ES 1.x 소프트웨어 구현(게임이 화면 전체를 텍스처로 올려 사각형 하나로 그림) → **EA 로고, 검은방 타이틀 화면 렌더링 확인**
- 아직 없음: 터치/키 입력, 소리, 세이브 영속화, 웹/APK 포장, 성능 최적화, 검은방 2~4·시그너스

## 실행(데스크톱 시험)
`cargo run -p wie-android -- libroomofprey1.so data.vfs.mp3` (`FRAMES=120 DUMP=폴더` 로 프레임 저장)

게임 파일(.so, data.vfs)은 사용자가 가진 APK 에서 꺼내 쓰며, 이 저장소에는 포함하지 않습니다.
이 실행기는 원래 자바 쪽에 있던 T store 라이선스 확인(ARM)·광고·결제 코드를 포함하지 않습니다.
