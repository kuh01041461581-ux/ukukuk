package com.seoultycoon.st2;

import java.util.*;

/**
 * 서울타이쿤2(WIPI Java AOT ARM 이미지) 호스트 런타임. tools/wipi_rt.py 의 자바 이식.
 * ThumbVm 위에서 자바 라이브러리·WIPI API 를 구현하고, 클래스 링크/객체 모델/스레드 스케줄러를 제공한다.
 */
public final class Host {
    // ---- 메모리 배치 (wipi_emu.py 와 동일)
    static final int BASE = 0x01000000, IMGSZ = 0x600000;
    static final int STACK = 0x20000000, STACK_SZ = 0x200000;
    static final int LOW = 0x02000000, LOW_SZ = 0x1800000;
    static final int HI = 0x50000000;
    static final int SCREEN_W = 240, SCREEN_H = 320;

    public final ThumbVm vm = new ThumbVm();
    final Platform pf;
    int lowPtr = LOW, hiPtr = HI, hiMapped = HI;

    // ---- 호스트 함수(매직 주소)
    interface Fn { long call(int[] a, int[] s); }
    interface Impl { long call(int self, long[] args); }
    static final class HFn { String name; char ret = 'I'; Fn fn; }
    final ArrayList<HFn> hfns = new ArrayList<>();
    final HashMap<String, Fn> named = new HashMap<>();
    int magicPtr = ThumbVm.MAGIC;

    public Host(Platform pf, byte[] image) {
        this.pf = pf;
        vm.map(BASE, IMGSZ); vm.map(STACK, STACK_SZ); vm.map(LOW, LOW_SZ); vm.map(ThumbVm.MAGIC, 0x1000); vm.map(0, 0x1000);
        vm.write(BASE, image, 0, image.length);
        vm.hook = this::onHost;
        patchArmStubs();
        // 재배치 스텁 실행
        vm.r[13] = STACK + STACK_SZ - 0x100; vm.r[14] = ThumbVm.RET | 1; vm.r[0] = 0x45c;
        vm.run(BASE + 0xc, 5_000_000);
        int entry = vm.r[0]; if (entry != BASE + 0xacac0) throw new IllegalStateException("bad entry " + Integer.toHexString(entry));
    }

    /** 이미지 내 ARM 모드 런타임 스텁(memset/memcpy/ldivmod)을 호스트 구현으로 교체 */
    void patchArmStubs() {
        int[] addr = {0x7d838, 0x7d8ec, 0x7da24, 0x7e038, 0x7e050};
        Fn[] fn = {
            (a, s) -> { for (int i = 0; i < a[2]; i++) vm.w8(a[0] + i, a[1] & 0xff); return a[0]; },
            (a, s) -> { byte[] t = new byte[a[2]]; for (int i = 0; i < t.length; i++) t[i] = (byte) vm.u8(a[1] + i); for (int i = 0; i < t.length; i++) vm.w8(a[0] + i, t[i] & 0xff); return a[0]; },
            (a, s) -> { long n = ((long) a[1] << 32) | (a[0] & 0xffffffffL), d = ((long) a[3] << 32) | (a[2] & 0xffffffffL);
                if (d == 0) { vm.r[2] = 0; vm.r[3] = 0; return 0; } long q = n / d, m = n % d; vm.r[2] = (int) m; vm.r[3] = (int) (m >> 32); return q; },
            (a, s) -> { int b = a[0]; for (int i = 0; i < 8; i++) w32(b + 4 * i, vm.r[4 + i]); w32(b + 32, vm.r[12]); w32(b + 36, vm.r[13]); w32(b + 40, vm.r[14]); return 0; },
            (a, s) -> { int b = a[0]; for (int i = 0; i < 8; i++) vm.r[4 + i] = r32(b + 4 * i); vm.r[12] = r32(b + 32); vm.r[13] = r32(b + 36); vm.r[14] = r32(b + 40); return a[1] == 0 ? 1 : a[1]; }};
        String[] nm = {"memset", "memcpy", "ldivmod", "setjmp", "longjmp"};
        for (int i = 0; i < 5; i++) {
            HFn h = new HFn(); h.name = nm[i]; h.ret = i == 2 ? 'J' : 'I'; h.fn = fn[i]; hfns.add(h);
            int m = magicPtr; magicPtr += 4;
            // thumb: ldr r3,[pc,#0]; bx r3; .word m|1
            vm.w16(BASE + addr[i], 0x4b00); vm.w16(BASE + addr[i] + 2, 0x4718); vm.w32(BASE + addr[i] + 4, m | 1);
        }
    }

    // ================================================================ 메모리 헬퍼
    int alloc(int n) {                       // 높은 힙(객체/핸들) — 필요 시 페이지 추가 매핑
        n = (n + 15) & ~15; int a = hiPtr; hiPtr += n;
        while (hiMapped < hiPtr) { vm.map(hiMapped, 0x10000); hiMapped += 0x10000; }
        return a;
    }
    int allocLow(int n) { n = (n + 15) & ~15; int a = lowPtr; lowPtr += n; if (lowPtr > LOW + LOW_SZ) throw new IllegalStateException("low heap"); return a; }
    int r32(int a) { return vm.r32(a); }
    void w32(int a, int v) { vm.w32(a, v); }
    int u16(int a) { return vm.u16(a); }
    void w16(int a, int v) { vm.w16(a, v); }
    String cstr(int a, int max) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < max; i++) { int c = vm.u8(a + i); if (c == 0) break; sb.append((char) c); }
        return sb.toString();
    }
    int putCStr(String s) { byte[] b = s.getBytes(java.nio.charset.StandardCharsets.ISO_8859_1); int a = allocLow(b.length + 1); vm.write(a, b, 0, b.length); vm.w8(a + b.length, 0); return a; }

    // ================================================================ 호스트 함수 등록/호출
    int host(String name, char ret, Fn f) {
        HFn h = new HFn(); h.name = name; h.ret = ret; h.fn = f; hfns.add(h);
        int a = magicPtr; magicPtr += 4;
        if (magicPtr > ThumbVm.MAGIC + 0x1000) { vm.map(ThumbVm.MAGIC + 0x1000 * (hfns.size() / 1024), 0x1000); }
        return a | 1;
    }
    void ensureMagicMapped() { }
    void onHost(int addr) {
        int idx = (addr - ThumbVm.MAGIC) >> 2;
        if (idx < 0 || idx >= hfns.size()) { vm.r[0] = 0; return; }
        HFn h = hfns.get(idx);
        int[] a = {vm.r[0], vm.r[1], vm.r[2], vm.r[3]};
        int sp = vm.r[13]; int[] s = {r32(sp), r32(sp + 4), r32(sp + 8), r32(sp + 12), r32(sp + 16), r32(sp + 20)};
        if (verbose) { recent.add(h.name + String.format("(%x,%x,%x) lr=%x", a[0], a[1], a[2], vm.r[14] - BASE)); if (recent.size() > 25) recent.remove(0); if (trace != null) trace.println(h.name + String.format("(%x,%x,%x) lr=%x sl=%x", a[0], a[1], a[2], vm.r[14] - BASE, vm.r[10])); }
        hostCalls++; Fn f = h.fn != null ? h.fn : named.get(h.name);
        long v = 0;
        if (f != null) v = f.call(a, s);
        else if (verbose) pf.log("[host?] " + h.name + String.format("(%x,%x,%x,%x)", a[0], a[1], a[2], a[3]));
        if (h.ret == 'J' || h.ret == 'D') { vm.r[1] = (int) (v >>> 32); vm.r[0] = (int) v; } else vm.r[0] = (int) v;
    }
    public String describe(int body) { int C = vm.r32(body) >>> 5; int I = vm.r32(C + 8); StringBuilder sb = new StringBuilder(cstr(vm.r32(I), 60) + " size=" + vm.u16(I + 0x1a) + " nf=" + vm.u16(I + 0x20) + " fields:"); int fa = vm.r32(I + 0x14); for (int i = 0; i < vm.u16(I + 0x20) && i < 40; i++) { int F = vm.r32(fa + 4 * i); sb.append(" [" + i + " off=" + vm.r32(F + 0xc) + "]"); } return sb.toString(); }
    boolean verbose = false; public int hiEnd() { return hiPtr; } public void vmWatch(int a) { vm.watch = a; } public void vmWatchVal(int v) { vm.watchVal = v; } public void setVerbose(boolean v) { verbose = v; }
    public java.io.PrintStream trace;
    public final ArrayList<String> recent = new ArrayList<>();
    public int dateYear = 1, dateMonth = 1; public String defaultName = "시장";
    public boolean threw = false, exited = false;

    // ================================================================ 문자열표 / 이미지 클래스표
    static final int STRTAB = BASE + 0x9cac0;
    String strtab(int i) { return cstr(r32(STRTAB + 4 * i), 300); }
    final LinkedHashMap<String, Integer> imgClasses = new LinkedHashMap<>();
    void scanImageClasses() {
        for (int i = 0; i < 193; i++) {
            int cx = BASE + 0xac6c4 + i * 0x14, ix = r32(cx + 8);
            if (ix >= BASE && ix < BASE + IMGSZ && r32(ix) >= BASE && r32(ix) < BASE + IMGSZ) imgClasses.put(cstr(r32(ix), 120), cx);
        }
    }
    static boolean inHeapOrImg(int p) { return (p >= BASE && p < BASE + IMGSZ) || (p >= LOW && p < LOW + LOW_SZ); }
    boolean chainHas(int c, int target) {
        for (int d = 0; c != 0 && d < 16; d++) {
            int sup = r32(r32(c + 8) + 8);
            if (sup == 0 || (sup & 1) != 0 || !inHeapOrImg(sup)) return false;
            if (sup == target) return true;
            c = sup;
        }
        return false;
    }

    // ================================================================ 호스트 클래스 합성
    final class HClass {
        final String name; final int C, I, vtab; final HashMap<String, Integer> methods = new HashMap<>();
        HClass(String name, int vt, int size) {
            this.name = name; C = allocLow(0x40); I = allocLow(0x40);
            w32(I, putCStr(name)); w16(I + 0x1a, size); w32(C + 8, I);
            w16(C + 0x10, vt); w16(C + 0x12, 0x88); vtab = allocLow(4 * Math.max(vt, 1)); w32(C + 0xc, vtab);
        }
        int method(int nameptr, String key) {
            Integer have = methods.get(key); if (have != null) return have;
            int M = allocLow(0x20); String ik = name + "::" + key; String hn = "M:" + ik;
            char ret = key.indexOf(')') >= 0 ? key.charAt(key.indexOf(')') + 1) : 'V';
            int fn = host(hn, ret, makeCaller(ik, key));
            int vidx = vidxNext++; if (vidxNext > 128) throw new IllegalStateException("host vtable overflow");
            int hid = 0x100 + nat.size(); nat.add(new String[]{ik, key}); w32(M + 8, hid);
            w32(M, fn); w32(M + 4, C); w32(M + 0xc, nameptr); w16(M + 0x14, vidx); w16(M + 0x16, 1);
            methods.put(key, M); w32(vtab + 4 * vidx, M); propagate(C, vidx, M);
            if (verbose && !impls.containsKey(ik)) pf.log("resolve(미구현) " + name + " :: " + key);
            return M;
        }
    }
    int vidxNext = 19;
    final ArrayList<String[]> nat = new ArrayList<>();
    final HashMap<String, HClass> hclasses = new HashMap<>();
    static final HashMap<String, Integer> HOST_SIZE = new HashMap<>();
    static final HashMap<String, String> SUPER = new HashMap<>();
    static final HashMap<String, int[]> FIELD_OFFS = new HashMap<>();
    static {
        HOST_SIZE.put("org/kwis/msp/lwc/TextComponent", 12); HOST_SIZE.put("org/kwis/msp/lwc/TextFieldComponent", 12); HOST_SIZE.put("org/kwis/msp/lwc/Component", 12);
        SUPER.put("org/kwis/msp/lwc/TextFieldComponent", "org/kwis/msp/lwc/TextComponent"); SUPER.put("org/kwis/msp/lwc/TextComponent", "org/kwis/msp/lwc/Component");
    }
    static int fieldOff(String cls, String fname, int dflt) {
        if (cls.equals("org/kwis/msp/lwc/TextComponent")) { if (fname.equals("imHandler")) return 0; if (fname.equals("m_cPos")) return 4; if (fname.equals("m_td")) return 8; }
        return dflt;
    }
    HClass hclass(String name) {
        HClass hc = hclasses.get(name); if (hc != null) return hc;
        if (name.equals("org/kwis/msp/lcdui/Jlet")) {
            hc = new HClass(name, 128, 0); hclasses.put(name, hc);
            int seoulI = r32(BASE + 0xac804 + 8);
            for (int k = 0; k < 19; k++) {
                int M = allocLow(0x20); w32(M, host("M:Jlet.vt" + k, 'I', null)); w32(M + 4, hc.C);
                if (k >= 15 && k <= 18) w32(M + 0xc, r32(r32(r32(seoulI + 0xc) + 4 * (k - 14)) + 0xc));
                else w32(M + 0xc, putCStr("Z()V+vt" + k));
                w16(M + 0x14, k); w16(M + 0x16, 1); w32(hc.vtab + 4 * k, M);
            }
        } else {
            hc = new HClass(name, 128, HOST_SIZE.getOrDefault(name, 0)); hclasses.put(name, hc);
            String sup = SUPER.get(name); if (sup != null) w32(hc.I + 8, hclass(sup).C);
        }
        return hc;
    }
    int classfor(String name) { Integer c = imgClasses.get(name); return c != null ? c : hclass(name).C; }
    void propagate(int hostC, int vidx, int M) {
        HClass obj = hclasses.get("java/lang/Object"); boolean isObj = false; /* Python 원본은 항상 False (chain_has 로만 전파) */
        ArrayList<Integer> targets = new ArrayList<>(); targets.add(hostC); targets.addAll(imgClasses.values());
        for (HClass h : hclasses.values()) targets.add(h.C);
        for (int cx : targets) if (cx == hostC || isObj || chainHas(cx, hostC)) { int vt = r32(cx + 0xc); if (vt != 0) { w32(vt + 4 * vidx, M); } }
    }

    // ================================================================ 객체 / 배열 / 문자열
    final HashMap<Integer, Object> st = new HashMap<>();
    final HashMap<Integer, int[]> ar = new HashMap<>();      // 핸들 -> {body, n, esz}
    int newObj(int C) {
        int size = u16(r32(C + 8) + 0x1a);
        int body = alloc(4 + size + 8); w32(body, C << 5);
        int h = alloc(8); w32(h, body); return h;
    }
    int newArray(int C, int n, int esz) {
        if (n < 0 || (long) n * esz > 4_000_000L) { pf.log("!!! huge array " + n + " " + esz); vm.stop = true; threw = true; n = 0; }
        int body = alloc(8 + n * esz + 8); w32(body, C << 5); w32(body + 4, n);
        int h = alloc(8); w32(h, body); ar.put(h, new int[]{body, n, esz}); return h;
    }
    int[] arrInfo(int h) { int[] i = ar.get(h); if (i != null) return i; int body = r32(h); return new int[]{body, r32(body + 4), 1}; }
    int jstr(String s) { int h = newObj(classfor("java/lang/String")); st.put(h, s); return h; }
    String pystr(int h) { if (h == 0) return null; Object o = st.get(h); return o instanceof String ? (String) o : ""; }
    int byteArray(byte[] d) { int h = newArray(classfor("[B"), d.length, 1); vm.write(arrInfo(h)[0] + 8, d, 0, d.length); return h; }
    byte[] arrayBytes(int h) { int[] i = arrInfo(h); return vm.read(i[0] + 8, i[1] * i[2]); }
    final HashMap<String, Integer> singletons = new HashMap<>();
    int single(String name) { Integer h = singletons.get(name); if (h == null) { h = newObj(classfor(name)); singletons.put(name, h); } return h; }
    int clsOf(int h) { return (r32(r32(h)) >> 5) & 0x7ffffff; }
    String cnameOf(int h) { try { return cstr(r32(r32(clsOf(h) + 8)), 120); } catch (Throwable t) { return "?"; } }
    int escOfClass(int C) {
        String nm = cstr(r32(r32(C + 8)), 60);
        switch (nm) { case "[Z": case "[B": return 1; case "[C": case "[S": return 2; case "[J": case "[D": return 8; case "[I": case "[F": return 4; default: return 4; }
    }

    // ================================================================ 호출 인자 해석
    static char[] parseSig(String key) {
        String d = key.substring(1, key.indexOf(')')); ArrayList<Character> out = new ArrayList<>(); int i = 0;
        while (i < d.length()) {
            char c = d.charAt(i);
            if (c == 'J' || c == 'D') { out.add(c); i++; }
            else if (c == 'L') { out.add('L'); i = d.indexOf(';', i) + 1; }
            else if (c == '[') { while (d.charAt(i) == '[') i++; if (d.charAt(i) == 'L') i = d.indexOf(';', i) + 1; else i++; out.add('L'); }
            else { out.add(c); i++; }
        }
        char[] r = new char[out.size()]; for (int k = 0; k < r.length; k++) r[k] = out.get(k); return r;
    }
    final HashMap<String, Impl> impls = new HashMap<>();
    final HashSet<String> statics = new HashSet<>();
    void reg(String cls, String sig, Impl f) { impls.put(cls + "::" + sig, f); }
    void regS(String cls, String sig, Impl f) { impls.put(cls + "::" + sig, f); statics.add(cls + "::" + sig); }
    Fn makeCaller(final String ik, final String key) {
        final char[] types = parseSig(key);
        return (a, s) -> {
            Impl fn = impls.get(ik);
            int[] regs = new int[a.length + s.length]; System.arraycopy(a, 0, regs, 0, a.length); System.arraycopy(s, 0, regs, a.length, s.length);
            boolean stat = statics.contains(ik); int i = 1, self = 0;
            if (!stat) { self = regs[1]; i = 2; }
            long[] args = new long[types.length];
            for (int k = 0; k < types.length; k++) {
                char t = types[k];
                if (t == 'J' || t == 'D') { if (i % 2 != 0) i++; args[k] = (regs[i] & 0xffffffffL) | ((long) regs[i + 1] << 32); i += 2; }
                else { args[k] = regs[i]; i++; }
            }
            if (fn == null) { if (verbose) pf.log("[UNIMPL] " + ik); return 0; }
            try { return fn.call(self, args); } catch (RuntimeException ex) { pf.log("[IMPL-ERR] " + ik + ": " + ex); return 0; }
        };
    }

    // ================================================================ JB / env 표
    int primtab; int resbuf; final HashMap<String, Integer> fcount = new HashMap<>();
    final HashMap<String, Integer> ifaces = new HashMap<>();
    void named(String n, Fn f) { named.put(n, f); }
    int iface(String name, int n) {
        int base = alloc(4 * n);
        for (int i = 0; i < n; i++) w32(base + 4 * i, host(name + "[" + i + "]", 'I', null));
        return base;
    }
    static final char[] PRIM_CH = {0, 0, 0, 0, 'Z', 'C', 'F', 'D', 'B', 'S', 'I', 'J'};
    static final int[] PRIM_SZ = {0, 0, 0, 0, 1, 2, 4, 8, 1, 2, 4, 8};
    void setupInterfaces() {
        named("env.lookup", (a, s) -> {
            String name = cstr(a[0], 100); Integer p = ifaces.get(name);
            if (p == null) {
                p = iface(name, 80); ifaces.put(name, p);
                if (name.equals("WIPIC_knlInterface")) { final int tbl = iface("clib", 24); named("WIPIC_knlInterface[33]", (x, y) -> tbl); }
            }
            return p;
        });
        named("env[8]", (a, s) -> { w32(a[0], classfor(cstr(a[1], 120))); return 0; });
        named("env[11]", (a, s) -> alloc(a[0]));
        named("WIPI_JBInterface[1]", this::jbResolve);
        named("WIPI_JBInterface[2]", this::jbThrow);
        named("WIPI_JBInterface[3]", this::jbAastore);
        named("WIPI_JBInterface[6]", (a, s) -> newArray(a[0], a[1], escOfClass(a[0])));
        named("WIPI_JBInterface[8]", (a, s) -> 0); named("WIPI_JBInterface[9]", (a, s) -> 0);
        named("WIPI_JBInterface[10]", (a, s) -> { initClass(a[0]); return 0; });
        named("WIPI_JBInterface[11]", (a, s) -> { int n = u16(a[0]); StringBuilder sb = new StringBuilder(); for (int i = 0; i < n; i++) sb.append((char) u16(a[0] + 2 + 2 * i)); return jstr(sb.toString()); });
        named("WIPI_JBInterface[12]", this::jb12);
        primtab = alloc(0x40);
        for (int t = 4; t <= 11; t++) w32(primtab + t * 4, classfor("[" + PRIM_CH[t]));
    }
    long jbResolve(int[] a, int[] s) {
        int slot = a[0], cb = a[1], w = r32(slot);
        if ((w & 1) == 0) return newObj(slot);
        if (cb == BASE + 0x7b4d5) { w32(slot, classfor(strtab(w >>> 1))); return 0; }
        String cname = strtab((w & 0xffff) >>> 1), mem = strtab(w >>> 16);
        int nameptr = r32(STRTAB + 4 * (w >>> 16)); String key = mem.substring(1);
        if (cb == BASE + 0x7b469) {
            int F = allocLow(0x20); int n = fcount.getOrDefault(cname, 0); fcount.put(cname, n + 1);
            String fname = key.substring(key.lastIndexOf('+') + 1);
            w32(F + 0xc, fieldOff(cname, fname, 4 * n)); w32(F + 4, classfor(cname)); w32(slot, F); return 0;
        }
        w32(slot, hclass(cname).method(nameptr, key)); return 0;
    }
    long jbThrow(int[] a, int[] s) {
        if (a[0] >= 0 && a[0] < 0x100) { int C = r32(primtab + a[0]); return newArray(C, a[1], PRIM_SZ[a[0] >> 2]); }
        if (a[2] == BASE + 0x7bded) { String nm = cstr(r32(r32(a[0] + 8)), 100); return newArray(classfor("[" + (nm.startsWith("[") ? nm : "L" + nm + ";")), a[1], 4); }
        if (a[2] == BASE + 0x7be75) {
            int w = r32(a[0]); String nm = (w & 1) != 0 ? strtab(w >>> 1) : cstr(r32(r32(w + 8)), 100);
            return newArray(classfor("[" + (nm.startsWith("[") ? nm : "L" + nm + ";")), a[1], 4);
        }
        if (a[2] == BASE + 0x7bd65) return a[1];                             // checkcast
        pf.log("!!! THROW " + cstr(a[0], 80) + String.format(" lr=%x", vm.r[14] - BASE)); vm.stop = true; threw = true; return 0;
    }
    long jbAastore(int[] a, int[] s) {
        int arr = a[0], idx = a[1], val = a[2];
        if (a[3] == BASE + 0x7bf5d) { int[] dims = new int[a[1]]; for (int i = 0; i < dims.length; i++) dims[i] = r32(a[2] + 4 * i); return multi(a[0], dims, 0); }
        if (a[3] == BASE + 0x7cbb1 && arr != 0) { int[] ai = arrInfo(arr); if (idx >= 0 && idx < ai[1]) w32(ai[0] + 8 + 4 * idx, val); return 0; }
        pf.log("!!! JB[3] unexpected"); return 0;
    }
    int multi(int C, int[] dims, int d) {
        String nm = cstr(r32(r32(C + 8)), 100); int n = dims[d];
        if (d == dims.length - 1) return newArray(C, n, escOfClass(C));
        int arr = newArray(C, n, 4); int body = arrInfo(arr)[0]; int sub = classfor(nm.substring(1));
        for (int i = 0; i < n; i++) w32(body + 8 + 4 * i, multi(sub, dims, d + 1));
        return arr;
    }
    long jb12(int[] a, int[] s) {
        if (resbuf == 0) resbuf = alloc(16);
        int h = a[0]; if (h < 0x100 || h - 0x100 >= nat.size()) return a[0];
        String[] nk = nat.get(h - 0x100); String ik = nk[0], key = nk[1]; Impl fn = impls.get(ik); boolean stat = statics.contains(ik);
        int[] words = new int[10]; for (int i = 0; i < 10; i++) words[i] = r32(a[1] + 4 * i);
        char[] types = parseSig(key); int i = 0, self = 0; long[] args = new long[types.length];
        if (!stat) { self = words[0]; i = 1; }
        for (int k = 0; k < types.length; k++) {
            if (types[k] == 'J' || types[k] == 'D') { args[k] = (words[i] & 0xffffffffL) | ((long) words[i + 1] << 32); i += 2; } else { args[k] = words[i]; i++; }
        }
        long r = 0;
        if (fn == null) { if (verbose) pf.log("[UNIMPL-native] " + ik); }
        else try { r = fn.call(self, args); } catch (RuntimeException ex) { pf.log("[IMPL-ERR] " + ik + ": " + ex); }
        vm.w32(resbuf, (int) r); vm.w32(resbuf + 4, (int) (r >>> 32));
        return resbuf;
    }

    // ================================================================ 이미지 코드 호출(중첩 포함)
    void nested(int code, int... args) {
        int[] ctx = vm.save(); boolean st0 = vm.stop;
        vm.r[13] = (vm.r[13] - 0x400) & ~7; for (int i = 0; i < args.length; i++) vm.r[i] = args[i]; vm.r[14] = ThumbVm.RET | 1;
        vm.run(code, 100_000_000); vm.restore(ctx); vm.stop = st0;
    }
    void initClass(int C) {
        int fl = u16(C + 0x12); if ((fl & 8) != 0) return; w16(C + 0x12, fl | 8);
        int I = r32(C + 8), sup = r32(I + 8);
        if (sup != 0 && (sup & 1) == 0 && inHeapOrImg(sup)) initClass(sup);
        int ms = r32(I + 0xc), n = u16(I + 0x18);
        for (int i = 0; i < n; i++) { int M = r32(ms + 4 * i); if (cstr(r32(M + 0xc), 80).endsWith("+<clinit>")) { nested(r32(M) & ~1, 0, 0); break; } }
    }
    /** 최상위 호출: 새 스택에서 코드 실행(스레드 양보 없이 끝까지) */
    int call(int code, int[] args, int[] stackArgs) {
        int sp = STACK + STACK_SZ - 0x100 - 4 * (stackArgs == null ? 0 : stackArgs.length);
        if (stackArgs != null) for (int i = 0; i < stackArgs.length; i++) w32(sp + 4 * i, stackArgs[i]);
        vm.r[13] = sp; vm.r[14] = ThumbVm.RET | 1; for (int i = 0; i < 4; i++) vm.r[i] = i < args.length ? args[i] : 0;
        vm.run(code, 400_000_000); return vm.r[0];
    }

    // ================================================================ 로더
    int scratch, root, env;
    public void boot() {
        scanImageClasses();
        installLib();
        env = alloc(64 * 4); for (int i = 0; i < 64; i++) w32(env + 4 * i, host("env[" + i + "]", 'I', null));
        w32(env, host("env.lookup", 'I', null));
        setupInterfaces();
        scratch = alloc(256); root = alloc(64); int l2 = alloc(64), l3 = alloc(256); w32(root, l2); w32(l2, l3);
        int r = call(BASE + 0x7b1ec, new int[]{scratch, root, 0, primtab}, new int[]{env}); if (r != 0) throw new IllegalStateException("init " + r);
        r = call(BASE + 0x7b02c, new int[]{0, 0, 0, 0}, null); if (r != 0) throw new IllegalStateException("load " + Integer.toHexString(r));
    }
    void installLib() { Lib.install(this); }

    // ================================================================ 스레드 스케줄러 / 화면
    public long vnow = 0;
    final ArrayList<Lib.Thr> threads = new ArrayList<>();
    final HashMap<Integer, Lib.Thr> thrOf = new HashMap<>();
    Lib.Thr curThread;
    void yieldNow(int ms) { if (curThread != null) curThread.wake = vnow + ms; vm.stop = true; }
    boolean idleLast = false;
    /** 한 스레드를 한 조각(최대 count 명령) 실행. 완료되면 done */
    void runSlice(Lib.Thr t, int count) {
        curThread = t;
        if (t.ctx == null) {
            int code = findCode(t.target, "()V+run");
            if (code == 0) { pf.log("thread: run() not found for " + cnameOf(t.target)); t.done = true; curThread = null; return; }
            vm.r[13] = STACK + STACK_SZ - 0x100 - (t.n + 1) * 0x20000; vm.r[14] = ThumbVm.RET | 1; vm.r[0] = 0; vm.r[1] = t.target;
            vm.run(code, count);
        } else {
            vm.restore(t.ctx);
            if (vm.r[15] >= ThumbVm.MAGIC && vm.r[15] < ThumbVm.MAGIC_END && vm.r[15] != ThumbVm.RET) vm.resumeFromMagic();
            vm.run(vm.r[15], count);
        }
        if (vm.r[15] == ThumbVm.RET || threw) t.done = true; else t.ctx = vm.save();
        curThread = null;
    }
    int findCode(int h, String suffix) {
        int C = clsOf(h), vt = r32(C + 0xc), n = u16(C + 0x10); int plus = suffix.lastIndexOf('+'); String want = suffix.substring(plus + 1), sig = suffix.substring(0, plus);
        for (int i = 0; i < n; i++) { int M = r32(vt + 4 * i); if (M == 0) continue;
            String nm = cstr(r32(M + 0xc), 200); nm = nm.length() > 0 ? nm.substring(1) : nm;
            if (nm.equals(suffix) || (nm.endsWith("+" + want) && nm.contains(sig))) return r32(M) & ~1; }
        return 0;
    }
    /** 가상 메소드 호출(최상위, 호스트→앱) */
    int vcall(int h, String suffix, int... args) {
        int code = findCode(h, suffix); if (code == 0) { pf.log("vcall not found " + suffix + " " + cnameOf(h)); return 0; }
        int[] a = new int[args.length + 2]; a[1] = h; System.arraycopy(args, 0, a, 2, args.length);
        int[] ctx = vm.save();
        int r = call(code, a, null);
        vm.restore(ctx); return r;
    }
    // 입력 이벤트 큐 (UI 스레드 → 게임 스레드)
    final ArrayDeque<int[]> keyQ = new ArrayDeque<>();
    public synchronized void postKey(int type, int key) { keyQ.add(new int[]{type, key}); }
    synchronized int[] pollKey() { return keyQ.poll(); }

    public int currentCard = 0; public boolean repaintReq = true;
    public Surf screen = new Surf(SCREEN_W, SCREEN_H, true);
    public int gfxScreen;
    /** 앱 시작: Seoul2.<init>, startApp */
    public void startApp() {
        int C = BASE + 0xac804, I = r32(C + 8), obj = newObj(C);
        int ms = r32(I + 0xc);
        int init = r32(r32(ms)) & ~1, start = r32(r32(ms + 4)) & ~1;
        call(init, new int[]{0, obj}, null); call(start, new int[]{0, obj, 0}, null);
    }
    /** 게임 한 프레임: 키 이벤트 전달 → 스레드 조각 실행 → 그리기. 반환: 화면이 갱신되었는가 */
    public boolean tick(int dtMs) {
        if (verbose) pf.log("TICK " + vnow);
        vnow += dtMs;
        int[] k; while ((k = pollKey()) != null) { pf.log("key " + k[0] + " " + k[1] + " card=" + Integer.toHexString(currentCard) + " " + (currentCard != 0 ? cnameOf(currentCard) : "")); if (currentCard != 0) pf.log("-> " + vcall(currentCard, "(II)Z+keyNotify", k[0], k[1])); }
        boolean worked = false;
        for (Lib.Thr t : new ArrayList<>(threads)) {
            if (t.done || t.wake > vnow) continue;
            long c0 = hostCalls; runSlice(t, idleLast ? 300_000 : 8_000_000); idleLast = hostCalls == c0; worked = true;
        }
        if (currentCard != 0) {
            if (gfxScreen == 0) gfxScreen = GfxLib.makeGfx(this, screen);
            screen.hasClip = false; vcall(currentCard, "(Lorg/kwis/msp/lcdui/Graphics;)V+paint", gfxScreen);
        }
        return true;
    }
    long hostCalls = 0;
}
