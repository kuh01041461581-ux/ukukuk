package com.seoultycoon.st2;

/** 도트 그림용 확대(Scale2x/EPX): 계단 모서리만 부드럽게 다듬고 색은 그대로 둔다. 화면 표시 단계에서만 쓴다(게임 내부 해상도는 240x296 고정). */
public final class PixelScale {
    private PixelScale() { }
    /** src(w*h, 0xRRGGBB 또는 ARGB) → 가로세로 2배 */
    public static int[] epx2(int[] s, int w, int h, int[] out) {
        int W2 = w * 2;
        if (out == null || out.length < W2 * h * 2) out = new int[W2 * h * 2];
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                int p = s[y * w + x];
                int a = y > 0 ? s[(y - 1) * w + x] : p, d = y < h - 1 ? s[(y + 1) * w + x] : p, c = x > 0 ? s[y * w + x - 1] : p, b = x < w - 1 ? s[y * w + x + 1] : p;
                int e0 = p, e1 = p, e2 = p, e3 = p;
                if (c == a && c != d && a != b) e0 = a;
                if (a == b && a != c && b != d) e1 = b;
                if (d == c && d != b && c != a) e2 = c;
                if (b == d && b != a && d != c) e3 = d;
                int o = (y * 2) * W2 + x * 2; out[o] = e0; out[o + 1] = e1; out[o + W2] = e2; out[o + W2 + 1] = e3;
            }
        }
        return out;
    }
    private static boolean sim(int p, int q) { if (p == q) return true; int d = Math.abs(((p >> 16) & 255) - ((q >> 16) & 255)) + Math.abs(((p >> 8) & 255) - ((q >> 8) & 255)) + Math.abs((p & 255) - (q & 255)); return d <= 54; }
    private static int mix(int p, int q, int wq) { if ((p >>> 24) == 0 || (q >>> 24) == 0) return p; int r = (((p >> 16) & 255) * (4 - wq) + ((q >> 16) & 255) * wq) >> 2, g = (((p >> 8) & 255) * (4 - wq) + ((q >> 8) & 255) * wq) >> 2, b = ((p & 255) * (4 - wq) + (q & 255) * wq) >> 2; return (p & 0xFF000000) | (r << 16) | (g << 8) | b; }
    /** 리마스터용 2배 확대: Scale2x 의 계단 모서리 조건을 '비슷한 색'까지 넓히고 모서리 화소를 이웃 색과 섞어 채워서, 색이 많은 그림(건물·지형·인물)이 부드러운 중간색 경계를 갖게 한다. */
    public static int[] epx2s(int[] s, int w, int h, int[] out) {
        int W2 = w * 2; if (out == null || out.length < W2 * h * 2) out = new int[W2 * h * 2];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) {
            int p = s[y * w + x];
            int a = y > 0 ? s[(y - 1) * w + x] : p, d = y < h - 1 ? s[(y + 1) * w + x] : p, c = x > 0 ? s[y * w + x - 1] : p, b = x < w - 1 ? s[y * w + x + 1] : p;
            int e0 = p, e1 = p, e2 = p, e3 = p;
            if (sim(c, a) && !sim(c, d) && !sim(a, b)) e0 = mix(p, mix(c, a, 2), 3);
            if (sim(a, b) && !sim(a, c) && !sim(b, d)) e1 = mix(p, mix(a, b, 2), 3);
            if (sim(d, c) && !sim(d, b) && !sim(c, a)) e2 = mix(p, mix(d, c, 2), 3);
            if (sim(b, d) && !sim(b, a) && !sim(d, c)) e3 = mix(p, mix(b, d, 2), 3);
            int o = (y * 2) * W2 + x * 2; out[o] = e0; out[o + 1] = e1; out[o + W2] = e2; out[o + W2 + 1] = e3;
        }
        return out;
    }
    /** 4배(2배를 두 번) */
    /** 업스케일 없이 4배 최근접(글씨·도트가 그대로, 번지지 않음) */
    public static int[] nn4(int[] s, int w, int h) { int[] o = new int[w * 4 * h * 4]; for (int y = 0; y < h * 4; y++) for (int x = 0; x < w * 4; x++) o[y * w * 4 + x] = s[(y >> 2) * w + (x >> 2)]; return o; }
    /** 정수배 최근접 확대 */
    public static int[] nn(int[] s, int w, int h, int k) { int[] o = new int[w * k * h * k]; for (int y = 0; y < h * k; y++) for (int x = 0; x < w * k; x++) o[y * w * k + x] = s[(y / k) * w + (x / k)]; return o; }
    public static int[] epx4(int[] s, int w, int h) { return epx2(epx2(s, w, h, null), w * 2, h * 2, null); }
}
