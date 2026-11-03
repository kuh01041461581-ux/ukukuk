import javax.microedition.lcdui.Display;
import java.awt.*;
import java.awt.event.*;
import javax.swing.*;

/** 서울타이쿤1 원본을 PC 창에서 직접 플레이. 방향키=2/4/6/8, Enter/Space=5, Z/X=왼/오른 소프트키, 숫자/*,# 그대로. 마우스 휠 없음. */
public class Player {
    static int map(KeyEvent e) {
        switch (e.getKeyCode()) {
            case KeyEvent.VK_UP: return '2';
            case KeyEvent.VK_LEFT: return '4';
            case KeyEvent.VK_RIGHT: return '6';
            case KeyEvent.VK_DOWN: return '8';
            case KeyEvent.VK_ENTER: case KeyEvent.VK_SPACE: return '5';
            case KeyEvent.VK_Z: return 194;   // 왼쪽 소프트키(추정)
            case KeyEvent.VK_X: return 195;   // 오른쪽 소프트키(추정)
        }
        char c = e.getKeyChar();
        return (c >= '0' && c <= '9' || c == '*' || c == '#') ? c : 0;
    }
    public static void main(String[] a) throws Exception {
        int scale = a.length > 0 ? Integer.parseInt(a[0]) : 3;
        Class<?> c = Class.forName("Seoul");
        javax.microedition.midlet.MIDlet m = (javax.microedition.midlet.MIDlet) c.getDeclaredConstructor().newInstance();
        JFrame f = new JFrame("서울타이쿤");
        JComponent view = new JComponent() {
            protected void paintComponent(java.awt.Graphics g) {
                java.awt.image.BufferedImage b = javax.microedition.lcdui.Canvas.lcd;
                g.drawImage(b, 0, 0, getWidth(), getHeight(), null);
            }
        };
        view.setPreferredSize(new Dimension(176 * scale, 224 * scale));
        f.add(view); f.pack(); f.setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE); f.setLocationRelativeTo(null);
        f.addKeyListener(new KeyAdapter() {
            public void keyPressed(KeyEvent e) { int k = map(e); if (k != 0 && Display.current != null) Display.current.keyPressed(k); }
            public void keyReleased(KeyEvent e) { int k = map(e); if (k != 0 && Display.current != null) Display.current.keyReleased(k); }
        });
        f.setVisible(true);
        java.lang.reflect.Method st = c.getDeclaredMethod("startApp"); st.setAccessible(true); st.invoke(m);
        new javax.swing.Timer(40, ev -> view.repaint()).start();
    }
}
