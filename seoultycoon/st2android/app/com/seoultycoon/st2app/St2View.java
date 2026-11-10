package com.seoultycoon.st2app;

import android.content.Context;
import android.graphics.*;
import android.os.Handler;
import android.os.Looper;
import android.view.MotionEvent;
import android.view.View;
import com.seoultycoon.st2.Host;

/**
 * LCD 비트맵을 그리고 터치/가상키 입력을 피처폰 키 입력으로 바꾼다.
 * 조작 방식 3가지:  MODE_PHONE=피처폰 버튼, MODE_JOY=조이스틱+버튼, MODE_TOUCH=터치 전용(전체 화면)
 */
public class St2View extends View {
    // ST2(WIPI) 키코드: 숫자키 '0'..'9', 2=위 4=왼 6=오 8=아래 5=확인. 화면 하단 안내 \"0:EXIT\" 에 따라 취소=숫자 0, 확인=5. 좌메뉴는 '*' 로 보낸다.
    public static final int UP = 50, DOWN = 56, LEFT = 52, RIGHT = 54, OK = 53, CLR = 48, SOFT_L = 42;
    public static final int MODE_PHONE = 0, MODE_JOY = 1, MODE_TOUCH = 2;

    static final class Btn {
        final RectF r = new RectF(); final String label, sub; final int code; boolean circle; float cx, cy, rad;
        Btn(String l, String sb, int c) { label = l; sub = sb; code = c; }
        boolean hit(float x, float y) {
            if (circle) { float dx = x - cx, dy = y - cy; return dx * dx + dy * dy <= rad * rad; }
            return r.contains(x, y);
        }
    }

    int mode = MODE_PHONE;
    Btn[] btns = new Btn[0];
    final Paint bp = new Paint(Paint.ANTI_ALIAS_FLAG), tp = new Paint(Paint.ANTI_ALIAS_FLAG), sp = new Paint(Paint.ANTI_ALIAS_FLAG), gp = new Paint();
    final RectF game = new RectF(), pad = new RectF(), gear = new RectF();
    final Rect src = new Rect(0, 0, 960, 1184);          // 화질 보정으로 4배 확대된 화면
    Bitmap big; Canvas bigC; int bigK; int shownFrame = -1; final Paint sm = new Paint(Paint.FILTER_BITMAP_FLAG | Paint.ANTI_ALIAS_FLAG);
    public volatile String err; public volatile int frames;
    public Host game_host; public android.graphics.Bitmap lcd;
    final Handler ui = new Handler(Looper.getMainLooper());
    final int[] ptrBtn = new int[16];
    final float density;
    // 화면(게임 영역) 제스처
    int gesturePtr = -1; float gx, gy; long gdown; boolean gmoved, glong, gtwo;
    final float swipe;
    // 조이스틱
    int joyPtr = -1; float jcx, jcy, jcx0, jcy0, jr, kx, ky; int joyDir = 0;
    // 방향키 반복: 처음 누를 때 1회, 280ms 뒤부터 90ms 간격으로 '눌림'만 추가 전송(뗌 없이) -> 메뉴는 연속 이동, 지도는 끊김 없이 스크롤
    final class Rep implements Runnable {
        int code; boolean on;
        void start(int c) { stop(); code = c; on = true; ui.postDelayed(this, 280); }
        void stop() { on = false; ui.removeCallbacks(this); }
        public void run() { if (on) { key(code, true); ui.postDelayed(this, 90); } }
    }
    final Rep joyRep = new Rep();
    final Rep[] ptrRep = new Rep[16];
    float zoneRight;                                    // 플로팅 조이스틱 인식 구역(패드 왼쪽 절반)

    public interface Opener { void openModeDialog(); }
    Opener host;

    public St2View(Context c) {
        super(c);
        density = c.getResources().getDisplayMetrics().density;
        swipe = 22 * density;
        gp.setFilterBitmap(false);
        sp.setColor(0xFF9AA0B8); sp.setTextAlign(Paint.Align.CENTER);
        tp.setColor(Color.WHITE); tp.setTextAlign(Paint.Align.CENTER); tp.setTypeface(Typeface.DEFAULT_BOLD);
        java.util.Arrays.fill(ptrBtn, -1);
        for (int i = 0; i < ptrRep.length; i++) ptrRep[i] = new Rep();
        setKeepScreenOn(true);
    }

    public void setMode(int m) { mode = m; relayout(getWidth(), getHeight()); invalidate(); }

    @Override protected void onSizeChanged(int w, int h, int ow, int oh) { relayout(w, h); }

    void set(int i, float x, float y, float w, float h) { btns[i].r.set(x, y, x + w, y + h); }
    void circ(int i, float cx, float cy, float rad) { Btn b = btns[i]; b.circle = true; b.cx = cx; b.cy = cy; b.rad = rad; b.r.set(cx - rad, cy - rad, cx + rad, cy + rad); }

    void relayout(int w, int h) {
        if (w == 0 || h == 0) return;
        final float aspect = 296f / 240f;
        boolean land = w > h;
        float gs = 48 * density; gear.set(w - gs - 6 * density, 6 * density, w - 6 * density, 6 * density + gs);
        joyPtr = -1; joyDir = 0; java.util.Arrays.fill(ptrBtn, -1);
        if (mode == MODE_TOUCH) {                       // 전체 화면
            float gh = land ? h : Math.min(h, w * aspect), gw = gh / aspect;
            if (gw > w) { gw = w; gh = gw * aspect; }
            game.set((w - gw) / 2, (h - gh) / 2, (w + gw) / 2, (h + gh) / 2);
            pad.setEmpty(); btns = new Btn[0]; return;
        }
        if (!land) {
            float gh = Math.min(w * aspect, h * 0.56f), gw = gh / aspect;
            game.set((w - gw) / 2, 0, (w + gw) / 2, gh); pad.set(0, gh, w, h);
        } else {
            float gw = Math.min(h / aspect, w * 0.55f), gh = gw * aspect;
            game.set(0, (h - gh) / 2, gw, (h + gh) / 2); pad.set(gw, 0, w, h);
        }
        float W = pad.width(), H = pad.height(), m = Math.max(4, Math.min(W, H) * 0.012f);
        if (mode == MODE_PHONE) {
            btns = new Btn[]{
                new Btn("좌메뉴", "", SOFT_L), new Btn("우메뉴", "", CLR), new Btn("통화", "", OK), new Btn("취소", "", CLR),
                new Btn("▲", "", UP), new Btn("◀", "", LEFT), new Btn("OK", "", OK), new Btn("▶", "", RIGHT), new Btn("▼", "", DOWN),
                new Btn("1", "", '1'), new Btn("2", "ABC", '2'), new Btn("3", "DEF", '3'),
                new Btn("4", "GHI", '4'), new Btn("5", "JKL", '5'), new Btn("6", "MNO", '6'),
                new Btn("7", "PQRS", '7'), new Btn("8", "TUV", '8'), new Btn("9", "WXYZ", '9'),
                new Btn("*", "", '*'), new Btn("0", "", '0'), new Btn("#", "", '#')};
            float rh = Math.min((H - 8 * m) / 7f, W / 6.2f);
            float top = pad.top + Math.max(m, (H - (7 * rh + 8 * m)) / 2);
            float sw = W * 0.30f, dw = W - 2 * sw - 4 * m, cell = Math.min(dw / 3f, rh);
            float lx = pad.left + m, rx = pad.right - m - sw, cx = pad.left + W / 2 - 1.5f * cell;
            set(0, lx, top, sw, rh * 0.9f); set(2, lx, top + rh + m, sw, rh * 0.9f);
            set(1, rx, top, sw, rh * 0.9f); set(3, rx, top + rh + m, sw, rh * 0.9f);
            float pcx = cx + 1.5f * cell, pcy = top + 1.5f * cell, as = cell * 0.82f, os = cell * 0.70f, off = cell * 1.05f;   // 확인은 작게, 방향키와 간격
            set(4, pcx - as / 2, pcy - off - as / 2, as, as); set(5, pcx - off - as / 2, pcy - as / 2, as, as); set(6, pcx - os / 2, pcy - os / 2, os, os);
            set(7, pcx + off - as / 2, pcy - as / 2, as, as); set(8, pcx - as / 2, pcy + off - as / 2, as, as);
            float ny = top + 3 * rh + 2 * m, nw = (W - 4 * m) / 3f;
            for (int i = 0; i < 12; i++) set(9 + i, pad.left + m + (i % 3) * (nw + m), ny + (i / 3) * (rh + m), nw, rh);
            tp.setTextSize(Math.min(nw, rh) * 0.40f); sp.setTextSize(Math.min(nw, rh) * 0.20f);
        } else {                                        // 방향키 버튼 모드: 십자 방향 버튼 + 확인/취소 + 필요한 숫자·기호 버튼
            btns = new Btn[]{ new Btn("▲", "", UP), new Btn("◀", "", LEFT), new Btn("▶", "", RIGHT), new Btn("▼", "", DOWN),
                              new Btn("확인", "", OK), new Btn("취소", "", CLR),
                              new Btn("좌메뉴", "", SOFT_L), new Btn("*", "", '*'), new Btn("0", "", '0'), new Btn("#", "", '#') };
            float s = Math.min(W * 0.13f, H * 0.21f), o = s * 1.12f, dcx = pad.left + W * 0.24f, dcy = pad.top + H * 0.42f;
            set(0, dcx - s / 2, dcy - o - s / 2, s, s); set(1, dcx - o - s / 2, dcy - s / 2, s, s);
            set(2, dcx + o - s / 2, dcy - s / 2, s, s); set(3, dcx - s / 2, dcy + o - s / 2, s, s);
            float ro = Math.min(W * 0.14f, H * 0.24f);
            circ(4, pad.left + W * 0.80f, pad.top + H * 0.34f, ro);
            circ(5, pad.left + W * 0.58f, pad.top + H * 0.62f, ro * 0.72f);
            float bw = (W * 0.52f - 5 * m) / 4f, bh = Math.min(H * 0.18f, bw * 0.8f), by = pad.bottom - m - bh;
            float bx = pad.left + W * 0.46f;
            for (int i = 0; i < 4; i++) set(6 + i, bx + i * (bw + m), by, bw, bh);
            tp.setTextSize(Math.min(s, ro) * 0.5f); sp.setTextSize(ro * 0.25f);
        }
    }

    @Override protected void onDraw(Canvas cv) {
        cv.drawColor(mode == MODE_TOUCH ? 0xFF000000 : 0xFF101018);
        if (lcd != null) {
            // 픽셀 도트가 비정수 배율에서 일그러지지 않게: 정수 배율(올림)로 먼저 키운 뒤 부드럽게 줄여서 그린다
            int k = Math.max(1, Math.min(8, (int) Math.ceil(game.width() / 240f)));
            if (big == null || bigK != k) { if (big != null) big.recycle(); big = Bitmap.createBitmap(240 * k, 296 * k, Bitmap.Config.ARGB_8888); bigC = new Canvas(big); bigK = k; shownFrame = -1; }
            if (shownFrame != frames) { shownFrame = frames; synchronized (lcd) { bigC.drawBitmap(lcd, src, new Rect(0, 0, 240 * k, 296 * k), gp); } }
            cv.drawBitmap(big, new Rect(0, 0, 240 * k, 296 * k), game, sm);
        }
        bp.setStyle(Paint.Style.FILL);
        for (int i = 0; i < btns.length; i++) {
            Btn b = btns[i]; boolean down = false;
            for (int k : ptrBtn) if (k >= 0 && btns[k] == b) down = true;
            bp.setStyle(Paint.Style.FILL); bp.setColor(down ? 0xFF4A66D8 : 0xFF23252E);
            if (b.circle) cv.drawCircle(b.cx, b.cy, b.rad, bp); else cv.drawRoundRect(b.r, 10, 10, bp);
            bp.setStyle(Paint.Style.STROKE); bp.setStrokeWidth(3); bp.setColor(0xFF4B4F5E);
            if (b.circle) cv.drawCircle(b.cx, b.cy, b.rad, bp); else cv.drawRoundRect(b.r, 10, 10, bp);
            tp.setColor(mode == MODE_PHONE && i == 2 ? 0xFF55D68A : Color.WHITE);
            float cy = b.r.centerY();
            if (b.sub.length() > 0) {
                cv.drawText(b.label, b.r.centerX(), cy + tp.getTextSize() * 0.15f, tp);
                cv.drawText(b.sub, b.r.centerX(), cy + tp.getTextSize() * 0.15f + sp.getTextSize() * 1.5f, sp);
            } else {
                float old = tp.getTextSize(), ts = (b.label.length() >= 2 && !b.circle) ? old * 0.62f : old;
                tp.setTextSize(ts); cv.drawText(b.label, b.r.centerX(), cy + ts * 0.35f, tp); tp.setTextSize(old);
            }
        }
        if (err != null || frames == 0) {
            Paint ep = new Paint(Paint.ANTI_ALIAS_FLAG); ep.setColor(0xFFFFB0B0); ep.setTextSize(11 * density);
            String m = err != null ? "오류:\n" + err : "로딩 중...";
            float y = game.top + 16 * density; for (String ln : m.split("\n")) { cv.drawText(ln.length() > 70 ? ln.substring(0, 70) : ln, 6 * density, y, ep); y += 13 * density; }
        }
        bp.setStyle(Paint.Style.FILL); bp.setColor(0x66000000); cv.drawRoundRect(gear, 14, 14, bp);   // 설정 버튼
        float old = tp.getTextSize(); tp.setColor(0xCCFFFFFF); tp.setTextSize(13 * density);
        cv.drawText("조작", gear.centerX(), gear.centerY() + 5 * density, tp); tp.setTextSize(old);
        postInvalidateOnAnimation();
    }

    void key(int code, boolean down) { if (game_host != null) game_host.postKey(down ? 1 : 2, code); }
    void buzz() { performHapticFeedback(android.view.HapticFeedbackConstants.VIRTUAL_KEY); }
    boolean isArrow(int c) { return c == UP || c == DOWN || c == LEFT || c == RIGHT; }
    /** 키 열을 90ms 간격으로 순서대로 눌렀다 뗀다 */
    void playSeq(final int[] seq) {
        for (int i = 0; i < seq.length; i++) {
            final int c = seq[i]; final long t = 90L * i;
            ui.postDelayed(new Runnable() { public void run() { key(c, true); } }, t);
            ui.postDelayed(new Runnable() { public void run() { key(c, false); } }, t + 45);
        }
    }
    boolean navTap(float x, float y) {                       // 시청 팝업 항목을 눌러 바로 선택
        if (!game.contains(x, y) || game_host == null) return false;
        int[] seq = game_host.tapKeys((int) ((x - game.left) / game.width() * 240f), (int) ((y - game.top) / game.height() * 296f));
        if (seq == null) return false;
        playSeq(seq); return true;
    }
    void tapKey(final int code) { key(code, true); ui.postDelayed(new Runnable() { public void run() { key(code, false); } }, 40); }

    /** 4방향 + 히스테리시스: 현재 방향 축을 쉽게 바꾸지 않아 대각선 근처에서 떨리지 않음 */
    void joyUpdate(float x, float y) {
        float dx = x - jcx, dy = y - jcy, d = (float) Math.sqrt(dx * dx + dy * dy), lim = jr * 0.58f;
        if (d > lim) { dx *= lim / d; dy *= lim / d; }
        kx = jcx + dx; ky = jcy + dy;
        int nd = 0;
        if (d > jr * 0.22f) {                                                        // 작은 데드존
            boolean horiz = Math.abs(dx) > Math.abs(dy);
            if (joyDir == LEFT || joyDir == RIGHT) horiz = Math.abs(dx) * 1.3f > Math.abs(dy);
            else if (joyDir == UP || joyDir == DOWN) horiz = Math.abs(dx) > Math.abs(dy) * 1.3f;
            nd = horiz ? (dx > 0 ? RIGHT : LEFT) : (dy > 0 ? DOWN : UP);
        }
        if (nd != joyDir) {
            joyRep.stop();
            if (joyDir != 0) key(joyDir, false);
            joyDir = nd;
            if (nd != 0) { key(nd, true); buzz(); joyRep.start(nd); }
        }
    }
    void joyEnd() {
        joyRep.stop(); if (joyDir != 0) key(joyDir, false); joyDir = 0; joyPtr = -1;
        jcx = jcx0; jcy = jcy0; kx = jcx; ky = jcy;
    }

    boolean hitBtn(Btn b, float x, float y) {                                        // 손가락 크기를 고려해 판정 영역 약간 확대
        float m = 6 * density;
        if (b.circle) { float dx = x - b.cx, dy = y - b.cy, r = b.rad + m; return dx * dx + dy * dy <= r * r; }
        return x >= b.r.left - m && x <= b.r.right + m && y >= b.r.top - m && y <= b.r.bottom + m;
    }
    int btnAt(float x, float y) {                                                    // 겹치면 가장 가까운 중심 우선
        int best = -1; float bd = 1e18f;
        for (int i = 0; i < btns.length; i++) if (hitBtn(btns[i], x, y)) {
            float dx = x - btns[i].r.centerX(), dy = y - btns[i].r.centerY(), dd = dx * dx + dy * dy;
            if (dd < bd) { bd = dd; best = i; }
        }
        return best;
    }
    void pressBtn(int id, int i) {
        ptrBtn[id] = i; int c = btns[i].code; key(c, true); buzz();
        if (isArrow(c)) ptrRep[id].start(c);
        invalidate();
    }
    void releaseBtn(int id) {
        int i = ptrBtn[id]; if (i < 0) return;
        ptrRep[id].stop(); key(btns[i].code, false); ptrBtn[id] = -1; invalidate();
    }

    @Override public boolean onTouchEvent(MotionEvent e) {
        int a = e.getActionMasked(), idx = e.getActionIndex(), id = e.getPointerId(idx);
        if (a == MotionEvent.ACTION_DOWN || a == MotionEvent.ACTION_POINTER_DOWN) {
            float x = e.getX(idx), y = e.getY(idx);
            if (gear.contains(x, y)) { if (host != null) host.openModeDialog(); return true; }
            if (game.contains(x, y)) {
                if (gesturePtr < 0) { gesturePtr = id; gx = x; gy = y; gdown = System.currentTimeMillis(); gmoved = false; glong = false; gtwo = false; }
                else if (!gmoved && System.currentTimeMillis() - gdown < 400) { gtwo = true; tapKey(CLR); buzz(); }   // 두 손가락 탭 = 취소
            } else if (false) {
                joyPtr = id;                                                                 // 플로팅: 터치한 자리가 스틱 중심
                jcx = Math.max(pad.left + jr, Math.min(zoneRight - jr * 0.2f, x)); jcy = Math.max(pad.top + jr, Math.min(pad.bottom - jr, y));
                kx = x; ky = y; joyUpdate(x, y); invalidate();
            } else if (id < ptrBtn.length) {
                int i = btnAt(x, y); if (i >= 0) pressBtn(id, i);
            }
        } else if (a == MotionEvent.ACTION_MOVE) {
            for (int p = 0; p < e.getPointerCount(); p++) {
                int pid = e.getPointerId(p); float x = e.getX(p), y = e.getY(p);
                if (pid == joyPtr) { joyUpdate(x, y); invalidate(); continue; }
                if (pid < ptrBtn.length && ptrBtn[pid] >= 0) {                              // 버튼 위에서 미끄러져 다른 버튼으로 이동 허용
                    int i = btnAt(x, y);
                    if (i != ptrBtn[pid]) { releaseBtn(pid); if (i >= 0) pressBtn(pid, i); }
                    continue;
                }
                if (pid != gesturePtr || gtwo) continue;
                float dx = x - gx, dy = y - gy;
                if (Math.abs(dx) > swipe || Math.abs(dy) > swipe) {
                    gmoved = true; tapKey(Math.abs(dx) > Math.abs(dy) ? (dx > 0 ? RIGHT : LEFT) : (dy > 0 ? DOWN : UP)); gx = x; gy = y;
                } else if (!gmoved && !glong && System.currentTimeMillis() - gdown > 600) { glong = true; tapKey(CLR); buzz(); }
            }
        } else if (a == MotionEvent.ACTION_UP || a == MotionEvent.ACTION_POINTER_UP || a == MotionEvent.ACTION_CANCEL) {
            if (id == joyPtr) { joyEnd(); invalidate(); }
            else if (id == gesturePtr) {
                if (a != MotionEvent.ACTION_CANCEL && !gmoved && !glong && !gtwo) { if (!navTap(e.getX(idx), e.getY(idx))) tapKey(OK); buzz(); }   // 탭 = 항목 직접 선택, 아니면 확인
                gesturePtr = -1;
            } else if (id < ptrBtn.length && ptrBtn[id] >= 0) releaseBtn(id);
            if (a == MotionEvent.ACTION_CANCEL) { for (int k = 0; k < ptrBtn.length; k++) releaseBtn(k); joyEnd(); }
        }
        return true;
    }
}
