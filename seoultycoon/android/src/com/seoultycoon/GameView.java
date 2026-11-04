package com.seoultycoon;

import android.content.Context;
import android.graphics.*;
import android.os.Handler;
import android.os.Looper;
import android.view.MotionEvent;
import android.view.View;
import javax.microedition.lcdui.Display;

/**
 * LCD 비트맵을 그리고 터치/가상키 입력을 피처폰 키 입력으로 바꾼다.
 * 조작 방식 3가지:  MODE_PHONE=피처폰 버튼, MODE_JOY=조이스틱+버튼, MODE_TOUCH=터치 전용(전체 화면)
 */
public class GameView extends View {
    public static final int UP = 141, DOWN = 146, LEFT = 142, RIGHT = 145, OK = 148, CLR = 8, SOFT_L = 190;
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
    final Rect src = new Rect(0, 0, 176, 224);
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

    public interface Host { void openModeDialog(); }
    Host host;

    public GameView(Context c) {
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
        final float aspect = 224f / 176f;
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
            set(4, cx + cell, top, cell, cell); set(5, cx, top + cell, cell, cell); set(6, cx + cell, top + cell, cell, cell);
            set(7, cx + 2 * cell, top + cell, cell, cell); set(8, cx + cell, top + 2 * cell, cell, cell);
            float ny = top + 3 * rh + 2 * m, nw = (W - 4 * m) / 3f;
            for (int i = 0; i < 12; i++) set(9 + i, pad.left + m + (i % 3) * (nw + m), ny + (i / 3) * (rh + m), nw, rh);
            tp.setTextSize(Math.min(nw, rh) * 0.40f); sp.setTextSize(Math.min(nw, rh) * 0.20f);
        } else {                                        // 조이스틱
            btns = new Btn[]{ new Btn("확인", "", OK), new Btn("취소", "", CLR),
                              new Btn("좌메뉴", "", SOFT_L), new Btn("*", "", '*'), new Btn("0", "", '0'), new Btn("#", "", '#') };
            jr = Math.min(W * 0.22f, H * 0.36f); jcx0 = pad.left + W * 0.27f; jcy0 = pad.top + H * 0.46f; jcx = jcx0; jcy = jcy0; kx = jcx; ky = jcy; zoneRight = pad.left + W * 0.52f;
            float ro = Math.min(W * 0.15f, H * 0.27f);
            circ(0, pad.left + W * 0.80f, pad.top + H * 0.36f, ro);
            circ(1, pad.left + W * 0.59f, pad.top + H * 0.58f, ro * 0.72f);
            float bw = (W * 0.52f - 5 * m) / 4f, bh = Math.min(H * 0.18f, bw * 0.8f), by = pad.bottom - m - bh;
            float bx = pad.left + W * 0.46f;
            for (int i = 0; i < 4; i++) set(2 + i, bx + i * (bw + m), by, bw, bh);
            tp.setTextSize(ro * 0.5f); sp.setTextSize(ro * 0.25f);
        }
    }

    @Override protected void onDraw(Canvas cv) {
        cv.drawColor(mode == MODE_TOUCH ? 0xFF000000 : 0xFF101018);
        cv.drawBitmap(javax.microedition.lcdui.Canvas.lcd, src, game, gp);
        bp.setStyle(Paint.Style.FILL);
        if (mode == MODE_JOY) {                          // 조이스틱 판
            bp.setColor(joyPtr >= 0 ? 0xFF2A3050 : 0x881C1E27); cv.drawCircle(jcx, jcy, jr, bp);
            bp.setStyle(Paint.Style.STROKE); bp.setStrokeWidth(4); bp.setColor(0xFF4B4F5E); cv.drawCircle(jcx, jcy, jr, bp);
            bp.setStyle(Paint.Style.FILL); bp.setColor(joyPtr >= 0 ? 0xFF5B7CFA : 0xFF3A3F55); cv.drawCircle(kx, ky, jr * 0.42f, bp);
        }
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
        bp.setStyle(Paint.Style.FILL); bp.setColor(0x66000000); cv.drawRoundRect(gear, 14, 14, bp);   // 설정 버튼
        float old = tp.getTextSize(); tp.setColor(0xCCFFFFFF); tp.setTextSize(13 * density);
        cv.drawText("조작", gear.centerX(), gear.centerY() + 5 * density, tp); tp.setTextSize(old);
        postInvalidateDelayed(33);
    }

    void key(int code, boolean down) {
        javax.microedition.lcdui.Canvas c = Display.current;
        if (c == null) return;
        if (down) c.keyPressed(code); else c.keyReleased(code);
    }
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
    boolean navTap(float x, float y) {                       // 화면 터치 → 메뉴 항목 직접 선택
        if (!game.contains(x, y)) return false;
        float gx = (x - game.left) / game.width() * 176f, gy = (y - game.top) / game.height() * 224f;
        int[] seq = TouchNav.tap(gx, gy);
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
            } else if (mode == MODE_JOY && joyPtr < 0 && x < zoneRight && y > pad.top && btnAt(x, y) < 0) {
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
