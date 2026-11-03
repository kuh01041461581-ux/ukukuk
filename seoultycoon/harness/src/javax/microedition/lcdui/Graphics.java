package javax.microedition.lcdui;
import java.awt.*;
import java.awt.image.BufferedImage;
public class Graphics {
    public static final int HCENTER = 1, VCENTER = 2, LEFT = 4, RIGHT = 8, TOP = 16, BOTTOM = 32, BASELINE = 64;
    public BufferedImage bi; public Graphics2D g; Font font = new Font("WenQuanYi Zen Hei", Font.PLAIN, 12);
    public Graphics(BufferedImage b) {
        bi = b; g = b.createGraphics();
        g.setRenderingHint(RenderingHints.KEY_TEXT_ANTIALIASING, RenderingHints.VALUE_TEXT_ANTIALIAS_OFF);
        g.setFont(font);
    }
    public void setColor(int rgb) { g.setColor(new Color(rgb & 0xFFFFFF)); }
    public void setColor(int r, int gr, int b) { g.setColor(new Color(r, gr, b)); }
    public void fillRect(int x, int y, int w, int h) { g.fillRect(x, y, w, h); }
    public void drawRect(int x, int y, int w, int h) { g.drawRect(x, y, w, h); }
    public void drawLine(int a, int b, int c, int d) { g.drawLine(a, b, c, d); }
    public void setClip(int x, int y, int w, int h) { g.setClip(x, y, w, h); }
    public void drawImage(Image im, int x, int y, int anchor) {
        if ((anchor & HCENTER) != 0) x -= im.getWidth() / 2; else if ((anchor & RIGHT) != 0) x -= im.getWidth();
        if ((anchor & VCENTER) != 0) y -= im.getHeight() / 2; else if ((anchor & BOTTOM) != 0) y -= im.getHeight();
        g.drawImage(im.bi, x, y, null);
    }
    public void drawString(String s, int x, int y, int anchor) {
        FontMetrics fm = g.getFontMetrics();
        int w = fm.stringWidth(s);
        if ((anchor & HCENTER) != 0) x -= w / 2; else if ((anchor & RIGHT) != 0) x -= w;
        int base = y + fm.getAscent();
        if ((anchor & BOTTOM) != 0) base = y - fm.getDescent();
        g.drawString(s, x, base);
    }
}
