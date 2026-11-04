import javax.microedition.lcdui.*;
import java.awt.image.BufferedImage;
import java.io.File;
import javax.imageio.ImageIO;

/** Runs the original Seoul Tycoon 1 MIDlet headlessly. Usage: Runner outDir [script]
 *  script = comma list: wait:ms | key:code | shot:name  */
public class Runner {
    static int shots = 0;
    static void shot(File dir, String name) throws Exception {
        BufferedImage b = Canvas.lcd;
        BufferedImage c = new BufferedImage(b.getWidth() * 2, b.getHeight() * 2, BufferedImage.TYPE_INT_RGB);
        c.getGraphics().drawImage(b, 0, 0, c.getWidth(), c.getHeight(), null);
        ImageIO.write(c, "png", new File(dir, name + ".png"));
    }
    public static void main(String[] a) throws Exception {
        System.setProperty("java.awt.headless", "true");
        File dir = new File(a[0]); dir.mkdirs();
        Class<?> c = Class.forName("Seoul");
        javax.microedition.midlet.MIDlet m = (javax.microedition.midlet.MIDlet) c.getDeclaredConstructor().newInstance();
        java.lang.reflect.Method st = c.getDeclaredMethod("startApp"); st.setAccessible(true); st.invoke(m);
        for (String s : (a.length > 1 ? a[1] : "wait:2000,shot:start").split(",")) {
            String[] p = s.split(":");
            switch (p[0]) {
                case "wait": Thread.sleep(Long.parseLong(p[1])); break;
                case "key": Display.current.keyPressed(Integer.parseInt(p[1])); Thread.sleep(80); Display.current.keyReleased(Integer.parseInt(p[1])); break;
                case "shot": shot(dir, p[1]); break;
                case "cls": { Class<?> k0 = Display.current.getClass(); java.lang.reflect.Field f = null; while (k0 != null && f == null) { try { f = k0.getDeclaredField("a"); } catch (NoSuchFieldException ex) { k0 = k0.getSuperclass(); } } f.setAccessible(true); Object sc = f.get(Display.current);
                    StringBuilder sb = new StringBuilder(sc.getClass().getName());
                    for (java.lang.reflect.Field g : sc.getClass().getDeclaredFields()) { g.setAccessible(true); if (g.getType() == int.class) sb.append(" ").append(g.getName()).append("=").append(g.get(sc)); }
                    System.out.println("SCREEN " + sb); break; }
            }
        }
        System.exit(0);
    }
}
