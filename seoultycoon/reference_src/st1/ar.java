/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class ar
extends aq {
    int L;
    int D;
    int ah;
    int aq;
    Image[] am;
    Image[] N;
    Image[] c;
    Image H;
    Image I;
    Image ae;
    Image at;
    w Q;
    String an;
    boolean[] W;
    boolean ap;
    boolean as;
    boolean o;
    boolean s;
    int E;
    int ai;
    int af;
    int aa;
    int ag;
    int M;
    int U;
    int P;
    int ac;
    int i;
    int O;
    int R;
    boolean Y;
    boolean q;
    boolean ad;
    byte[] al;
    byte g;
    byte J;
    byte z;
    byte X;
    int p;
    int[] C;
    int S;
    int[] ar;
    f[] V;
    int B;
    Image F;
    int ak;
    int aj;
    int ao;
    final byte[] K = new byte[]{9, 10, 6, 2, 1, 0, 4, 8};
    boolean T;
    int au;
    int ab;
    int[] Z;
    byte w;
    int G;
    int t;

    ar(aq aq2) {
        super(aq2);
        int n;
        this.H = this.n.n[2];
        try {
            this.I = this.n.c("ti");
        }
        catch (Exception exception) {
            // empty catch block
        }
        this.W = new boolean[6];
        this.V = new f[16];
        for (n = 0; n < this.V.length; ++n) {
            this.V[n] = new f();
        }
        this.Z = new int[30];
        for (n = 0; n < this.Z.length; n += 2) {
            this.Z[n + 1] = this.e() % (this.k + 50);
            this.Z[n] = 15 + this.e() % (this.j - 32);
        }
        this.Q = new w(this, this.n.n[1], 0, 8, 4, 7);
        this.ah = this.n.f() - 1;
        this.aa = this.M = this.d;
        this.ag = this.U = this.v;
    }

    void b() {
        this.d(20, 9, 0);
        this.u();
    }

    void d(int n) {
        this.p = 0;
        if (this.P != 0 && this.R >= 0) {
            return;
        }
        switch (n) {
            case 50: 
            case 52: 
            case 141: 
            case 142: {
                this.D = (this.D + 5) % 6;
                break;
            }
            case 54: 
            case 56: 
            case 145: 
            case 146: {
                this.D = (this.D + 1) % 6;
                break;
            }
            case 53: 
            case 131: 
            case 148: {
                this.ap = true;
                this.g(false);
                if ((this.n.d.x & 4) == 0 && this.L == 3 && this.D != 4) {
                    this.k(710);
                    return;
                }
                if (this.P != 0 && this.ac == this.D) {
                    this.aq = 50;
                    this.b(new z(this));
                } else if (!this.N()) {
                    int n2 = this.o && this.D == this.n.d.Q[this.L] ? 323 : 264;
                    this.k(n2);
                }
                return;
            }
            case 8: {
                this.b(new aa(this, 1));
                return;
            }
            case 48: {
                if ((this.n.d.x & 4) == 0) {
                    this.k(710);
                    return;
                }
                this.D();
                return;
            }
            default: {
                return;
            }
        }
        this.ap = true;
        this.k();
    }

    void b(int n) {
        if (this.B()) {
            return;
        }
        if (this.A()) {
            return;
        }
        if (this.ap) {
            this.f(false);
        }
        if (this.aa != this.M) {
            this.aa = this.aa + this.M >> 1;
        }
        if (this.ag != this.U) {
            this.ag = this.ag + this.U >> 1;
        }
        this.g(this.P == 0 || this.R < 0);
        if (++this.p > 100) {
            this.p = 0;
            this.g(500);
            this.n.i(-1);
            this.n();
            this.a(8, 338 + this.e() % 3);
        }
        this.p();
    }

    void d(int n, int n2, int n3) {
        this.a(false);
        this.L = n3 / 10;
        this.al = this.n.d.o[this.L];
        try {
            byte by;
            int n4;
            this.C = new int[this.n.d.P[this.L] / 20 * 6];
            this.ar = new int[21];
            this.am = new Image[n2];
            for (n4 = 0; n4 < 6; ++n4) {
                by = this.al[n4];
                if (by > 0) {
                    int n5 = this.n.f[232 + this.L * 6 + n4] + by - n4 - 1;
                    this.am[n5] = this.n.c("t" + (n5 + n));
                    if (this.L == 0 && n4 == 0 && by == 2) {
                        n5 = this.n.f[232 + this.L * 6 + n4] - n4;
                        this.am[n5] = this.n.c("t" + (n5 + n));
                    }
                }
                this.c(false);
            }
            this.c = new Image[4];
            this.c[0] = this.n.c("wa");
            this.c[1] = this.n.c("tl");
            this.c[2] = this.n.c("u10");
            this.c(false);
            n4 = this.n.d.A & 0x7F;
            if (n4 != 0) {
                if (n4 < 27) {
                    if ((this.n.d.j & 3) < 3) {
                        this.n.d.j = (byte)(this.n.d.j + 1);
                    }
                    if (((by = this.n.f[1700 + n4]) & 0xFF) >> 6 == this.L) {
                        this.P = n4;
                        this.ac = by >> 3 & 7;
                        this.c[3] = this.n.c("e" + this.n.f[1726 + n4]);
                        this.O = this.c[3].getWidth() >> 1;
                        this.i = this.c[3].getHeight();
                    } else {
                        this.q = true;
                    }
                    if (n4 == 23) {
                        this.n.d.m |= 0x200;
                    }
                } else {
                    this.ad = true;
                }
            } else {
                if ((this.n.d.t & 0x8000) != 0 && this.n.h(0) && this.n.h(3) && (this.n.d.m & 0x40) != 0 && (this.e() & 3) == 0) {
                    this.au = 1;
                } else if ((this.n.d.a & 0x4000000) != 0 && this.n.d.o[2][3] != 0 && this.n.d.C >= 20 && this.n.d.C <= 50 && (this.n.d.m & 0x180) == 256) {
                    this.au = 2;
                } else if (this.n.d.z[this.L] >= 100 && (this.n.d.m & 0x200) != 0) {
                    this.au = 3;
                }
                this.n.d.m &= 0xFFFFFDFF;
            }
            this.N = new Image[5];
            this.N[0] = this.n.c("w" + n3);
            this.c(false);
            if (!this.f.z) {
                for (n4 = 1; n4 < 5; ++n4) {
                    this.N[n4] = this.n.c("w" + (n4 + n3));
                    this.c(false);
                }
            }
            n4 = 1818;
            switch (this.L) {
                case 0: {
                    if (this.al[3] == 1) {
                        n4 = 0;
                    }
                    if (this.al[1] == 0 || this.ah % 48 != 16) break;
                    this.F = this.n.c("z4");
                    this.ak = (this.e() & 0x3F) - 32;
                    this.aj = (this.e() & 0x3F) - 52;
                    this.ao = this.e() & 7;
                    break;
                }
                case 1: {
                    if (this.al[2] != 1) break;
                    n4 = 3;
                    break;
                }
                case 2: {
                    if (this.al[5] != 0) {
                        n4 = 2;
                        break;
                    }
                    if (this.al[0] != 1) break;
                    n4 = 1;
                    break;
                }
                case 3: {
                    if (this.al[5] == 0) break;
                    n4 = 3 + this.al[5];
                }
            }
            if (n4 != 1818 && !this.f.z) {
                this.ae = this.n.c("tq" + n4);
            } else if ((this.n.j & 0x30) == 0 && this.e() % 3 == 0) {
                this.w = (byte)(((this.ah % 48 >> 2) + 1) % 12 / 3 + 1);
                switch (this.w) {
                    case 1: 
                    case 2: 
                    case 4: {
                        this.at = this.n.c("tr" + this.w);
                    }
                }
            }
        }
        catch (Exception exception) {
            aq.r();
        }
        this.n();
        this.b(false);
        this.L();
        this.E();
        this.ap = true;
        if (this.n.d.H[this.L] < 0) {
            this.w = 0;
            this.n.d.H[this.L] = 0;
            this.af = this.n.d.Q[this.L] | 0xC0;
            this.n();
            this.b(3, false);
        } else {
            this.y();
        }
        this.o();
        this.p = 0;
    }

    void x() {
        this.p = 0;
        int n = this.aq;
        this.aq = 0;
        if (n < 20) {
            switch (n) {
                case 0: {
                    return;
                }
                case 1: {
                    if (this.A == 100) {
                        this.f(1);
                    }
                    return;
                }
                case 2: {
                    if (this.A == 100) {
                        if (this.a(2, (int)this.n.d.e, 261)) {
                            this.aq = 3;
                        } else {
                            this.C();
                        }
                    } else if (this.A == 101) {
                        if (this.H()) {
                            this.aq = 5;
                            this.W[this.D] = true;
                            this.b(new t(this));
                        } else if (this.a(10, (int)this.n.d.e, 262)) {
                            this.aq = 4;
                        } else {
                            this.e(false);
                        }
                    }
                    return;
                }
                case 3: {
                    this.C();
                    return;
                }
                case 4: {
                    this.e(false);
                    return;
                }
                case 5: {
                    this.M();
                    return;
                }
                case 6: {
                    int n2;
                    if (this.A != 100) {
                        this.N();
                        return;
                    }
                    this.aq = 7;
                    if (this.o) {
                        n2 = 324;
                    } else if (this.n.d.C < this.n.f[this.E + 2]) {
                        n2 = 318;
                    } else if (this.n.d.B < this.n.f[this.E]) {
                        n2 = 319;
                    } else if (this.n.d.R < this.n.f[this.E + 1]) {
                        n2 = 320;
                    } else {
                        this.d(false);
                        return;
                    }
                    this.k(n2);
                    return;
                }
                case 7: {
                    this.N();
                    return;
                }
                case 8: {
                    this.aq = 5;
                    try {
                        p p2 = new p(this);
                        p2.o = (this.e() & 3) + 1;
                        this.n.n(p2.o);
                        this.n();
                        this.O();
                        this.b(new ac(this, p2, 0, "\uc790\uae08\ud68d\ub4dd"));
                    }
                    catch (Exception exception) {
                        // empty catch block
                    }
                    return;
                }
                case 9: {
                    this.aq = 5;
                    try {
                        boolean bl;
                        p p3 = new p(this);
                        if (this.al[this.D] == 0) {
                            n = (this.e() & 1) + 1;
                            bl = false;
                        } else {
                            n = this.e() % 3 + 2;
                            bl = true;
                        }
                        if (this.L == 1 || this.L == 2) {
                            bl ^= true;
                        }
                        if (!bl) {
                            p3.n = n;
                            this.n.f(p3.n);
                        } else {
                            p3.c = n;
                            this.n.c(p3.c);
                        }
                        this.n();
                        this.O();
                        this.b(new ac(this, p3, 0, "\uc790\uc6d0\ud68d\ub4dd"));
                    }
                    catch (Exception exception) {
                        // empty catch block
                    }
                    return;
                }
                case 10: {
                    this.D();
                    return;
                }
                case 16: {
                    this.e(true);
                    return;
                }
                case 17: {
                    if (this.A != 100) {
                        this.aq = 5;
                        this.k(346);
                        return;
                    }
                    this.d(true);
                    return;
                }
            }
        } else if (n >= 100) {
            this.m(n);
        } else {
            switch (n) {
                case 20: {
                    this.aq = 5;
                    this.k(712);
                    return;
                }
                case 50: {
                    this.W[this.D] = true;
                    if (this.n.d.A == 0) {
                        this.P = 0;
                        this.y();
                    }
                    this.O();
                    this.M();
                    return;
                }
                case 60: {
                    this.P = 0;
                    this.y();
                    this.O();
                    return;
                }
                case 70: {
                    this.d();
                    this.O();
                    return;
                }
                case 80: 
                case 81: {
                    n = 1137 + (n - 80 + this.n.f[878 + this.t]) * 6;
                    p p4 = new p(this);
                    p4.o = this.n.f[n] * 10;
                    p4.f[this.n.f[n + 1]] = this.n.f[n + 2];
                    p4.d = this.n.f[n + 3];
                    p4.i = this.n.f[n + 4];
                    this.n.n(p4.o);
                    this.n.b(this.n.f[n + 1], this.n.f[n + 2]);
                    this.n.i(this.n.f[n + 3]);
                    this.n.m(this.n.f[n + 4]);
                    this.n.d.x |= 1 << this.t + 8;
                    this.n();
                    this.O();
                    this.aq = 5;
                    this.b(new ac(this, p4, 0, "\ucc98\ub9ac\uacb0\uacfc"));
                    return;
                }
                case 90: {
                    this.aq = 91;
                    this.a(8, 785);
                    return;
                }
                case 91: {
                    this.aq = 92;
                    this.a(16, 786);
                    return;
                }
                case 92: {
                    this.aq = 93;
                    this.a(6, 787);
                    return;
                }
                case 93: {
                    this.aq = 94;
                    this.b(new l(this, 788, 789));
                    return;
                }
                case 94: {
                    this.n.d.m |= 0x40;
                    if (this.A == 100) {
                        this.n.c(30);
                        this.ab = 0;
                    } else {
                        this.n.f(30);
                        this.ab = 1;
                    }
                    this.n();
                    this.aq = 95;
                    this.k(790 + this.ab);
                    return;
                }
                case 95: {
                    this.aq = 96;
                    this.a(16, 792);
                    return;
                }
                case 96: {
                    this.aq = 97;
                    this.a(7, 793);
                    return;
                }
                case 97: {
                    p p5 = new p(this);
                    if (this.ab == 0) {
                        p5.c = 30;
                    } else {
                        p5.n = 30;
                    }
                    this.O();
                    this.b(new ac(this, p5, 0, "\uc870\ud3ed\uc758 \ud68c\uac1c"));
                    return;
                }
                case 30: {
                    this.aq = 31;
                    this.a(8, 795);
                    return;
                }
                case 31: {
                    this.aq = 32;
                    this.a(14, 796);
                    return;
                }
                case 32: {
                    this.aq = 33;
                    this.b(new l(this, 797, 798));
                    return;
                }
                case 33: {
                    this.n.d.m |= 0x80;
                    if (this.A == 100) {
                        this.ab = -this.n.d.C;
                        this.n.n(this.ab);
                        this.n.i(-30);
                        this.n();
                        this.aq = 34;
                        this.a(6, 799);
                        return;
                    }
                    this.n.n(30);
                    this.n();
                    this.aq = 41;
                    this.a(8, 805);
                    return;
                }
                case 34: {
                    this.aq = 35;
                    this.a(14, 800);
                    return;
                }
                case 35: {
                    this.aq = 36;
                    this.k(801);
                    return;
                }
                case 36: {
                    this.aq = 37;
                    this.a(14, 802);
                    return;
                }
                case 37: {
                    this.aq = 38;
                    this.a(8, 803);
                    return;
                }
                case 38: {
                    this.aq = 39;
                    this.k(804);
                    return;
                }
                case 39: {
                    this.aq = 40;
                    try {
                        Image image = this.n.c("e1");
                        this.e(0);
                        while (this.h()) {
                            this.g(false);
                            for (n = 0; n < 6; ++n) {
                                int n3 = this.L * 6 + 140 + n;
                                this.a(image, this.aa + this.n.f[n3] - 16, this.ag + this.n.f[n3 + 24] - 26, (this.f.p & 2) << 4, 0, 32, 32);
                            }
                            this.p();
                            this.s();
                        }
                        this.o();
                        this.x();
                    }
                    catch (Exception exception) {
                        // empty catch block
                    }
                    return;
                }
                case 40: {
                    p p6 = new p(this);
                    p6.o = this.ab;
                    p6.d = -30;
                    this.O();
                    this.b(new ac(this, p6, 0, "\ub3c4\ubc15\uc0ac\uae30"));
                    return;
                }
                case 41: {
                    this.aq = 42;
                    this.a(14, 806);
                    return;
                }
                case 42: {
                    this.aq = 43;
                    this.a(7, 807);
                    return;
                }
                case 43: {
                    this.aq = 44;
                    this.k(808);
                    return;
                }
                case 44: {
                    this.aq = 45;
                    this.a(7, 809);
                    return;
                }
                case 45: {
                    p p7 = new p(this);
                    p7.o = 30;
                    this.O();
                    this.b(new ac(this, p7, 0, "\ucd9c\uc5f0\ub8cc"));
                    return;
                }
                case 47: {
                    this.aq = 48;
                    this.k(811);
                    return;
                }
                case 48: {
                    this.aq = 49;
                    this.T = true;
                    this.e(0);
                    while (this.h()) {
                        this.g(false);
                        this.p();
                        this.s();
                    }
                    this.o();
                    this.T = false;
                    this.g(false);
                    this.p();
                    this.x();
                    return;
                }
                case 49: {
                    this.a(7, 812);
                    return;
                }
            }
        }
    }

    void g() {
        this.g(false);
    }

    boolean A() {
        if (this.s) {
            return false;
        }
        if ((this.af & 0x80) != 0) {
            this.I();
            return true;
        }
        if (this.n.d.h >= 70 && this.a(15, (int)this.n.d.e, 713)) {
            this.g(false);
            return true;
        }
        if (this.a(5, (int)this.n.d.e, 5)) {
            this.g(false);
            return true;
        }
        if ((this.n.d.x & 4) == 0 && this.a(14, (int)this.n.d.e, 709)) {
            this.g(false);
            return true;
        }
        if (this.P != 0) {
            if (this.R >= 0) {
                if (this.ac != this.D) {
                    this.D = this.ac;
                    this.ap = true;
                }
                this.R = this.R < 10 ? ++this.R : -1;
            }
            if (this.R < 0 && !this.Y) {
                this.Y = true;
                if (this.P > 22) {
                    this.aq = 60;
                    this.b(new i(this));
                } else {
                    this.b(new n((aq)this, (int)this.n.d.e, 541, 399 + this.P, 542));
                }
                return true;
            }
            return false;
        }
        if (this.ad) {
            this.ad = false;
            this.g(false);
            this.b(new r(this));
            return true;
        }
        if (this.q) {
            this.q = false;
            this.aq = 70;
            this.g(false);
            this.q();
            return true;
        }
        if (this.au == 1) {
            this.au = 0;
            this.aq = 90;
            this.g(false);
            this.a(16, 784);
            return true;
        }
        if (this.au == 2) {
            this.au = 0;
            this.aq = 30;
            this.g(false);
            this.a(14, 794);
            return true;
        }
        if (this.au == 3) {
            this.au = 0;
            this.aq = 47;
            this.g(false);
            this.a(6, 810);
            return true;
        }
        this.s = true;
        return false;
    }

    boolean B() {
        if ((this.n.d.t & 0x540) == 1344) {
            this.a(new ag(this, 9));
            return true;
        }
        if ((this.n.d.x & 8) != 0) {
            this.f(1);
            return true;
        }
        if (this.n.d.h > 99) {
            if ((this.n.d.x & 0x20) != 0) {
                this.a(new ag(this, 7));
            } else {
                this.f(1);
            }
            return true;
        }
        if (this.n.d.q < 1) {
            this.f(1);
            return true;
        }
        return false;
    }

    void g(boolean bl) {
        int n2;
        int n3;
        if (this.ap) {
            this.G();
            this.ap = false;
            this.g = (byte)14;
            n3 = this.n.a(this.L, this.D);
            this.J = this.n.f[n3];
            this.z = this.n.f[n3 + 1];
            this.X = this.n.f[n3 + 2];
        }
        this.K();
        int n4 = this.f.p & 2;
        if (bl) {
            int n5 = this.L * 6 + this.D;
            int n6 = (this.f.p & 1) << 1;
            n3 = this.aa + this.n.f[n5 + 140];
            n5 = this.ag + this.n.f[n5 + 164];
            this.a(this.c[0], n3 - 27 + n6, n5 - 5, 0, 0, 12, 11);
            this.a(this.c[0], n3 + 15 - n6, n5 - 5, 30, 0, 12, 11);
            this.a(this.c[0], n3 - 9, n5 - 15 + n6, 12, 0, 18, 8);
            this.a(this.c[0], n3 - 9, n5 + 8 - n6, 42, 0, 18, 8);
            if (this.g > 0) {
                this.g = (byte)(this.g - 1);
                if (this.o && this.D == this.n.d.Q[this.L]) {
                    int n7 = this.ai * this.n.e + 16 >> 1;
                    n6 = Math.min(Math.max(n7, n3), this.k - n7);
                    n2 = Math.min(Math.max(15, n5 - 50), this.j - 43);
                    this.b(n6 - n7, n2, n7 << 1, 26, n3 > n6 ? 3 : 0);
                    this.e.setColor(0xFFFFFF);
                    this.a(this.an, n6 - 1, n2 + 5, 1);
                } else if (this.al[this.D] == 0 || this.F()) {
                    n6 = Math.min(Math.max(42, n3), this.k - 41);
                    n2 = Math.min(Math.max(15, n5 - 36), this.j - 39);
                    this.b(n6 - 42, n2, 83, 22, n3 > n6 ? 3 : 0);
                    this.a(this.H, n6 - 40, n2 + 2, 0, 10, 13, 13);
                    this.a(this.H, n6 - 11, n2 + 2, 13, 10, 13, 13);
                    this.a(this.H, n6 + 13, n2 + 2, 26, 10, 13, 13);
                    this.Q.a(this.X, n6 - 19, n2 + 5, 1);
                    this.Q.a(this.J, n6 + 7, n2 + 5, 1);
                    this.Q.a(this.z, n6 + 31, n2 + 5, 1);
                }
            }
        }
        if (this.P != 0) {
            n2 = this.L * 6 + 140 + this.ac;
            this.a(this.c[3], this.aa + this.n.f[n2] - (this.O >> 1), this.ag + this.n.f[n2 + 24] - this.i + 6, this.O * (n4 >> 1), 0, this.O, this.i);
        }
        if (this.n.d.h >= 70 && n4 == 0) {
            n2 = this.n.d.h >= 90 ? 39 : 26;
            this.a(this.n.n[10], Math.max(this.d - 120, 0), Math.max(this.v - 120, 15), n2, 0, 13, 13);
        }
        this.a(this.n.n[5], Math.min(this.d + 120, this.k) - 21, Math.min(this.v + 120, this.j - 17) - 7, 0, 0, 21, 7);
    }

    void f(boolean bl) {
        int n2 = this.L * 6 + this.D;
        byte by = this.n.f[n2 + 140];
        n2 = this.n.f[n2 + 164];
        this.M = Math.min(Math.max(this.d, 32 - by), this.k - by - 32);
        this.U = Math.min(Math.max(this.v, 45 - n2), this.j - n2 - 37);
        if (bl) {
            this.aa = this.M;
            this.ag = this.U;
        }
    }

    void K() {
        int n2;
        int n3;
        this.e.setClip(0, 15, this.k, this.j - 32);
        this.e.setColor(4830720);
        if (this.k > 240) {
            this.e.fillRect(0, 0, this.d - 120, this.j);
            this.e.fillRect(this.d + 120, 0, this.k - this.d - 120, this.j);
        }
        if (this.j > 240) {
            this.e.fillRect(0, 0, this.k, this.v - 120);
            this.e.fillRect(0, this.v + 120, this.k, this.j - this.v - 120);
        }
        int n4 = this.L * 9 + 40;
        int n5 = this.L * 6 + 140;
        this.e.drawImage(this.N[0], this.aa - 92, this.ag - 74, 20);
        if (!this.f.z) {
            for (n3 = 1; n3 < 5; ++n3) {
                this.e.drawImage(this.N[n3], this.aa + this.n.f[776 + n3], this.ag + this.n.f[780 + n3], 20);
            }
        }
        for (n3 = 0; n3 < 6; ++n3) {
            int n6;
            this.n(n3);
            n2 = this.al[n3];
            int n7 = this.aa;
            int n8 = this.ag;
            if (this.T) {
                n7 += this.e() % 5 - 2;
                n8 += this.e() % 5 - 2;
            }
            if (this.o && n3 == this.n.d.Q[this.L]) {
                n6 = 32 - n2 * 6;
                this.a(this.c[1], n7 + this.n.f[n5] - (n6 >> 1), n8 + this.n.f[n5 + 24] - 27, n2 * 32, 0, 32 - n2 * 6, 35);
            } else if (n2 > 0) {
                if (this.L == 0 && n3 == 0 && n2 == 2) {
                    n6 = this.n.f[232 + this.L * 6 + n3] - n3;
                    this.e.drawImage(this.am[n6], n7 + this.n.f[n4 + n6], n8 + this.n.f[n4 + n6 + 50], 20);
                }
                n6 = this.n.f[232 + this.L * 6 + n3] + n2 - n3 - 1;
                this.e.drawImage(this.am[n6], n7 + this.n.f[n4 + n6], n8 + this.n.f[n4 + n6 + 50], 20);
                if (this.af > 0 && (this.af & 0xF) == n3) {
                    n2 = this.f.p & 2;
                    this.a(this.c[1], this.aa + this.n.f[n5] - 11, this.ag + this.n.f[n5 + 24] - 30 + (n2 >> 1), n2 * 11, 35, 22, 22);
                }
            }
            ++n5;
        }
        this.n(6);
        this.z();
        ++this.S;
        if (this.F != null) {
            if ((this.ao & 0xFF00) == 0) {
                this.ao = (this.e() & 0xF) + 15 << 8 | (this.ao & 7) + this.e() % 3 + 7 & 7;
            }
            n3 = this.K[this.ao & 7] >> 2;
            n4 = this.K[this.ao & 7] & 3;
            if (this.aa + this.ak < 20) {
                n3 = 2;
            } else if (this.aa + this.ak > this.k - 20) {
                n3 = 0;
            }
            if (this.ag + this.aj < 30) {
                n4 = 2;
            } else if (this.ag + this.aj > this.j - 30) {
                n4 = 0;
            }
            this.ak += n3 - 1;
            this.aj += n4 - 1;
            n3 = (n3 << 2) + n4;
            for (n2 = 0; n2 < 8 && this.K[n2] != n3; ++n2) {
            }
            if (n2 >= 8) {
                n2 = this.e() & 7;
            }
            this.ao = this.ao & 0xFF00 | n2;
            this.ao -= 256;
            this.e.drawImage(this.F, this.aa + this.ak, this.ag + this.aj + ((this.f.p & 2) >> 1), 3);
        } else if (this.w > 0) {
            this.e.setColor(0xD0D0FF);
            for (n3 = 0; n3 < this.Z.length; n3 += 2) {
                if (this.Z[n3] > this.j - 17) {
                    this.Z[n3 + 1] = this.e() % (this.k + 50);
                    this.Z[n3] = 15;
                    continue;
                }
                if (this.Z[n3] > 15) {
                    int n9 = n3 + 1;
                    this.Z[n9] = this.Z[n9] + ((this.e() & 7) - 4);
                    switch (this.w) {
                        case 3: {
                            n4 = this.e() % (this.j - 47);
                            this.e.drawLine(this.Z[n3 + 1], n4, this.Z[n3 + 1] - 5, n4 + 30);
                            break;
                        }
                        case 1: 
                        case 2: 
                        case 4: {
                            this.e.drawImage(this.at, this.Z[n3 + 1], this.Z[n3], 20);
                        }
                    }
                }
                int n10 = n3;
                this.Z[n10] = this.Z[n10] + 2;
            }
        }
    }

    void O() {
        this.E();
        this.G();
    }

    void E() {
        this.e.setColor(2359551);
        this.e.fillRect(0, 0, this.k, 15);
        this.e.setColor(4812287);
        this.e.drawLine(0, 0, this.k - 1, 0);
        this.e.setColor(2359466);
        this.e.drawLine(0, 14, this.k - 1, 14);
        if (this.k >= 128) {
            this.e.setColor(0xFF6DFF);
            this.e.drawLine(0, 0, 22, 0);
            this.e.setColor(9568341);
            this.e.drawLine(0, 14, 22, 14);
            this.e.setColor(14361770);
            this.e.fillRect(0, 1, 23, 13);
            this.a(this.H, 2, 2, this.n.f[228 + this.L] * 19, 0, 19, 10);
        }
        this.a(this.k - 92, 3, 23, 9, 0x242455);
        this.a(this.k - 49, 3, 17, 9, 0x242455);
        this.a(this.k - 21, 3, 19, 9, 0x242455);
        this.a(this.H, this.k - 103, 1, 0, 10, 13, 13);
        this.a(this.H, this.k - 61, 1, 13, 10, 13, 13);
        this.a(this.H, this.k - 31, 1, 26, 10, 13, 13);
        this.a(this.H, this.k - 68, 3, 55, 43, 6, 9);
        this.Q.a(this.n.d.C, this.k - 71, 4, 0);
        this.Q.a(this.n.d.B, this.k - 34, 4, 0);
        this.Q.a(this.n.d.R, this.k - 4, 4, 0);
    }

    void G() {
        byte by;
        this.e.setColor(2359551);
        this.e.fillRect(0, this.j - 17, this.k, 17);
        int n2 = Math.min(94, this.k - 34);
        this.e.setColor(0x242455);
        this.e.drawRect(0, this.j - 17, n2, 16);
        this.e.setColor(170);
        this.e.fillRect(1, this.j - 15, n2 - 1, 14);
        this.e.drawRect(this.k - 33, this.j - 17, 32, 16);
        this.e.drawLine(this.k - 17, this.j - 16, this.k - 17, this.j - 2);
        this.a(this.n.n[0], 2, this.j - 14, 123, 10, 2, 2);
        this.a(this.n.n[0], n2 - 3, this.j - 14, 123, 10, 2, 2);
        this.a(this.n.n[0], 2, this.j - 4, 123, 10, 2, 2);
        this.a(this.n.n[0], n2 - 3, this.j - 4, 123, 10, 2, 2);
        this.e.setColor(4812287);
        this.e.drawLine(this.k - 32, this.j - 16, this.k - 18, this.j - 16);
        this.e.drawLine(this.k - 32, this.j - 16, this.k - 32, this.j - 2);
        this.e.drawLine(this.k - 16, this.j - 16, this.k - 2, this.j - 16);
        this.e.drawLine(this.k - 16, this.j - 16, this.k - 16, this.j - 2);
        this.e.setColor(0xFFFFFF);
        this.a(this.n.a(this.L, this.D, this.al[this.D]), n2 >> 1, this.j - 14, 1);
        this.a(this.I, this.k - 31, this.j - 15, this.W[this.D] ? 13 : 0, 0, 13, 13);
        n2 = this.af > 0 && this.D == (this.af & 0xF) ? 91 : (this.H() ? 65 : (this.o ? (this.D == this.n.d.Q[this.L] ? 78 : 39) : ((by = this.al[this.D]) > 1 ? 39 : (by == 1 ? (!this.F() ? 39 : 52) : 26))));
        this.a(this.I, this.k - 15, this.j - 15, n2, 0, 13, 13);
    }

    void z() {
        int n2;
        int n3;
        if (this.ae == null || (this.n.j & 0x20) != 0) {
            return;
        }
        switch (this.L) {
            case 1: {
                if (this.B == 0 || this.B > 240) {
                    this.B = -30;
                }
                if (this.B < 0) {
                    ++this.B;
                    if (this.B >= 0) {
                        this.B = 8;
                    }
                    n3 = 8;
                    n2 = -22;
                    break;
                }
                n3 = this.B;
                if (n3 < 60) {
                    n2 = (n3 >> 1) - 26;
                    this.B += 4;
                    break;
                }
                n2 = (n3 >> 2) - 11;
                this.B += 8;
                break;
            }
            case 3: {
                if (this.B == 0) {
                    this.B = -30;
                }
                if (this.al[5] == 2) {
                    if (this.B < 0) {
                        ++this.B;
                        if (this.B >= 0) {
                            this.B = 500;
                        }
                        n3 = 90;
                        n2 = 24;
                        break;
                    }
                    n3 = this.B < 328 ? this.B - 88 : this.B - 410;
                    n2 = 69 - (n3 >> 1);
                    this.B -= 4;
                    if (this.B >= 178) break;
                    this.B = 0;
                    break;
                }
                if (this.B < 0) {
                    ++this.B;
                    if (this.B >= 0) {
                        this.B = 500;
                    }
                    n3 = 102;
                    n2 = 27;
                    break;
                }
                n3 = this.B < 344 ? this.B - 104 : this.B - 398;
                n2 = 78 - (n3 >> 1);
                this.B -= 2;
                if (this.B >= 206) break;
                this.B = 0;
                break;
            }
            default: {
                return;
            }
        }
        this.e.drawImage(this.ae, this.aa + n3 - 120, this.ag + n2, 20);
    }

    void n(int n2) {
        int n3;
        int n4;
        if ((this.n.j & 0x20) != 0) {
            return;
        }
        int n5 = 0;
        for (n4 = 0; n4 < this.ar.length; n4 += 7) {
            if (this.ar[n4 + 5] <= 0) {
                if ((this.e() & 0xFF) > this.n.d.P[this.L]) {
                    this.ar[n4] = 6;
                    this.ar[n4 + 6] = 1818;
                    this.ar[n4 + 5] = 64;
                } else {
                    do {
                        this.ar[n4 + 6] = this.e() & 3;
                        for (n3 = 0; n3 < this.ar.length && (n3 == n4 || this.ar[n3 + 6] != this.ar[n4 + 6]); n3 += 7) {
                        }
                    } while (n3 < this.ar.length);
                    n3 = this.ar[n4 + 6] * 5 + 798 + this.L * 20;
                    this.ar[n4] = this.n.f[n3 + 4];
                    this.ar[n4 + 1] = this.n.f[n3];
                    this.ar[n4 + 2] = this.n.f[n3 + 1];
                    this.ar[n4 + 3] = this.n.f[n3 + 2];
                    this.ar[n4 + 4] = this.n.f[n3 + 3];
                    if ((this.e() & 1) == 0) {
                        n3 = this.ar[n4 + 1];
                        this.ar[n4 + 1] = this.ar[n4 + 3];
                        this.ar[n4 + 3] = n3;
                        n3 = this.ar[n4 + 2];
                        this.ar[n4 + 2] = this.ar[n4 + 4];
                        this.ar[n4 + 4] = n3;
                    }
                    this.ar[n4 + 5] = (Math.abs(this.ar[n4 + 3] - this.ar[n4 + 1]) >> 1) + 1;
                    this.ar[n4 + 3] = this.ar[n4 + 3] > this.ar[n4 + 1] ? 2 : -2;
                    int n6 = this.ar[n4 + 4] = this.ar[n4 + 4] > this.ar[n4 + 2] ? 1 : -1;
                }
            }
            if (this.ar[n4] != n2) continue;
            if (this.ar[n4 + 6] != 1818) {
                if (n5 < this.V.length) {
                    if (this.ar[n4 + 6] == 0 && this.L == 0 && this.ae != null && this.ar[n4 + 3] > 0) {
                        this.V[n5++].a(this.ae, this.aa + this.ar[n4 + 1], this.ag + this.ar[n4 + 2] - 10, 0, 0, 24, 21);
                    } else if (this.ar[n4 + 6] == 0 && this.L == 2 && this.ae != null && this.al[5] == 0 && this.ar[n4 + 3] < 0) {
                        this.V[n5++].a(this.ae, this.aa + this.ar[n4 + 1] - 8, this.ag + this.ar[n4 + 2] - 3, 0, 0, 15, 9);
                    } else if (this.ar[n4 + 6] == 2 && this.L == 2 && this.ae != null && this.al[5] != 0 && this.ar[n4 + 3] > 0) {
                        this.V[n5++].a(this.ae, this.aa + this.ar[n4 + 1], this.ag + this.ar[n4 + 2] - 6, 0, 0, 24, 18);
                    } else {
                        n3 = this.ar[n4 + 3] * this.ar[n4 + 4] > 0 ? 1 : 0;
                        this.V[n5++].a(this.c[1], this.aa + this.ar[n4 + 1] - 8, this.ag + this.ar[n4 + 2] - 5, 44, n3 != 0 ? 35 : 44, 15, n3 != 0 ? 9 : 10);
                    }
                }
                int n7 = n4 + 1;
                this.ar[n7] = this.ar[n7] + this.ar[n4 + 3];
                int n8 = n4 + 2;
                this.ar[n8] = this.ar[n8] + this.ar[n4 + 4];
            }
            int n9 = n4 + 5;
            this.ar[n9] = this.ar[n9] - 1;
        }
        for (n4 = 0; n4 < this.C.length; n4 += 6) {
            if (this.C[n4 + 5] <= 0) {
                n3 = this.e() % 10 * 5 + 2016 + this.L * 50;
                this.C[n4] = this.n.f[n3 + 4];
                this.C[n4 + 1] = this.n.f[n3];
                this.C[n4 + 2] = this.n.f[n3 + 1];
                this.C[n4 + 3] = this.n.f[n3 + 2];
                this.C[n4 + 4] = this.n.f[n3 + 3];
                if ((this.e() & 1) == 0) {
                    n3 = this.C[n4 + 1];
                    this.C[n4 + 1] = this.C[n4 + 3];
                    this.C[n4 + 3] = n3;
                    n3 = this.C[n4 + 2];
                    this.C[n4 + 2] = this.C[n4 + 4];
                    this.C[n4 + 4] = n3;
                }
                this.C[n4 + 5] = (Math.abs(this.C[n4 + 3] - this.C[n4 + 1]) >> 1) + 1;
                this.C[n4 + 3] = this.C[n4 + 3] > this.C[n4 + 1] ? 1 : -1;
                int n10 = this.C[n4 + 4] = this.C[n4 + 4] > this.C[n4 + 2] ? 1 : -1;
            }
            if (this.C[n4] != n2) continue;
            if (n5 < this.V.length) {
                this.V[n5++].a(this.c[1], this.aa + this.C[n4 + 1] - 2, this.ag + this.C[n4 + 2] - 5, (this.C[n4 + 3] > 0 ? 0 : 8) + ((this.S & 1) << 2), 57, 4, 5);
            }
            int n11 = n4 + 1;
            this.C[n11] = this.C[n11] + this.C[n4 + 3];
            if ((this.S & 1) != 0) continue;
            int n12 = n4 + 2;
            this.C[n12] = this.C[n12] + this.C[n4 + 4];
            int n13 = n4 + 5;
            this.C[n13] = this.C[n13] - 1;
        }
        f.a(this.V, n5, this);
    }

    void C() {
        this.W[this.D] = true;
        this.aq = 5;
        this.J();
    }

    void c(int n2) {
        if (n2 == 10) {
            this.a(this.c[2]);
        } else {
            super.c(n2);
        }
    }

    void c() {
        this.b(-1, 0);
    }

    void b(int n2, int n3) {
        int n4;
        this.c(10);
        boolean bl = false;
        boolean bl2 = false;
        int n5 = n4 = this.al[this.D] == 0 ? 0 : 1;
        if (this.L == 1 || this.L == 2) {
            n4 ^= 1;
        }
        if (n2 < 0) {
            n2 = 3;
            bl2 = n4 == 0 ? this.n.d.B < 17 : this.n.d.R < 17;
            switch (this.n.d.I) {
                case 0: {
                    boolean bl3 = bl = this.n.d.C < 80;
                    if (!bl && !bl2) break;
                    n2 = 8;
                    break;
                }
                case 1: 
                case 2: {
                    boolean bl4 = bl = this.n.d.C < 50;
                    if (!bl && !bl2) break;
                    n2 = 6;
                    break;
                }
                default: {
                    boolean bl5 = bl = this.n.d.C < 30;
                    if (!bl && !bl2) break;
                    n2 = 4;
                }
            }
            n2 = this.e() % n2;
        }
        if (n2 < 3) {
            n2 += (this.n.f[this.n.f[232 + this.L * 6 + this.D] + this.al[this.D] + this.L * 15 + 276] & 0xFF) + n3;
        } else {
            if (!(bl ^ bl2)) {
                boolean bl6 = bl = (this.e() & 1) == 0;
            }
            if (bl) {
                this.aq = 8;
                n2 = 317;
            } else {
                n2 = 321 + n4;
                this.aq = 9;
            }
        }
        if (this.j < 160) {
            n2 = -n2;
        }
        this.k(n2);
    }

    void M() {
        this.O();
        for (int j = 0; j < 6; ++j) {
            if (this.W[j]) continue;
            return;
        }
        if (this.as) {
            return;
        }
        this.as = true;
        this.g(false);
        this.p();
        this.aq = 10;
        this.k(265);
    }

    void e(boolean bl) {
        this.aq = bl && this.n.e(this.E) ? 17 : 6;
        this.E = this.n.a(this.L, this.D);
        p p2 = new p(this);
        p2.n = -this.n.f[this.E];
        p2.c = -this.n.f[this.E + 1];
        p2.o = -this.n.f[this.E + 2];
        p2.g = this.n.f[this.E + 3];
        p2.i = this.n.f[this.E + 9];
        if (bl) {
            p2.d = 1;
        }
        this.b(new ac(this, p2, 1, this.n.a(this.L, this.D, this.al[this.D] + 1)));
    }

    void d(boolean bl) {
        if ((this.n.d.x & 4) == 0) {
            this.n.d.x |= 4;
            this.aq = 20;
        } else {
            this.aq = 5;
        }
        this.W[this.D] = true;
        this.n.f(-this.n.f[this.E]);
        this.n.c(-this.n.f[this.E + 1]);
        this.n.n(-this.n.f[this.E + 2]);
        this.n.m(this.n.f[this.E + 9]);
        this.n.d.H[this.L] = (short)(this.ah + (this.n.f[this.E + 3] << 2));
        this.n.d.Q[this.L] = (byte)this.D;
        if (bl) {
            this.n.i(1);
            if (this.L == 0) {
                if (this.D == 5 && this.al[this.D] == 0) {
                    this.n.d.c |= 0x20000000;
                } else if (this.D == 0 && this.al[this.D] == 1) {
                    this.n.d.c |= 0x40000000;
                }
            }
        }
        this.n();
        this.L();
        this.O();
        this.k(bl ? 345 : 263);
    }

    boolean e(int n2, int n3, int n4) {
        if (this.al[this.D] != n2 - 1) {
            return false;
        }
        if (this.o || (this.n.d.c & 1 << n3) != 0) {
            return false;
        }
        if (n2 == 2 && !this.F()) {
            return false;
        }
        this.E = this.n.a(this.L, this.D);
        if (n3 != 0 && !this.n.e(this.E)) {
            return false;
        }
        this.n.d.c |= 1 << n3;
        this.aq = 16;
        this.k(n4);
        return true;
    }

    boolean F() {
        return this.al[this.D] == 1 && (this.n.d.n & 1 << this.L * 6 + this.D) != 0;
    }

    void D() {
        this.aq = 1;
        this.b(new v(this, 10, 1, 0, 2));
    }

    void L() {
        short s2 = this.n.d.H[this.L];
        if (s2 > this.ah) {
            this.o = true;
            if (this.P == 0 || this.P > 22 || this.ac != this.n.d.Q[this.L]) {
                this.W[this.n.d.Q[this.L]] = true;
            }
            int n2 = s2 - this.ah;
            this.an = "D-";
            if (n2 >= 4) {
                this.an = this.an + (n2 >> 2) + "\uac1c\uc6d4";
            }
            if ((n2 & 3) != 0) {
                this.an = this.an + (n2 & 3) + "\uc8fc";
            }
            this.ai = this.an.getBytes().length;
        } else {
            this.o = false;
        }
    }

    void I() {
        int n2;
        int n3;
        int n4;
        int n5;
        int n6;
        int n7;
        boolean bl = true;
        this.D = this.af & 0xF;
        this.af &= 0x7F;
        if ((this.n.d.a & 1 << 28 + this.L) == 0) {
            for (n7 = 0; bl && n7 < 6; ++n7) {
                if (this.al[n7] != 0) continue;
                bl = false;
            }
            for (n7 = 0; bl && n7 < 6; ++n7) {
                if ((this.n.d.n & 1 << this.L * 6 + n7) == 0 || this.al[n7] >= 2) continue;
                bl = false;
            }
        } else {
            bl = false;
        }
        if (bl) {
            this.n.d.a |= 1 << 28 + this.L;
            this.n();
            n6 = 9;
        } else {
            n6 = 3;
        }
        this.G();
        this.f(true);
        if (this.f.z) {
            n5 = 144;
            n4 = this.aa - 72;
            n3 = 112;
            n2 = this.ag - 54;
        } else {
            n5 = 200;
            n4 = this.aa - 100;
            n3 = 200;
            n2 = this.ag - 100;
        }
        n4 = Math.max(n4, 15);
        n5 = Math.min(n5, this.k - n4 - 15);
        n2 = Math.max(n2, 30);
        n3 = Math.min(n3, this.j - n2 - 32);
        try {
            Image[] imageArray = new Image[3];
            for (n7 = 0; n7 < 3; ++n7) {
                imageArray[n7] = this.n.c("tm" + n7);
            }
            int[] nArray = new int[n6 * 3];
            this.e(0);
            for (n7 = 0; n7 < 1772; ++n7) {
                if (n7 >= 8 && !this.h()) {
                    switch (this.f.y()) {
                        case 53: 
                        case 131: 
                        case 148: {
                            n7 = 1772;
                        }
                    }
                }
                this.K();
                for (int j = 0; j < n6; ++j) {
                    int n8 = j;
                    nArray[n8] = nArray[n8] - 1;
                    if (nArray[j] < 0) {
                        nArray[j] = (this.e() & 3) + 3;
                        nArray[j + n6] = this.e() % n5 + n4;
                        nArray[j + (n6 << 1)] = this.e() % n3 + n2;
                        continue;
                    }
                    if (nArray[j] >= 3) continue;
                    byte by = this.n.f[771 + nArray[j]];
                    byte by2 = this.n.f[774 + nArray[j]];
                    this.a(imageArray[nArray[j]], nArray[j + n6] - (by >> 1), nArray[j + (n6 << 1)] - (by2 >> 1) - (nArray[j] << 2), by * (j % 3), 0, by, by2);
                }
                this.p();
                this.e(150);
            }
        }
        catch (Exception exception) {
            // empty catch block
        }
        this.y();
        if (bl) {
            this.b(new n((aq)this, (int)this.n.d.e, 399, "@@@@", s.b[61 + this.L]));
        }
    }

    boolean N() {
        int n2 = 2;
        byte by = this.al[this.D];
        if (by > 1) {
            n2 = 4;
        } else if (by == 1) {
            n2 = this.H() ? 7 : (this.F() ? 8 : 4);
        }
        if (!this.W[this.D]) {
            this.aq = 2;
            this.b(new v(this, -1, 4, n2, 0));
            return true;
        }
        return false;
    }

    void a(int n2, String string) {
        if (this.n.d.h < n2) {
            n2 = this.n.d.h;
        }
        p p2 = new p(this);
        p2.i = -n2;
        this.n.m(-n2);
        this.n();
        this.aq = 5;
        this.b(new ac(this, p2, 0, string));
    }

    boolean H() {
        return this.D == 4 && this.L == 1 && this.n.k(14);
    }

    void y() {
        if (this.P == 0) {
            if (this.L == 1 || this.L == 3) {
                this.b(5, true);
            } else {
                this.b(6, true);
            }
        } else {
            this.b(4, true);
        }
    }

    void a(String string, int n2) {
        p p2 = new p(this);
        p2.j = string;
        p2.o = -n2;
        this.aq = 5;
        this.O();
        this.b(new ac(this, p2, 0, "\ube44\uc11c\ub4f1\uc6a9"));
    }

    void J() {
        switch (this.D) {
            case 0: {
                if (this.e(2, 12, 22)) {
                    return;
                }
                if (this.al[this.D] != 2 || this.n.l(5) || (this.n.d.c & 0x40000000) == 0) break;
                this.t = 5;
                this.aq = 80;
                this.k(369);
                return;
            }
            case 1: {
                if (this.F != null) {
                    this.aq = 501;
                    this.k(782);
                    return;
                }
                if (this.n.b(10) && this.e(1, 14, 29)) {
                    return;
                }
                if (this.al[this.D] != 1 || !this.n.h(2) && !this.n.h(4) || this.n.k(13) || (this.e() & 3) != 0) break;
                this.G = this.n.h(2) ? 3 : 5;
                this.t = 7;
                if (this.n.l(this.t) && this.n.e(1074)) {
                    this.aq = 401;
                    this.a(this.G, 527);
                    return;
                }
                this.aq = 411;
                this.a(this.G, 374);
                return;
            }
            case 2: {
                if (this.e(1, 10, 36)) {
                    return;
                }
                if (this.n.k(10)) {
                    if (this.e(2, 11, 43)) {
                        return;
                    }
                    if (this.al[this.D] != 1) break;
                    this.b(-1, 3);
                    return;
                }
                if (this.al[this.D] != 1 || !this.n.h(2) && !this.n.h(4) || (this.e() & 3) != 0) break;
                this.G = this.n.h(2) ? 3 : 5;
                this.t = 4;
                if (this.n.l(this.t) && this.n.e(1053)) {
                    this.aq = 201;
                    this.a(this.G, 524);
                    return;
                }
                this.aq = 211;
                this.a(this.G, 365);
                return;
            }
            case 3: {
                if (!this.e(1, 8, 50)) break;
                return;
            }
            case 4: {
                if (this.n.b(9) && this.e(1, 13, 57)) {
                    return;
                }
                if (this.al[this.D] == 2 && (this.n.h(3) || this.n.h(4)) && !this.n.k(12) && (this.e() & 3) == 0) {
                    this.G = this.n.h(3) ? 4 : 5;
                    this.t = 6;
                    if (this.n.l(this.t) && this.n.e(1067)) {
                        this.aq = 301;
                        this.a(this.G, 526);
                        return;
                    }
                    this.aq = 311;
                    this.a(this.G, 370);
                    return;
                }
                if (this.n.d.h <= 0) break;
                switch (this.al[this.D]) {
                    case 1: {
                        this.aq = 101;
                        this.k(58);
                        return;
                    }
                    case 2: {
                        this.aq = 102;
                        this.k(62);
                        return;
                    }
                }
                break;
            }
            case 5: {
                if (this.n.k(9) && this.e(1, 9, 69)) {
                    return;
                }
                if (this.al[this.D] != 1 || this.n.l(3) || (this.n.d.c & 0x20000000) == 0) break;
                this.t = 3;
                this.aq = 80;
                this.k(364);
                return;
            }
        }
        this.c();
    }

    void m(int n2) {
        switch (n2) {
            case 101: {
                this.a(40, "\uc601\uc591\uc81c");
                return;
            }
            case 102: {
                this.a(50, "\uace0\uae09\uc601\uc591\uc81c");
                return;
            }
            case 201: {
                this.aq = 202;
                this.l(10);
                return;
            }
            case 202: {
                int n3;
                if (this.A == 100) {
                    this.aq = 203;
                    n3 = 536;
                } else {
                    this.aq = 81;
                    n3 = 537;
                }
                this.a(this.G, n3);
                return;
            }
            case 203: {
                this.aq = 5;
                this.j(10);
                this.n();
                this.O();
                return;
            }
            case 211: {
                this.aq = 212;
                this.b(new l(this, 366, 367));
                return;
            }
            case 212: {
                int n4;
                if (this.A == 100) {
                    this.aq = 80;
                    n4 = 368;
                } else {
                    this.aq = 81;
                    n4 = 513;
                }
                this.a(this.G, n4);
                return;
            }
            case 301: {
                this.aq = 302;
                this.l(12);
                return;
            }
            case 302: {
                int n5;
                if (this.A == 100) {
                    this.aq = 303;
                    n5 = 536;
                } else {
                    this.aq = 81;
                    n5 = 537;
                }
                this.a(this.G, n5);
                return;
            }
            case 303: {
                this.aq = 5;
                this.j(12);
                this.n();
                this.O();
                return;
            }
            case 311: {
                this.aq = 312;
                this.b(new l(this, 371, 372));
                return;
            }
            case 312: {
                int n6;
                if (this.A == 100) {
                    this.aq = 80;
                    n6 = 513;
                } else {
                    this.aq = 81;
                    n6 = 373;
                }
                this.a(this.G, n6);
                return;
            }
            case 401: {
                this.aq = 402;
                this.l(13);
                return;
            }
            case 402: {
                int n7;
                if (this.A == 100) {
                    this.aq = 403;
                    n7 = 536;
                } else {
                    this.aq = 81;
                    n7 = 537;
                }
                this.a(this.G, n7);
                return;
            }
            case 403: {
                this.aq = 5;
                this.j(13);
                this.n();
                this.O();
                return;
            }
            case 411: {
                this.aq = 412;
                this.b(new l(this, 375, 376));
                return;
            }
            case 412: {
                int n8;
                if (this.A == 100) {
                    this.aq = 80;
                    n8 = 513;
                } else {
                    this.aq = 81;
                    n8 = 377;
                }
                this.a(this.G, n8);
                return;
            }
            case 501: {
                this.aq = 502;
                this.a(7, 783);
                return;
            }
            case 502: {
                p p2 = new p(this);
                p2.d = 3;
                this.n.i(3);
                this.n();
                this.aq = 5;
                this.b(new ac(this, p2, 0, "\uc5b4\ub9b0\uc774\ub0a0\ud589\uc0ac"));
                return;
            }
        }
    }
}
