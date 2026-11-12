/* 익산타이쿤 윈도우 실행기: exe 옆의 jre\bin\javaw.exe 로 IksanTycoon-PC.jar 를 실행한다(없으면 설치된 javaw 를 찾는다). */
#include <windows.h>
#include <wchar.h>
int WINAPI wWinMain(HINSTANCE h, HINSTANCE p, LPWSTR cmd, int show) {
    wchar_t dir[MAX_PATH], java[MAX_PATH * 2], line[MAX_PATH * 4];
    GetModuleFileNameW(NULL, dir, MAX_PATH);
    wchar_t *s = wcsrchr(dir, L'\\'); if (s) *s = 0;
    swprintf(java, MAX_PATH * 2, L"%ls\\jre\\bin\\javaw.exe", dir);
    if (GetFileAttributesW(java) == INVALID_FILE_ATTRIBUTES) wcscpy(java, L"javaw.exe");
    swprintf(line, MAX_PATH * 4, L"\"%ls\" -jar \"%ls\\IksanTycoon-PC.jar\"", java, dir);
    STARTUPINFOW si; PROCESS_INFORMATION pi; ZeroMemory(&si, sizeof si); si.cb = sizeof si;
    if (!CreateProcessW(NULL, line, NULL, NULL, FALSE, 0, NULL, dir, &si, &pi)) {
        MessageBoxW(NULL, L"자바 실행 파일을 찾지 못했습니다.\njre 폴더가 exe 옆에 있는지 확인해 주세요.", L"익산타이쿤", MB_ICONERROR);
        return 1;
    }
    CloseHandle(pi.hThread); CloseHandle(pi.hProcess); return 0;
}
