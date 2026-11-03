/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class ae
extends aq {
    int g;
    int o;
    Image c;
    Image p;
    Image i;
    boolean q;

    ae(aq aq2, Image image) {
        super(aq2);
        this.p = image;
        this.q = image != null;
    }

    void b() {
        if (this.n.d.T > 0) {
            this.g = 1;
        }
        if (this.q) {
            this.e(0);
            for (int i = 0; i < 60; i += 10) {
                this.a(this.p, 0, i);
                this.p();
                this.s();
            }
        }
        try {
            this.p = this.n.c("z6");
            this.i = this.n.c("z7");
        }
        catch (Exception exception) {
            aq.r();
        }
        if (this.q) {
            this.c = this.a(0, 0, this.k, this.j);
        }
        this.d(true);
        this.o();
        this.u();
    }

    void d(int n) {
        switch (n) {
            case 50: 
            case 52: 
            case 141: 
            case 142: {
                this.g = (this.g + 6) % 7;
                break;
            }
            case 54: 
            case 56: 
            case 145: 
            case 146: {
                this.g = (this.g + 1) % 7;
                break;
            }
            case 53: 
            case 131: 
            case 148: {
                switch (this.g) {
                    case 0: {
                        if (this.n.d.T > 0) {
                            this.o = 1;
                            this.b(new v(this, 714, 1, 0, 1, true));
                        } else {
                            this.c();
                        }
                        return;
                    }
                    case 1: {
                        if (this.n.d.T > 0) {
                            this.a(new c(this));
                        } else {
                            this.c();
                        }
                        return;
                    }
                    case 2: 
                    case 3: 
                    case 4: 
                    case 5: 
                    case 6: {
                        this.o = 2;
                        this.b(new m(this, this.p, this.g));
                        return;
                    }
                }
                return;
            }
            case 8: {
                this.a(new u(this));
                return;
            }
            case 190: {
                this.n.d.V = "???";
                return;
            }
        }
        this.d(true);
    }

    void x() {
        switch (this.o) {
            case 1: {
                if (this.A == 100) {
                    this.c();
                } else {
                    this.d(true);
                }
                return;
            }
            case 2: {
                this.d(true);
                return;
            }
        }
    }

    void d(boolean bl) {
        int n = this.d - 31;
        int n2 = this.v - 57;
        int n3 = 0;
        int n4 = 0;
        if (this.c == null) {
            this.i(2378239);
        }
        this.a(this.c, 0, 0);
        for (int i = 0; i < 7; ++i) {
            if (i == this.g) {
                this.a(this.i, n, n2 - 1, n4, 13, 15, 15);
                this.a(this.p, n + 20, n2, 42, n3, 42, 13);
            } else {
                this.a(this.i, n, n2, n4, 0, 15, 13);
                this.a(this.p, n + 20, n2, 0, n3, 42, 13);
            }
            n2 += 17;
            n3 += 13;
            n4 += 15;
        }
        if (bl) {
            this.p();
        }
    }

    void c() {
        aj aj2 = this.n.d;
        this.n.d = new aj();
        this.n.a(aj2);
        this.n.d.T = 1;
        this.n.d.F = (short)(this.n.d.F | 1);
        this.n.d.F = (short)(this.n.d.F | 0x2000);
        if (this.n.d.s > 0) {
            this.n.d.x |= 4;
        }
        this.n();
        this.f(1);
    }

    void g() {
        this.d(false);
    }
}
