package com.seoultycoon;

import android.content.Context;
import android.graphics.*;
import android.os.Handler;
import android.os.Looper;
import android.view.MotionEvent;
import android.view.View;
import javax.microedition.lcdui.Display;

/** LCD 비트맵을 정수에 가까운 배율로 그리고, 터치/가상키패드를 피처폰 키 입력으로 바꾼다. */
public class GameView extends View {
    // SKT 키코드
    public static final int UP = 141, DOWN = 146, LEFT = 142, RIGHT = 145, OK = 148, CLR = 8, SOFT_L = 190;
    static final class Btn { final RectF r = new RectF(); final String label, sub; final int code; Btn(String l, String sb, int c) { label = l; sub = sb; code = c; } }
    final Btn[] btns;
    final Paint bp = new Paint(Paint.ANTI_ALIAS_FLAG), tp = new Paint(Paint.ANTI_ALIAS_FLAG), sp = new Paint(Paint.ANTI_ALIAS_FLAG), gp = new Paint();
    final RectF game = new RectF(), pad = new RectF();
    final Rect src = new Rect(0, 0, 176, 224);
    final Handler ui = new Handler(Looper.getMainLooper());
    final int[] ptrBtn = new int[16];
    // 화면 제스처
    int gesturePtr = -1; float gx, gy, gdx, gdy; long gdown; boolean gmoved, glong;
    final float swipe;

    public GameView(Context c) {
        super(c);
        float d = c.getResources().getDisplayMetrics().density;
        swipe = 22 * d;
        gp.setFilterBitmap(false);
        sp.setColor(0xFF9AA0B8); sp.setTextAlign(Paint.Align.CENTER);
        tp.setColor(Color.WHITE); tp.setTextAlign(Paint.Align.CENTER); tp.setTypeface(Typeface.DEFAULT_BOLD);
        // 피처폰 모양 키패드: [0]좌메뉴 [1]우메뉴 [2]통화 [3]취소 [4..8]방향/확인 [9..20]숫자
        btns = new Btn[]{
            new Btn("좌메뉴", "", SOFT_L), new Btn("우메뉴", "", CLR), new Btn("통화", "", OK), new Btn("취소", "", CLR),
            new Btn("▲", "", UP), new Btn("◀", "", LEFT), new Btn("OK", "", OK), new Btn("▶", "", RIGHT), new Btn("▼", "", DOWN),
            new Btn("1", "", '1'), new Btn("2", "ABC", '2'), new Btn("3", "DEF", '3'),
            new Btn("4", "GHI", '4'), new Btn("5", "JKL", '5'), new Btn("6", "MNO", '6'),
            new Btn("7", "PQRS", '7'), new Btn("8", "TUV", '8'), new Btn("9", "WXYZ", '9'),
            new Btn("*", "", '*'), new Btn("0", "", '0'), new Btn("#", "", '#')};
        java.util.Arrays.fill(ptrBtn, -1);
        setKeepScreenOn(true);
    }

    @Override protected void onSizeChanged(int w, int h, int ow, int oh) {
        float aspect = 224f / 176f;
        boolean land = w > h;
        if (!land) {
            float gh = Math.min(w * aspect, h * 0.56f), gw = gh / aspect;
            game.set((w - gw) / 2, 0, (w + gw) / 2, gh);
            pad.set(0, gh, w, h);
        } else {
            float gw = Math.min(h / aspect, w * 0.55f), gh = gw * aspect;
            game.set(0, (h - gh) / 2, gw, (h + gh) / 2);
            pad.set(gw, 0, w, h);
        }
        float W = pad.width(), H = pad.height(), m = Math.max(4, Math.min(W, H) * 0.012f);
        float rh = Math.min((H - 8 * m) / 7f, W / 6.2f);              // 행 높이
        float top = pad.top + Math.max(m, (H - (7 * rh + 8 * m)) / 2);
        float sw = W * 0.30f, dw = W - 2 * sw - 4 * m;                // 소프트키 폭 / 방향키 영역 폭
        float cell = Math.min(dw / 3f, rh);
        float lx = pad.left + m, rx = pad.right - m - sw, cx = pad.left + W / 2 - 1.5f * cell;
        set(0, lx, top, sw, rh * 0.9f);  set(2, lx, top + rh + m, sw, rh * 0.9f);
        set(1, rx, top, sw, rh * 0.9f);  set(3, rx, top + rh + m, sw, rh * 0.9f);
        set(4, cx + cell, top, cell, cell);                set(5, cx, top + cell, cell, cell);
        set(6, cx + cell, top + cell, cell, cell);         set(7, cx + 2 * cell, top + cell, cell, cell);
        set(8, cx + cell, top + 2 * cell, cell, cell);
        float ny = top + 3 * rh + 2 * m, nw = (W - 4 * m) / 3f;
        for (int i = 0; i < 12; i++) {
            int col = i % 3, row = i / 3;
            set(9 + i, pad.left + m + col * (nw + m), ny + row * (rh + m), nw, rh);
        }
        tp.setTextSize(Math.min(nw, rh) * 0.40f);
        sp.setTextSize(Math.min(nw, rh) * 0.20f);
    }

    void set(int i, float x, float y, float w, float h) { btns[i].r.set(x, y, x + w, y + h); }

    @Override protected void onDraw(Canvas cv) {
        cv.drawColor(0xFF101018);
        cv.drawBitmap(javax.microedition.lcdui.Canvas.lcd, src, game, gp);
        bp.setStyle(Paint.Style.FILL);
        for (int i = 0; i < btns.length; i++) {
            Btn b = btns[i];
            boolean down = false;
            for (int k : ptrBtn) if (k >= 0 && btns[k] == b) down = true;
            bp.setStyle(Paint.Style.FILL);
            bp.setColor(down ? 0xFF4A66D8 : 0xFF23252E);
            cv.drawRoundRect(b.r, 10, 10, bp);
            bp.setStyle(Paint.Style.STROKE); bp.setStrokeWidth(3); bp.setColor(0xFF4B4F5E);
            cv.drawRoundRect(b.r, 10, 10, bp);
            tp.setColor(i == 2 ? 0xFF55D68A : Color.WHITE);                 // 통화 = 녹색
            float cy = b.r.centerY();
            if (b.sub.length() > 0) {
                cv.drawText(b.label, b.r.centerX(), cy + tp.getTextSize() * 0.15f, tp);
                cv.drawText(b.sub, b.r.centerX(), cy + tp.getTextSize() * 0.15f + sp.getTextSize() * 1.5f, sp);
            } else {
                float ts = (i < 4) ? tp.getTextSize() * 0.62f : tp.getTextSize();
                float old = tp.getTextSize(); tp.setTextSize(ts);
                cv.drawText(b.label, b.r.centerX(), cy + ts * 0.35f, tp);
                tp.setTextSize(old);
            }
        }
        postInvalidateDelayed(33);
    }

    void key(int code, boolean down) {
        javax.microedition.lcdui.Canvas c = Display.current;
        if (c == null) return;
        if (down) c.keyPressed(code); else c.keyReleased(code);
    }

    void tapKey(final int code) {
        key(code, true);
        ui.postDelayed(new Runnable() { public void run() { key(code, false); } }, 70);
    }

    @Override public boolean onTouchEvent(MotionEvent e) {
        int a = e.getActionMasked(), idx = e.getActionIndex(), id = e.getPointerId(idx);
        if (a == MotionEvent.ACTION_DOWN || a == MotionEvent.ACTION_POINTER_DOWN) {
            float x = e.getX(idx), y = e.getY(idx);
            if (game.contains(x, y)) {
                if (gesturePtr < 0) { gesturePtr = id; gx = x; gy = y; gdown = System.currentTimeMillis(); gmoved = false; glong = false; }
            } else if (id < ptrBtn.length) {
                for (int i = 0; i < btns.length; i++) if (btns[i].r.contains(x, y)) { ptrBtn[id] = i; key(btns[i].code, true); break; }
            }
        } else if (a == MotionEvent.ACTION_MOVE) {
            for (int p = 0; p < e.getPointerCount(); p++) {
                if (e.getPointerId(p) != gesturePtr) continue;
                float x = e.getX(p), y = e.getY(p);
                float dx = x - gx, dy = y - gy;
                if (Math.abs(dx) > swipe || Math.abs(dy) > swipe) {
                    gmoved = true;
                    tapKey(Math.abs(dx) > Math.abs(dy) ? (dx > 0 ? RIGHT : LEFT) : (dy > 0 ? DOWN : UP));
                    gx = x; gy = y;
                } else if (!gmoved && !glong && System.currentTimeMillis() - gdown > 600) {
                    glong = true; tapKey(CLR);          // 길게 누르면 취소
                }
            }
        } else if (a == MotionEvent.ACTION_UP || a == MotionEvent.ACTION_POINTER_UP || a == MotionEvent.ACTION_CANCEL) {
            if (id == gesturePtr) {
                if (a != MotionEvent.ACTION_CANCEL && !gmoved && !glong) tapKey(OK);   // 탭 = 확인
                gesturePtr = -1;
            } else if (id < ptrBtn.length && ptrBtn[id] >= 0) {
                key(btns[ptrBtn[id]].code, false); ptrBtn[id] = -1;
            }
        }
        return true;
    }
}
