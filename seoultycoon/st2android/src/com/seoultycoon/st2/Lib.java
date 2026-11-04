package com.seoultycoon.st2;

import java.io.ByteArrayOutputStream;
import java.nio.charset.Charset;
import java.util.*;

/** 자바/WIPI 라이브러리 구현 (tools/wipi_rt.py 의 @impl 이식) */
final class Lib {
    static final Charset CP949 = Charset.forName("x-windows-949"), EUCKR = Charset.forName("EUC-KR");
    static Charset cs() { try { return Charset.forName("x-windows-949"); } catch (Exception e) { return Charset.forName("EUC-KR"); } }

    /** 읽기 스트림 */
    static class BS { byte[] b; int p; BS(byte[] b) { this.b = b; } byte[] take(int n) { n = Math.max(0, Math.min(n, b.length - p)); byte[] d = Arrays.copyOfRange(b, p, p + n); p += n; return d; }
        int pos() { return p; } void pos(int v) { p = v; } }
    /** File 과 읽기 위치를 공유하는 스트림 */
    static class FileRef { String name; int mode; int pos; byte[] buf = new byte[0]; }
    static final class FBS extends BS { final FileRef f; FBS(FileRef f) { super(f.buf); this.f = f; }
        @Override byte[] take(int n) { b = f.buf; p = f.pos; byte[] d = super.take(n); f.pos = p; return d; } }
    static class Thr { int h; int[] ctx; boolean done; long wake; int n; int target; }

    static void install(final Host H) {
        final Charset cs = cs();
        // ---- java.lang.Object
        H.reg("java/lang/Object", "()V+<init>", (t, a) -> 0);
        H.reg("java/lang/Object", "()I+hashCode", (t, a) -> t);
        H.reg("java/lang/Object", "(Ljava/lang/Object;)Z+equals", (t, a) -> t == (int) a[0] ? 1 : 0);
        H.reg("java/lang/Object", "()Ljava/lang/Class;+getClass", (t, a) -> { int h = H.newObj(H.classfor("java/lang/Class")); H.st.put(h, new int[]{H.clsOf(t)}); return h; });
        H.reg("java/lang/Object", "()Ljava/lang/String;+toString", (t, a) -> H.jstr(H.cnameOf(t) + "@" + Integer.toHexString(t)));
        H.reg("java/lang/Object", "()V+notify", (t, a) -> 0); H.reg("java/lang/Object", "()V+notifyAll", (t, a) -> 0);
        for (String w : new String[]{"()V+wait", "(J)V+wait", "(JI)V+wait"}) H.reg("java/lang/Object", w, (t, a) -> { H.yieldNow(10); return 0; });
        // ---- String / StringBuffer
        H.reg("java/lang/String", "([B)V+<init>", (t, a) -> { H.st.put(t, new String(H.arrayBytes((int) a[0]), cs)); return 0; });
        H.reg("java/lang/String", "([BII)V+<init>", (t, a) -> { byte[] b = H.arrayBytes((int) a[0]); int o = (int) a[1], l = (int) a[2]; H.st.put(t, new String(b, o, Math.min(l, b.length - o), cs)); return 0; });
        H.reg("java/lang/String", "()I+length", (t, a) -> H.pystr(t).length());
        H.reg("java/lang/String", "()[B+getBytes", (t, a) -> H.byteArray(H.pystr(t).getBytes(cs)));
        H.reg("java/lang/String", "(I)Ljava/lang/String;+substring", (t, a) -> { String s = H.pystr(t); return H.jstr(s.substring(Math.min((int) a[0], s.length()))); });
        H.reg("java/lang/String", "(II)Ljava/lang/String;+substring", (t, a) -> { String s = H.pystr(t); int i = Math.min((int) a[0], s.length()), j = Math.min((int) a[1], s.length()); return H.jstr(s.substring(i, Math.max(i, j))); });
        H.reg("java/lang/String", "(Ljava/lang/Object;)Z+equals", (t, a) -> { int o = (int) a[0]; return o != 0 && H.st.get(o) instanceof String && H.st.get(o).equals(H.st.get(t)) ? 1 : 0; });
        H.reg("java/lang/String", "(Ljava/lang/String;I)I+indexOf", (t, a) -> H.pystr(t).indexOf(H.pystr((int) a[0]), (int) a[1]));
        H.reg("java/lang/StringBuffer", "()V+<init>", (t, a) -> { H.st.put(t, new StringBuilder()); return 0; });
        H.reg("java/lang/StringBuffer", "(I)V+<init>", (t, a) -> { H.st.put(t, new StringBuilder()); return 0; });
        H.reg("java/lang/StringBuffer", "(Ljava/lang/String;)Ljava/lang/StringBuffer;+append", (t, a) -> { int s = (int) a[0]; ((StringBuilder) H.st.get(t)).append(s == 0 ? "null" : H.pystr(s)); return t; });
        H.reg("java/lang/StringBuffer", "(I)Ljava/lang/StringBuffer;+append", (t, a) -> { ((StringBuilder) H.st.get(t)).append((int) a[0]); return t; });
        H.reg("java/lang/StringBuffer", "()Ljava/lang/String;+toString", (t, a) -> H.jstr(H.st.get(t).toString()));
        H.reg("java/lang/StringBuffer", "()I+length", (t, a) -> ((StringBuilder) H.st.get(t)).length());
        H.reg("java/lang/StringBuffer", "(II)Ljava/lang/StringBuffer;+delete", (t, a) -> { StringBuilder sb = (StringBuilder) H.st.get(t); int i = (int) a[0], j = Math.min((int) a[1], sb.length()); if (i < j) sb.delete(i, j); return t; });
        H.regS("java/lang/Integer", "(I)Ljava/lang/String;+toString", (t, a) -> H.jstr(Integer.toString((int) a[0])));
        H.regS("java/lang/Integer", "(I)Ljava/lang/String;+toHexString", (t, a) -> H.jstr(Integer.toHexString((int) a[0])));
        H.regS("java/lang/Math", "(I)I+abs", (t, a) -> Math.abs((int) a[0]));
        H.regS("java/lang/Math", "(II)I+max", (t, a) -> Math.max((int) a[0], (int) a[1]));
        H.regS("java/lang/Math", "(II)I+min", (t, a) -> Math.min((int) a[0], (int) a[1]));
        // ---- System / Runtime / Random / Stack
        H.regS("java/lang/System", "()J+currentTimeMillis", (t, a) -> { H.vnow += 2; return 1700000000000L + H.vnow; });
        H.regS("java/lang/System", "()V+gc", (t, a) -> 0);
        H.regS("java/lang/System", "(I)V+exit", (t, a) -> { H.exited = true; H.vm.stop = true; return 0; });
        H.regS("java/lang/System", "(Ljava/lang/Object;ILjava/lang/Object;II)V+arraycopy", (t, a) -> {
            int[] s = H.arrInfo((int) a[0]), d = H.arrInfo((int) a[2]); int sp = (int) a[1], dp = (int) a[3], n = (int) a[4];
            byte[] data = H.vm.read(s[0] + 8 + sp * s[2], n * s[2]); H.vm.write(d[0] + 8 + dp * d[2], data, 0, data.length); return 0; });
        H.regS("java/lang/Runtime", "()Ljava/lang/Runtime;+getRuntime", (t, a) -> H.single("java/lang/Runtime"));
        H.reg("java/util/Random", "()V+<init>", (t, a) -> { H.st.put(t, new Random()); return 0; });
        H.reg("java/util/Random", "(J)V+setSeed", (t, a) -> { ((Random) H.st.get(t)).setSeed(a[0]); return 0; });
        H.reg("java/util/Random", "()I+nextInt", (t, a) -> ((Random) H.st.get(t)).nextInt());
        H.reg("java/util/Stack", "()V+<init>", (t, a) -> { H.st.put(t, new ArrayList<Integer>()); return 0; });
        H.reg("java/util/Stack", "(Ljava/lang/Object;)Ljava/lang/Object;+push", (t, a) -> { @SuppressWarnings("unchecked") ArrayList<Integer> l = (ArrayList<Integer>) H.st.get(t); l.add((int) a[0]); return a[0]; });
        H.reg("java/util/Stack", "()Ljava/lang/Object;+pop", (t, a) -> { @SuppressWarnings("unchecked") ArrayList<Integer> l = (ArrayList<Integer>) H.st.get(t); return l.isEmpty() ? 0 : l.remove(l.size() - 1); });
        H.reg("java/util/Stack", "()Z+empty", (t, a) -> { @SuppressWarnings("unchecked") ArrayList<Integer> l = (ArrayList<Integer>) H.st.get(t); return l.isEmpty() ? 1 : 0; });
        // ---- 스트림
        H.reg("java/lang/Class", "(Ljava/lang/String;)Ljava/io/InputStream;+getResourceAsStream", (t, a) -> {
            String name = H.pystr((int) a[0]); byte[] d = name == null ? null : H.pf.resource(name.startsWith("/") ? name.substring(1) : name);
            if (d == null) { H.pf.log("resource not found " + name); return 0; }
            int h = H.newObj(H.classfor("java/io/InputStream")); H.st.put(h, new BS(d)); return h; });
        H.reg("java/io/InputStream", "()I+available", (t, a) -> { BS b = (BS) H.st.get(t); return b.b.length - b.pos(); });
        H.reg("java/io/InputStream", "()V+close", (t, a) -> 0);
        H.reg("java/io/InputStream", "([B)I+read", (t, a) -> readInto(H, t, (int) a[0], 0, -1));
        H.reg("java/io/InputStream", "([BII)I+read", (t, a) -> readInto(H, t, (int) a[0], (int) a[1], (int) a[2]));
        H.reg("java/io/InputStream", "(J)J+skip", (t, a) -> ((BS) H.st.get(t)).take((int) a[0]).length);
        H.reg("java/io/DataInputStream", "(Ljava/io/InputStream;)V+<init>", (t, a) -> { H.st.put(t, H.st.get((int) a[0])); return 0; });
        H.reg("java/io/DataInputStream", "()B+readByte", (t, a) -> (byte) rd(H, t, 1));
        H.reg("java/io/DataInputStream", "()I+readInt", (t, a) -> (int) rd(H, t, 4));
        H.reg("java/io/DataInputStream", "()S+readShort", (t, a) -> (short) rd(H, t, 2));
        H.reg("java/io/DataInputStream", "()I+readUnsignedShort", (t, a) -> rd(H, t, 2) & 0xffff);
        H.reg("java/io/DataInputStream", "()V+close", (t, a) -> 0);
        H.reg("java/io/DataInputStream", "([B)I+read", (t, a) -> readInto(H, t, (int) a[0], 0, -1));
        H.reg("java/io/DataInputStream", "([BII)I+read", (t, a) -> readInto(H, t, (int) a[0], (int) a[1], (int) a[2]));
        H.reg("java/io/DataInputStream", "(I)I+skipBytes", (t, a) -> ((BS) H.st.get(t)).take((int) a[0]).length);
        H.reg("java/io/ByteArrayInputStream", "([B)V+<init>", (t, a) -> { H.st.put(t, new BS(H.arrayBytes((int) a[0]))); return 0; });
        H.reg("java/io/ByteArrayOutputStream", "()V+<init>", (t, a) -> { H.st.put(t, new ByteArrayOutputStream()); return 0; });
        H.reg("java/io/ByteArrayOutputStream", "()[B+toByteArray", (t, a) -> H.byteArray(((ByteArrayOutputStream) H.st.get(t)).toByteArray()));
        H.reg("java/io/ByteArrayOutputStream", "()I+size", (t, a) -> ((ByteArrayOutputStream) H.st.get(t)).size());
        H.reg("java/io/DataOutputStream", "(Ljava/io/OutputStream;)V+<init>", (t, a) -> { H.st.put(t, H.st.get((int) a[0])); return 0; });
        H.reg("java/io/DataOutputStream", "(I)V+writeByte", (t, a) -> { ((ByteArrayOutputStream) H.st.get(t)).write((int) a[0]); return 0; });
        H.reg("java/io/DataOutputStream", "(I)V+writeShort", (t, a) -> { ByteArrayOutputStream o = (ByteArrayOutputStream) H.st.get(t); o.write((int) a[0] >> 8); o.write((int) a[0]); return 0; });
        H.reg("java/io/DataOutputStream", "(I)V+writeInt", (t, a) -> { ByteArrayOutputStream o = (ByteArrayOutputStream) H.st.get(t); int v = (int) a[0]; o.write(v >> 24); o.write(v >> 16); o.write(v >> 8); o.write(v); return 0; });
        H.reg("java/io/DataOutputStream", "()V+close", (t, a) -> 0);
        H.reg("java/io/DataOutputStream", "([BII)V+write", (t, a) -> { byte[] b = H.arrayBytes((int) a[0]); ((ByteArrayOutputStream) H.st.get(t)).write(b, (int) a[1], (int) a[2]); return 0; });
        // ---- 스레드
        H.reg("java/lang/Thread", "()V+<init>", (t, a) -> { H.st.put(t, new int[]{0}); return 0; });
        H.reg("java/lang/Thread", "(Ljava/lang/Runnable;)V+<init>", (t, a) -> { H.st.put(t, new int[]{(int) a[0]}); return 0; });
        H.reg("java/lang/Thread", "(Ljava/lang/Runnable;Ljava/lang/String;)V+<init>", (t, a) -> { H.st.put(t, new int[]{(int) a[0]}); return 0; });
        H.reg("java/lang/Thread", "(Ljava/lang/String;)V+<init>", (t, a) -> { H.st.put(t, new int[]{0}); return 0; });
        H.reg("java/lang/Thread", "()V+start", (t, a) -> { Thr th = new Thr(); th.h = t; th.n = H.threads.size(); int[] st = (int[]) H.st.get(t); th.target = st[0] != 0 ? st[0] : t; H.threads.add(th); H.thrOf.put(t, th); return 0; });
        H.regS("java/lang/Thread", "(J)V+sleep", (t, a) -> { H.yieldNow((int) Math.max(a[0], 1)); return 0; });
        H.regS("java/lang/Thread", "()V+yield", (t, a) -> { H.yieldNow(0); return 0; });
        H.regS("java/lang/Thread", "()Ljava/lang/Thread;+currentThread", (t, a) -> H.curThread != null ? H.curThread.h : H.single("java/lang/Thread"));
        H.reg("java/lang/Thread", "()V+join", (t, a) -> { H.yieldNow(5); return 0; });
        H.reg("java/lang/Thread", "()Z+isAlive", (t, a) -> { Thr th = H.thrOf.get(t); return th != null && !th.done ? 1 : 0; });
        H.reg("java/lang/Thread", "(I)V+setPriority", (t, a) -> 0); H.reg("java/lang/Thread", "()V+interrupt", (t, a) -> 0);
        // ---- 날짜
        H.regS("java/util/Calendar", "()Ljava/util/Calendar;+getInstance", (t, a) -> { int h = H.newObj(H.classfor("java/util/Calendar")); H.st.put(h, java.util.Calendar.getInstance()); return h; });
        H.reg("java/util/Calendar", "(I)I+get", (t, a) -> { java.util.Calendar c = (java.util.Calendar) H.st.get(t);
            switch ((int) a[0]) { case 1: return c.get(java.util.Calendar.YEAR) - 1900; case 2: return c.get(java.util.Calendar.MONTH); case 5: return c.get(java.util.Calendar.DAY_OF_MONTH);
                case 11: return c.get(java.util.Calendar.HOUR_OF_DAY); case 12: return c.get(java.util.Calendar.MINUTE); case 13: return c.get(java.util.Calendar.SECOND); case 7: return c.get(java.util.Calendar.DAY_OF_WEEK); default: return 0; } });
        H.reg("java/util/Date", "()V+<init>", (t, a) -> { H.st.put(t, new Date()); return 0; });
        H.reg("java/util/Calendar", "(Ljava/util/Date;)V+setTime", (t, a) -> { ((java.util.Calendar) H.st.get(t)).setTime((Date) H.st.get((int) a[0])); return 0; });
        // ---- 파일(저장소)
        H.regS("org/kwis/msp/io/FileSystem", "(Ljava/lang/String;)Z+exists", (t, a) -> H.pf.fileExists(H.pystr((int) a[0]).replaceAll("^/+", "")) ? 1 : 0);
        H.reg("org/kwis/msp/io/File", "(Ljava/lang/String;I)V+<init>", (t, a) -> { FileRef f = new FileRef(); f.name = H.pystr((int) a[0]).replaceAll("^/+", ""); f.mode = (int) a[1];
            if (f.mode != 3) { byte[] d = H.pf.readFile(f.name); if (d != null) f.buf = d; } H.st.put(t, f); return 0; });
        H.reg("org/kwis/msp/io/File", "([BII)I+write", (t, a) -> { FileRef f = (FileRef) H.st.get(t); byte[] b = H.arrayBytes((int) a[0]); int o = (int) a[1], l = (int) a[2];
            int need = f.pos + l; if (need > f.buf.length) f.buf = Arrays.copyOf(f.buf, need); System.arraycopy(b, o, f.buf, f.pos, l); f.pos += l; return l; });
        H.reg("org/kwis/msp/io/File", "([BII)I+read", (t, a) -> fileRead(H, t, (int) a[0], (int) a[1], (int) a[2]));
        H.reg("org/kwis/msp/io/File", "([B)I+read", (t, a) -> fileRead(H, t, (int) a[0], 0, H.arrInfo((int) a[0])[1]));
        H.reg("org/kwis/msp/io/File", "()I+sizeOf", (t, a) -> ((FileRef) H.st.get(t)).buf.length);
        H.reg("org/kwis/msp/io/File", "(I)V+seek", (t, a) -> { ((FileRef) H.st.get(t)).pos = (int) a[0]; return 0; });
        H.reg("org/kwis/msp/io/File", "()V+close", (t, a) -> { FileRef f = (FileRef) H.st.get(t); if (f.mode != 1) H.pf.writeFile(f.name, f.buf); return 0; });
        H.reg("org/kwis/msp/io/File", "()Ljava/io/DataInputStream;+openDataInputStream", (t, a) -> { int h = H.newObj(H.classfor("java/io/DataInputStream")); H.st.put(h, new FBS((FileRef) H.st.get(t))); return h; });
        // ---- 미디어/진동(소리는 상위에서 연결)
        H.reg("org/kwis/msp/media/Clip", "(Ljava/lang/String;)V+<init>", (t, a) -> 0);
        H.reg("org/kwis/msp/media/Clip", "(Ljava/lang/String;[B)V+<init>", (t, a) -> { H.st.put(t, H.arrayBytes((int) a[1])); return 0; });
        H.reg("org/kwis/msp/media/Clip", "(I)Z+setVolume", (t, a) -> { H.pf.setClipVolume((int) a[0]); return 1; });
        H.reg("org/kwis/msp/media/Player", "(Lorg/kwis/msp/media/Clip;Z)Z+play", (t, a) -> { Object d = H.st.get((int) a[0]); if (d instanceof byte[]) H.pf.playClip((byte[]) d, a[1] != 0); return 1; });
        H.reg("org/kwis/msp/media/Player", "(Lorg/kwis/msp/media/Clip;)Z+stop", (t, a) -> { H.pf.stopClip(); return 1; });
        H.reg("org/kwis/msp/media/Vibrator", "(II)V+on", (t, a) -> 0);
        GfxLib.install(H);
    }
    static long rd(Host H, int t, int n) { byte[] d = ((BS) H.st.get(t)).take(n); long v = 0; for (int i = 0; i < n; i++) v = (v << 8) | (i < d.length ? d[i] & 0xff : 0); return v; }
    static long readInto(Host H, int t, int arr, int off, int len) {
        BS s = (BS) H.st.get(t); int[] ai = H.arrInfo(arr); if (len < 0) len = ai[1];
        byte[] d = s.take(len); if (d.length == 0 && len != 0) return -1;
        H.vm.write(ai[0] + 8 + off, d, 0, d.length); return d.length; }
    static long fileRead(Host H, int t, int arr, int off, int len) {
        FileRef f = (FileRef) H.st.get(t); int n = Math.max(0, Math.min(len, f.buf.length - f.pos)); if (n == 0 && len != 0) return -1;
        H.vm.write(H.arrInfo(arr)[0] + 8 + off, f.buf, f.pos, n); f.pos += n; return n; }
}
