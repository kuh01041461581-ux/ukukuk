package com.skt.m;
import javax.microedition.lcdui.*;
import java.awt.image.BufferedImage;
public class Graphics2D {
    Graphics g;
    public static Graphics2D getGraphics2D(Graphics g) { Graphics2D r = new Graphics2D(); r.g = g; return r; }
    public static Graphics2D getGraphics(Graphics g) { return getGraphics2D(g); }
    public int getPixel(int x, int y) { return g.bi.getRGB(x, y) & 0xFFFFFF; }
    public void setPixel(int x, int y, int c) { g.bi.setRGB(x, y, 0xFF000000 | c); }
    public void drawImage(int x, int y, Image im, int sx, int sy, int w, int h, int flags) {
        // draws sub-rect (sx,sy,w,h) of im at (x,y); flags: transform (0 = none)
        BufferedImage sub = im.bi.getSubimage(sx, sy, w, h);
        java.awt.Graphics2D gg = g.g;
        java.awt.Shape clip = gg.getClip();
        gg.drawImage(sub, x, y, null);
    }
    public static Image captureLCD(int x, int y, int w, int h) {
        BufferedImage s = Canvas.lcd.getSubimage(x, y, w, h);
        BufferedImage c = new BufferedImage(w, h, BufferedImage.TYPE_INT_ARGB);
        c.getGraphics().drawImage(s, 0, 0, null);
        return new Image(c);
    }
}
