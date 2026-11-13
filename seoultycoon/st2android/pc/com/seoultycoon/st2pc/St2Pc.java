package com.seoultycoon.st2pc;

import com.seoultycoon.st2.Host;
import com.seoultycoon.st2.Platform;
import com.seoultycoon.st2.PixelScale;
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
        Font hdFont; { try { hdFont = Font.createFont(Font.TRUETYPE_FONT, new ByteArrayInputStream(res("/pretendard.ttf"))).deriveFont(11f * com.seoultycoon.st2.Surf.K); } catch (Exception e) { hdFont = null; } }
        public byte[] renderTextHd(String s, int targetW, int[] dims) {
                if (hdFont == null || targetW <= 0 || s.isEmpty()) return null;
                int K = com.seoultycoon.st2.Surf.K, h = 14 * K, w = targetW + K;
                BufferedImage im = new BufferedImage(w, h, BufferedImage.TYPE_INT_ARGB); Graphics2D g = im.createGraphics();
                g.setRenderingHint(RenderingHints.KEY_TEXT_ANTIALIASING, RenderingHints.VALUE_TEXT_ANTIALIAS_ON); g.setRenderingHint(RenderingHints.KEY_FRACTIONALMETRICS, RenderingHints.VALUE_FRACTIONALMETRICS_ON);
                g.setFont(hdFont); int sw = g.getFontMetrics().stringWidth(s); if (sw <= 0) { g.dispose(); return null; }
                g.scale(targetW / (double) sw, 1.0); g.setColor(Color.WHITE); g.drawString(s, 0, 11 * K - 1); g.dispose();
                byte[] r = new byte[w * h]; for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) r[y * w + x] = (byte) (im.getRGB(x, y) >>> 24); dims[0] = w; dims[1] = h; return r; }
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
        public boolean fileExists(String n) { if (System.getProperty("st2.nopak") != null && n.equals("data1.pak")) return false; if (Files.exists(dir.resolve(n))) return true; return n.equals("data1.pak") && resource(n) != null; }
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
        final BufferedImage lcd = new BufferedImage(960, 1184, BufferedImage.TYPE_INT_RGB); volatile boolean hq = false; volatile Host host; volatile String err;
        int ox, oy; double sc = 3;
        GamePanel() { setBackground(new Color(10, 10, 20)); setFocusable(true); setPreferredSize(new Dimension(240 * 3, 296 * 3)); }
        @Override protected void paintComponent(Graphics g0) {
            super.paintComponent(g0); Graphics2D g = (Graphics2D) g0;
            sc = Math.max(1.0, Math.min(getWidth() / 240.0, getHeight() / 296.0)); if (!hq) sc = Math.max(1, Math.floor(sc));      // 1/4 배율 단위(도트가 덜 번짐)
            int w = (int) (240 * sc), h = (int) (296 * sc); ox = (getWidth() - w) / 2; oy = (getHeight() - h) / 2;
            g.setRenderingHint(RenderingHints.KEY_INTERPOLATION, hq ? RenderingHints.VALUE_INTERPOLATION_BICUBIC : RenderingHints.VALUE_INTERPOLATION_NEAREST_NEIGHBOR); g.setRenderingHint(RenderingHints.KEY_RENDERING, RenderingHints.VALUE_RENDER_QUALITY);
            synchronized (lcd) { g.drawImage(lcd, ox, oy, w, h, null); }
            if (err != null) { g.setColor(new Color(0, 0, 0, 200)); g.fillRect(0, 0, getWidth(), 120); g.setColor(Color.WHITE); int y = 18; for (String ln : err.split("\n")) { g.drawString(ln.length() > 120 ? ln.substring(0, 120) : ln, 8, y); y += 14; if (y > 110) break; } }
        }
        int gx(int mx) { return (int) ((mx - ox) / sc); } int gy(int my) { return (int) ((my - oy) / sc); }
    }

    static volatile boolean dragInvert = false;
    static final String HELP = "마우스\n  왼쪽 클릭 = 확인 · 번호 선택지(1.예 2.아니오 …)는 그 글자를 클릭\n  시청 팝업은 항목 클릭 · 오른쪽 클릭 = 취소 · 가운데 클릭 = 지도\n  끌기 = 지도 이동(설정에서 방향 반대로 가능) · 휠 = 위/아래\n\n키보드(엑스박스 패드 배치)\n  방향: 방향키 / WASD · A(확인): Enter / Space / Z · B(취소): Esc / X / Backspace\n  X(정보 *): C · Y(지도 #): V / Tab · 숫자 선택: 0~9 · 전체 화면: F11";
    static Host host; static GamePanel panel; static PcPlatform pf; static volatile boolean running = true; static volatile double speed = 2.0;
    static void key(int code, boolean down) { Host h = host; if (h != null) h.postKey(down ? 1 : 2, code); }
    static void tap(int code) { key(code, true); new javax.swing.Timer(60, e -> key(code, false)) {{ setRepeats(false); start(); }}; }
    static void seq(int[] s) { for (int i = 0; i < s.length; i++) { final int c = s[i]; final int t = 110 * i; Timer a = new Timer(t + 1, e -> key(c, true)); a.setRepeats(false); a.start(); Timer b = new Timer(t + 55, e -> key(c, false)); b.setRepeats(false); b.start(); } }

    /** 엑스박스 패드 배치를 키보드로: 방향 = 방향키/WASD(십자·왼쪽 스틱), A = Enter/Space/Z(확인), B = Esc/X/Backspace(취소), X = C(정보 *), Y = V 또는 Tab(지도 #). 숫자키는 그대로 번호 선택. */
    static int mapKey(KeyEvent e) {
        switch (e.getKeyCode()) {
            case KeyEvent.VK_UP: case KeyEvent.VK_W: return UP; case KeyEvent.VK_DOWN: case KeyEvent.VK_S: return DOWN;
            case KeyEvent.VK_LEFT: case KeyEvent.VK_A: return LEFT; case KeyEvent.VK_RIGHT: case KeyEvent.VK_D: return RIGHT;
            case KeyEvent.VK_ENTER: case KeyEvent.VK_SPACE: case KeyEvent.VK_Z: return OK;
            case KeyEvent.VK_ESCAPE: case KeyEvent.VK_BACK_SPACE: case KeyEvent.VK_X: return CLR;
            case KeyEvent.VK_C: case KeyEvent.VK_MULTIPLY: return STAR;
            case KeyEvent.VK_V: case KeyEvent.VK_TAB: case KeyEvent.VK_NUMBER_SIGN: return HASH;
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
        JLabel tip = new JLabel("<html><div style='width:170px;color:#444'>왼쪽 클릭=확인 · 오른쪽 클릭=취소<br>휠=위/아래 · 시청 메뉴는 항목 직접 클릭<br>패드식 키: 방향 WASD, A=Enter/Z, B=Esc/X</div></html>"); tip.setFont(tip.getFont().deriveFont(12f)); p.add(tip, c);
        return p;
    }

    static void startGame(String name) {
        Thread t = new Thread(null, () -> {
            try {
                byte[] img = pf.resource("client.bin1116"); Host h = new Host(pf, img); h.defaultName = name; h.timeScale = speed; if (pf.fileExists("skiptut.txt")) { h.startAutoTutorial(); new java.io.File(System.getProperty("user.home"), ".iksan-tycoon/skiptut.txt").delete(); } h.boot(); h.startApp(); host = h; panel.host = h;
                long last = System.nanoTime(); int[] px = new int[240 * 296];
                while (running) {
                    long t0 = System.nanoTime(); int dt = (int) Math.min(100, (t0 - last) / 1000000); last = t0; h.timeScale = speed;
                    if (h.tick(Math.max(8, dt))) { Surf s = h.screen; int[] hd = s.hd(); int[] p2 = new int[hd.length]; for (int i = 0; i < p2.length; i++) p2[i] = hd[i] | 0xFF000000; int[] big = (panel.hq ? PixelScale.epx2(p2, 480, 592, null) : PixelScale.nn(p2, 480, 592, 2)); synchronized (panel.lcd) { panel.lcd.setRGB(0, 0, 960, 1184, big, 0, 960); } panel.repaint(); }
                    long el = (System.nanoTime() - t0) / 1000000; if (el < 16) Thread.sleep(16 - el);
                }
            } catch (Throwable ex) { StringWriter sw = new StringWriter(); ex.printStackTrace(new PrintWriter(sw)); panel.err = sw.toString(); panel.repaint(); try { pf.writeFile("error.txt", sw.toString().getBytes(StandardCharsets.UTF_8)); } catch (Exception e2) { } }
        }, "st2-game", 256L * 1024 * 1024);
        t.setDaemon(true); t.start();
    }

    public static void main(String[] a) throws Exception {
        Font font = Font.createFont(Font.TRUETYPE_FONT, new ByteArrayInputStream(PcPlatform.res("/galmuri11.ttf"))).deriveFont(12f);
        Path dir = Paths.get(System.getProperty("user.home"), ".iksan-tycoon"); pf = new PcPlatform(dir, font);
        String name = null; try { Path np = dir.resolve("name.txt"); if (Files.exists(np)) name = new String(Files.readAllBytes(np), StandardCharsets.UTF_8).trim(); } catch (IOException e) { }
        final String auto = System.getProperty("st2.script");            // 시험용 스크립트
        if (name == null || name.isEmpty()) {
            String s = auto != null ? "시장" : JOptionPane.showInputDialog(null, "시장님 존함 (게임 시작 시 사용, 5자까지)", "익산타이쿤", JOptionPane.QUESTION_MESSAGE);
            name = s == null || s.trim().isEmpty() ? "시장" : s.trim(); if (name.length() > 5) name = name.substring(0, 5);
            pf.writeFile("name.txt", name.getBytes(StandardCharsets.UTF_8));
        }
        final String nm = name;
        SwingUtilities.invokeAndWait(() -> {
            panel = new GamePanel();
            JFrame f = new JFrame("익산타이쿤"); f.setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
            JMenuBar mb = new JMenuBar(); JMenu m = new JMenu("설정"); ButtonGroup g = new ButtonGroup();
            String[] sn = {"게임 속도 1배", "게임 속도 1.5배", "게임 속도 2배 (기본)", "게임 속도 3배"};
            for (int i = 0; i < 4; i++) { final int k = i; JRadioButtonMenuItem it = new JRadioButtonMenuItem(sn[i], i == 2); g.add(it); m.add(it); it.addActionListener(e -> speed = SPEEDS[k]); }
            m.addSeparator(); JMenuItem rn = new JMenuItem("시장님 이름 바꾸기 (다음 실행부터)"); rn.addActionListener(e -> { String s = JOptionPane.showInputDialog(f, "새 이름 (5자까지)"); if (s != null && !s.trim().isEmpty()) pf.writeFile("name.txt", (s.trim().length() > 5 ? s.trim().substring(0, 5) : s.trim()).getBytes(StandardCharsets.UTF_8)); }); m.add(rn);
            JMenuItem sk = new JMenuItem("다음 실행 때 튜토리얼 자동 넘김"); sk.addActionListener(e -> { pf.writeFile("skiptut.txt", new byte[]{1}); JOptionPane.showMessageDialog(f, "다음에 게임을 켤 때 튜토리얼을 자동으로 넘깁니다."); }); m.add(sk);
            JMenuItem fs = new JMenuItem("전체 화면 전환 (F11)"); fs.addActionListener(e -> toggleFull(f)); m.add(fs);
            mb.add(m); f.setJMenuBar(mb);
            JPanel ctl = controls(); ctl.setPreferredSize(new Dimension(210, 0)); ctl.setVisible(false);
            f.add(panel, BorderLayout.CENTER); f.add(ctl, BorderLayout.EAST);
            JCheckBoxMenuItem vp = new JCheckBoxMenuItem("가상 키패드 보기 (선택 사항)"); vp.addActionListener(e -> { ctl.setVisible(vp.isSelected()); f.pack(); }); m.add(vp);
            JCheckBoxMenuItem hqm = new JCheckBoxMenuItem("화질 보정(도트 확대 + 부드럽게, 글씨가 번질 수 있음)", false); hqm.addActionListener(e -> { panel.hq = hqm.isSelected(); panel.repaint(); }); m.add(hqm);
            JCheckBoxMenuItem inv = new JCheckBoxMenuItem("지도 끌기 방향 반대로"); inv.addActionListener(e -> dragInvert = inv.isSelected()); m.add(inv);
            JMenuItem help = new JMenuItem("조작 안내"); help.addActionListener(e -> JOptionPane.showMessageDialog(f, HELP, "조작 안내", JOptionPane.INFORMATION_MESSAGE)); m.add(help);
            Dimension sd = Toolkit.getDefaultToolkit().getScreenSize(); int s0 = Math.max(2, Math.min(4, (sd.height - 140) / 296)); panel.setPreferredSize(new Dimension(240 * s0, 296 * s0));
            f.pack(); f.setLocationRelativeTo(null);
            KeyAdapter ka = new KeyAdapter() {
                final java.util.Set<Integer> down = new java.util.HashSet<>();
                @Override public void keyPressed(KeyEvent e) { if (e.getKeyCode() == KeyEvent.VK_F11) { toggleFull(f); return; } int k = mapKey(e); if (k != 0 && down.add(e.getKeyCode())) key(k, true); }
                @Override public void keyReleased(KeyEvent e) { int k = mapKey(e); if (k != 0 && down.remove(e.getKeyCode())) key(k, false); }
            };
            f.addKeyListener(ka); panel.addKeyListener(ka);
            MouseAdapter ma = new MouseAdapter() {
                int px0, py0, ax, ay; boolean down, dragged;
                @Override public void mousePressed(MouseEvent e) {
                    panel.requestFocusInWindow(); Host h = host; if (h == null) return;
                    if (SwingUtilities.isRightMouseButton(e)) { tap(CLR); return; }
                    if (SwingUtilities.isMiddleMouseButton(e)) { tap(HASH); return; }
                    down = true; dragged = false; px0 = ax = panel.gx(e.getX()); py0 = ay = panel.gy(e.getY());
                }
                @Override public void mouseDragged(MouseEvent e) {
                    if (!down) return; int x = panel.gx(e.getX()), y = panel.gy(e.getY()); int dx = x - ax, dy = y - ay, STEP = 20;
                    while (Math.abs(dx) >= STEP || Math.abs(dy) >= STEP) {                         // 끌기 = 지도 잡아 끌기: 끈 방향으로 화면이 따라오게 반대 방향키
                        int code; if (Math.abs(dx) >= Math.abs(dy)) { code = dx > 0 ? LEFT : RIGHT; ax += dx > 0 ? STEP : -STEP; } else { code = dy > 0 ? UP : DOWN; ay += dy > 0 ? STEP : -STEP; }
                        if (dragInvert) code = code == LEFT ? RIGHT : code == RIGHT ? LEFT : code == UP ? DOWN : UP;
                        tap(code); dragged = true; dx = x - ax; dy = y - ay;
                    }
                }
                @Override public void mouseReleased(MouseEvent e) {
                    if (!down) return; down = false; Host h = host; if (h == null || dragged) return;
                    int x = panel.gx(e.getX()), y = panel.gy(e.getY()); if (x < 0 || y < 0 || x >= 240 || y >= 296) return;
                    int dg = h.hitChoice(x, y); if (dg > 0) { tap('0' + dg); return; }                                                  // 번호 선택지: 그 번호 키
                    int[] sq = h.tapKeys(x, y); if (sq != null) seq(sq); else tap(OK);                                                        // 시청 팝업 항목 직접 선택, 나머지는 확인
                }
            };
            panel.addMouseListener(ma); panel.addMouseMotionListener(ma);
            panel.addMouseWheelListener(e -> { if (e.getWheelRotation() < 0) tap(UP); else if (e.getWheelRotation() > 0) tap(DOWN); });
            f.setVisible(true); panel.requestFocusInWindow();
        });
        startGame(nm);
        if (auto != null) script(auto);
    }

    static java.awt.Window fullWin;
    static void toggleFull(JFrame f) {
        GraphicsDevice gd = GraphicsEnvironment.getLocalGraphicsEnvironment().getDefaultScreenDevice();
        if (gd.getFullScreenWindow() == f) { gd.setFullScreenWindow(null); } else { gd.setFullScreenWindow(f); }
        f.revalidate();
    }

    /** 시험용 스크립트: "wait:ms;tap:코드;click:gx:gy;drag:x1:y1:x2:y2;rclick;shot:경로;dump" 를 순서대로 실행하고 종료. */
    static void script(String spec) throws Exception {
        for (String it : spec.split(";")) {
            String[] q = it.split(":");
            switch (q[0]) {
                case "wait": Thread.sleep(Long.parseLong(q[1])); break;
                case "tap": tap(Integer.parseInt(q[1])); break;
                case "click": case "drag": {
                    final int gx = Integer.parseInt(q[1]), gy = Integer.parseInt(q[2]); final String kind = q[0]; final int gx2 = q.length > 3 ? Integer.parseInt(q[3]) : gx, gy2 = q.length > 4 ? Integer.parseInt(q[4]) : gy;
                    SwingUtilities.invokeAndWait(() -> {
                        int sx = panel.ox + (int) (gx * panel.sc), sy = panel.oy + (int) (gy * panel.sc), ex = panel.ox + (int) (gx2 * panel.sc), ey = panel.oy + (int) (gy2 * panel.sc); long t = System.currentTimeMillis();
                        panel.dispatchEvent(new MouseEvent(panel, MouseEvent.MOUSE_PRESSED, t, 0, sx, sy, 1, false, MouseEvent.BUTTON1));
                        if (kind.equals("drag")) for (int i = 1; i <= 10; i++) panel.dispatchEvent(new MouseEvent(panel, MouseEvent.MOUSE_DRAGGED, t, 0, sx + (ex - sx) * i / 10, sy + (ey - sy) * i / 10, 1, false, MouseEvent.NOBUTTON));
                        panel.dispatchEvent(new MouseEvent(panel, MouseEvent.MOUSE_RELEASED, t, 0, ex, ey, 1, false, MouseEvent.BUTTON1));
                    }); break; }
                case "rclick": SwingUtilities.invokeAndWait(() -> panel.dispatchEvent(new MouseEvent(panel, MouseEvent.MOUSE_PRESSED, System.currentTimeMillis(), 0, panel.ox + 10, panel.oy + 10, 1, false, MouseEvent.BUTTON3))); break;
                case "dump": { Host h = host; StringBuilder sb = new StringBuilder("ROWS"); synchronized (h) { for (int[] r : h.clickRows) sb.append(" [").append(r[0]).append(':').append(r[1]).append(',').append(r[2]).append('-').append(r[3]).append(',').append(r[4]).append(']'); } System.out.println(sb); break; }
                case "shot": { final String path = q[1]; SwingUtilities.invokeAndWait(() -> { try { Container c = SwingUtilities.getWindowAncestor(panel); BufferedImage im = new BufferedImage(c.getWidth(), c.getHeight(), BufferedImage.TYPE_INT_RGB); c.paint(im.getGraphics()); ImageIO.write(im, "png", new File(path)); } catch (Exception e) { e.printStackTrace(); } }); break; }
            }
        }
        System.exit(0);
    }
}
