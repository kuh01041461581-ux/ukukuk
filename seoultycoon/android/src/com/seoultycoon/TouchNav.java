package com.seoultycoon;

import java.lang.reflect.Field;
import javax.microedition.lcdui.Display;

/**
 * 게임 화면을 직접 터치해서 메뉴 항목/버튼을 고르게 하는 보조 모듈.
 * 원본 게임은 방향키+확인만 받으므로, 터치 좌표를 "방향키 몇 번 + 확인" 입력열로 바꿔 준다.
 * 화면 객체(난독화 클래스)의 선택 필드를 리플렉션으로 읽어 현재 위치를 안다.
 *  - ae : 메인 메뉴(7항목, 선택 g)
 *  - v  : 확인/취소 대화창(선택 p, 왼쪽 s / 오른쪽 o)
 */
final class TouchNav {
    static final int UP = GameView.UP, DOWN = GameView.DOWN, LEFT = GameView.LEFT, RIGHT = GameView.RIGHT, OK = GameView.OK;

    static Object screen() {
        try {
            Object q = Display.current; if (q == null) return null;
            Field f = field(q.getClass(), "a"); return f == null ? null : f.get(q);
        } catch (Throwable t) { return null; }
    }
    static Field field(Class<?> c, String n) {
        for (; c != null; c = c.getSuperclass()) { try { Field f = c.getDeclaredField(n); f.setAccessible(true); return f; } catch (NoSuchFieldException e) { } }
        return null;
    }
    static int geti(Object o, String n) throws Exception { Field f = field(o.getClass(), n); return (f == null || f.getType() != int.class) ? 0 : f.getInt(o); }

    /** (gx,gy)=게임 좌표(176x224). 처리했으면 눌러 줄 키 열을 돌려주고, 아니면 null */
    static int[] tap(float gx, float gy) {
        try {
            Object sc = screen(); if (sc == null) return null;
            String cn = sc.getClass().getName();
            if (cn.equals("ae")) {                                    // 메인 메뉴: 항목 i 의 위치 x=d-31.., y=(v-57)+17*i
                int cur = geti(sc, "g"), d = geti(sc, "d"), v = geti(sc, "v");
                float x0 = d - 31, y0 = v - 57;
                if (gx < x0 - 18 || gx > x0 + 62 + 26) return null;
                int hit = -1;
                for (int i = 0; i < 7; i++) { float y = y0 + 17 * i; if (gy >= y - 2 && gy < y + 15) { hit = i; break; } }
                if (hit < 0) return null;
                int fw = (hit - cur + 7) % 7, bk = 7 - fw;
                int n = fw <= bk ? fw : bk, dir = fw <= bk ? DOWN : UP;
                int[] seq = new int[n + 1];
                for (int i = 0; i < n; i++) seq[i] = dir;
                seq[n] = OK; return seq;
            }
            if (cn.equals("v")) {                                     // 확인/취소 대화창
                int s = geti(sc, "s"), o = geti(sc, "o"), p = geti(sc, "p"), d = geti(sc, "d");
                if (s == o) return new int[]{OK};
                int want = gx < d ? 0 : 1;
                return want == p ? new int[]{OK} : new int[]{want == 0 ? LEFT : RIGHT, OK};
            }
        } catch (Throwable t) { }
        return null;
    }
}
