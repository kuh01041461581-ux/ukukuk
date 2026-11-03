package javax.microedition.lcdui;
import android.graphics.*;
public class Graphics {
    public static final int HCENTER = 1, VCENTER = 2, LEFT = 4, RIGHT = 8, TOP = 16, BOTTOM = 32, BASELINE = 64;
    public final Bitmap bm; public final android.graphics.Canvas c; final Paint p = new Paint(); final Paint tp = new Paint();
    final int base; final Rect r = new Rect(), r2 = new Rect();
    public Graphics(Bitmap b) {
        bm = b; c = new android.graphics.Canvas(b); base = c.save();
        p.setAntiAlias(false); p.setFilterBitmap(false); p.setDither(false); p.setStyle(Paint.Style.FILL);
        tp.setAntiAlias(false); tp.setTextSize(12); tp.setStyle(Paint.Style.FILL);
        setColor(0);
    }
    public void setColor(int rgb) { int v = 0xFF000000 | rgb; p.setColor(v); tp.setColor(v); }
    public void setColor(int r, int g, int b) { setColor((r << 16) | (g << 8) | b); }
    public void fillRect(int x, int y, int w, int h) { p.setStyle(Paint.Style.FILL); c.drawRect(x, y, x + w, y + h, p); }
    public void drawRect(int x, int y, int w, int h) {   // MIDP: (w+1)x(h+1) 외곽선
        p.setStyle(Paint.Style.FILL);
        c.drawRect(x, y, x + w + 1, y + 1, p); c.drawRect(x, y + h, x + w + 1, y + h + 1, p);
        c.drawRect(x, y, x + 1, y + h + 1, p); c.drawRect(x + w, y, x + w + 1, y + h + 1, p);
    }
    public void drawLine(int x1, int y1, int x2, int y2) {
        if (y1 == y2) { c.drawRect(Math.min(x1, x2), y1, Math.max(x1, x2) + 1, y1 + 1, p); return; }
        if (x1 == x2) { c.drawRect(x1, Math.min(y1, y2), x1 + 1, Math.max(y1, y2) + 1, p); return; }
        int dx = Math.abs(x2 - x1), dy = Math.abs(y2 - y1), sx = x1 < x2 ? 1 : -1, sy = y1 < y2 ? 1 : -1, e = dx - dy;
        while (true) {
            c.drawRect(x1, y1, x1 + 1, y1 + 1, p);
            if (x1 == x2 && y1 == y2) break;
            int e2 = 2 * e; if (e2 > -dy) { e -= dy; x1 += sx; } if (e2 < dx) { e += dx; y1 += sy; }
        }
    }
    public void setClip(int x, int y, int w, int h) { c.restoreToCount(base); c.save(); c.clipRect(x, y, x + w, y + h); }
    public void drawImage(Image im, int x, int y, int anchor) {
        if ((anchor & HCENTER) != 0) x -= im.getWidth() / 2; else if ((anchor & RIGHT) != 0) x -= im.getWidth();
        if ((anchor & VCENTER) != 0) y -= im.getHeight() / 2; else if ((anchor & BOTTOM) != 0) y -= im.getHeight();
        c.drawBitmap(im.bm, x, y, p);
    }
    public void drawRegion(Bitmap src, int x, int y, int sx, int sy, int w, int h) {
        r.set(sx, sy, sx + w, sy + h); r2.set(x, y, x + w, y + h); c.drawBitmap(src, r, r2, p);
    }
    public void drawString(String s, int x, int y, int anchor) {
        float w = tp.measureText(s);
        if ((anchor & HCENTER) != 0) x -= (int) (w / 2); else if ((anchor & RIGHT) != 0) x -= (int) w;
        float base = y - tp.ascent();
        if ((anchor & BOTTOM) != 0) base = y - tp.descent();
        c.drawText(s, x, base, tp);
    }
}
