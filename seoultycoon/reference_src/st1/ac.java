/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class ac
extends aq {
    p w;
    int F;
    int o;
    int z;
    int g;
    int s;
    int c;
    int G;
    int E;
    int t;
    int J;
    Image i;
    Image B;
    Image I;
    Image H;
    Image C;
    String q;
    w p;
    boolean D;

    ac(aq aq2, p p2, int n, Image image, int n2, int n3, int n4) {
        super(aq2);
        this.a = aq2;
        this.w = p2;
        this.F = n;
        this.C = image;
        this.c = n2;
        this.G = n3;
        this.E = n4;
    }

    ac(aq aq2, p p2, int n, String string) {
        super(aq2);
        this.a = aq2;
        this.w = p2;
        this.F = n;
        this.q = string;
        this.E = string.getBytes().length * this.n.e - 1;
    }

    void b() {
        if (this.w.b() == 0) {
            this.v();
            return;
        }
        this.t = this.n.f[this.F + 1793];
        this.J = this.n.f[this.F + 1800];
        this.s = this.w.l + 46;
        this.g = this.v - (this.s >> 1);
        this.D = this.w.l < this.w.m;
        this.I = this.n.n[2];
        this.p = new w(this, this.n.n[1], 0, 0, 5, 8);
        this.i = this.a(this.d - 60, this.g, 120, this.s);
        this.c(this.g, this.s, 3, this.E + 12, this.F == 2 ? 1 : 0);
        if (this.C != null) {
            this.a(this.C, this.d - 53, this.g + 4, this.c, this.G, this.E, 10);
        } else {
            this.e.setColor(0xFFFFFF);
            this.a(this.q, this.d - 53, this.g + 4, 4);
        }
        if (this.D) {
            this.B = this.a(this.d - 56, this.g + 24, 112, this.w.l);
            this.H = this.a(this.d + 39, this.g + this.s - 21, 18, 6);
        }
        this.c();
        this.u();
    }

    void d(int n) {
        switch (n) {
            case 50: 
            case 141: {
                if (!this.D) {
                    return;
                }
                this.z = Math.max(0, this.z - 16);
                break;
            }
            case 56: 
            case 146: {
                if (!this.D) {
                    return;
                }
                this.z = Math.min(this.z + 16, this.w.m - this.w.l);
                break;
            }
            case 52: 
            case 142: {
                if (this.F == 0) {
                    return;
                }
                this.o = 0;
                break;
            }
            case 54: 
            case 145: {
                if (this.F == 0) {
                    return;
                }
                this.o = 1;
                break;
            }
            case 53: 
            case 131: 
            case 148: {
                this.y();
                return;
            }
            case 8: {
                if (this.z()) {
                    this.k(711);
                    return;
                }
                this.A();
                return;
            }
            default: {
                return;
            }
        }
        this.c();
    }

    void x() {
        this.p();
    }

    void c() {
        this.b(this.g + this.s - 19, this.t, this.J, this.o);
        if (this.D) {
            int n = this.d + 48;
            int n2 = this.g + this.s - 21;
            this.a(this.H, n - 9, n2);
            if (this.z < this.w.m - this.w.l) {
                this.a(this.n.n[0], n, n2, 114, 18, 9, 6);
                n -= 9;
            }
            if (this.z > 0) {
                this.a(this.n.n[0], n, n2, 105, 18, 9, 6);
            }
            this.a(this.B, this.d - 56, this.g + 24);
            this.e.setClip(this.d - 56, this.g + 24, 112, this.w.l);
        }
        this.m(this.g + 24 - this.z);
        this.p();
    }

    void m(int n) {
        if (this.w.h[0]) {
            this.e.setColor(0xFFFFFF);
            this.a(this.w.j, this.d - 54, n + 3, 4);
            n += 18;
        }
        if (this.w.h[1]) {
            this.a(this.d - 56, n, 112, 15, 7162112);
            this.a(this.d - 23, n + 3, 27, 10, 4793344);
            if (this.w.p > 0) {
                this.a(this.I, this.d - 54, n + 3, 29, 53, 29, 10);
                this.a(this.I, this.d + 5, n + 3, 57, 10, 18, 10);
            } else {
                this.a(this.I, this.d - 54, n + 3, 29, 53, 10, 10);
                this.a(this.I, this.d - 44, n + 3, 58, 53, 18, 10);
                this.a(this.I, this.d + 5, n + 3, 0, 23, 18, 10);
            }
            this.c(this.w.p + (this.w.p < 0 ? 1 : 0), this.d + 1, n + 4, 0);
            n += 18;
        }
        if (this.w.h[2]) {
            this.a(this.d - 56, n, 112, 29, 7162112);
            this.a(this.d - 34, n + 3, 27, 10, 4793344);
            this.a(this.d - 34, n + 17, 21, 10, 4793344);
            this.a(this.d + 20, n + 17, 21, 10, 4793344);
            this.a(this.I, this.d - 51, n + 1, 0, 10, 13, 13);
            this.a(this.I, this.d - 51, n + 15, 13, 10, 13, 13);
            this.a(this.I, this.d + 2, n + 15, 26, 10, 13, 13);
            this.a(this.I, this.d - 6, n + 3, 0, 23, 18, 10);
            this.c(this.w.o, this.d - 10, n + 4, 23);
            this.c(this.w.n, this.d - 16, n + 18, 4);
            this.c(this.w.c, this.d + 38, n + 18, 4);
            n += 32;
        }
        if (this.w.h[3]) {
            this.a(this.d - 56, n, 112, 15, 7162112);
            this.a(this.d - 11, n + 3, 21, 10, 4793344);
            this.a(this.I, this.d - 51, n + 3, 18, 23, 39, 10);
            this.a(this.I, this.d + 11, n + 3, 57, 23, 17, 10);
            this.c(this.w.g, this.d + 7, n + 4, 0);
            n += 18;
        }
        if (this.w.h[4]) {
            this.a(this.d - 56, n, 112, 29, 7162112);
            this.a(this.d - 34, n + 3, 21, 10, 4793344);
            this.a(this.d + 20, n + 3, 21, 10, 4793344);
            this.a(this.d - 34, n + 17, 21, 10, 4793344);
            this.a(this.d + 20, n + 17, 21, 10, 4793344);
            this.a(this.I, this.d - 54, n + 3, 0, 33, 19, 10);
            this.a(this.I, this.d + 1, n + 3, 19, 33, 19, 10);
            this.a(this.I, this.d - 54, n + 17, 38, 33, 19, 10);
            this.a(this.I, this.d + 1, n + 17, 57, 33, 18, 10);
            this.a(this.I, this.d - 12, n + 4, 53, 10, 4, 7);
            this.a(this.I, this.d + 42, n + 4, 53, 10, 4, 7);
            this.a(this.I, this.d - 12, n + 18, 53, 10, 4, 7);
            this.a(this.I, this.d + 42, n + 18, 53, 10, 4, 7);
            this.c(this.w.f[0], this.d - 16, n + 4, 9);
            this.c(this.w.f[1], this.d + 38, n + 4, 9);
            this.c(this.w.f[2], this.d - 16, n + 18, 9);
            this.c(this.w.f[3], this.d + 38, n + 18, 9);
            n += 32;
        }
        if (this.w.h[5]) {
            this.a(this.d - 56, n, 112, 29, 7162112);
            this.a(this.d - 34, n + 3, 21, 10, 4793344);
            this.a(this.d - 34, n + 17, 21, 10, 4793344);
            this.a(this.I, this.d - 54, n + 3, 0, 43, 18, 10);
            this.a(this.I, this.d - 54, n + 17, 18, 43, 18, 10);
            this.a(this.I, this.d - 12, n + 3, 57, 10, 18, 10);
            this.a(this.I, this.d - 12, n + 18, 53, 10, 4, 7);
            this.c(this.w.b, this.d - 16, n + 4, 23);
            this.c(this.w.k, this.d - 16, n + 18, 9);
            n += 32;
        }
        if (this.w.h[6]) {
            this.a(this.d - 56, n, 112, 29, 7162112);
            this.a(this.d - 34, n + 3, 21, 10, 4793344);
            this.a(this.d - 23, n + 17, 21, 10, 4793344);
            this.a(this.I, this.d - 54, n + 3, 36, 43, 19, 10);
            this.a(this.I, this.d - 54, n + 17, 0, 53, 29, 10);
            this.a(this.I, this.d - 12, n + 4, 53, 10, 4, 7);
            this.a(this.I, this.d - 1, n + 18, 53, 10, 4, 7);
            this.c(this.w.i, this.d - 16, n + 4, 9);
            this.c(this.w.d, this.d - 5, n + 18, 9);
        }
    }

    void c(int n, int n2, int n3, int n4) {
        if (n == 0) {
            this.e.setColor(0xFFFFFF);
            this.e.drawLine(n2 - 4, n3 + 4, n2, n3 + 4);
            return;
        }
        this.p.a(n, n2, n3, 0);
        if (n4 == 0) {
            return;
        }
        this.a(this.I, n2 + n4, n3 - 1, n < 0 ? 39 : 46, 10, 7, 10);
    }

    void y() {
        if (this.z() && this.o != 0) {
            this.k(711);
            return;
        }
        this.a.A = 100 + this.o;
        this.A();
    }

    void A() {
        this.a(this.i, this.d - 60, this.g);
        this.v();
    }

    boolean z() {
        return this.F == 1 && (this.n.d.x & 4) == 0;
    }
}
