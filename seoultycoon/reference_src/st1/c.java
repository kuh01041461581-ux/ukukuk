/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class c
extends aq {
    Image[] B;
    Image o;
    Image[] c;
    String[] g;
    int D;
    int E;
    int i;
    int F;
    int z;
    int p;
    boolean q;
    boolean[] w;
    boolean s;
    boolean t;
    static final int[] C = new int[]{8835619, 1201406, 15840031, 5752575, 15925163, 8634623, 0xFFF5A5, 0xFFFFFF};

    c(aq aq2) {
        super(aq2);
    }

    void b() {
        this.a(true);
        this.z = this.n.f();
        this.w = new boolean[4];
        this.B = new Image[12];
        this.c = new Image[4];
        try {
            for (int i = 0; i < 12; ++i) {
                this.B[i] = this.n.c("t" + i);
                if ((i & 1) != 1) continue;
                this.c(true);
            }
            this.c[0] = this.n.n[this.n.g() ? 7 : 8];
            this.c[1] = this.n.c("tc");
            this.c(true);
            this.c[2] = this.n.c("td");
            this.c(true);
            this.c[3] = this.n.c("tb");
            this.c(true);
        }
        catch (Exception exception) {
            aq.r();
        }
        this.n();
        this.b(true);
        this.d(false);
        this.K();
        this.o();
        this.b(2, true);
        this.u();
    }

    void d(int n) {
        this.p = 0;
        switch (n) {
            case 50: 
            case 141: {
                if (this.D == 4) {
                    this.D = 0;
                    break;
                }
                if (this.D == 1) {
                    this.D = 4;
                    break;
                }
                this.D = 1;
                break;
            }
            case 56: 
            case 146: {
                if (this.D == 1) {
                    this.D = 0;
                    break;
                }
                if (this.D == 4) {
                    this.D = 1;
                    break;
                }
                this.D = 4;
                break;
            }
            case 52: 
            case 142: {
                if (this.D == 3) {
                    this.D = 0;
                    break;
                }
                if (this.D == 2) {
                    this.D = 3;
                    break;
                }
                this.D = 2;
                break;
            }
            case 54: 
            case 145: {
                if (this.D == 2) {
                    this.D = 0;
                    break;
                }
                if (this.D == 3) {
                    this.D = 2;
                    break;
                }
                this.D = 3;
                break;
            }
            case 53: 
            case 131: 
            case 148: {
                if (this.D == 0) {
                    if (this.n.d.u == 7 && this.n.d.d == 2 && (this.n.d.m & 1 << 2 + this.n.d.I) == 0) {
                        this.n(0);
                        return;
                    }
                    this.t = true;
                    this.b(new ad(this));
                    return;
                }
                this.z();
                return;
            }
            case 8: {
                this.b(new aa(this, 0));
                return;
            }
            default: {
                return;
            }
        }
        this.E = 0;
        this.k();
    }

    void b(int n2) {
        if (this.C()) {
            return;
        }
        this.e(false);
        if (++this.p > 100) {
            this.p = 0;
            if ((this.e() & 0x3FF) == 123) {
                this.F = 10;
                this.a(7, 341);
            } else {
                this.g(500);
                this.n.d.F = (short)(this.n.d.F | 0x1000);
                this.n.i(-1);
                this.n();
                this.b(new n(this, 18, 342 + this.e() % 3));
            }
            return;
        }
        if (this.A()) {
            return;
        }
        this.p();
    }

    void x() {
        this.p = 0;
        if (this.F > 0) {
            int n2 = this.F;
            this.F = 0;
            switch (n2) {
                case 1: {
                    this.z();
                    return;
                }
                case 2: {
                    this.I();
                    return;
                }
                case 3: {
                    this.t = true;
                    this.b(new b(this));
                    return;
                }
                case 5: {
                    this.t = true;
                    this.b(new ai(this, false));
                    return;
                }
                case 10: {
                    p p2 = new p(this);
                    p2.i = -100;
                    this.n.d.h = 0;
                    this.n();
                    this.b(new ac(this, p2, 0, "\uc190 \ub193\uace0 \ud734\uc2dd"));
                    return;
                }
                case 20: {
                    p p3 = new p(this);
                    p3.o = 400;
                    p3.d = 5;
                    this.n.n(400);
                    this.n.i(5);
                    this.n();
                    this.b(new ac(this, p3, 0, "\uccad\ub834\ud55c \uacf5\ubb34\uc6d0 \uc0c1"));
                    return;
                }
                case 30: {
                    p p4 = new p(this);
                    p4.o = 100;
                    p4.d = 3;
                    this.n.n(100);
                    this.n.i(3);
                    this.n();
                    this.b(new ac(this, p4, 0, "\uc6a9\uac10\ud55c \uc2dc\ubbfc \uc0c1"));
                    return;
                }
                case 40: {
                    p p5 = new p(this);
                    p5.o = 100;
                    this.n.n(100);
                    this.n();
                    this.b(new ac(this, p5, 0, "\ucd5c\uace0\ub3c4\uc2dc \uc0c1"));
                    return;
                }
                case 50: {
                    this.K();
                    this.d();
                    return;
                }
                case 200: {
                    this.D();
                    return;
                }
                case 300: {
                    this.F = 301;
                    this.n(1);
                    return;
                }
                case 301: {
                    this.F = 302;
                    this.n(1);
                    return;
                }
                case 302: {
                    this.F = 303;
                    this.n(1);
                    return;
                }
                case 303: {
                    this.F = 304;
                    this.n(2);
                    return;
                }
                case 304: {
                    this.n(3);
                    return;
                }
                case 1818: {
                    this.F = 1819;
                    this.a(6, 738);
                    return;
                }
                case 1819: {
                    this.F = 1820;
                    this.k(739);
                    return;
                }
                case 1820: {
                    this.a(new ag(this, -1));
                    return;
                }
                case 1838: {
                    this.F = 1839;
                    this.a(8, 741);
                    return;
                }
                case 1839: {
                    this.F = 1840;
                    this.k(742);
                    return;
                }
                case 1840: {
                    this.a(new ag(this, 8));
                    return;
                }
                case 9000: {
                    this.F = 9010;
                    this.g = new String[1];
                    this.b(new j(this, "\uc2dc\uc7a5\ub2d8 \uc874\ud568", 6, this.g));
                    return;
                }
                case 9010: {
                    if (this.g[0].equals("???")) {
                        this.F = 9000;
                        this.x();
                        return;
                    }
                    this.n.d.V = this.g[0];
                    this.n();
                    this.e(false);
                    this.b(new n((aq)this, 7, 746, "@@@@@@", this.n.d.V));
                    return;
                }
            }
            return;
        }
        if (this.t) {
            this.e(true);
            this.t = false;
        } else {
            this.K();
            this.y();
        }
        this.k();
    }

    void g() {
        this.e(true);
    }

    void d(boolean bl) {
        int n2;
        Image image = this.n.n[6];
        int n3 = (this.n.d.u + 1) / 3 + 3 & 3;
        this.e.setColor(C[n3]);
        this.e.fillRect(0, 0, this.k, this.v - 40);
        this.e.setColor(C[n3 + 4]);
        this.e.fillRect(0, this.v + 50, this.k, this.j - this.v - 50);
        n3 <<= 1;
        for (n2 = 0; n2 < this.k; n2 += 2) {
            this.a(this.n.n[11], n2, this.v - 47, n3, 0, 2, 105);
        }
        this.a(image, this.d - 62, this.v - 33, 4, 0, 27, 8);
        this.a(image, this.d - 43, this.v - 47, 4, 8, 12, 5);
        this.a(image, this.d - 37, this.v - 45, 0, 15, 35, 11);
        this.a(image, this.d + 35, this.v - 41, 16, 8, 14, 5);
        this.a(image, this.d + 47, this.v - 48, 4, 8, 12, 5);
        if (bl) {
            return;
        }
        for (n2 = 0; n2 < 8; ++n2) {
            this.c(this.n.f[n2 + 24], 0);
        }
        this.o = this.a(this.d - 63, this.v - 50, 126, 115);
    }

    void e(boolean bl) {
        if (bl) {
            this.d(true);
            this.K();
            this.y();
        } else if (this.E == 0) {
            this.y();
        }
        this.G();
        this.H();
        this.E();
        if (this.E < 20) {
            this.F();
        }
    }

    void G() {
        this.e.setClip(0, 15, this.k, this.j - 30);
        this.a(this.o, this.d - 63, this.v - 50);
        switch (this.D) {
            case 0: {
                this.c(2, -5);
                break;
            }
            case 1: {
                this.c(0, -5);
                break;
            }
            case 2: {
                this.c(8, -5);
                break;
            }
            case 3: {
                this.c(5, -5);
                this.c(10, 0);
                break;
            }
            case 4: {
                this.c(10, -5);
            }
        }
        this.e.setClip(0, 0, this.k, this.j);
    }

    void c(int n2, int n3) {
        this.e.drawImage(this.B[n2], this.d + this.n.f[n2], this.v + this.n.f[n2 + 12] + n3 + 9, 20);
        if (n2 != 2 && n3 < 0) {
            this.c(n2 + 1, 0);
        }
    }

    void K() {
        Image image = this.c[1];
        Image image2 = this.c[2];
        w w2 = new w(this, image2, 0, 22, 5, 9);
        this.e.setColor(2359551);
        this.e.fillRect(0, 0, this.k, 15);
        this.e.fillRect(0, this.j - 15, this.k, 15);
        this.e.setColor(4812287);
        this.e.drawLine(0, 0, this.k, 0);
        this.e.setColor(2359466);
        this.e.drawLine(0, 14, this.k, 14);
        this.a(image2, this.k - 87, 2, 36, 0, 18, 11);
        this.a(image2, this.k - 59, 2, 54, 0, 8, 11);
        this.a(image2, this.k - 39, 2, 62, 0, 8, 11);
        this.a(image2, this.k - 19, 2, 70, 0, 18, 11);
        w2.a(this.n.d.I + 1, this.k - 61, 3, 0);
        w2.a(this.n.d.u + 1, this.k - 41, 3, 0);
        w2.a(this, image2, 0, 0, 9, 11);
        w2.a(this.n.d.d, this.k - 21, 2, 0);
        this.e.setColor(170);
        this.e.drawLine(0, this.j - 15, this.k, this.j - 15);
        this.e.drawLine(0, this.j - 1, this.k, this.j - 1);
        this.e.setColor(4812287);
        this.e.drawLine(0, this.j - 14, this.k, this.j - 14);
        this.e.setColor(2378154);
        this.e.drawLine(0, this.j - 2, this.k, this.j - 2);
        this.a(this.d - 44, this.j - 12, 19, 9, 0x212152);
        this.a(this.d + 23, this.j - 12, 19, 9, 0x212152);
        this.a(image, this.d - 62, this.j - 12, 0, 0, 19, 9);
        this.a(image, this.d - 4, this.j - 12, 19, 0, 28, 9);
        this.a(image, this.d - 24, this.j - 11, 47, 0, 4, 7);
        this.a(image, this.d + 43, this.j - 11, 47, 0, 4, 7);
        int n2 = this.n.d.h & 0xFF;
        w2.a(this, this.n.n[1], 0, 8, 4, 7);
        w2.a(n2, this.d - 28, this.j - 11, 0);
        n2 = n2 < 31 ? 0 : (n2 < 70 ? 13 : (n2 < 90 ? 26 : 39));
        this.a(this.n.n[10], this.d - 19, this.j - 14, n2, 0, 13, 13);
        w2.a(this.n.d.q, this.d + 39, this.j - 11, 0);
        this.a(this.n.n[10], this.d + 48, this.j - 14, this.n.d.L > 0 ? 52 : 65, 0, 13, 13);
    }

    void E() {
        int n2 = (this.E & 2) >> 1;
        int n3 = 0;
        if (this.E < 16) {
            n3 = 1 + n2;
        } else if (this.E >= 64) {
            if (this.i < 15) {
                n3 = 5 + n2;
            } else if (this.i < 50) {
                n3 = 3 + n2;
            }
        }
        this.m(n3);
        if ((this.E & 0xF) == 0) {
            this.i = this.e() % 100;
        }
    }

    void m(int n2) {
        byte by = this.n.f[188 + n2];
        n2 = this.n.f[195 + n2];
        this.a(this.c[0], this.d + this.n.f[202 + this.D] - (n2 >> 1), this.v + this.n.f[207 + this.D], by, 0, n2, 27);
    }

    void y() {
        Image image = this.c[1];
        Image image2 = this.c[2];
        this.e.setColor(14492842);
        this.e.fillRect(0, 1, 29, 13);
        this.e.setColor(0xFF6DFF);
        this.e.drawLine(0, 0, 28, 0);
        this.e.setColor(9568341);
        this.e.drawLine(0, 14, 28, 14);
        this.a(image, 1, 2, 47, 7, 2, 2);
        this.a(image, 26, 2, 47, 7, 2, 2);
        this.a(image, 1, 11, 47, 7, 2, 2);
        this.a(image, 26, 11, 47, 7, 2, 2);
        this.a(image2, 5, 2, this.D * 19, 11, 19, 11);
    }

    void F() {
        if (this.D == 0) {
            return;
        }
        int n2 = this.d + this.n.f[211 + this.D];
        int n3 = this.v + this.n.f[215 + this.D];
        Image image = this.c[1];
        this.a(image, n2, n3, 0, 19, 70, 6);
        this.e.setColor(0);
        this.e.drawRect(n2, n3 + 6, 69, 26);
        this.e.drawRect(n2 + 2, n3 + 6, 65, 26);
        this.e.setColor(27904);
        this.e.fillRect(n2 + 3, n3 + 7, 64, 26);
        this.e.setColor(0xDBDBAA);
        this.e.drawLine(n2 + 1, n3 + 6, n2 + 1, n3 + 32);
        this.e.setColor(0xB6B6AA);
        this.e.drawLine(n2 + 68, n3 + 6, n2 + 68, n3 + 32);
        this.a(image, n2, n3 + 33, 0, 25, 69, 4);
        this.a(image, n2 + 5, n3 + 9, 0, 9, 18, 10);
        this.a(image, n2 + 5, n3 + 22, 18, 9, 18, 10);
        this.a(image, n2 + 47, n3 + 9, 36, 9, 18, 10);
        this.a(image, n2 + 49, n3 + 22, 54, 9, 15, 10);
        this.a(n2 + 25, n3 + 9, 21, 10, 0);
        this.a(n2 + 25, n3 + 22, 21, 10, 0);
        byte by = this.n.f[767 + this.D - 1];
        w w2 = new w(this, this.c[2], 50, 22, 5, 8);
        w2.a(this.n.d.P[by], n2 + 43, n3 + 10, 0);
        w2.a(this.n.d.z[by], n2 + 43, n3 + 23, 0);
    }

    void H() {
        int n2 = 751;
        for (int i = 0; i < 4; ++i) {
            int n3;
            int n4;
            byte by = this.n.f[228 + i];
            n2 = 751 + by;
            int n5 = n4 = by == this.D - 1 ? -5 : 0;
            if (this.n.d.H[i] > this.z) {
                this.a(this.c[3], this.d + this.n.f[n2], this.v + this.n.f[n2 + 4] + n4, 0, 0, 15, 15);
            }
            if ((this.f.p & 1) != 0 || (n3 = this.n.d.A & 0x7F) == 0 || n3 >= 27 || (this.n.f[1700 + n3] & 0xFF) >> 6 != i) continue;
            this.a(this.c[3], this.d + this.n.f[n2 + 8], this.v + this.n.f[n2 + 12] + n4, 15, 0, 15, 15);
            if ((this.f.p & 2) == 0) {
                this.g(500);
                continue;
            }
            this.g(0);
        }
    }

    void I() {
        int n2 = 0;
        for (int i = 0; i < 4; ++i) {
            n2 += this.n.d.P[i];
        }
        p p2 = new p(this);
        p2.p = n2;
        p2.o = n2 / 10;
        this.n.n(p2.o);
        this.n.d.G = (byte)(this.n.d.G + 1);
        this.n();
        this.b(new ac(this, p2, 0, this.n.d.u + 1 + "\uc6d4\uc758 \uc138\uae08\uc218\uc785"));
    }

    void z() {
        byte by = this.n.f[766 + this.D];
        if ((this.n.d.x & 4) == 0 && this.D != 1) {
            this.k(708);
            return;
        }
        if (this.z < this.n.d.g[by]) {
            this.k(563);
            return;
        }
        if (this.a(4, (int)this.n.d.e, 4)) {
            this.F = 1;
            return;
        }
        this.n.i();
        if (this.n.d.A != 0) {
            this.n.d.A = (byte)(this.n.d.A | 0x80);
        }
        this.n.m(2);
        this.n.d.Y[by] = (byte)(this.z + 1);
        this.f(this.D + 1);
    }

    boolean A() {
        int n2;
        if (this.q) {
            ++this.E;
            return false;
        }
        if ((this.n.d.A & 0x80) != 0) {
            n2 = this.n.d.A & 0x7F;
            if (n2 < 27) {
                this.F = 50;
                this.q();
                return true;
            }
            this.n.d.A = 0;
            this.n();
        }
        if ((this.n.d.x & 0x18) != 0) {
            this.F = 200;
            if ((this.n.d.x & 0x10) != 0) {
                this.k(737);
            } else {
                this.k(716);
            }
            return true;
        }
        if (this.a(0, 7, 0)) {
            return true;
        }
        if (this.n.d.V.equals("???")) {
            this.F = 9000;
            this.a(6, 745);
            return true;
        }
        if (this.a(1, 0, 1)) {
            return true;
        }
        if ((this.n.d.t & 0xA80) == 2688 && (this.n.d.a & 0x4000000) == 0) {
            this.n.d.a |= 0x4000000;
            this.F = 20;
            this.c(7);
            this.k(-597);
            return true;
        }
        if ((this.n.d.t & 0x2000) != 0 && (this.n.d.a & 0x8000000) == 0) {
            this.n.d.a |= 0x8000000;
            this.F = 30;
            this.c(7);
            this.k(-610);
            return true;
        }
        if (this.n.d.N == 4095 && (this.n.d.a & 0x10000000) == 0) {
            this.n.d.a |= 0x10000000;
            this.F = 40;
            this.c(7);
            this.k(-743);
            return true;
        }
        for (n2 = 0; n2 < 4; ++n2) {
            int n3;
            if (this.n.d.H[n2] <= 0) continue;
            if (this.a(11, (int)this.n.d.e, 260)) {
                return true;
            }
            if (this.n.d.H[n2] > this.z) continue;
            if (!this.w[n2]) {
                this.w[n2] = true;
                this.b(new n((aq)this, (int)this.n.d.e, 398, "@@@@", s.b[61 + n2]));
                return true;
            }
            p p2 = new p(this);
            int n4 = this.n.d.Q[n2];
            int n5 = this.n.a(n2, n4);
            byte[] byArray = this.n.d.o[n2];
            int n6 = n4;
            int n7 = byArray[n6] + 1;
            byArray[n6] = n7;
            int n8 = n7;
            p2.j = this.n.a(n2, n4, n8);
            p2.k = this.n.f[n5 + 4];
            p2.b = p2.k << 1;
            this.n.d.z[n2] = (byte)Math.min(this.n.d.z[n2] + p2.k, 100);
            this.n.d.P[n2] = (short)Math.min(this.n.d.P[n2] + (p2.k << 1), 250);
            for (n3 = 0; n3 < 4; ++n3) {
                p2.f[n3] = this.n.f[n5 + 5 + n3];
                this.n.d.r[n3] = (byte)Math.min(this.n.d.r[n3] + p2.f[n3], 100);
            }
            this.n.d.H[n2] = -1;
            n5 = 232 + n2 * 6;
            n8 += this.n.f[n5 + n4];
            n4 = n8;
            if ((n2 & 1) == 1) {
                n4 += 9;
            }
            n3 = n8;
            for (n8 = 0; n8 < 6 && n3 >= this.n.f[n5]; ++n8) {
                ++n5;
            }
            n4 = 1 << n4 - n8;
            if (n2 < 2) {
                this.n.d.p |= n4;
            } else {
                this.n.d.l |= n4;
            }
            this.n();
            this.b(new ac(this, p2, 0, "\uc644\uacf5"));
            return true;
        }
        if ((this.n.d.Y[4] & 0xFF) < this.z) {
            this.n.d.Y[4] = (byte)this.z;
            n2 = 0;
            for (int i = 0; i < 4; ++i) {
                if ((this.n.d.Y[i] & 0xFF) >= this.z - 7) continue;
                ++n2;
            }
            if (n2 > 0) {
                this.n.i(-n2);
                this.n();
                this.k(744);
                return true;
            }
            this.n();
        }
        if ((this.n.d.G & 0xFF) <= this.z) {
            switch (this.n.d.G & 3) {
                case 0: {
                    this.F = 2;
                    if (this.s) break;
                    this.k(2);
                    break;
                }
                case 1: {
                    this.F = 3;
                    if (this.s) break;
                    this.k(12);
                    break;
                }
                case 2: {
                    this.n.d.G = (byte)(this.n.d.G + 1);
                    this.n();
                    this.b(new o(this));
                    break;
                }
                case 3: {
                    this.F = 5;
                    this.n.d.G = (byte)(this.n.d.G + 1);
                    this.n();
                    if (this.s) break;
                    this.k(11);
                }
            }
            if (this.s) {
                this.x();
            }
            return true;
        }
        if (this.a(3, (int)this.n.d.e, 3)) {
            return true;
        }
        if ((this.n.d.x & 4) == 0 && this.a(13, (int)this.n.d.e, 707)) {
            return true;
        }
        for (n2 = 0; n2 < 5; ++n2) {
            if (this.n.d(n2)) continue;
            this.t = true;
            this.b(new x(this));
            return true;
        }
        if (this.n.d.A == 0 && (this.n.d.I != 0 || this.n.d.u != 0) && this.n.d.h < 80 && ((n2 = this.e() & 0x3FF) < 100 ? this.J() : (n2 < 250 ? this.B() : n2 < 350 + this.n.d.I * 100 && this.c()))) {
            return true;
        }
        if (this.n.d.A == 0) {
            this.n.d.X = 0;
        }
        this.q = true;
        return false;
    }

    boolean C() {
        if ((this.n.d.t & 0x540) == 1344) {
            this.a(new ag(this, 9));
            return true;
        }
        if (this.n.d.h > 99) {
            if ((this.n.d.x & 0x20) != 0) {
                this.a(new ag(this, 7));
                return true;
            }
            this.n.d.x |= 0x10;
            this.n();
        }
        if (this.n.d.q < 1) {
            this.F = 1838;
            this.k(740);
            return true;
        }
        if (this.n.d.I > 3) {
            this.F = 1818;
            this.k(727);
            return true;
        }
        return false;
    }

    boolean J() {
        int n2;
        int n3 = 0;
        int n4 = 1;
        int n5 = 0;
        int n6 = 0;
        for (n2 = 0; n2 < 22; ++n2) {
            int n7;
            if ((this.n.d.D & n4) == 0 && this.n.d.o[(n7 = this.n.f[1701 + n2] & 0xFF) >> 6][n7 >> 3 & 7] == (n7 & 7) && this.z >= this.n.d.g[n7 >> 6]) {
                n3 |= n4;
                if (n5 == 0 && !this.n.b(n2)) {
                    n5 = 1;
                }
                ++n6;
            }
            n4 <<= 1;
        }
        if ((n3 & 4) != 0 && (this.n.d.Q[3] != 0 || this.n.d.H[3] <= 0)) {
            n3 ^= 4;
            --n6;
        }
        if (n5 == 1) {
            n4 = 1;
            for (n2 = 0; n2 < 22; ++n2) {
                if ((n3 & n4) != 0 && this.n.b(n2)) {
                    n3 ^= n4;
                    --n6;
                }
                n4 <<= 1;
            }
        }
        if (n6 < 1) {
            return false;
        }
        n4 = 1;
        n6 = this.e() % n6;
        for (n2 = 0; n2 < 22; ++n2) {
            if ((n3 & n4) != 0) {
                if (n6 == 0) {
                    n5 = ((this.n.f[1701 + n2] & 0xFF) >> 6) + 1;
                    if (this.n.d.X != n5) {
                        this.n.d.X = (byte)n5;
                        this.n.d.A = (byte)(n2 + 1);
                        this.n.d.O |= 1 << n2;
                        this.n();
                        break;
                    }
                    return false;
                }
                --n6;
            }
            n4 <<= 1;
        }
        return this.a(12, (int)this.n.d.e, 397);
    }

    boolean c() {
        int n2 = this.n.d.u;
        int n3 = n2 < 2 || n2 > 10 ? 1 : (n2 < 5 ? 2 : (n2 < 8 ? 0 : 3));
        if (this.n.k(this.n.f[1807 + n3])) {
            return false;
        }
        n2 = (this.n.f[1723 + n3] & 0xFF) >> 6;
        if (this.z < this.n.d.g[n2]) {
            return false;
        }
        if (this.n.d.X == n2 + 1) {
            return false;
        }
        this.n.d.X = (byte)(n2 + 1);
        this.n.d.A = (byte)(23 + n3);
        this.n();
        return this.a(12, (int)this.n.d.e, 397);
    }

    boolean B() {
        switch (this.e() & 3) {
            case 0: {
                if ((this.n.d.t & 0x2A) != 0) break;
                this.b(27, 3);
                if (this.n.d.C >= (this.n.d.A - 26) * 10) break;
                this.n.d.A = 0;
                break;
            }
            case 1: {
                this.b(30, 3);
                break;
            }
            case 2: {
                if (!this.n.h(3) && !this.n.h(4) || this.n.d.C < 50) break;
                this.b(33, 2);
                break;
            }
            case 3: {
                if (!this.n.h(1) && !this.n.h(4)) break;
                this.b(35, 1);
            }
        }
        if (this.n.d.A != 0) {
            this.n();
        }
        return false;
    }

    void b(int n2, int n3) {
        int n4 = 3 << (n2 - 27 << 1);
        for (int i = 0; i < n3; ++i) {
            if ((this.n.d.t & n4) == 0) {
                this.n.d.A = (byte)(n2 + i);
                return;
            }
            n4 <<= 2;
        }
    }

    void D() {
        if ((this.n.d.x & 8) != 0) {
            this.n.d.x ^= 8;
        }
        if ((this.n.d.x & 0x10) != 0) {
            this.n.d.x ^= 0x10;
            this.n.d.x |= 0x20;
        }
        this.n.i();
        this.n.i();
        this.n.m(-100);
        this.n();
        this.z = this.n.f();
        this.s = true;
        this.K();
        this.y();
        this.G();
        this.e.setClip(0, 0, this.k, this.j);
        int n2 = this.n.f() - 2;
        w w2 = new w(this, this.c[2], 0, 22, 5, 9);
        w w3 = new w(this, this.c[2], 0, 0, 9, 11);
        try {
            Image[] imageArray = new Image[]{this.n.c("u0"), this.n.c("u1")};
            int n3 = 0;
            int n4 = 0;
            int n5 = 0;
            int n6 = 0;
            int n7 = 0;
            int n8 = 0;
            for (int i = 0; i < 24; ++i) {
                this.a(imageArray[(i & 4) >> 2]);
                int n9 = i % 12;
                if (n9 == 0) {
                    n6 = n3 = n2 / 48 + 1;
                    n7 = n4 = (n2 >> 2) % 12 + 1;
                    n5 = n2 & 3;
                    n8 = n5 + 1;
                    if (n8 > 3) {
                        n8 = 0;
                        if (++n7 > 12) {
                            n7 = 1;
                            if (++n6 > 4) break;
                        }
                    }
                    ++n2;
                }
                this.a(n3, n6, this.k - 61, 3, w2, 5, n9, 9);
                this.a(n4, n7, this.k - 41, 3, w2, 9, n9, 9);
                this.a(n5, n8, this.k - 21, 2, w3, 9, n9, 11);
                this.p();
                aq.h(333);
            }
        }
        catch (Exception exception) {
            // empty catch block
        }
        this.E = 0;
        this.K();
        this.q = false;
    }

    void a(int n2, int n3, int n4, int n5, w w2, int n6, int n7, int n8) {
        this.e.setColor(2359551);
        this.e.setClip(n4 - n6, n5, n6 + 1, n8);
        this.e.fillRect(n4 - n6, n5, n6 + 1, n8);
        if (n2 == n3) {
            w2.a(n2, n4, n5, 0);
            return;
        }
        w2.a(n2, n4, n5 - n7, 0);
        w2.a(n3, n4, n5 - n7 + 12, 0);
    }

    void n(int n2) {
        if (n2 == 0) {
            Image image;
            try {
                image = this.n.c("ts");
            }
            catch (Exception exception) {
                return;
            }
            this.e(0);
            while (this.h()) {
                this.G();
                if ((this.f.p & 1) == 0) {
                    this.a(image, this.d - 36, this.v - 29, 0, 0, 73, 59);
                } else {
                    this.a(image, this.d - 38, this.v - 28, 73, 0, 76, 56);
                }
                this.p();
                this.e(300);
            }
            this.o();
            this.e(false);
            this.F = 300;
            this.k(780);
            return;
        }
        if (n2 == 1) {
            this.e(0);
            this.G();
            this.m(0);
            this.p();
            this.e(1000);
            this.G();
            this.m(6);
            this.a(7, 781);
            return;
        }
        if (n2 == 2) {
            p p2 = new p(this);
            p2.d = 3;
            this.n.d.m |= 1 << 2 + this.n.d.I;
            this.n.i(3);
            this.n();
            this.K();
            this.y();
            this.b(new ac(this, p2, 0, "\uad11\ubcf5\uc808\ud589\uc0ac"));
            return;
        }
        this.G();
        this.m(0);
        this.t = true;
        this.b(new ad(this));
    }
}
