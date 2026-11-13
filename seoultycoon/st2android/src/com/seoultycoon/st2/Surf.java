package com.seoultycoon.st2;

/** 소프트웨어 2D 서피스(ARGB int[]) — WIPI Graphics 의 그리기 연산과 클립을 구현 */
public final class Surf {
    public final int w, h; public final int[] px;
    /** HD 층: 같은 그림을 K배 해상도로 함께 유지한다(리마스터). 게임 로직은 px(원래 해상도)만 보고, 화면 출력은 hp 를 쓴다.
     *  HD 그림이 따로 없는 이미지는 px 를 그대로 K배 확대해 쓴다. */
    public static final int K = 2;
    public int[] hp;
    public int[] hd() {
        if (hp == null) { int W = w * K; int[] o = new int[W * h * K];
            for (int y = 0; y < h; y++) { int ro = y * K * W; for (int x = 0; x < w; x++) { int p = px[y * w + x]; for (int b = 0; b < K; b++) o[ro + x * K + b] = p; }
                for (int a = 1; a < K; a++) System.arraycopy(o, ro, o, ro + a * W, W); }
            hp = o; }
        return hp;
    }
    public int color = 0xFF000000;
    public int cx, cy, cw, ch; public boolean hasClip;
    public Surf(int w, int h, boolean opaqueBlack) { this.w = w; this.h = h; px = new int[w * h]; if (opaqueBlack) java.util.Arrays.fill(px, 0xFF000000); }
    public void setColor(int rgb) { color = 0xFF000000 | rgb; }
    public void setClip(int x, int y, int cw_, int ch_) { cx = x; cy = y; cw = cw_; ch = ch_; hasClip = true; }
    // 클립 ∩ 서피스 경계
    private int x0, y0, x1, y1;
    private boolean bounds() {
        x0 = 0; y0 = 0; x1 = w; y1 = h;
        if (hasClip) { x0 = Math.max(x0, cx); y0 = Math.max(y0, cy); x1 = Math.min(x1, cx + cw); y1 = Math.min(y1, cy + ch); }
        return x1 > x0 && y1 > y0;
    }
    public void fillRect(int x, int y, int ww, int hh) {
        if (ww <= 0 || hh <= 0 || !bounds()) return;
        int ax = Math.max(x, x0), ay = Math.max(y, y0), bx = Math.min(x + ww, x1), by = Math.min(y + hh, y1);
        for (int j = ay; j < by; j++) java.util.Arrays.fill(px, j * w + ax, j * w + Math.max(ax, bx), color);
        int[] H2 = hd(); int W = w * K; if (bx > ax) for (int j = ay * K; j < by * K; j++) java.util.Arrays.fill(H2, j * W + ax * K, j * W + bx * K, color);
    }
    private void put(int x, int y) { if (x >= x0 && x < x1 && y >= y0 && y < y1) { px[y * w + x] = color; int[] H2 = hd(); int W = w * K; for (int a = 0; a < K; a++) for (int b = 0; b < K; b++) H2[(y * K + a) * W + x * K + b] = color; } }
    public void drawRect(int x, int y, int ww, int hh) {
        if (ww < 0 || hh < 0 || !bounds()) return;
        for (int i = 0; i <= ww; i++) { put(x + i, y); put(x + i, y + hh); }
        for (int j = 0; j <= hh; j++) { put(x, y + j); put(x + ww, y + j); }
    }
    public void drawLine(int a, int b, int c, int d) {
        if (!bounds()) return;
        int dx = Math.abs(c - a), dy = Math.abs(d - b), sx = a < c ? 1 : -1, sy = b < d ? 1 : -1, e = dx - dy;
        for (int guard = 0; guard < 4096; guard++) {
            put(a, b); if (a == c && b == d) break;
            int e2 = 2 * e; if (e2 > -dy) { e -= dy; a += sx; } if (e2 < dx) { e += dx; b += sy; }
        }
    }
    /** 타원(채움/외곽) — 호(arc)는 전체 타원으로 근사 */
    public void ellipse(int x, int y, int ww, int hh, boolean fill) {
        if (ww <= 0 || hh <= 0 || !bounds()) return;
        double rx = ww / 2.0, ry = hh / 2.0, ccx = x + rx, ccy = y + ry;
        for (int j = y; j <= y + hh; j++) for (int i = x; i <= x + ww; i++) {
            double ux = (i + 0.5 - ccx) / rx, uy = (j + 0.5 - ccy) / ry, d = ux * ux + uy * uy;
            if (fill ? d <= 1.0 : (d <= 1.0 && d >= 0.72)) put(i, j);
        }
    }
    public void roundRect(int x, int y, int ww, int hh, int aw, int ah, boolean fill) {
        if (ww < 0 || hh < 0) return;
        if (fill) { fillRect(x, y, ww, hh); } else drawRect(x, y, ww, hh);   // 모서리 둥글림은 생략(작은 반경)
    }
    /** 이미지 블릿(알파 합성). anchor 보정은 호출측에서 처리 */
    public void drawImage(Surf src, int x, int y) {
        if (!bounds()) return;
        int ax = Math.max(x, x0), ay = Math.max(y, y0), bx = Math.min(x + src.w, x1), by = Math.min(y + src.h, y1);
        for (int j = ay; j < by; j++) {
            int so = (j - y) * src.w - x, dof = j * w;
            for (int i = ax; i < bx; i++) {
                int p = src.px[so + i]; int al = p >>> 24;
                if (al == 255) px[dof + i] = p;
                else if (al != 0) {
                    int d = px[dof + i], ia = 255 - al;
                    int r = (((p >> 16) & 255) * al + ((d >> 16) & 255) * ia) / 255, g = (((p >> 8) & 255) * al + ((d >> 8) & 255) * ia) / 255, bl = ((p & 255) * al + (d & 255) * ia) / 255;
                    px[dof + i] = 0xFF000000 | (r << 16) | (g << 8) | bl;
                }
            }
        }
        int[] sh = src.hd(), dh = hd(); int W = w * K, sw = src.w * K;
        for (int j = ay * K; j < by * K; j++) {
            int so = (j - y * K) * sw - x * K, dof = j * W;
            for (int i = ax * K; i < bx * K; i++) {
                int p = sh[so + i]; int al = p >>> 24;
                if (al == 255) dh[dof + i] = p;
                else if (al != 0) {
                    int d = dh[dof + i], ia = 255 - al;
                    int r = (((p >> 16) & 255) * al + ((d >> 16) & 255) * ia) / 255, g = (((p >> 8) & 255) * al + ((d >> 8) & 255) * ia) / 255, bl = ((p & 255) * al + (d & 255) * ia) / 255;
                    dh[dof + i] = 0xFF000000 | (r << 16) | (g << 8) | bl;
                }
            }
        }
    }
    /** 글자 마스크(알파)를 현재 색으로 칠함. ha 가 있으면 HD 층에는 그 매끈한 마스크(알파 섞기)를 쓴다 */
    public void drawMask(byte[] a, int mw, int mh, int x, int y) { drawMask(a, mw, mh, x, y, null, 0, 0); }
    public void drawMask(byte[] a, int mw, int mh, int x, int y, byte[] ha, int hw, int hh) {
        if (!bounds()) return;
        int[] H2 = hd(); int W = w * K;
        for (int j = 0; j < mh; j++) { int yy = y + j; if (yy < y0 || yy >= y1) continue;
            for (int i = 0; i < mw; i++) { int xx = x + i; if (xx < x0 || xx >= x1) continue;
                if ((a[j * mw + i] & 0xff) >= 128) { px[yy * w + xx] = color;
                    if (ha == null) for (int q = 0; q < K; q++) for (int b = 0; b < K; b++) H2[(yy * K + q) * W + xx * K + b] = color; } } }
        if (ha != null) {
            int cr = (color >> 16) & 255, cg = (color >> 8) & 255, cb = color & 255;
            for (int j = 0; j < hh; j++) { int yy = y * K + j; if (yy < y0 * K || yy >= y1 * K) continue;
                for (int i = 0; i < hw; i++) { int xx = x * K + i; if (xx < x0 * K || xx >= x1 * K) continue;
                    int al = ha[j * hw + i] & 0xff; if (al == 0) continue;
                    if (al >= 250) { H2[yy * W + xx] = color; continue; }
                    int d = H2[yy * W + xx], ia = 255 - al;
                    H2[yy * W + xx] = 0xFF000000 | (((cr * al + ((d >> 16) & 255) * ia) / 255) << 16) | (((cg * al + ((d >> 8) & 255) * ia) / 255) << 8) | ((cb * al + (d & 255) * ia) / 255); } }
        }
    }
    public int getPixel(int x, int y) { return (x < 0 || y < 0 || x >= w || y >= h) ? 0 : (px[y * w + x] & 0xFFFFFF); }
}
