package javax.microedition.lcdui;
import java.awt.image.BufferedImage;
public class Image {
    public BufferedImage bi;
    public Image(BufferedImage b) { bi = b; }
    public static Image createImage(int w, int h) { return new Image(new BufferedImage(w, h, BufferedImage.TYPE_INT_ARGB)); }
    public static Image createImage(String res) throws java.io.IOException {
        java.io.InputStream in = Image.class.getResourceAsStream("/" + res.replaceFirst("^/", ""));
        if (in == null) throw new java.io.IOException("no resource " + res);
        BufferedImage b = javax.imageio.ImageIO.read(in);
        BufferedImage c = new BufferedImage(b.getWidth(), b.getHeight(), BufferedImage.TYPE_INT_ARGB);
        c.getGraphics().drawImage(b, 0, 0, null);
        return new Image(c);
    }
    public int getWidth() { return bi.getWidth(); }
    public int getHeight() { return bi.getHeight(); }
    public Graphics getGraphics() { return new Graphics(bi); }
}
