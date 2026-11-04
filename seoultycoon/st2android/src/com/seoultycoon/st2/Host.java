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
        // 재배치 스텁 실행
        vm.r[13] = STACK + STACK_SZ - 0x100; vm.r[14] = ThumbVm.RET | 1; vm.r[0] = 0x45c;
        vm.run(BASE + 0xc, 5_000_000);
        int entry = vm.r[0]; if (entry != BASE + 0xacac0) throw new IllegalStateException("bad entry " + Integer.toHexString(entry));
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
        Fn f = h.fn != null ? h.fn : named.get(h.name);
        long v = 0;
        if (f != null) v = f.call(a, s);
        else if (verbose) pf.log("[host?] " + h.name + String.format("(%x,%x,%x,%x)", a[0], a[1], a[2], a[3]));
        if (h.ret == 'J' || h.ret == 'D') { vm.r[1] = (int) (v >>> 32); vm.r[0] = (int) v; } else vm.r[0] = (int) v;
    }
    boolean verbose = false;
    public boolean threw = false, exited = false;
}
