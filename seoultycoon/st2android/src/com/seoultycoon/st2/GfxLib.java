package com.seoultycoon.st2;

/** WIPI lcdui(Display/Card/Graphics/Image/Jlet) + lwc 최소 구현 (wipi_rt.py 의 그래픽부 이식) */
final class GfxLib {
    static final String G = "org/kwis/msp/lcdui/Graphics";
    static int makeGfx(Host H, Surf s) { int h = H.newObj(H.classfor(G)); H.st.put(h, s); return h; }
    static int newImage(Host H, Surf s) { int h = H.newObj(H.classfor("org/kwis/msp/lcdui/Image")); H.st.put(h, s); return h; }
    static Surf surf(Host H, int h) { return (Surf) H.st.get(h); }
    static Surf decode(Host H, byte[] d) {
        int[] wh = new int[2]; int[] px = H.pf.decodePng(d, wh); if (px == null) return null;
        Surf s = new Surf(wh[0], wh[1], false); System.arraycopy(px, 0, s.px, 0, px.length); return s;
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
            // --- 원본과 시트 간격이 달라 생기는 위치 보정(PC 에뮬레이터와 동일)
            boolean restore = false; int scx = dst.cx, scy = dst.cy, scw = dst.cw, sch = dst.ch;
            if (src.w == 56 && src.h == 96 && dst.hasClip && dst.ch == 12) { x -= 12; y -= 2; dst.cx -= 12; dst.cy -= 2; restore = true; }       // 메인 메뉴 글자 시트
            if (src.w == 37 && src.h == 31 && dst.hasClip && dst.cy - y >= 12) y += 3;                                                                  // 하단 바 라벨 시트
            dst.drawImage(src, x, y);
            if (restore) { dst.cx = scx; dst.cy = scy; dst.cw = scw; dst.ch = sch; }
            if (src.w == 77 && src.h == 11 && y == 5 && dst.hasClip && dst.cx == 6 && dst.cy == 5 && dst.cw == 22 && dst.ch == 13) drawDateDigits(H, dst);   // 상단 바 임기 년·월
            return 0; });
        H.reg(G, "(Ljava/lang/String;III)V+drawString", (t, a) -> {
            String s = H.pystr((int) a[0]); if (s == null) return 0; Surf dst = surf(H, t); int x = (int) a[1], y = (int) a[2], anchor = (int) a[3];
            int[] dims = new int[2]; byte[] mask = H.pf.renderText(s, dims); if (mask == null) return 0;
            if ((anchor & 1) != 0) x -= dims[0] / 2; if ((anchor & 8) != 0) x -= dims[0]; if ((anchor & 32) != 0) y -= 12;
            dst.drawMask(mask, dims[0], dims[1], x, y - 2); return 0; });
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
