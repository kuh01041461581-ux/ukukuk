package javax.microedition.lcdui;
import javax.microedition.midlet.MIDlet;
public class Display {
    static Display inst = new Display();
    public static Canvas current;
    public static Display getDisplay(MIDlet m) { return inst; }
    public void setCurrent(Displayable d) {
        current = (Canvas) d;
        try {
            java.lang.reflect.Method m = Canvas.class.getDeclaredMethod("paint", Graphics.class);
            m.setAccessible(true);
            m.invoke(d, new Graphics(Canvas.lcd));
        } catch (Exception e) { throw new RuntimeException(e); }
    }
}
