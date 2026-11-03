/*
 * Decompiled with CFR 0.152.
 */
class g
extends aq {
    String[] g = new String[]{"\uac74\ubb3c", "\uac74\ubb3cUP", "\uc815\ucc45", "\ud64d\ubcf4", "\uc9c0\uc9c0\ub3c4", "\ucd1d\uc778\uad6c", "\ub4f1\uc6a9\ube44\uc11c", "\uc784\uae30"};
    String[] o = new String[9];
    int q;
    int i;
    int z = Math.min(this.k, 176);
    int s = Math.min(this.j, 178);
    int w;
    int p;
    boolean c;
    boolean t;

    g(aq aq2) {
        super(aq2);
        int n;
        int n2;
        this.q = this.d - (this.z >> 1);
        this.i = this.v - (this.s >> 1);
        int n3 = 0;
        for (n2 = 0; n2 < 4; ++n2) {
            for (n = 0; n < 6; ++n) {
                if (this.n.d.o[n2][n] <= 0) continue;
                ++n3;
            }
        }
        n3 = n3 * 1000 / 24;
        this.o[0] = n3 / 10 + "." + n3 % 10 + "%";
        n3 = 0;
        for (n2 = 0; n2 < 4; ++n2) {
            for (n = 0; n < 6; ++n) {
                if (this.n.d.o[n2][n] <= 1) continue;
                ++n3;
            }
        }
        n3 = n3 * 1000 / 13;
        this.o[1] = n3 / 10 + "." + n3 % 10 + "%";
        n3 = 0;
        for (n2 = 0; n2 < 22; ++n2) {
            if (!this.n.k(n2)) continue;
            ++n3;
        }
        n3 = n3 * 1000 / 22;
        this.o[2] = n3 / 10 + "." + n3 % 10 + "%";
        n3 = 0;
        n = 1;
        for (n2 = 0; n2 < 12; ++n2) {
            if ((this.n.d.N & n) != 0) {
                ++n3;
            }
            n <<= 1;
        }
        n3 = n3 * 1000 / 12;
        this.o[3] = n3 / 10 + "." + n3 % 10 + "%";
        this.o[4] = this.n.d.q + "%";
        n3 = 0;
        for (n2 = 0; n2 < 4; ++n2) {
            n3 += this.n.d.P[n2];
        }
        this.o[5] = n3 + "\ub9cc\uba85";
        n3 = 0;
        for (n2 = 0; n2 < 5; ++n2) {
            if (!this.n.h(n2)) continue;
            ++n3;
        }
        this.o[6] = n3 + "\uba85";
        this.o[7] = this.n.d.S + "\ud68c\uc9f8";
        this.o[8] = "Score " + this.n.d.W;
    }

    void b() {
        this.a(this.q, this.i, this.z, this.s, 1, false);
        this.a("\uacbd\uc601\uacb0\uacfc", this.d, this.i + 12, 1, 0xFFFFFF);
        this.u();
        this.c(0, 0, 300);
    }

    void d(int n) {
        switch (n) {
            case 50: 
            case 52: 
            case 141: 
            case 142: {
                if (this.w > 0) {
                    --this.w;
                    break;
                }
                return;
            }
            case 54: 
            case 56: 
            case 145: 
            case 146: {
                if (this.t) {
                    ++this.w;
                    break;
                }
                return;
            }
            case 8: 
            case 53: 
            case 131: 
            case 148: {
                this.a(new u(this));
                return;
            }
            default: {
                return;
            }
        }
        this.c = false;
        this.c(0, 0, 300);
    }

    void b(int n) {
        this.c();
    }

    void c() {
        this.a(this.q + 5, this.i + 30, this.z - 10, this.s - 35, 85);
        this.e.setColor(0xFFFFFF);
        int n = this.i + 35;
        int n2 = n + this.s - 44;
        this.e.setClip(this.q + 7, this.i + 32, this.z - 14, this.s - 39);
        this.p = this.w;
        while (this.p < 10 && n < n2) {
            switch (this.p) {
                case 9: {
                    this.a(this.o[8], this.d, n, 1);
                    break;
                }
                case 8: {
                    this.e.drawLine(this.d - 52, n + 6, this.d + 52, n + 6);
                    break;
                }
                default: {
                    this.a(this.g[this.p], this.d - 50, n, 4);
                    this.a(this.o[this.p], this.d + 50, n, 8);
                }
            }
            n += this.n.l + 4;
            ++this.p;
        }
        this.t = this.p < 10 || (n -= 4) >= n2;
        this.e.setClip(0, 0, this.k, this.j);
        this.c ^= true;
        if (this.c) {
            if (this.w > 0) {
                this.a(this.n.n[0], this.q + this.z - 15, this.i + 31, 115, 24, 9, 6);
            }
            if (this.t) {
                this.a(this.n.n[0], this.q + this.z - 15, this.i + this.s - 12, 115, 30, 9, 6);
            }
        }
        this.p();
    }
}
