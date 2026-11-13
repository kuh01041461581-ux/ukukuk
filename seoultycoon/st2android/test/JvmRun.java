import com.seoultycoon.st2.*;
import java.awt.*; import java.awt.image.*; import java.io.*; import java.nio.file.*; import javax.imageio.ImageIO;

/** JVM 에서 ST2 부팅 테스트: java JvmRun <client.bin> <resdir> <outdir> <font.ttf> [rounds] [keys round:type:key,...] */
public class JvmRun {
    public static void main(String[] a) throws Exception {
        final Path res = Paths.get(a[1]), out = Paths.get(a[2]); Files.createDirectories(out.resolve("save"));
        final Font font = Font.createFont(Font.TRUETYPE_FONT, new File(a[3])).deriveFont(12f);
        Font hf0 = null; try { hf0 = Font.createFont(Font.TRUETYPE_FONT, new File(new File(a[3]).getParentFile(), "pretendard.ttf")).deriveFont(11f * com.seoultycoon.st2.Surf.K); } catch (Exception e) { } final Font hdFont = hf0;
        Platform pf = new Platform() {
            public byte[] resource(String n) { if (System.getenv("RESLOG") != null) System.out.println("RES " + n); String mod = System.getenv("MOD"); if (mod != null) try { return Files.readAllBytes(Paths.get(mod).resolve(n)); } catch (IOException e0) { } try { return Files.readAllBytes(res.resolve(n)); } catch (IOException e) { return null; } }
            public int[] decodePng(byte[] d, int[] o) { try { BufferedImage im = ImageIO.read(new ByteArrayInputStream(d)); if (im == null) return null; o[0] = im.getWidth(); o[1] = im.getHeight(); return im.getRGB(0, 0, o[0], o[1], null, 0, o[0]); } catch (Exception e) { return null; } }
            public byte[] renderTextHd(String s, int targetW, int[] dims) {
                if (hdFont == null || targetW <= 0 || s.isEmpty()) return null;
                int K = com.seoultycoon.st2.Surf.K, h = 14 * K, w = targetW + K;
                BufferedImage im = new BufferedImage(w, h, BufferedImage.TYPE_INT_ARGB); Graphics2D g = im.createGraphics();
                g.setRenderingHint(RenderingHints.KEY_TEXT_ANTIALIASING, RenderingHints.VALUE_TEXT_ANTIALIAS_ON); g.setRenderingHint(RenderingHints.KEY_FRACTIONALMETRICS, RenderingHints.VALUE_FRACTIONALMETRICS_ON);
                Font f0 = hdFont.deriveFont(11.8f * K); g.setFont(f0); int sw = g.getFontMetrics().stringWidth(s); if (sw <= 0) { g.dispose(); return null; }
                if (sw > targetW) { f0 = f0.deriveFont(f0.getSize2D() * targetW / sw); g.setFont(f0); }      // 넘치면 글자를 가로세로 같은 비율로 줄인다(찌그러지지 않게)
                FontMetrics fm = g.getFontMetrics(); sw = fm.stringWidth(s); float extra = s.length() > 1 ? Math.min(K * 1.2f, (targetW - sw) / (float) (s.length() - 1)) : 0f;   // 남는 폭은 글자 사이로
                g.setColor(Color.WHITE); float cx = 0; int by = 11 * K - 1;
                for (int i = 0; i < s.length(); i++) { String ch = s.substring(i, i + 1); g.drawString(ch, cx, by); cx += fm.stringWidth(ch) + extra; } g.dispose();
                byte[] r = new byte[w * h]; for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) r[y * w + x] = (byte) (im.getRGB(x, y) >>> 24); dims[0] = w; dims[1] = h; return r; }
            public byte[] renderText(String s, int[] dims) {
                BufferedImage m = new BufferedImage(1, 1, BufferedImage.TYPE_INT_ARGB); Graphics2D g = m.createGraphics(); g.setFont(font); FontMetrics fm = g.getFontMetrics(); int w = fm.stringWidth(s) + 1, h = 14; g.dispose();
                BufferedImage im = new BufferedImage(Math.max(w, 1), h, BufferedImage.TYPE_INT_ARGB); g = im.createGraphics(); g.setFont(font); g.setColor(Color.WHITE); g.drawString(s, 0, 11); g.dispose();
                byte[] r = new byte[w * h]; for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) r[y * w + x] = (byte) (im.getRGB(x, y) >>> 24 >= 128 ? 255 : 0); dims[0] = w; dims[1] = h; return r; }
            public byte[] readFile(String n) { String mod = System.getenv("MOD"); if (mod != null && n.equals("data1.pak")) try { return Files.readAllBytes(Paths.get(mod).resolve(n)); } catch (IOException e0) { } try { return Files.readAllBytes(out.resolve("save").resolve(n)); } catch (IOException e) { return null; } }
            public void writeFile(String n, byte[] d) { try { Files.write(out.resolve("save").resolve(n), d); } catch (IOException e) {} }
            public boolean fileExists(String n) { return Files.exists(out.resolve("save").resolve(n)); }
            public void playClip(int id, byte[] m, boolean loop) { System.out.println("PLAYCLIP id=" + id + " len=" + m.length + " head=" + new String(m, 0, 4) + " loop=" + loop); }
            public void log(String s) { System.out.println(s); }
        };
        Host H = new Host(pf, pf.resource(new File(a[0]).getName())!=null?pf.resource(new File(a[0]).getName()):Files.readAllBytes(Paths.get(a[0])));
        if (System.getenv("VERBOSE") != null) { H.setVerbose(true); H.trace = new java.io.PrintStream(new java.io.FileOutputStream("/tmp/jtrace.txt")); }
        if (System.getenv("WATCH") != null) H.vmWatch((int) Long.parseLong(System.getenv("WATCH"), 16)); if (System.getenv("WV") != null) H.vmWatchVal(Integer.parseInt(System.getenv("WV"))); H.boot(); try { H.startApp(); } catch (Exception e) { for (String x : H.recent) System.out.println(x); throw e; }
        int rounds = a.length > 4 ? Integer.parseInt(a[4]) : 100;
        java.util.Map<Integer, int[]> keys = new java.util.HashMap<>();
        if (a.length > 5 && !a[5].isEmpty()) for (String k : a[5].split(",")) { String[] p = k.split(":"); keys.put(Integer.parseInt(p[0]), new int[]{Integer.parseInt(p[1]), Integer.parseInt(p[2])}); }
        if (System.getenv("AUTOTUT") != null) H.startAutoTutorial();
        if (System.getenv("REPL") != null) {                                   // 대화형 시험: REPL=폴더. 폴더/cmd 파일의 줄을 실행하고 폴더/done 을 쓴다.  k 코드 | t 라운드 | s 그림경로 | q
            Path rd = Paths.get(System.getenv("REPL")); Files.createDirectories(rd); int dtm = System.getenv("DT") != null ? Integer.parseInt(System.getenv("DT")) : 50;
            System.out.println("REPL ready"); Files.write(rd.resolve("done"), "ready".getBytes());
            for (;;) {
                Path cf = rd.resolve("cmd"); if (!Files.exists(cf)) { Thread.sleep(80); continue; }
                Thread.sleep(30); java.util.List<String> lines = Files.readAllLines(cf); Files.delete(cf);
                for (String ln : lines) { String[] q = ln.trim().split("\\s+"); if (q[0].isEmpty()) continue;
                    if (q[0].equals("k")) { int c = Integer.parseInt(q[1]); H.postKey(1, c); H.tick(dtm); H.tick(dtm); H.postKey(2, c); H.tick(dtm); }
                    else if (q[0].equals("t")) { for (int i = 0; i < Integer.parseInt(q[1]); i++) H.tick(dtm); }
                    else if (q[0].equals("S")) { int[] hd = H.screen.hd(); BufferedImage im = new BufferedImage(480, 592, BufferedImage.TYPE_INT_RGB); im.setRGB(0, 0, 480, 592, hd, 0, 480); ImageIO.write(im, "png", new File(q[1])); }       // HD 화면
                    else if (q[0].equals("s")) { BufferedImage im = new BufferedImage(240, 296, BufferedImage.TYPE_INT_RGB); im.setRGB(0, 0, 240, 296, H.screen.px, 0, H.screen.w); ImageIO.write(im, "png", new File(q[1])); }
                    else if (q[0].equals("w")) { H.vm.w32((int) Long.parseLong(q[1], 16), Integer.parseInt(q[2])); }                          // w 주소(16진) 값 : 메모리 쓰기
                    else if (q[0].equals("r")) { StringBuilder sb = new StringBuilder("R"); int base = (int) Long.parseLong(q[1], 16); for (int i = -8; i < 24; i++) sb.append(' ').append(H.vm.r32(base + 4 * i)); System.out.println(sb); }   // r 주소 : 주변 32단어 읽기
                    else if (q[0].equals("d")) { int cls = H.classfor("[I"), end = H.hiEnd(), hdr = cls << 5, cnt = 0;                    // d 월 일 : 날짜 배열(int[12]) 에 쓰기
                        for (int aa = 0x50000000; aa < end; aa += 4) { if (H.vm.r32(aa) != hdr || H.vm.r32(aa + 4) != 12) continue; int y0 = H.vm.r32(aa + 8), m0 = H.vm.r32(aa + 12), d0 = H.vm.r32(aa + 16); if (y0 < 1 || y0 > 4 || m0 < 1 || m0 > 12 || d0 < 1 || d0 > 31) continue; H.vm.w32(aa + 12, Integer.parseInt(q[1])); H.vm.w32(aa + 16, Integer.parseInt(q[2])); cnt++; } System.out.println("D wrote " + cnt); }
                    else if (q[0].equals("q")) { System.out.println("bye"); return; } }
                Files.write(rd.resolve("done"), String.valueOf(System.nanoTime()).getBytes());
            }
        }
        if (System.getenv("AUTOTUT") != null) H.startAutoTutorial();
        for (int r = 1; r <= rounds; r++) {
            if (keys.containsKey(r)) H.postKey(keys.get(r)[0], keys.get(r)[1]);
            if (System.getenv("JUMP") != null) { String[] q = System.getenv("JUMP").split(":"); if (r == Integer.parseInt(q[0])) {            // 시험용: 상태 배열(int[12])의 날짜·남은 일수를 바꾼다  JUMP=라운드:년:월:일:남은일수[:지지도]
                int cls = H.classfor("[I"), end = H.hiEnd(), hdr = cls << 5, cnt = 0;
                for (int aa = 0x50000000; aa < end; aa += 4) {
                    if (H.vm.r32(aa) != hdr || H.vm.r32(aa + 4) != 12) continue;
                    int y0 = H.vm.r32(aa + 8), m0 = H.vm.r32(aa + 12), d0 = H.vm.r32(aa + 16); if (y0 < 1 || y0 > 4 || m0 < 1 || m0 > 12 || d0 < 1 || d0 > 31) continue;
                    H.vm.w32(aa + 8, Integer.parseInt(q[1])); H.vm.w32(aa + 12, Integer.parseInt(q[2])); H.vm.w32(aa + 16, Integer.parseInt(q[3])); H.vm.w32(aa + 48, Integer.parseInt(q[4])); if (q.length > 5) H.vm.w32(aa + 36, Integer.parseInt(q[5])); if (q.length > 6) H.vm.w32(aa + 20, Integer.parseInt(q[6])); if (q.length > 7) H.vm.w32(aa + 24, Integer.parseInt(q[7])); if (q.length > 8) H.vm.w32(aa + 28, Integer.parseInt(q[8])); cnt++; }
                System.out.println("JUMP wrote " + cnt + " arrays"); } }
            if (System.getenv("PATCH") != null) { String[] q = System.getenv("PATCH").split(":"); if (r == Integer.parseInt(q[0])) {      // 시험용: 예산/자재/인력 값(190,10,10 연속)을 찾아 바꾼다  PATCH=라운드:예산:자재:인력
                int cnt = 0; for (int aa = 0x50000000; aa < H.hiEnd(); aa += 4) if (H.vm.r32(aa) == 190 && H.vm.r32(aa + 4) >= 8 && H.vm.r32(aa + 4) < 20 && H.vm.r32(aa + 8) >= 8 && H.vm.r32(aa + 8) < 20) { System.out.println("PATCH at " + Integer.toHexString(aa) + " prev " + H.vm.r32(aa - 4)); if (H.vm.r32(aa - 4) != 12) { H.vm.w32(aa, Integer.parseInt(q[1])); H.vm.w32(aa + 4, Integer.parseInt(q[2])); H.vm.w32(aa + 8, Integer.parseInt(q[3])); cnt++; } } for (int aa = 0x01000000; aa < 0x01600000; aa += 4) if (H.vm.r32(aa) == 190 && H.vm.r32(aa + 4) >= 8 && H.vm.r32(aa + 4) < 20 && H.vm.r32(aa + 8) >= 8 && H.vm.r32(aa + 8) < 20) { System.out.println("IMG at " + Integer.toHexString(aa)); H.vm.w32(aa, Integer.parseInt(q[1])); H.vm.w32(aa + 4, Integer.parseInt(q[2])); H.vm.w32(aa + 8, Integer.parseInt(q[3])); cnt++; } System.out.println("PATCH wrote " + cnt); } }
            H.tick(System.getenv("DT") != null ? Integer.parseInt(System.getenv("DT")) : 50);
            if (r % Math.max(1, rounds / 10) == 0 || keys.containsKey(r - 3) || (System.getenv("SHOT") != null && r % Integer.parseInt(System.getenv("SHOT")) == 0)) {
                BufferedImage im = new BufferedImage(240, 320, BufferedImage.TYPE_INT_ARGB); im.setRGB(0, 0, 240, 296, H.screen.px, 0, H.screen.w); ImageIO.write(im, "png", out.resolve("f" + r + ".png").toFile()); }
        }
        if (System.getenv("SCAN") != null) { int v = Integer.parseInt(System.getenv("SCAN")); for (int aa = 0x50000000; aa < H.hiEnd(); aa += 4) if (H.vm.r32(aa) == v) { StringBuilder sb = new StringBuilder("SCAN " + Integer.toHexString(aa) + ":"); for (int k = -8; k <= 8; k++) sb.append(" ").append(H.vm.r32(aa + 4 * k)); System.out.println(sb); } }
        if (System.getenv("DUMP") != null) { int base = (int) Long.parseLong(System.getenv("DUMP"), 16); StringBuilder sb = new StringBuilder("DUMP"); for (int k = -16; k < 48; k++) sb.append(' ').append(H.vm.r32(base + 4 * k)); System.out.println(sb); }
        if (System.getenv("CLS") != null) { int h = (int) Long.parseLong(System.getenv("CLS"), 16); System.out.println("CLS " + H.describe(h)); }
        if (System.getenv("TAP") != null) { String[] q = System.getenv("TAP").split(","); System.out.println("TAPKEYS " + java.util.Arrays.toString(H.tapKeys(Integer.parseInt(q[0]), Integer.parseInt(q[1])))); }
        System.out.println("done");
    }
}
