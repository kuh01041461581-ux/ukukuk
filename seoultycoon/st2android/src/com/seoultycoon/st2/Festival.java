package com.seoultycoon.st2;

/** 익산 행사 달력: 날짜가 바뀌어 행사일이 되면 자금·자재를 보너스로 주고 알림을 띄운다. 상태 객체는 `머리값 446808346` 바로 뒤의 (자금, 자재, 인력)으로 찾는다. */
public final class Festival {
    static final int HEAD = 446808346;
    static final Object[][] EVENTS = {   // 월, 일, 자금, 자재, 문구
        {4, 15, 12, 1, "국가유산 야행 시작! 관광 수입 +12억, 자재 +1"},
        {5, 10, 6, 0, "익산 서동축제 개막! 관광 수입 +6억"},
        {9, 1, 12, 1, "미디어아트 페스티벌 개막! 방문객 수입 +12억, 자재 +1"},
        {10, 20, 10, 0, "국화축제 개막! 관광 수입 +10억"},
    };
    static int[] last; static int stateAddr, ticks;
    static int findState(Host H) {
        int end = H.hiEnd();
        if (stateAddr != 0 && H.vm.r32(stateAddr) == HEAD) return stateAddr;
        for (int a = 0x50000000; a < end - 16; a += 4) {
            if (H.vm.r32(a) != HEAD) continue;
            int m = H.vm.r32(a + 4), t = H.vm.r32(a + 8), w = H.vm.r32(a + 12);
            if (m < 0 || m > 999999 || t < 0 || t > 9999 || w < 0 || w > 9999) continue;
            return stateAddr = a;
        }
        return 0;
    }
    public static int[] testDate;      // 시험용(하니스): 지정하면 게임 날짜 대신 이 값을 쓴다
    static int[] readDate(Host H) {
        if (testDate != null) return testDate;
        int hdr = H.classfor("[I") << 5, end = H.hiEnd(); long best = -1; int[] bd = null;
        for (int a = 0x50000000; a < end; a += 4) {
            if (H.vm.r32(a) != hdr || H.vm.r32(a + 4) != 12) continue;
            int y = H.vm.r32(a + 8), m = H.vm.r32(a + 12), d = H.vm.r32(a + 16);
            if (y < 1 || y > 4 || m < 1 || m > 12 || d < 1 || d > 31) continue;
            int e3 = H.vm.r32(a + 20), e7 = H.vm.r32(a + 36); if (e3 <= 0 || e7 < 0 || e7 > 100) continue;
            long key = ((long) y * 100 + m) * 100 + d; if (key > best) { best = key; bd = new int[]{y, m, d}; }
        }
        return bd;
    }
    static long acc;
    public static void tick(Host H, int dtMs) {
        acc += dtMs; if (acc < 1500) return; acc = 0;
        if (H.currentCard == 0) return;
        int[] d = readDate(H); if (System.getenv("LOGFEST") != null) H.pf.log("FEST date=" + (d == null ? "null" : d[0] + "/" + d[1] + "/" + d[2])); if (d == null) return;
        if (last == null || d[0] * 10000 + d[1] * 100 + d[2] < last[0] * 10000 + last[1] * 100 + last[2]) { last = d; return; }   // 처음 보거나 세이브를 다시 불러온 경우
        if (d[0] == last[0] && d[1] == last[1] && d[2] == last[2]) return;
        int[] prev = last; last = d;
        int pk = prev[1] * 100 + prev[2], ck = d[1] * 100 + d[2];
        for (Object[] e : EVENTS) {
            int ek = (Integer) e[0] * 100 + (Integer) e[1];
            boolean hit = d[0] == prev[0] ? (pk < ek && ek <= ck) : (ek > pk || ek <= ck);      // 이번에 건너온 날짜 구간 안에 행사일이 있나
            if (!hit) continue;
            int s = findState(H); if (s == 0) return;
            H.vm.w32(s + 4, H.vm.r32(s + 4) + (Integer) e[2]); H.vm.w32(s + 8, H.vm.r32(s + 8) + (Integer) e[3]);
            H.pf.toast((String) e[4]); H.repaintReq = true;
        }
    }
}
