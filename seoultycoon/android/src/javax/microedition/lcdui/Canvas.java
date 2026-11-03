package javax.microedition.lcdui;
import android.graphics.Bitmap;
/** MIDP Canvas 스텁(안드로이드). 176x(208+16) 비트맵이 곧 LCD. */
public abstract class Canvas extends Displayable {
    public static final int W = 176, H = 208;
    public static final Bitmap lcd = Bitmap.createBitmap(W, H + 16, Bitmap.Config.ARGB_8888);
    public int getWidth() { return W; }
    public int getHeight() { return H; }
    protected abstract void paint(Graphics g);
    public void keyPressed(int k) {}
    public void keyReleased(int k) {}
    public void repaint() {}
    public void serviceRepaints() {}
}
