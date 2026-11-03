/*
 * Decompiled with CFR 0.152.
 */
class ag
extends o {
    byte p;
    boolean o;

    ag(aq aq2, int n) {
        super(aq2);
        boolean bl = this.o = n < 90;
        if (!this.o) {
            n -= 100;
        }
        this.p = (byte)n;
    }

    void b() {
        int n;
        this.w();
        if (this.o) {
            this.n.d.T = 0;
            this.p = ag.a(this.n.d, this.p);
            this.n.d.w = (byte)(this.p + 1);
            this.n.d.W = this.c();
            this.n.d.M = this.n.d.M == 0 ? this.n.d.w : (byte)Math.min(this.n.d.M, this.n.d.w);
            this.n.d.s = Math.max(this.n.d.s, this.n.d.W);
            if (this.p < 7) {
                this.n.d.S = Math.min(1000000, this.n.d.S + 1);
            }
            this.n.d.U = (short)(this.n.d.U | 1 << this.p);
            this.n();
        }
        switch (this.p) {
            case 7: {
                this.a(0xFF0000);
                break;
            }
            case 8: {
                this.a(0);
                break;
            }
            case 9: {
                this.a(0);
                break;
            }
            default: {
                this.l();
            }
        }
        int n2 = Math.min(144, this.j);
        int n3 = this.m(n2 - 66);
        try {
            this.e.drawImage(this.n.c("v" + this.p), this.d - 50, n3 + 27, 20);
        }
        catch (Exception exception) {
            // empty catch block
        }
        int n4 = n2 - 94;
        int n5 = n3 + 90 + n4;
        d d2 = this.n.g(717 + this.p);
        d2.a(120 / this.n.e, false);
        String[] stringArray = new String[d2.d()];
        for (n = 0; n < stringArray.length; ++n) {
            stringArray[n] = d2.f();
        }
        n = 1;
        int n6 = 200;
        this.e(0);
        while (n != 0) {
            if (!this.h()) {
                n6 >>= 1;
                this.o();
            }
            this.e.setColor(0xB6B6AA);
            this.e.fillRect(this.d - 60, n3 + 90, 120, n4);
            this.e.setColor(0);
            this.e.setClip(this.d - 60, n3 + 90, 120, n4);
            int n7 = n5;
            for (int i = 0; i < stringArray.length && n7 < n3 + 90 + n4; n7 += this.n.l + 2, ++i) {
                if (n7 <= n3 + 90 - this.n.l) continue;
                this.a(stringArray[i], this.d, n7, 1);
            }
            n = n7 > n3 + 90 - this.n.l ? 1 : 0;
            this.p();
            this.e(n6);
            n5 -= 2;
        }
        this.e.setColor(0);
        this.a("THE END", this.d, n3 + 90 + (n4 - this.n.l >> 1), 1);
        this.p();
        this.u();
        this.o();
    }

    void d(int n) {
        switch (n) {
            case 8: 
            case 53: 
            case 131: 
            case 148: {
                if (this.o) {
                    this.a(new g(this));
                } else {
                    this.v();
                }
                return;
            }
        }
    }

    static byte a(aj aj2, int n) {
        int n2;
        int n3;
        if (n >= 0) {
            return (byte)n;
        }
        if (aj2.q >= 90) {
            for (n3 = 0; n3 < 4 && aj2.z[n3] >= 100; ++n3) {
            }
            if (n3 >= 4) {
                for (n3 = 0; n3 < 4 && aj2.r[n3] >= 90; ++n3) {
                }
                if (n3 >= 4 && aj2.N == 4095) {
                    return 0;
                }
            }
        }
        if (aj2.q >= 80) {
            n2 = 0;
            for (n3 = 0; n3 < 4; ++n3) {
                if (aj2.z[n3] >= 100) continue;
                ++n2;
            }
            if (n2 < 2) {
                for (n3 = 0; n3 < 4 && aj2.z[n3] >= 70; ++n3) {
                }
                if (n3 >= 4) {
                    n2 = 0;
                    for (n3 = 0; n3 < 4; ++n3) {
                        if (aj2.r[n3] >= 90) continue;
                        ++n2;
                    }
                    if (n2 < 2) {
                        for (n3 = 0; n3 < 4 && aj2.r[n3] >= 70; ++n3) {
                        }
                        if (n3 >= 4) {
                            return 1;
                        }
                    }
                }
            }
        }
        if (aj2.q >= 50) {
            n2 = 0;
            for (n3 = 0; n3 < 4; ++n3) {
                if (aj2.z[n3] >= 100) continue;
                ++n2;
            }
            if (n2 < 4) {
                for (n3 = 0; n3 < 4 && aj2.z[n3] >= 70; ++n3) {
                }
                if (n3 >= 4) {
                    n2 = 0;
                    for (n3 = 0; n3 < 4; ++n3) {
                        if (aj2.r[n3] >= 90) continue;
                        ++n2;
                    }
                    if (n2 < 4) {
                        for (n3 = 0; n3 < 4 && aj2.r[n3] >= 70; ++n3) {
                        }
                        if (n3 >= 4) {
                            return 2;
                        }
                    }
                }
            }
        }
        for (n3 = 0; n3 < 4 && aj2.z[n3] >= 100; ++n3) {
        }
        if (n3 >= 4) {
            return 5;
        }
        for (n3 = 0; n3 < 4 && aj2.r[n3] >= 100; ++n3) {
        }
        if (n3 >= 4) {
            return 6;
        }
        for (n3 = 0; n3 < 4 && aj2.z[n3] >= 50; ++n3) {
        }
        if (n3 >= 4) {
            for (n3 = 0; n3 < 4 && aj2.r[n3] >= 50; ++n3) {
            }
            if (n3 >= 4) {
                return 3;
            }
        }
        return 4;
    }

    int c() {
        int n;
        int n2 = 0;
        int n3 = 0;
        for (n = 0; n < 22; ++n) {
            if (!this.n.k(n)) continue;
            ++n3;
        }
        n2 += n3 * n3;
        n3 = 0;
        for (n = 0; n < 12; ++n) {
            if ((this.n.d.N & 1 << n) == 0) continue;
            ++n3;
        }
        n2 += n3 * n3;
        int n4 = 0;
        for (n = 0; n < 4; ++n) {
            for (n3 = 0; n3 < 6; ++n3) {
                if (this.n.d.o[n][n3] <= 0) continue;
                ++n4;
            }
        }
        n2 += n4 * n4;
        n4 = 0;
        for (n = 0; n < 4; ++n) {
            for (n3 = 0; n3 < 6; ++n3) {
                if (this.n.d.o[n][n3] <= 1) continue;
                ++n4;
            }
        }
        n2 += n4 * n4 * 3;
        n2 += (this.n.d.v[11] + this.n.d.v[23] + this.n.d.v[35] + this.n.d.v[47]) * 3;
        for (n = 0; n < 4; ++n) {
            if (!this.n.h(n)) continue;
            n2 += 10;
        }
        if (this.n.h(4)) {
            n2 += 40;
        }
        n = this.n.d.w < 5 ? (5 - this.n.d.w) * 200 : (this.n.d.w < 7 ? 100 : 0);
        n2 += n;
        if (this.p < 7) {
            n2 += n * this.n.d.S / 10;
        }
        n = 0;
        for (n3 = n2; n3 > 0; n3 /= 10) {
            n += n3 % 10;
        }
        n = (19 - n % 20) * 5;
        return n2 * 100 + n;
    }
}
