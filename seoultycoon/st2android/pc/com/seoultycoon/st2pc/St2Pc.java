package com.seoultycoon.st2pc;

import com.seoultycoon.st2.Host;
import com.seoultycoon.st2.Platform;
import com.seoultycoon.st2.Surf;
import java.awt.*;
import java.awt.event.*;
import java.awt.image.BufferedImage;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.HashMap;
import java.util.Map;
import javax.imageio.ImageIO;
import javax.sound.midi.MidiSystem;
import javax.sound.midi.Sequence;
import javax.sound.midi.Sequencer;
import javax.swing.*;

/** 익산타이쿤 2 PC 판: 마우스 클릭(+키보드)으로 조작하는 Swing 창. 게임 엔진은 안드로이드와 같은 ThumbVm/Host. */
public class St2Pc {
    static final int UP = 50, DOWN = 56, LEFT = 52, RIGHT = 54, OK = 53, CLR = 48, STAR = 42, HASH = 35;
    static final double[] SPEEDS = {1.0, 1.5, 2.0, 3.0};

    // ---------------- 플랫폼 ----------------
    static class PcPlatform implements Platform {
        final Path dir; final Font font; final Map<Integer, Sequencer> clips = new HashMap<>(); volatile int vol = 100;
        PcPlatform(Path dir, Font font) { this.dir = dir; this.font = font; }
        static byte[] res(String p) { try (InputStream in = St2Pc.class.getResourceAsStream(p)) { if (in == null) return null; ByteArrayOutputStream o = new ByteArrayOutputStream(); byte[] b = new byte[8192]; int n; while ((n = in.read(b)) > 0) o.write(b, 0, n); return o.toByteArray(); } catch (IOException e) { return null; } }
        public byte[] resource(String n) { byte[] b = res("/st2mod/" + n); return b != null ? b : res("/st2/" + n); }
        public int[] decodePng(byte[] d, int[] o) { try { BufferedImage im = ImageIO.read(new ByteArrayInputStream(d)); if (im == null) return null; o[0] = im.getWidth(); o[1] = im.getHeight(); return im.getRGB(0, 0, o[0], o[1], null, 0, o[0]); } catch (IOException e) { return null; } }
        public byte[] renderText(String s, int[] dims) {
            BufferedImage m = new BufferedImage(1, 1, BufferedImage.TYPE_INT_ARGB); Graphics2D g = m.createGraphics(); g.setFont(font); FontMetrics fm = g.getFontMetrics(); int w = fm.stringWidth(s) + 1, h = 14; g.dispose();
            BufferedImage im = new BufferedImage(Math.max(w, 1), h, BufferedImage.TYPE_INT_ARGB); g = im.createGraphics(); g.setFont(font); g.setColor(Color.WHITE); g.drawString(s, 0, 11); g.dispose();
            byte[] r = new byte[w * h]; for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) r[y * w + x] = (byte) (im.getRGB(x, y) >>> 24 >= 128 ? 255 : 0);
            dims[0] = w; dims[1] = h; return r;
        }
        public byte[] readFile(String n) {
            try { Path p = dir.resolve(n); if (Files.exists(p)) return Files.readAllBytes(p); } catch (IOException e) { }
            return n.equals("data1.pak") ? resource(n) : null;           // 건물·지형 그림 묶음은 처음엔 모드(익산) → 원본 순
        }
        public void writeFile(String n, byte[] d) { try { Files.createDirectories(dir); Files.write(dir.resolve(n), d); } catch (IOException e) { } }
        public boolean fileExists(String n) { if (Files.exists(dir.resolve(n))) return true; return n.equals("data1.pak") && resource(n) != null; }
        public void log(String s) { }
        public synchronized void playClip(int id, byte[] midi, boolean loop) {
            stopClip(id);
            try { Sequencer sq = MidiSystem.getSequencer(); sq.open(); sq.setSequence(MidiSystem.getSequence(new ByteArrayInputStream(midi))); sq.setLoopCount(loop ? Sequencer.LOOP_CONTINUOUSLY : 0); sq.start(); clips.put(id, sq); }
            catch (Throwable t) { }
        }
        public synchronized void stopClip(int id) { Sequencer s = clips.remove(id); if (s != null) try { s.stop(); s.close(); } catch (Throwable t) { } }
        public synchronized void setClipVolume(int v) { vol = Math.max(0, Math.min(100, v * 5)); }
    }

    // ---------------- 화면 ----------------
    static class GamePanel extends JPanel {
        final BufferedImage lcd = new BufferedImage(240, 296, BufferedImage.TYPE_INT_RGB); volatile Host host; volatile String err;
        int ox, oy; double sc = 3;
        GamePanel() { setBackground(new Color(10, 10, 20)); setFocusable(true); setPreferredSize(new Dimension(240 * 3, 296 * 3)); }
        @Override protected void paintComponent(Graphics g0) {
            super.paintComponent(g0); Graphics2D g = (Graphics2D) g0;
            sc = Math.max(1.0, Math.min(getWidth() / 240.0, getHeight() / 296.0)); sc = Math.floor(sc * 4) / 4;      // 1/4 배율 단위(도트가 덜 번짐)
            int w = (int) (240 * sc), h = (int) (296 * sc); ox = (getWidth() - w) / 2; oy = (getHeight() - h) / 2;
            g.setRenderingHint(RenderingHints.KEY_INTERPOLATION, RenderingHints.VALUE_INTERPOLATION_NEAREST_NEIGHBOR);
            synchronized (lcd) { g.drawImage(lcd, ox, oy, w, h, null); }
            if (err != null) { g.setColor(new Color(0, 0, 0, 200)); g.fillRect(0, 0, getWidth(), 120); g.setColor(Color.WHITE); int y = 18; for (String ln : err.split("\n")) { g.drawString(ln.length() > 120 ? ln.substring(0, 120) : ln, 8, y); y += 14; if (y > 110) break; } }
        }
        int gx(int mx) { return (int) ((mx - ox) / sc); } int gy(int my) { return (int) ((my - oy) / sc); }
    }

    static Host host; static GamePanel panel; static PcPlatform pf; static volatile boolean running = true; static volatile double speed = 2.0;
    static void key(int code, boolean down) { Host h = host; if (h != null) h.postKey(down ? 1 : 2, code); }
    static void tap(int code) { key(code, true); new javax.swing.Timer(60, e -> key(code, false)) {{ setRepeats(false); start(); }}; }
    static void seq(int[] s) { for (int i = 0; i < s.length; i++) { final int c = s[i]; final int t = 110 * i; Timer a = new Timer(t + 1, e -> key(c, true)); a.setRepeats(false); a.start(); Timer b = new Timer(t + 55, e -> key(c, false)); b.setRepeats(false); b.start(); } }

    static int mapKey(KeyEvent e) {
        switch (e.getKeyCode()) {
            case KeyEvent.VK_UP: case KeyEvent.VK_W: return UP; case KeyEvent.VK_DOWN: case KeyEvent.VK_S: return DOWN;
            case KeyEvent.VK_LEFT: case KeyEvent.VK_A: return LEFT; case KeyEvent.VK_RIGHT: case KeyEvent.VK_D: return RIGHT;
            case KeyEvent.VK_ENTER: case KeyEvent.VK_SPACE: return OK;
            case KeyEvent.VK_ESCAPE: case KeyEvent.VK_BACK_SPACE: return CLR;
            case KeyEvent.VK_MULTIPLY: return STAR; case KeyEvent.VK_NUMBER_SIGN: return HASH;
        }
        char c = e.getKeyChar();
        if (c >= '0' && c <= '9') return c; if (c == '*') return STAR; if (c == '#') return HASH;
        int k = e.getKeyCode(); if (k >= KeyEvent.VK_NUMPAD0 && k <= KeyEvent.VK_NUMPAD9) return '0' + (k - KeyEvent.VK_NUMPAD0);
        return 0;
    }

    static JButton pad(String label, final int code, String tip) {
        JButton b = new JButton(label); b.setFocusable(false); b.setFont(b.getFont().deriveFont(Font.BOLD, 15f)); b.setMargin(new Insets(2, 2, 2, 2)); b.setToolTipText(tip);
        b.addMouseListener(new MouseAdapter() {                                  // 누르는 동안 키가 눌린 상태(길게 누르면 계속 이동)
            @Override public void mousePressed(MouseEvent e) { key(code, true); }
            @Override public void mouseReleased(MouseEvent e) { key(code, false); }
        });
        return b;
    }

    static JPanel controls() {
        JPanel p = new JPanel(new GridBagLayout()); p.setBorder(BorderFactory.createEmptyBorder(8, 8, 8, 8)); p.setBackground(new Color(238, 240, 236));
        GridBagConstraints c = new GridBagConstraints(); c.fill = GridBagConstraints.BOTH; c.weightx = 1; c.weighty = 1; c.insets = new Insets(3, 3, 3, 3);
        String[][] rows = {{"", "▲", ""}, {"◀", "확인", "▶"}, {"", "▼", ""}};
        int[][] codes = {{0, UP, 0}, {LEFT, OK, RIGHT}, {0, DOWN, 0}};
        for (int r = 0; r < 3; r++) for (int k = 0; k < 3; k++) { c.gridx = k; c.gridy = r; if (codes[r][k] == 0) p.add(new JLabel(), c); else p.add(pad(rows[r][k], codes[r][k], r == 1 && k == 1 ? "확인(Enter/Space)" : "방향키"), c); }
        c.gridx = 0; c.gridy = 3; p.add(pad("정보 *", STAR, "* 정보 (숫자패드 *)"), c);
        c.gridx = 1; c.gridy = 3; p.add(pad("취소", CLR, "취소(Esc/우클릭)"), c);
        c.gridx = 2; c.gridy = 3; p.add(pad("지도 #", HASH, "# 지도"), c);
        String[] nums = {"1", "2", "3", "4", "5", "6", "7", "8", "9", "", "0", ""};
        for (int i = 0; i < 12; i++) { if (nums[i].isEmpty()) continue; c.gridx = i % 3; c.gridy = 4 + i / 3; p.add(pad(nums[i], nums[i].charAt(0), "숫자 " + nums[i]), c); }
        c.gridx = 0; c.gridy = 8; c.gridwidth = 3; c.weighty = 0;
        JLabel tip = new JLabel("<html><div style='width:170px;color:#444'>왼쪽 클릭=확인 · 오른쪽 클릭=취소<br>휠=위/아래 · 시청 메뉴는 항목 직접 클릭<br>키보드: 방향키/WASD, Enter, Esc, 숫자</div></html>"); tip.setFont(tip.getFont().deriveFont(11f)); p.add(tip, c);
        return p;
    }

    static void startGame(String name) {
        Thread t = new Thread(null, () -> {
            try {
                byte[] img = pf.resource("client.bin1116"); Host h = new Host(pf, img); h.defaultName = name; h.timeScale = speed; h.boot(); h.startApp(); host = h; panel.host = h;
                long last = System.nanoTime(); int[] px = new int[240 * 296];
                while (running) {
                    long t0 = System.nanoTime(); int dt = (int) Math.min(100, (t0 - last) / 1000000); last = t0; h.timeScale = speed;
                    if (h.tick(Math.max(8, dt))) { Surf s = h.screen; for (int i = 0; i < px.length; i++) px[i] = s.px[i] | 0xFF000000; synchronized (panel.lcd) { panel.lcd.setRGB(0, 0, 240, 296, px, 0, 240); } panel.repaint(); }
                    long el = (System.nanoTime() - t0) / 1000000; if (el < 16) Thread.sleep(16 - el);
                }
            } catch (Throwable ex) { StringWriter sw = new StringWriter(); ex.printStackTrace(new PrintWriter(sw)); panel.err = sw.toString(); panel.repaint(); try { pf.writeFile("error.txt", sw.toString().getBytes(StandardCharsets.UTF_8)); } catch (Exception e2) { } }
        }, "st2-game", 256L * 1024 * 1024);
        t.setDaemon(true); t.start();
    }

    public static void main(String[] a) throws Exception {
        Font font = Font.createFont(Font.TRUETYPE_FONT, new ByteArrayInputStream(PcPlatform.res("/galmuri11.ttf"))).deriveFont(11f);
        Path dir = Paths.get(System.getProperty("user.home"), ".iksan-tycoon"); pf = new PcPlatform(dir, font);
        String name = null; try { Path np = dir.resolve("name.txt"); if (Files.exists(np)) name = new String(Files.readAllBytes(np), StandardCharsets.UTF_8).trim(); } catch (IOException e) { }
        final String auto = System.getProperty("st2.autoshot");            // 시험용: "경로,대기ms,키열(라운드ms:키;...)"
        if (name == null || name.isEmpty()) {
            String s = auto != null ? "시장" : JOptionPane.showInputDialog(null, "시장님 존함 (게임 시작 시 사용, 5자까지)", "익산타이쿤 2", JOptionPane.QUESTION_MESSAGE);
            name = s == null || s.trim().isEmpty() ? "시장" : s.trim(); if (name.length() > 5) name = name.substring(0, 5);
            pf.writeFile("name.txt", name.getBytes(StandardCharsets.UTF_8));
        }
        final String nm = name;
        SwingUtilities.invokeAndWait(() -> {
            panel = new GamePanel();
            JFrame f = new JFrame("익산타이쿤 2"); f.setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
            JMenuBar mb = new JMenuBar(); JMenu m = new JMenu("설정"); ButtonGroup g = new ButtonGroup();
            String[] sn = {"게임 속도 1배", "게임 속도 1.5배", "게임 속도 2배 (기본)", "게임 속도 3배"};
            for (int i = 0; i < 4; i++) { final int k = i; JRadioButtonMenuItem it = new JRadioButtonMenuItem(sn[i], i == 2); g.add(it); m.add(it); it.addActionListener(e -> speed = SPEEDS[k]); }
            m.addSeparator(); JMenuItem rn = new JMenuItem("시장님 이름 바꾸기 (다음 실행부터)"); rn.addActionListener(e -> { String s = JOptionPane.showInputDialog(f, "새 이름 (5자까지)"); if (s != null && !s.trim().isEmpty()) pf.writeFile("name.txt", (s.trim().length() > 5 ? s.trim().substring(0, 5) : s.trim()).getBytes(StandardCharsets.UTF_8)); }); m.add(rn);
            JMenuItem fs = new JMenuItem("전체 화면 전환 (F11)"); fs.addActionListener(e -> toggleFull(f)); m.add(fs);
            mb.add(m); f.setJMenuBar(mb);
            JPanel ctl = controls(); ctl.setPreferredSize(new Dimension(210, 0));
            f.add(panel, BorderLayout.CENTER); f.add(ctl, BorderLayout.EAST);
            Dimension sd = Toolkit.getDefaultToolkit().getScreenSize(); int s0 = Math.max(2, Math.min(4, (sd.height - 140) / 296)); panel.setPreferredSize(new Dimension(240 * s0, 296 * s0));
            f.pack(); f.setLocationRelativeTo(null);
            KeyAdapter ka = new KeyAdapter() {
                final java.util.Set<Integer> down = new java.util.HashSet<>();
                @Override public void keyPressed(KeyEvent e) { if (e.getKeyCode() == KeyEvent.VK_F11) { toggleFull(f); return; } int k = mapKey(e); if (k != 0 && down.add(e.getKeyCode())) key(k, true); }
                @Override public void keyReleased(KeyEvent e) { int k = mapKey(e); if (k != 0 && down.remove(e.getKeyCode())) key(k, false); }
            };
            f.addKeyListener(ka); panel.addKeyListener(ka);
            panel.addMouseListener(new MouseAdapter() {
                @Override public void mousePressed(MouseEvent e) {
                    panel.requestFocusInWindow(); Host h = host; if (h == null) return;
                    if (SwingUtilities.isRightMouseButton(e)) { tap(CLR); return; }
                    if (SwingUtilities.isMiddleMouseButton(e)) { tap(HASH); return; }
                    int x = panel.gx(e.getX()), y = panel.gy(e.getY()); if (x < 0 || y < 0 || x >= 240 || y >= 296) return;
                    int[] s = h.tapKeys(x, y); if (s != null) seq(s); else tap(OK);          // 시청 팝업 항목은 바로 선택, 나머지는 확인
                }
            });
            panel.addMouseWheelListener(e -> { if (e.getWheelRotation() < 0) tap(UP); else if (e.getWheelRotation() > 0) tap(DOWN); });
            f.setVisible(true); panel.requestFocusInWindow();
        });
        startGame(nm);
        if (auto != null) autoshot(auto);
    }

    static java.awt.Window fullWin;
    static void toggleFull(JFrame f) {
        GraphicsDevice gd = GraphicsEnvironment.getLocalGraphicsEnvironment().getDefaultScreenDevice();
        if (gd.getFullScreenWindow() == f) { gd.setFullScreenWindow(null); } else { gd.setFullScreenWindow(f); }
        f.revalidate();
    }

    /** 시험용: 지정 시간 뒤 창 내용을 PNG 로 저장하고 종료. 형식 "out.png,대기ms[,t:키;t:키]" */
    static void autoshot(String spec) throws Exception {
        String[] p = spec.split(","); long wait = Long.parseLong(p[1]);
        if (p.length > 2) for (String k : p[2].split(";")) { String[] q = k.split(":"); long at = Long.parseLong(q[0]); int code = Integer.parseInt(q[1]); Thread.sleep(Math.max(0, at - (System.currentTimeMillis() % 1)));
            tap(code); }
        Thread.sleep(wait);
        SwingUtilities.invokeAndWait(() -> { try { Container c = SwingUtilities.getWindowAncestor(panel); BufferedImage im = new BufferedImage(c.getWidth(), c.getHeight(), BufferedImage.TYPE_INT_RGB); c.paint(im.getGraphics()); ImageIO.write(im, "png", new File(p[0])); } catch (Exception e) { e.printStackTrace(); } });
        System.exit(0);
    }
}
