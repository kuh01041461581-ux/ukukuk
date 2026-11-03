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
    public static final int UP = 141, DOWN = 146, LEFT = 142, RIGHT = 145, OK = 148, CLR = 8;
    static final class Btn { final RectF r = new RectF(); final String label; final int code; Btn(String l, int c) { label = l; code = c; } }
    final Btn[] btns;
    final Paint bp = new Paint(Paint.ANTI_ALIAS_FLAG), tp = new Paint(Paint.ANTI_ALIAS_FLAG), gp = new Paint();
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
        tp.setColor(Color.WHITE); tp.setTextAlign(Paint.Align.CENTER); tp.setTypeface(Typeface.DEFAULT_BOLD);
        btns = new Btn[]{
            new Btn("취소", CLR), new Btn("▲", UP), new Btn("#", '#'),
            new Btn("◀", LEFT), new Btn("확인", OK), new Btn("▶", RIGHT),
            new Btn("*", '*'), new Btn("▼", DOWN), new Btn("0", '0'),
            new Btn("1", '1'), new Btn("2", '2'), new Btn("3", '3'),
            new Btn("4", '4'), new Btn("5", '5'), new Btn("6", '6'),
            new Btn("7", '7'), new Btn("8", '8'), new Btn("9", '9')};
        java.util.Arrays.fill(ptrBtn, -1);
        setKeepScreenOn(true);
    }

    @Override protected void onSizeChanged(int w, int h, int ow, int oh) {
        float aspect = 224f / 176f;
        boolean land = w > h;
        if (!land) {
            float gh = Math.min(w * aspect, h * 0.62f), gw = gh / aspect;
            game.set((w - gw) / 2, 0, (w + gw) / 2, gh);
            pad.set(0, gh, w, h);
        } else {
            float gw = Math.min(h / aspect, w * 0.55f), gh = gw * aspect;
            game.set(0, (h - gh) / 2, gw, (h + gh) / 2);
            pad.set(gw, 0, w, h);
        }
        float m = Math.min(pad.width(), pad.height()) * 0.015f + 4;
        float cw = (pad.width() - m * 4) / 3, ch = (pad.height() - m * 7) / 6;
        for (int i = 0; i < btns.length; i++) {
            int cx = i % 3, cy = i / 3;
            float x = pad.left + m + cx * (cw + m), y = pad.top + m + cy * (ch + m);
            btns[i].r.set(x, y, x + cw, y + ch);
        }
        tp.setTextSize(Math.min(cw, ch) * 0.42f);
    }

    @Override protected void onDraw(Canvas cv) {
        cv.drawColor(0xFF101018);
        cv.drawBitmap(javax.microedition.lcdui.Canvas.lcd, src, game, gp);
        bp.setStyle(Paint.Style.FILL);
        for (Btn b : btns) {
            boolean down = false;
            for (int k : ptrBtn) if (k >= 0 && btns[k] == b) down = true;
            bp.setColor(down ? 0xFF5B7CFA : (b.code == OK ? 0xFF2F4A9A : 0xFF2A2A3C));
            cv.drawRoundRect(b.r, 18, 18, bp);
            cv.drawText(b.label, b.r.centerX(), b.r.centerY() + tp.getTextSize() * 0.35f, tp);
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
