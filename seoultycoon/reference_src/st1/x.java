/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class x
extends aq {
    Image o;
    Image c;
    Image C;
    Image E;
    Image D;
    int[] g;
    int i;
    int p;
    int t;
    int B;
    d s;
    boolean w;
    boolean z;
    w q;

    x(aq aq2) {
        super(aq2);
    }

    void b() {
        int n;
        try {
            this.c = this.n.c("c0");
            this.C = this.n.c("tn0");
            this.E = this.n.c("c1");
        }
        catch (Exception exception) {
            aq.r();
        }
        this.q = new w(this, this.C, 0, 41, 4, 7);
        this.g = new int[6];
        this.g[this.i++] = 0;
        int n2 = 1;
        short s2 = this.n.d.b;
        for (n = 1; n < 6; ++n) {
            if ((s2 & n2) != 0) {
                this.g[this.i++] = n;
            }
            n2 <<= 1;
        }
        for (n = this.i; n < 6; ++n) {
            this.g[n] = 6;
        }
        this.z = false;
        for (n = 0; n < 5; ++n) {
            if (this.n.d(n)) continue;
            this.z = true;
            break;
        }
        if (this.z) {
            this.k(728);
        } else if (!this.a(9, (int)this.n.d.e, 9)) {
            this.c();
        }
        this.u();
    }

    void d(int n2) {
        switch (n2) {
            case 50: 
            case 141: {
                if (this.z) {
                    return;
                }
                if (this.t > 0) {
                    --this.t;
                }
                this.y();
                return;
            }
            case 56: 
            case 146: {
                if (this.z) {
                    return;
                }
                if (this.w) {
                    ++this.t;
                }
                this.y();
                return;
            }
            case 52: 
            case 142: {
                this.p = (this.p + 5) % 6;
                break;
            }
            case 54: 
            case 145: {
                this.p = (this.p + 1) % 6;
                break;
            }
            case 53: 
            case 131: 
            case 148: {
                if (this.z) {
                    if (this.g[this.p] < 1 || this.g[this.p] > 5) {
                        return;
                    }
                    if (this.n.d(this.g[this.p] - 1)) {
                        return;
                    }
                    this.B = 4;
                    p p2 = new p(this);
                    p2.p = -this.n.d.C - 1;
                    p2.o = this.g[this.p] < 5 ? -20 : -50;
                    this.b(new ac(this, p2, 4, "\uc5f0\ubd09\uc9c0\uae09"));
                    return;
                }
                if (this.g[this.p] == 6 || this.g[this.p] == this.n.d.e) {
                    return;
                }
                this.B = 1;
                this.b(new n(this, this.g[this.p], 332 + this.g[this.p]));
                return;
            }
            case 8: {
                if (this.z) {
                    for (int i = 0; i < 5; ++i) {
                        if (this.n.d(i)) continue;
                        this.B = 2;
                        this.k(730);
                        return;
                    }
                }
                this.v();
                return;
            }
            default: {
                return;
            }
        }
        this.z();
    }

    /*
     * WARNING - void declaration
     */
    void z() {
        void var4_10;
        int n2;
        int n3 = this.d - 60;
        int n4 = this.v - 56;
        int by2 = this.g[this.p];
        boolean bl = by2 == 0 && (this.n.d.m & 2) != 0;
        this.a(this.o, n3, n4);
        this.c(n4, 112, this.p * 15, 40, 0);
        if (bl) {
            this.a(this.E, n3 + 10 + 15 * this.p, n4 + 5, 76, 0, 19, 10);
        } else {
            this.a(this.c, n3 + 5 + 15 * this.p, n4 + 5, 0, by2 * 10, 29, 10);
        }
        this.a(this.C, n3 + 52, n4 + 23, 0, 20, 32, 21);
        this.a(this.C, n3 + 81, n4 + 23, 2, 20, 36, 21);
        this.a(this.C, n3 + 56, n4 + 28, 19, 10, 19, 10);
        int n5 = 19;
        if (by2 == 0) {
            boolean by = false;
            n2 = 10;
        } else if (by2 == 6) {
            int n6 = 40;
            n2 = 39;
            n5 = 6;
        } else if (by2 == 5) {
            int n7 = 46;
            n2 = 20;
            n5 = 29;
        } else {
            int n8 = by2 * 19 - 19;
            n2 = 0;
        }
        this.a(this.C, n3 + 80, n4 + 28, (int)var4_10, n2, n5, 10);
        this.a(this.C, n3 + 52, n4 + 45, 0, 20, 65, 21);
        this.a(this.C, n3 + 81, n4 + 45, 2, 20, 36, 21);
        this.a(this.C, n3 + 56, n4 + 50, 38, 10, 19, 10);
        this.a(this.C, n3 + 91, n4 + 50, 57, 10, 19, 10);
        if (by2 == 6) {
            this.a(this.C, n3 + 80, n4 + 50, (int)var4_10, n2, n5, 10);
        } else {
            void var4_14;
            if (by2 == 0) {
                boolean bl2 = false;
            } else if (by2 < 5) {
                int n9 = 20;
            } else {
                int n10 = 50;
            }
            this.q.a((int)var4_14, n3 + 84, n4 + 51, 1);
        }
        if (by2 < 6) {
            if (by2 == this.n.d.e) {
                this.a(this.C, n3 + 52, n4 + 68, 46, 30, 31, 9);
            } else if (!this.z) {
                this.a(this.C, n3 + 52, n4 + 69, 46, 39, 14, 7);
                this.a(this.C, n3 + 67, n4 + 68, 46, 30, 31, 9);
            }
        }
        this.e.setColor(9577472);
        this.e.drawRect(n3 + 5, n4 + 24, 44, 54);
        this.e.setColor(0x494955);
        this.e.drawRect(n3 + 4, n4 + 23, 44, 54);
        this.e.setColor(0xFFFFFF);
        this.e.drawRect(n3 + 5, n4 + 24, 42, 52);
        this.e.setColor(11983871);
        this.e.fillRect(n3 + 6, n4 + 25, 41, 51);
        if (bl) {
            int n11 = 15;
        } else {
            int n12 = by2;
        }
        this.e.setClip(n3 + 6, n4 + 25, 41, 51);
        try {
            void var4_17;
            if (var4_17 == 6) {
                this.e.drawImage(this.n.c("b19"), n3 + 10, n4 + 26, 20);
            } else {
                this.e.drawImage(this.n.c("b" + (int)var4_17), n3 + 31, n4 + 53, 3);
            }
        }
        catch (Exception exception) {
            // empty catch block
        }
        this.e.setClip(0, 0, this.k, this.j);
        this.a(this.C, n3 + 7, n4 + 19, 38, 20, 8, 17);
        if (this.D == null) {
            this.D = this.a(n3 + 5, n4 + 81, 109, 27);
        }
        if (this.z) {
            this.e.setColor(0xFFFFFF);
            String string = by2 == 6 ? "" : (by2 == 0 || this.n.d(by2 - 1) ? "\uc5f0\ubd09\uc9c0\uae09\uc644\ub8cc" : "OK : \uc5f0\ubd09\uc9c0\uae09");
            this.a(string, this.d, n4 + 87, 1);
            this.p();
        } else {
            if (by2 == 6) {
                this.s = null;
            } else {
                void var4_20;
                if (bl) {
                    int n13 = 779;
                } else {
                    int n14 = 326 + by2;
                }
                this.s = this.n.g((int)var4_20);
                this.s.a(100 / this.n.e, false);
                this.t = 0;
            }
            this.y();
        }
    }

    void y() {
        if (this.s != null) {
            int n2;
            int n3 = this.v + 25;
            this.a(this.D, this.d - 55, n3);
            this.e.setColor(0xFFFFFF);
            this.s.h();
            for (n2 = 0; n2 < this.t; ++n2) {
                this.s.f();
            }
            for (n2 = 0; n2 < 2; ++n2) {
                if (this.s.c() <= 0) continue;
                this.a(this.s.f(), this.d - 55, n3, 4);
                n3 += this.n.l + 2;
            }
            boolean bl = this.w = this.s.c() > 0;
            if (this.t > 0) {
                this.a(this.n.n[0], this.d + 45, this.v + 25, 105, 18, 9, 6);
            }
            if (this.w) {
                this.a(this.n.n[0], this.d + 45, this.v + 43, 114, 18, 9, 6);
            }
        }
        this.p();
    }

    void x() {
        int n2 = this.B;
        this.B = 0;
        switch (n2) {
            case 0: {
                this.c();
                return;
            }
            case 1: {
                this.n.d.e = (byte)this.g[this.p];
                this.n();
                break;
            }
            case 2: {
                this.B = 3;
                this.b(new v(this, 731, 1, 0, 0));
                return;
            }
            case 3: {
                if (this.A != 100) break;
                int n3 = 1;
                for (int i = 0; i < 5; ++i) {
                    if (!this.n.d(i)) {
                        this.n.d.b = (short)(this.n.d.b ^ n3);
                    }
                    n3 <<= 1;
                }
                if (this.n.d.e != 0 && !this.n.h(this.n.d.e - 1)) {
                    this.n.d.e = 0;
                }
                this.n();
                this.v();
                return;
            }
            case 4: {
                int n4;
                if (this.A != 100) break;
                int n5 = n4 = this.g[this.p] < 5 ? 20 : 50;
                if (this.n.d.C >= n4) {
                    this.n.n(-n4);
                    int n6 = this.g[this.p] - 1 << 1;
                    this.n.d.f = (short)(this.n.d.f ^ this.n.d.f & 3 << n6);
                    this.n.d.f = (short)(this.n.d.f | this.n.d.I << n6);
                    this.n();
                    this.B = 5;
                    this.b(new n(this, this.g[this.p], 332 + this.g[this.p]));
                } else {
                    this.B = 5;
                    this.k(736);
                }
                return;
            }
        }
        this.z();
    }

    void c() {
        this.f();
        int n2 = 5;
        for (int i = 75; i >= 0; i -= 15) {
            this.b(this.v - 54, i, 36);
            this.a(this.c, this.d - 57 + i, this.v - 51, 0, this.g[n2] * 10, 29, 10);
            --n2;
        }
        this.o = this.a(this.d - 60, this.v - 56, 120, 16);
        this.z();
    }
}
