package com.seoultycoon.st2;

/** WIPI lcdui(Display/Card/Graphics/Image/Jlet) + lwc 최소 구현 (wipi_rt.py 의 그래픽부 이식) */
final class GfxLib {
    static final String G = "org/kwis/msp/lcdui/Graphics";
    static int makeGfx(Host H, Surf s) { int h = H.newObj(H.classfor(G)); H.st.put(h, s); return h; }
    static int newImage(Host H, Surf s) { int h = H.newObj(H.classfor("org/kwis/msp/lcdui/Image")); H.st.put(h, s); return h; }
    static Surf surf(Host H, int h) { return (Surf) H.st.get(h); }
    static Surf decode(Host H, byte[] d) {
        int[] wh = new int[2]; int[] px = H.pf.decodePng(d, wh); if (px == null) return null;
        Surf s = new Surf(wh[0], wh[1], false); System.arraycopy(px, 0, s.px, 0, px.length);
        { java.util.HashSet<Integer> cs = new java.util.HashSet<>(); for (int p : px) if ((p >>> 24) > 127) { cs.add(p & 0xFFFFFF); if (cs.size() > 14) break; } s.smooth = cs.size() > 14 && wh[0] >= 24 && wh[1] >= 24; }      // 색이 많은 그림(건물·지형·인물)만 모서리를 다듬는다. 숫자·글자 시트는 획이 깨지므로 그대로
        // HD 그림: PNG 끝(IEND) 뒤에 K배 크기 PNG 가 한 장 더 붙어 있으면 그것을 HD 층으로 쓴다(tools/build_sprites.py 가 붙인다)
        for (int i = 8; i + 12 <= d.length - 8; i++) if (d[i] == 'I' && d[i + 1] == 'E' && d[i + 2] == 'N' && d[i + 3] == 'D') {
            int o = i + 8; if (o + 8 < d.length && (d[o] & 0xff) == 0x89 && d[o + 1] == 'P' && d[o + 2] == 'N' && d[o + 3] == 'G') {
                int[] wh2 = new int[2]; int[] p2 = H.pf.decodePng(java.util.Arrays.copyOfRange(d, o, d.length), wh2);
                if (p2 != null && wh2[0] == s.w * Surf.K && wh2[1] == s.h * Surf.K) s.hp = p2; }
            break; }
        return s;
    }
    static Surf digitSheet;
    static void install(final Host H) {
        H.regS("org/kwis/msp/lcdui/Display", "()Lorg/kwis/msp/lcdui/Display;+getDefaultDisplay", (t, a) -> H.single("org/kwis/msp/lcdui/Display"));
        H.reg("org/kwis/msp/lcdui/Display", "(Lorg/kwis/msp/lcdui/Card;)V+pushCard", (t, a) -> { H.currentCard = (int) a[0]; return 0; });
        final int[][] GA = {{50, 1}, {52, 2}, {54, 5}, {56, 6}, {53, 8}, {-1, 1}, {-2, 6}, {-3, 2}, {-4, 5}, {-5, 8}};
        H.regS("org/kwis/msp/lcdui/Display", "(I)I+getGameAction", (t, a) -> { for (int[] g : GA) if (g[0] == (int) a[0]) return g[1]; return 0; });
        H.statics.add("org/kwis/msp/lcdui/Display::(I)I+getGameAction");
        H.reg("org/kwis/msp/lcdui/Card", "()I+getWidth", (t, a) -> Host.SCREEN_W);
        H.reg("org/kwis/msp/lcdui/Card", "()I+getHeight", (t, a) -> Host.SCREEN_H);
        for (String n : new String[]{"()V+<init>", "(Z)V+<init>", "(I)V+<init>", "(IZ)V+<init>", "(IIII)V+<init>", "(IIIII)V+<init>", "(Lorg/kwis/msp/lcdui/Display;)V+<init>", "(Lorg/kwis/msp/lcdui/Display;IIII)V+<init>", "(Lorg/kwis/msp/lcdui/Display;IIIIZ)V+<init>"})
            H.reg("org/kwis/msp/lcdui/Card", n, (t, a) -> 0);
        for (String n : new String[]{"()V+repaint", "(IIII)V+repaint", "()V+serviceRepaints"}) H.reg("org/kwis/msp/lcdui/Card", n, (t, a) -> { H.repaintReq = true; return 0; });
        H.reg("org/kwis/msp/lcdui/Card", "()Lorg/kwis/msp/lcdui/Display;+getDisplay", (t, a) -> H.single("org/kwis/msp/lcdui/Display"));
        H.reg("org/kwis/msp/lcdui/Card", "()Z+isShown", (t, a) -> 1);
        H.reg("org/kwis/msp/lcdui/Card", "()I+getX", (t, a) -> 0); H.reg("org/kwis/msp/lcdui/Card", "()I+getY", (t, a) -> 0);
        for (String n : new String[]{"()V+<init>", "(Z)V+<init>", "()V+show"}) H.reg("org/kwis/msp/lwc/AnnunciatorComponent", n, (t, a) -> 0);
        H.regS("org/kwis/msp/handset/BackLight", "()V+alwaysOn", (t, a) -> 0);
        H.reg("org/kwis/msp/lcdui/Jlet", "()V+<init>", (t, a) -> 0);
        // ---- Image
        H.regS("org/kwis/msp/lcdui/Image", "(Ljava/lang/String;)Lorg/kwis/msp/lcdui/Image;+createImage", (t, a) -> {
            String n = H.pystr((int) a[0]); byte[] d = n == null ? null : H.pf.resource(n.startsWith("/") ? n.substring(1) : n);
            if (d == null) { H.pf.log("image not found " + n); return 0; } Surf s = decode(H, d); return s == null ? 0 : newImage(H, s); });
        H.regS("org/kwis/msp/lcdui/Image", "([BII)Lorg/kwis/msp/lcdui/Image;+createImage", (t, a) -> { byte[] d = H.arrayBytes((int) a[0]); Surf s = decode(H, java.util.Arrays.copyOfRange(d, (int) a[1], (int) a[1] + (int) a[2])); return s == null ? 0 : newImage(H, s); });
        H.regS("org/kwis/msp/lcdui/Image", "(II)Lorg/kwis/msp/lcdui/Image;+createImage", (t, a) -> newImage(H, new Surf((int) a[0], (int) a[1], false)));
        H.reg("org/kwis/msp/lcdui/Image", "()I+getWidth", (t, a) -> surf(H, t).w);
        H.reg("org/kwis/msp/lcdui/Image", "()I+getHeight", (t, a) -> surf(H, t).h);
        H.reg("org/kwis/msp/lcdui/Image", "()Lorg/kwis/msp/lcdui/Graphics;+getGraphics", (t, a) -> makeGfx(H, surf(H, t)));
        // ---- Graphics
        H.reg(G, "(I)V+setColor", (t, a) -> { surf(H, t).setColor((int) a[0]); return 0; });
        H.reg(G, "()I+getColor", (t, a) -> surf(H, t).color & 0xFFFFFF);
        H.reg(G, "(I)V+setAlpha", (t, a) -> 0);
        H.reg(G, "(IIII)V+fillRect", (t, a) -> { surf(H, t).fillRect((int) a[0], (int) a[1], (int) a[2], (int) a[3]); return 0; });
        H.reg(G, "(IIII)V+drawRect", (t, a) -> { surf(H, t).drawRect((int) a[0], (int) a[1], (int) a[2], (int) a[3]); return 0; });
        H.reg(G, "(IIII)V+drawLine", (t, a) -> { surf(H, t).drawLine((int) a[0], (int) a[1], (int) a[2], (int) a[3]); return 0; });
        H.reg(G, "(IIII)V+setClip", (t, a) -> { surf(H, t).setClip((int) a[0], (int) a[1], (int) a[2], (int) a[3]); return 0; });
        H.reg(G, "(IIIIII)V+fillRoundRect", (t, a) -> { surf(H, t).roundRect((int) a[0], (int) a[1], (int) a[2], (int) a[3], (int) a[4], (int) a[5], true); return 0; });
        H.reg(G, "(IIIIII)V+drawRoundRect", (t, a) -> { surf(H, t).roundRect((int) a[0], (int) a[1], (int) a[2], (int) a[3], (int) a[4], (int) a[5], false); return 0; });
        H.reg(G, "(IIIIII)V+fillArc", (t, a) -> { surf(H, t).ellipse((int) a[0], (int) a[1], (int) a[2], (int) a[3], true); return 0; });
        H.reg(G, "(IIIIII)V+drawArc", (t, a) -> { surf(H, t).ellipse((int) a[0], (int) a[1], (int) a[2], (int) a[3], false); return 0; });
        H.reg(G, "(II)I+getPixel", (t, a) -> surf(H, t).getPixel((int) a[0], (int) a[1]));
        H.reg(G, "()I+getClipX", (t, a) -> surf(H, t).hasClip ? surf(H, t).cx : 0);
        H.reg(G, "()I+getClipY", (t, a) -> surf(H, t).hasClip ? surf(H, t).cy : 0);
        H.reg(G, "()I+getClipWidth", (t, a) -> surf(H, t).hasClip ? surf(H, t).cw : Host.SCREEN_W);
        H.reg(G, "()I+getClipHeight", (t, a) -> surf(H, t).hasClip ? surf(H, t).ch : Host.SCREEN_H);
        H.reg(G, "(Lorg/kwis/msp/lcdui/Image;III)V+drawImage", (t, a) -> {
            int im = (int) a[0]; if (im == 0 || !(H.st.get(im) instanceof Surf)) return 0;
            Surf dst = surf(H, t), src = surf(H, im); int x = (int) a[1], y = (int) a[2], anchor = (int) a[3];
            if ((anchor & 1) != 0) x -= src.w / 2; if ((anchor & 8) != 0) x -= src.w; if ((anchor & 2) != 0) y -= src.h / 2; if ((anchor & 32) != 0) y -= src.h;
            if (H.verbose) H.pf.log("DI src=" + src.w + "x" + src.h + " x=" + x + " y=" + y + " clip=" + (dst.hasClip ? dst.cx + "," + dst.cy + "," + dst.cw + "," + dst.ch : "none") + " anchor=" + anchor + " lr=" + Integer.toHexString(H.vm.r[14] - Host.BASE));
            if (H.verbose && src.w == 77 && src.h == 11 && y == 5 && dst.cx == 6) { StringBuilder sb = new StringBuilder("STK"); for (int i = 0; i < 48; i++) { int w = H.vm.r32(H.vm.r[13] + 4 * i); if ((w & 1) == 1 && w > Host.BASE && w < Host.BASE + 0x83000) sb.append(' ').append(Integer.toHexString(w - Host.BASE)); } H.pf.log(sb.toString()); }
            if (System.getenv("DILOG") != null && src.w >= 90 && src.w <= 100 && src.h >= 40) H.pf.log("BSPR " + src.w + "x" + src.h + " at " + x + "," + y + " clip=" + (dst.hasClip ? dst.cx + "," + dst.cy + "," + dst.cw + "," + dst.ch : "none") + " lr=" + Integer.toHexString(H.vm.r[14] - Host.BASE) + stkAddrs(H) + " px0=" + Integer.toHexString(src.px[src.w * (src.h / 2) + src.w / 2]));
            if (System.getenv("LOGDIG") != null && ((src.w == 50 && src.h == 9) || (src.w == 40 && src.h == 7) || (src.w == 41 && src.h == 9))) H.pf.log("DIG " + src.w + "x" + src.h + " at " + x + "," + y + " clip=" + (dst.hasClip ? dst.cx + "," + dst.cy + "," + dst.cw + "," + dst.ch : "none") + " lr=" + Integer.toHexString(H.vm.r[14] - Host.BASE) + stkAddrs(H));
            if (System.getenv("LOGSEC") != null && src.w == 30 && src.h == 80) H.pf.log("SEC at " + x + "," + y + " clip=" + (dst.hasClip ? dst.cx + "," + dst.cy + "," + dst.cw + "," + dst.ch : "none"));
            if (special(H, dst, src, x, y)) return 0;
            // --- 원본과 시트 간격이 달라 생기는 위치 보정(PC 에뮬레이터와 동일)
            boolean restore = false; int scx = dst.cx, scy = dst.cy, scw = dst.cw, sch = dst.ch;
            if (src.w == 56 && src.h == 96 && dst.hasClip && dst.ch == 12) { x -= 12; y -= 2; dst.cx -= 12; dst.cy -= 2; restore = true; }       // 메인 메뉴 글자 시트
            if (System.getenv("DILOG") != null && ((src.w == 37 && src.h == 31) || (src.w == 77 && src.h == 11))) H.pf.log("L" + src.w + " at " + x + "," + y + " clip=" + dst.cx + "," + dst.cy + "," + dst.cw + "," + dst.ch);
            if (src.w == 77 && src.h == 11 && dst.hasClip && dst.cx - x == 42 && dst.cw == 10 && y == 5) return 0;   // 날짜 옆에 잘못 찍히던 동전 조각(시트 차이로 생긴 빈 칸)
            if (src.w == 77 && src.h == 11 && dst.hasClip && dst.cx - x >= 52) {     // 상단 바 아이콘: 게임은 더 넓은 시트(돈 52, 자재 63, 인력 78, D 89)를 가정 → 실제 시트의 칸(돈 42, 자재 51, 인력 61, D 70)을 골라 그린다
                int off = dst.cx - x, sx, sw; if (off == 52) { sx = 42; sw = 9; } else if (off == 63) { sx = 51; sw = 10; } else if (off == 78) { sx = 61; sw = 9; } else if (off == 89 && dst.ch > 2) { sx = 70; sw = 7; } else return 0;
                Surf cell = new Surf(sw, 11, false); for (int r = 0; r < 11; r++) System.arraycopy(src.px, r * 77 + sx, cell.px, r * sw, sw);
                boolean hc = dst.hasClip; dst.hasClip = false; dst.drawImage(cell, dst.cx + Math.max(0, (dst.cw - sw) / 2), dst.cy); dst.hasClip = hc; return 0; }
            if (src.w == 37 && src.h == 31 && dst.hasClip && dst.cw == 11 && dst.ch == 12 && dst.cy == y) {   // 피로 얼굴 아이콘: 게임은 11x12 칸으로 가정하지만 실제 시트는 9x9 얼굴 4개(맨 윗줄) → 해당 얼굴 한 칸만 그린다
                int k = Math.max(0, Math.min(3, (dst.cx - x) / 11)); Surf cell = new Surf(9, 9, false); for (int r = 0; r < 9; r++) System.arraycopy(src.px, r * 37 + k * 9, cell.px, r * 9, 9);
                boolean hc = dst.hasClip; dst.hasClip = false; dst.drawImage(cell, dst.cx + 1, dst.cy + 1); dst.hasClip = hc; return 0; }
            if (src.w == 37 && src.h == 31 && dst.hasClip && dst.cy - y >= 12) y += 3;                                                                  // 하단 바 라벨 시트
            dst.drawImage(src, x, y);
            if (restore) { dst.cx = scx; dst.cy = scy; dst.cw = scw; dst.ch = sch; }
            return 0; });
        H.reg(G, "(Ljava/lang/String;III)V+drawString", (t, a) -> {
            String s = H.pystr((int) a[0]); if (s == null) return 0; s = fullName(s); Surf dst = surf(H, t); int x = (int) a[1], y = (int) a[2], anchor = (int) a[3];
            if (H.verbose) H.pf.log("DS '" + s + "' x=" + x + " y=" + y + " anchor=" + anchor + " clip=" + (dst.hasClip ? dst.cx + "," + dst.cy + "," + dst.cw + "," + dst.ch : "none"));
            int[] dims = new int[2]; byte[] mask = H.pf.renderText(s, dims); if (mask == null) return 0;
            if ((anchor & 1) != 0) x -= dims[0] / 2; if ((anchor & 8) != 0) x -= dims[0]; if ((anchor & 32) != 0) y -= 12;
            int[] d2 = new int[2]; byte[] hm = H.pf.renderTextHd(s, (dims[0] - 1) * Surf.K, d2);
            dst.drawMask(mask, dims[0], dims[1], x, y - 2, hm, d2[0], d2[1]);
            if (dst.w == 240 && dst.h >= 290) {                                       // 번호 선택지("1.예 2.아니오" 등)의 클릭 영역 기록(그린 직후의 화면 픽셀 해시와 함께)
                java.util.regex.Matcher mm = java.util.regex.Pattern.compile("(?:^|\\s)([1-9])\\s*\\.").matcher(s); java.util.ArrayList<int[]> st = new java.util.ArrayList<>();
                while (mm.find()) st.add(new int[]{mm.start(1), mm.group(1).charAt(0) - '0'});
                for (int i = 0; i < st.size(); i++) {
                    int a0 = st.get(i)[0], a1 = i + 1 < st.size() ? st.get(i + 1)[0] : s.length(); int[] d0 = new int[2], d1 = new int[2];
                    byte[] m0 = a0 == 0 ? null : H.pf.renderText(s.substring(0, a0), d0); byte[] m1 = H.pf.renderText(s.substring(0, a1), d1);
                    H.addClickRow(dst, st.get(i)[1], x + (m0 == null ? 0 : d0[0]), y - 3, x + (m1 == null ? dims[0] : d1[0]), y + 12);
                }
            }
            return 0; });
        // ---- lwc (이름 입력창 등)
        String LC = "org/kwis/msp/lwc/Component", TC = "org/kwis/msp/lwc/TextComponent", TF = "org/kwis/msp/lwc/TextFieldComponent";
        for (String n : new String[]{"()V+<init>", "()V+repaint", "(IIII)V+repaint", "()V+serviceRepaints", "()V+invalidate", "()V+validate", "()V+setFocus", "(I)V+setBackground", "(I)V+setForeground", "(I)V+calcPreferredSize", "(Lorg/kwis/msp/lwc/EventListener;Ljava/lang/Object;)V+setEventListener"})
            H.reg(LC, n, (t, a) -> 0);
        String[] cn = {"()I+getBackground", "()I+getForeground", "()I+getX", "()I+getY", "()I+getWidth", "()I+getHeight", "()I+getXOnScreen", "()I+getYOnScreen", "()Z+hasFocus", "()Z+isShown", "()Z+isValid", "()Z+canHandleInput"};
        int[] cv = {0xffffff, 0, 0, 0, 100, 16, 0, 0, 1, 1, 1, 1};
        for (int i = 0; i < cn.length; i++) { final int v = cv[i]; H.reg(LC, cn[i], (t, a) -> v); }
        H.reg(TF, "(Ljava/lang/String;I)V+<init>", (t, a) -> { H.st.put(t, new String[]{H.pystr((int) a[0]) == null ? "" : H.pystr((int) a[0]), String.valueOf((int) a[1])}); initTextFields(H, t, (int) a[1]); return 0; });
        H.reg(TC, "(I)V+<init>", (t, a) -> { H.st.put(t, new String[]{"", String.valueOf((int) a[0])}); initTextFields(H, t, (int) a[0]); return 0; });
        H.reg(TF, "(Ljava/lang/String;)V+setString", (t, a) -> { tf(H, t)[0] = H.pystr((int) a[0]) == null ? "" : H.pystr((int) a[0]); return 0; });
        H.reg(TC, "(Ljava/lang/String;I)V+setString", (t, a) -> { tf(H, t)[0] = H.pystr((int) a[0]) == null ? "" : H.pystr((int) a[0]); return 0; });
        H.reg(TC, "()Ljava/lang/String;+getString", (t, a) -> { String v = tf(H, t)[0]; return H.jstr(v.isEmpty() ? H.defaultName : v); });
        H.reg(TC, "()I+getMaxLength", (t, a) -> Integer.parseInt(tf(H, t)[1]));
        H.reg(TC, "(I)V+setMaxLength", (t, a) -> { tf(H, t)[1] = String.valueOf((int) a[0]); return 0; });
        for (String n : new String[]{"(I)V+setConstraint", "(Lorg/kwis/msp/lcdui/Font;)V+setFont", "()V+calcViewPortArea", "(Z)V+showNotify", "(I)V+modeSetting"}) H.reg(TC, n, (t, a) -> 0);
        for (String n : new String[]{"()V+controlPopup", "(III)V+controlCursor", "(IIIII)V+configure", "(Lorg/kwis/msp/lcdui/Graphics;)V+paintContent", "(Z)V+focusNotify", "(II)V+delete", "([CIII)V+insert"}) H.reg(TF, n, (t, a) -> 0);
        H.reg(TF, "()I+getPreferredWidth", (t, a) -> 100); H.reg(TF, "()I+getPreferredHeight", (t, a) -> 16); H.reg(TF, "(I)I+getPreferredHeight", (t, a) -> 16); H.reg(TF, "(II)Z+keyNotify", (t, a) -> 0);
        H.regS("org/kwis/msp/lcdui/InputMethodHandler", "(I)Z+setCurrentMode", (t, a) -> 1);
    }

    // ---- 라벨 시트(글자가 겹쳐 있어 원본 조각 경계로는 잘리지 않음)는 글자 단위로 다시 그린다
    static int curBarX = 0, curBarY = 0;
    /** 시청 팝업 막대 위치(터치로 항목을 바로 고르기 위해): {x, y, 선택여부, 그려진 시각} */
    static final java.util.ArrayList<int[]> bars = new java.util.ArrayList<>();
    static synchronized void recordBar(Host H, int x, int y, boolean sel) {
        for (int[] b : bars) if (b[1] == y) { b[0] = x; b[2] = sel ? 1 : 0; b[3] = (int) H.vnow; return; }
        bars.add(new int[]{x, y, sel ? 1 : 0, (int) H.vnow});
    }
    /** 화면(240x296) 좌표 (gx,gy) 를 눌렀을 때 보낼 키 열. 팝업 막대를 눌렀으면 [방향키 n번..., 확인], 아니면 null */
    static synchronized int[] tapKeys(Host H, int gx, int gy) {
        java.util.ArrayList<int[]> cur = new java.util.ArrayList<>();
        for (int[] b : bars) if ((int) H.vnow - b[3] < 400) cur.add(b);
        if (cur.size() < 2) return null;
        cur.sort((p, q) -> p[1] - q[1]);
        int sel = -1, hit = -1;
        for (int i = 0; i < cur.size(); i++) { int[] b = cur.get(i); if (b[2] == 1) sel = i; if (gx >= b[0] - 3 && gx <= b[0] + 51 && gy >= b[1] - 3 && gy <= b[1] + 17) hit = i; }
        if (sel < 0 || hit < 0) return null;
        int d = hit - sel, n = Math.abs(d); int[] seq = new int[n + 1];
        for (int i = 0; i < n; i++) seq[i] = d > 0 ? 56 : 50;
        seq[n] = 53; return seq;
    }
    static final String[] MENU_LABELS = {"현황", "비서", "행사", "정보"};
    static final int[] daysDigit = {-1, -1, -1, -1};
    static void label(Host H, Surf dst, String t, int x, int y) {
        int[] dims = new int[2]; byte[] m = H.pf.renderText(t, dims); if (m == null) return;
        int sc = dst.color; boolean hc = dst.hasClip; dst.hasClip = false;
        int[] d2 = new int[2]; byte[] hm = H.pf.renderTextHd(t, (dims[0] - 1) * Surf.K, d2);
        dst.setColor(0x000000); dst.drawMask(m, dims[0], dims[1], x + 1, y + 1, hm, d2[0], d2[1]); dst.drawMask(m, dims[0], dims[1], x + 1, y, hm, d2[0], d2[1]);
        dst.setColor(0xFFFFFF); dst.drawMask(m, dims[0], dims[1], x, y, hm, d2[0], d2[1]);
        dst.color = sc; dst.hasClip = hc;
    }
    // 게임의 상태 배열 int[12] = {년, 월, 일, ..., [10]=우상단 표시값, ...} 를 찾아 날짜를 읽는다(게임이 날짜 숫자를 직접 그리지 않기 때문)
    static int[] lastDate = {1, 1, 1}; static int dateTick = 0;
    /** 힙의 int[12] 중 날짜 범위가 맞는 것들 가운데 '가장 늦은 날짜'를 현재 날짜로 본다(행동마다 복사본이 생기며 날짜는 증가만 하므로 오래된 복사본보다 큼). 15프레임마다 갱신 */
    static int[] readDate(Host H, int shown) {
        if (dateTick++ % 15 != 0) return lastDate;
        int cls = H.classfor("[I"), end = H.hiEnd(), hdr = cls << 5; long best = -1; int[] bd = null; long bestShown = -1; int[] bs = null;
        for (int a = 0x50000000; a < end; a += 4) {
            if (H.vm.r32(a) != hdr || H.vm.r32(a + 4) != 12) continue;
            int y = H.vm.r32(a + 8), m = H.vm.r32(a + 12), d = H.vm.r32(a + 16);
            if (y < 1 || y > 4 || m < 1 || m > 12 || d < 1 || d > 31) continue;
            int e3 = H.vm.r32(a + 20), e7 = H.vm.r32(a + 36);
            if (e3 <= 0 || e7 < 0 || e7 > 100) continue;
            long key = ((long) y * 100 + m) * 100 + d;
            if (key > best) { best = key; bd = new int[]{y, m, d}; }
            if (H.vm.r32(a + 48) == shown && key > bestShown) { bestShown = key; bs = new int[]{y, m, d}; }
        }
        int[] r = bd != null ? bd : bs; if (r != null) lastDate = r; return lastDate;
    }
    static String stkAddrs(Host H) { StringBuilder sb = new StringBuilder(" STK"); for (int i = 0; i < 64; i++) { int w = H.vm.r32(H.vm.r[13] + 4 * i); if ((w & 1) == 1 && w > Host.BASE && w < Host.BASE + 0x83000) sb.append(' ').append(Integer.toHexString(w - Host.BASE)); } return sb.toString(); }
    /** 코드 이미지 안의 이름은 글자 수를 못 늘려 줄여 넣었다 → 화면에 그릴 때 온전한 이름으로 바꾼다. */
    static final java.util.HashMap<String, String> FULL = new java.util.HashMap<>();
    static { String[][] m = {{"롯데시네", "롯데시네마"}, {"코스트코점", "코스트코 익산점"}, {"WL호텔", "웨스턴라이프호텔"}, {"테슬라본사", "테슬라 익산본사"}, {"KTX익산", "KTX 익산역"}, {"예술전당", "익산예술의전당"},
        {"원광대병원 ", "원광대병원"}, {"원광대학교 ", "원광대학교"}, {"익산 야구장", "익산야구장"}}; for (String[] e : m) FULL.put(e[0], e[1]); }
    static String fullName(String s) { String f = FULL.get(s); return f != null ? f : s; }
    static boolean special(Host H, Surf dst, Surf src, int x, int y) {
        if (!dst.hasClip) return false;
        int cx = dst.cx, cy = dst.cy, cw = dst.cw, ch = dst.ch;
        if (src.w == 50 && src.h == 9 && y == 6 && cx >= 200 && cx <= 218) {                         // 우상단 남은 일수 숫자: 날짜 계산용으로 읽어 둔다
            daysDigit[(cx - 200) / 6] = (cx - x) / 5; return false; }
        if (src.w == 77 && src.h == 11) {
            if (cx == 6 && cy == 5 && cw == 22) {                                                      // "임기 N년 M월 D일" 를 글자 단위로 다시 그림
                int shown = 0, ok = 1; for (int d : daysDigit) { if (d < 0) ok = 0; shown = shown * 10 + Math.max(d, 0); }
                int[] dt = ok == 1 ? readDate(H, shown) : new int[]{1, 1, 1}; int yr = dt[0], mo = dt[1], dy = dt[2];
                label(H, dst, "임기 " + yr + "년 " + mo + "월 " + dy + "일", 6, 4); return true; }
            if (y == 5 && (cx == 42 || cx == 75)) return true;                                          // 원본의 어긋난 년·월 조각은 생략
            return false;
        }
        if (src.w == 70 && src.h == 9) {                                                                 // 시청 팝업 메뉴 라벨
            int srcx = cx - x; int[] st = {0, 20, 38, 57}; int menuIdx = 0; for (int i = 1; i < 4; i++) if (Math.abs(srcx - st[i]) < Math.abs(srcx - st[menuIdx])) menuIdx = i;   // 선택 항목이 먼저 그려지므로 그리는 순서가 아니라 시트 속 위치로 라벨을 고른다
            label(H, dst, MENU_LABELS[menuIdx], curBarX + 19, curBarY + 1); return true; }
        if (src.w == 48 && src.h == 34) {                                                                // 팝업 마름모 버튼: 선택=빨강, 그 외=파랑
            int row0 = (cy - y) < 9 ? 0 : 14; Surf cell = new Surf(48, 14, false); System.arraycopy(src.px, row0 * 48, cell.px, 0, 48 * 14);
            boolean hc = dst.hasClip; dst.hasClip = false; dst.drawImage(cell, x, cy); dst.hasClip = hc; curBarX = x; curBarY = cy; recordBar(H, x, cy, row0 == 0); return true; }
        if (src.w == 25 && src.h == 21) {                                                                // 하단 키 안내: 원본은 그림(*:INFO, #:MAP, 0:EXIT). 글꼴로 직접 그려 플랫폼 키 이름(PC: C:정보 V:지도 0:나가기)에 맞춘다
            int k = Math.max(0, Math.min(2, Math.round((float) (cy - y) / 10f))); String[] hints = H.pf.keyHints();
            int sc = dst.color; boolean hcx = dst.hasClip; dst.hasClip = false; int ty = cy + 1;
            int[] dm = new int[2]; byte[] m = H.pf.renderText(hints[k], dm); int[] d2 = new int[2]; byte[] hm = H.pf.renderTextHd(hints[k], (dm[0] - 1) * Surf.K, d2);
            dst.setColor(0x000040); dst.drawMask(m, dm[0], dm[1], x + 1, ty - 4, hm, d2[0], d2[1]); dst.setColor(0x6DB6FF); dst.drawMask(m, dm[0], dm[1], x, ty - 5, hm, d2[0], d2[1]);
            dst.color = sc; dst.hasClip = hcx; return true; }
        if (src.w == 10 && src.h == 36) {                                                                // 아이콘 4개(그래프/얼굴/폭죽/서류)가 세로로 이어진 시트
            int k = Math.round((float) (cy - y) / 12f); if (k < 0 || k > 3) return true;
            int[] b = {0, 7, 17, 27, 36}; int h = b[k + 1] - b[k]; Surf cell = new Surf(10, h, false); System.arraycopy(src.px, b[k] * 10, cell.px, 0, 10 * h);
            boolean hc2 = dst.hasClip; dst.hasClip = false; dst.drawImage(cell, x, curBarY + (14 - h) / 2); dst.hasClip = hc2; return true; }
        return false;
    }
    static String[] tf(Host H, int t) { Object o = H.st.get(t); if (o instanceof String[]) return (String[]) o; String[] n = {"", "0"}; H.st.put(t, n); return n; }
    static void initTextFields(Host H, int t, int maxlen) {
        int body = H.r32(t); int ims = H.single("org/kwis/msp/lcdui/InputMethodHandler");
        H.w32(body + 4, ims); H.w32(body + 8, 0);
        if (maxlen < 0 || maxlen > 256) maxlen = 32;
        H.w32(body + 12, H.newArray(H.classfor("[C"), maxlen + 8, 2));
    }
    /** 상단 바 "임기 □년 □월" 숫자 칸 채우기(게임이 그리지 않음; DATE 기본 1,1) */
    static void drawDateDigits(Host H, Surf dst) {
        if (digitSheet == null) { byte[] d = H.pf.resource("48.m"); if (d != null) digitSheet = decode(H, d); }
        if (digitSheet == null) return;
        int[][] fields = {{H.dateYear, 40}, {H.dateMonth, 73}};
        boolean had = dst.hasClip; dst.hasClip = false;
        for (int[] f : fields) { String s = String.valueOf(f[0]); int x = f[1] - 5 * s.length();
            for (char c : s.toCharArray()) { Surf one = new Surf(5, 9, false); int dg = c - '0'; for (int j = 0; j < 9; j++) for (int i = 0; i < 5; i++) one.px[j * 5 + i] = digitSheet.px[j * digitSheet.w + 5 * dg + i]; dst.drawImage(one, x, 6); x += 5; } }
        dst.hasClip = had;
    }
}
