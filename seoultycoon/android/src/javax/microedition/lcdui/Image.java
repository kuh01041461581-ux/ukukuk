package javax.microedition.lcdui;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import java.io.InputStream;
public class Image {
    public Bitmap bm;
    public Image(Bitmap b) { bm = b; }
    public static Image createImage(int w, int h) { return new Image(Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888)); }
    public static Image createImage(String res) throws java.io.IOException {
        InputStream in = Image.class.getResourceAsStream("/" + res.replaceFirst("^/", ""));
        if (in == null) throw new java.io.IOException("no resource " + res);
        BitmapFactory.Options o = new BitmapFactory.Options();
        o.inScaled = false; o.inPreferredConfig = Bitmap.Config.ARGB_8888; o.inMutable = true;
        Bitmap b = BitmapFactory.decodeStream(in, null, o);
        in.close();
        if (b == null) throw new java.io.IOException("decode " + res);
        return new Image(b);
    }
    public int getWidth() { return bm.getWidth(); }
    public int getHeight() { return bm.getHeight(); }
    public Graphics getGraphics() { return new Graphics(bm); }
}
