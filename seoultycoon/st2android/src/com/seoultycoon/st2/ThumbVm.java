package com.seoultycoon.st2;

/** ARM Thumb-1 인터프리터 (서울타이쿤2 WIPI AOT 이미지 실행용). 32비트 BL 쌍과 BX/BLX 만 32비트로 취급. */
public final class ThumbVm {
    public static final int MAGIC = 0x40000000, MAGIC_END = 0x40100000, RET = 0x40080000;
    public interface Hook { void call(int addr); }

    final byte[][] pg = new byte[1 << 20][];
    public final int[] r = new int[16];
    boolean fn, fz, fc, fv;
    public Hook hook;
    public boolean stop;
    public int insnCount;

    // ---- 메모리
    public void map(int base, int size) { for (int a = base >>> 12; a < ((base + size + 4095) >>> 12); a++) if (pg[a] == null) pg[a] = new byte[4096]; }
    byte[] page(int a) { byte[] p = pg[a >>> 12]; if (p == null) throw new IllegalStateException(String.format("unmapped %08x pc=%08x", a, r[15])); return p; }
    public int u8(int a) { return page(a)[a & 4095] & 0xff; }
    String stk() { StringBuilder sb = new StringBuilder(" STK"); for (int i = 0; i < 64; i++) { int w = r32(r[13] + 4 * i); if ((w & 1) == 1 && w > 0x1000000 && w < 0x1083000) sb.append(' ').append(Integer.toHexString(w - 0x1000000)); } return sb.toString(); }
    public int watch = -1, watchVal = Integer.MIN_VALUE;
    public void w8(int a, int v) { if (watch != -1 && (a & ~3) == watch) { new Throwable("WATCH w8 " + Integer.toHexString(a) + "=" + Integer.toHexString(v) + " pc=" + Integer.toHexString(r[15] - 0x1000000)).printStackTrace(); } page(a)[a & 4095] = (byte) v; }
    public int u16(int a) { return u8(a) | (u8(a + 1) << 8); }
    public void w16(int a, int v) { w8(a, v); w8(a + 1, v >> 8); }
    public int r32(int a) {
        if ((a & 4095) <= 4092) { byte[] p = page(a); int o = a & 4095; return (p[o] & 0xff) | ((p[o + 1] & 0xff) << 8) | ((p[o + 2] & 0xff) << 16) | ((p[o + 3] & 0xff) << 24); }
        return u8(a) | (u8(a + 1) << 8) | (u8(a + 2) << 16) | (u8(a + 3) << 24);
    }
    public void w32(int a, int v) {
        if (v == watchVal && (r[15] - 0x1000000) != 0x7ccc8 + 2 && (r[15] - 0x1000000) != 0x7ccc8) System.out.println("WV a=" + Integer.toHexString(a) + " pc=" + Integer.toHexString(r[15] - 0x1000000) + " lr=" + Integer.toHexString(r[14] - 0x1000000) + stk());
        if (watch != -1 && a == watch) new Throwable("WATCH w32 " + Integer.toHexString(v) + " pc=" + Integer.toHexString(r[15] - 0x1000000)).printStackTrace();
        if ((a & 4095) <= 4092) { byte[] p = page(a); int o = a & 4095; p[o] = (byte) v; p[o + 1] = (byte) (v >> 8); p[o + 2] = (byte) (v >> 16); p[o + 3] = (byte) (v >> 24); return; }
        w8(a, v); w8(a + 1, v >> 8); w8(a + 2, v >> 16); w8(a + 3, v >> 24);
    }
    public void write(int a, byte[] d, int off, int len) { for (int i = 0; i < len; i++) w8(a + i, d[off + i]); }
    public byte[] read(int a, int len) { byte[] d = new byte[len]; for (int i = 0; i < len; i++) d[i] = (byte) u8(a + i); return d; }

    // ---- 컨텍스트
    public int[] save() { int[] c = new int[17]; System.arraycopy(r, 0, c, 0, 16); c[16] = (fn ? 8 : 0) | (fz ? 4 : 0) | (fc ? 2 : 0) | (fv ? 1 : 0); return c; }
    public void restore(int[] c) { System.arraycopy(c, 0, r, 0, 16); int f = c[16]; fn = (f & 8) != 0; fz = (f & 4) != 0; fc = (f & 2) != 0; fv = (f & 1) != 0; }

    // ---- 플래그/연산
    void nz(int v) { fn = v < 0; fz = v == 0; }
    int add(int a, int b, int cin) { long s = (a & 0xffffffffL) + (b & 0xffffffffL) + cin; int res = (int) s; fc = (s >>> 32) != 0; fv = (((a ^ res) & (b ^ res)) < 0); nz(res); return res; }
    int sub(int a, int b) { return add(a, ~b, 1); }
    boolean cond(int c) {
        switch (c) {
            case 0: return fz; case 1: return !fz; case 2: return fc; case 3: return !fc; case 4: return fn; case 5: return !fn;
            case 6: return fv; case 7: return !fv; case 8: return fc && !fz; case 9: return !fc || fz;
            case 10: return fn == fv; case 11: return fn != fv; case 12: return !fz && fn == fv; case 13: return fz || fn != fv; default: return true;
        }
    }

    /** pc 에서 시작해 RET 에 닿거나 stop/명령수 한계까지 실행. 반환: 실행한 명령 수 */
    public int run(int pc, int max) {
        stop = false; int n = 0; r[15] = pc & ~1;
        while (n < max && !stop) {
            int p = r[15];
            if (p >= MAGIC && p < MAGIC_END) {
                if (p == RET) return n;
                hook.call(p);
                if (stop) { return n; }                      // 스레드 양보: pc 는 magic 주소에 남아 재개 시 bx lr 수행
                r[15] = r[14] & ~1; n++; continue;
            }
            int op = u16(p); r[15] = p + 2; n++;
            exec(op, p);
        }
        return n;
    }
    /** 스레드 재개 시 magic 주소에서 멈춘 상태를 정리: 호스트 호출을 마친 것으로 보고 lr 로 복귀 */
    public void resumeFromMagic() { r[15] = r[14] & ~1; }

    void exec(int op, int p) {
        switch (op >> 11) {
            case 0: { int rd = op & 7, rs = (op >> 3) & 7, sh = (op >> 6) & 31; int v = r[rs]; if (sh == 0) { r[rd] = v; nz(v); } else { fc = ((v >>> (32 - sh)) & 1) != 0; r[rd] = v << sh; nz(r[rd]); } return; }              // LSL imm
            case 1: { int rd = op & 7, rs = (op >> 3) & 7, sh = (op >> 6) & 31; int v = r[rs]; if (sh == 0) { fc = v < 0; r[rd] = 0; } else { fc = ((v >>> (sh - 1)) & 1) != 0; r[rd] = v >>> sh; } nz(r[rd]); return; } // LSR imm
            case 2: { int rd = op & 7, rs = (op >> 3) & 7, sh = (op >> 6) & 31; int v = r[rs]; if (sh == 0) { fc = v < 0; r[rd] = v >> 31; } else { fc = ((v >> (sh - 1)) & 1) != 0; r[rd] = v >> sh; } nz(r[rd]); return; }  // ASR imm
            case 3: { int rd = op & 7, rs = (op >> 3) & 7; int b = ((op >> 6) & 7); if ((op & 0x0400) == 0) b = r[b]; r[rd] = ((op & 0x0200) == 0) ? add(r[rs], b, 0) : sub(r[rs], b); return; }          // ADD/SUB reg/imm3
            case 4: { int rd = (op >> 8) & 7; r[rd] = op & 0xff; nz(r[rd]); return; }                                                           // MOV imm8
            case 5: { sub(r[(op >> 8) & 7], op & 0xff); return; }                                                                               // CMP imm8
            case 6: { int rd = (op >> 8) & 7; r[rd] = add(r[rd], op & 0xff, 0); return; }                                                       // ADD imm8
            case 7: { int rd = (op >> 8) & 7; r[rd] = sub(r[rd], op & 0xff); return; }                                                          // SUB imm8
            case 8: alu(op); return;
            case 9: { int rd = (op >> 8) & 7; r[rd] = r32(((p + 4) & ~3) + ((op & 0xff) << 2)); return; }                                       // LDR pc-rel
            case 10: case 11: ldstReg(op); return;
            case 12: { int rd = op & 7, rb = (op >> 3) & 7, i = (op >> 6) & 31; w32(r[rb] + (i << 2), r[rd]); return; }                         // STR imm
            case 13: { int rd = op & 7, rb = (op >> 3) & 7, i = (op >> 6) & 31; r[rd] = r32(r[rb] + (i << 2)); return; }                        // LDR imm
            case 14: { int rd = op & 7, rb = (op >> 3) & 7, i = (op >> 6) & 31; w8(r[rb] + i, r[rd]); return; }                                 // STRB imm
            case 15: { int rd = op & 7, rb = (op >> 3) & 7, i = (op >> 6) & 31; r[rd] = u8(r[rb] + i); return; }                                // LDRB imm
            case 16: { int rd = op & 7, rb = (op >> 3) & 7, i = (op >> 6) & 31; w16(r[rb] + (i << 1), r[rd]); return; }                         // STRH imm
            case 17: { int rd = op & 7, rb = (op >> 3) & 7, i = (op >> 6) & 31; r[rd] = u16(r[rb] + (i << 1)); return; }                        // LDRH imm
            case 18: { int rd = (op >> 8) & 7; w32(r[13] + ((op & 0xff) << 2), r[rd]); return; }                                                // STR sp
            case 19: { int rd = (op >> 8) & 7; r[rd] = r32(r[13] + ((op & 0xff) << 2)); return; }                                               // LDR sp
            case 20: { int rd = (op >> 8) & 7; r[rd] = ((p + 4) & ~3) + ((op & 0xff) << 2); return; }                                           // ADR
            case 21: { int rd = (op >> 8) & 7; r[rd] = r[13] + ((op & 0xff) << 2); return; }                                                    // ADD rd, sp, #
            case 22: case 23: misc(op); return;
            case 24: { int rb = (op >> 8) & 7, a = r[rb]; for (int i = 0; i < 8; i++) if ((op & (1 << i)) != 0) { w32(a, r[i]); a += 4; } r[rb] = a; return; }   // STMIA
            case 25: { int rb = (op >> 8) & 7, a = r[rb]; boolean wb = (op & (1 << rb)) == 0; for (int i = 0; i < 8; i++) if ((op & (1 << i)) != 0) { r[i] = r32(a); a += 4; } if (wb) r[rb] = a; return; } // LDMIA
            case 26: case 27: { int c = (op >> 8) & 15; if (c == 15) throw new IllegalStateException("svc"); if (cond(c)) r[15] = p + 4 + ((byte) op << 1); return; }
            case 28: { int off = (op & 0x7ff) << 21 >> 20; r[15] = p + 4 + off; return; }                                                         // B
            case 29: { /* BLX suffix(미사용) */ throw new IllegalStateException("blx " + Integer.toHexString(op)); }
            case 30: { int hi = ((op & 0x7ff) << 21) >> 9; r[14] = p + 4 + hi; return; }                                                          // BL prefix
            case 31: { int off = (op & 0x7ff) << 1; int t = r[14] + off; r[14] = (p + 2) | 1; r[15] = t & ~1; return; }                          // BL suffix
        }
    }

    void alu(int op) {
        if ((op & 0x0400) != 0) { hireg(op); return; }
        int rd = op & 7, rs = (op >> 3) & 7, a = r[rd], b = r[rs], o = (op >> 6) & 15, res;
        switch (o) {
            case 0: r[rd] = a & b; nz(r[rd]); return; case 1: r[rd] = a ^ b; nz(r[rd]); return;
            case 2: { int s = b & 255; if (s == 0) {} else if (s < 32) { fc = ((a >>> (32 - s)) & 1) != 0; a <<= s; } else { fc = s == 32 && (a & 1) != 0; a = 0; } r[rd] = a; nz(a); return; }
            case 3: { int s = b & 255; if (s == 0) {} else if (s < 32) { fc = ((a >>> (s - 1)) & 1) != 0; a >>>= s; } else { fc = s == 32 && a < 0; a = 0; } r[rd] = a; nz(a); return; }
            case 4: { int s = b & 255; if (s == 0) {} else if (s < 32) { fc = ((a >> (s - 1)) & 1) != 0; a >>= s; } else { fc = a < 0; a >>= 31; } r[rd] = a; nz(a); return; }
            case 5: r[rd] = add(a, b, fc ? 1 : 0); return; case 6: r[rd] = add(a, ~b, fc ? 1 : 0); return;
            case 7: { int s = b & 255; if (s != 0) { s &= 31; if (s == 0) fc = a < 0; else { fc = ((a >>> (s - 1)) & 1) != 0; a = (a >>> s) | (a << (32 - s)); } } r[rd] = a; nz(a); return; }
            case 8: nz(a & b); return; case 9: r[rd] = sub(0, b); return; case 10: sub(a, b); return; case 11: add(a, b, 0); return;
            case 12: r[rd] = a | b; nz(r[rd]); return; case 13: r[rd] = a * b; nz(r[rd]); return; case 14: r[rd] = a & ~b; nz(r[rd]); return; default: r[rd] = ~b; nz(r[rd]); return;
        }
    }
    void hireg(int op) {
        int o = (op >> 8) & 3, rd = (op & 7) | ((op >> 4) & 8), rs = (op >> 3) & 15;
        int pcv = r[15] + 2;                                       // 현재 명령 주소 + 4
        int b = rs == 15 ? pcv : r[rs];
        switch (o) {
            case 0: { int a = rd == 15 ? pcv : r[rd]; int v = a + b; if (rd == 15) r[15] = v & ~1; else r[rd] = v; return; }
            case 1: { int a = rd == 15 ? pcv : r[rd]; sub(a, b); return; }
            case 2: if (rd == 15) r[15] = b & ~1; else r[rd] = b; return;
            default: { if ((op & 0x80) != 0) { r[14] = r[15] | 1; }   // BLX reg
                if ((b & 1) == 0) throw new IllegalStateException(String.format("ARM-mode branch to %08x from %08x", b, r[15])); r[15] = b & ~1; return; }
        }
    }
    void ldstReg(int op) {
        int rd = op & 7, rb = (op >> 3) & 7, ro = (op >> 6) & 7; int a = r[rb] + r[ro];
        if ((op & 0x0200) == 0) {
            switch ((op >> 10) & 3) {
                case 0: w32(a, r[rd]); return; case 1: w8(a, r[rd]); return; case 2: r[rd] = r32(a); return; default: r[rd] = u8(a); return;
            }
        }
        switch ((op >> 10) & 3) {
            case 0: w16(a, r[rd]); return; case 1: r[rd] = (byte) u8(a); return; case 2: r[rd] = u16(a); return; default: r[rd] = (short) u16(a); return;
        }
    }
    void misc(int op) {
        if ((op & 0xff00) == 0xb000) { int i = (op & 0x7f) << 2; r[13] += (op & 0x80) != 0 ? -i : i; return; }
        if ((op & 0x0600) == 0x0400) {                                                                      // PUSH/POP
            boolean lrpc = (op & 0x100) != 0;
            if ((op & 0x0800) == 0) {                                                                       // PUSH
                int n = Integer.bitCount(op & 0xff) + (lrpc ? 1 : 0); int a = r[13] - 4 * n; r[13] = a;
                for (int i = 0; i < 8; i++) if ((op & (1 << i)) != 0) { w32(a, r[i]); a += 4; }
                if (lrpc) w32(a, r[14]);
            } else {
                int a = r[13];
                for (int i = 0; i < 8; i++) if ((op & (1 << i)) != 0) { r[i] = r32(a); a += 4; }
                if (lrpc) { int v = r32(a); a += 4; r[13] = a; if ((v & 1) == 0) throw new IllegalStateException(String.format("ARM-mode pop pc %08x", v)); r[15] = v & ~1; return; }
                r[13] = a;
            }
            return;
        }
        if ((op & 0xffc0) == 0xb240 || (op & 0xff00) == 0xb200) {                                           // SXTH/SXTB/UXTH/UXTB
            int rd = op & 7, rs = (op >> 3) & 7;
            switch ((op >> 6) & 3) { case 0: r[rd] = (short) r[rs]; break; case 1: r[rd] = (byte) r[rs]; break; case 2: r[rd] = r[rs] & 0xffff; break; default: r[rd] = r[rs] & 0xff; }
            return;
        }
        if ((op & 0xff00) == 0xba00) { int rd = op & 7, rs = (op >> 3) & 7, v = r[rs]; switch ((op >> 6) & 3) { case 0: r[rd] = Integer.reverseBytes(v); break; case 1: r[rd] = ((v & 0x00ff00ff) << 8 | (v >>> 8) & 0x00ff00ff); break; default: r[rd] = (short) (((v & 0xff) << 8) | ((v >> 8) & 0xff)); } return; }
        if ((op & 0xff00) == 0xbe00 || (op & 0xff00) == 0xbf00) return;                                    // BKPT/NOP
        throw new IllegalStateException(String.format("unsupported op %04x at %08x", op, r[15] - 2));
    }
}
