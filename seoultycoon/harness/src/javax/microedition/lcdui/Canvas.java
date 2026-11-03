package javax.microedition.lcdui;
/** Minimal MIDP Canvas stub: a fixed-size back buffer that doubles as the "LCD". */
public abstract class Canvas extends Displayable {
    public static final int W = 176, H = 208;   // game uses getHeight()+16 internally
    public static java.awt.image.BufferedImage lcd = new java.awt.image.BufferedImage(W, H + 16, java.awt.image.BufferedImage.TYPE_INT_RGB);
    public int getWidth() { return W; }
    public int getHeight() { return H; }
    protected abstract void paint(Graphics g);
    public void keyPressed(int k) {}
    public void keyReleased(int k) {}
    public void repaint() {}
    public void serviceRepaints() {}
}
