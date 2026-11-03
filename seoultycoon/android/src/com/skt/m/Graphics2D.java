package com.skt.m;
import javax.microedition.lcdui.*;
import android.graphics.Bitmap;
public class Graphics2D {
    Graphics g;
    public static Graphics2D getGraphics2D(Graphics g) { Graphics2D r = new Graphics2D(); r.g = g; return r; }
    public static Graphics2D getGraphics(Graphics g) { return getGraphics2D(g); }
    public int getPixel(int x, int y) { return g.bm.getPixel(x, y) & 0xFFFFFF; }
    public void setPixel(int x, int y, int c) { g.bm.setPixel(x, y, 0xFF000000 | c); }
    public void drawImage(int x, int y, Image im, int sx, int sy, int w, int h, int flags) { g.drawRegion(im.bm, x, y, sx, sy, w, h); }
    public static Image captureLCD(int x, int y, int w, int h) {
        Image im = Image.createImage(w, h);
        im.getGraphics().drawRegion(Canvas.lcd, 0, 0, x, y, w, h);
        return im;
    }
}
