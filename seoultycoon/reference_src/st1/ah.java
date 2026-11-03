/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class ah
extends aq {
    int i;
    int c;
    int o;
    int p;
    Image q;
    Image s;
    Image g;

    ah(aq aq2) {
        super(aq2);
        this.a = aq2;
    }

    void b() {
        try {
            this.q = this.n.c("tg");
            this.s = this.n.c("th");
        }
        catch (Exception exception) {
            aq.r();
        }
        if (!this.a(8, (int)this.n.d.e, 8)) {
            this.y();
        } else {
            this.p = 1;
        }
        this.u();
    }

    void d(int n) {
        switch (n) {
            case 50: 
            case 52: 
            case 141: 
            case 142: {
                this.i = (this.i + this.c - 1) % this.c;
                break;
            }
            case 54: 
            case 56: 
            case 145: 
            case 146: {
                this.i = (this.i + 1) % this.c;
                break;
            }
            case 53: 
            case 131: 
            case 148: {
                this.z();
                return;
            }
            case 8: {
                this.v();
                return;
            }
            default: {
                return;
            }
        }
        this.c();
    }

    void c() {
        this.a(this.g, this.d - 56, this.v - 31);
        if (this.i < this.o) {
            this.o = this.i;
        } else if (this.o < this.i - 3) {
            this.o = this.i - 3;
        }
        int n = this.d - 60;
        int n2 = this.v - 56;
        this.a(this.q, n + 4, n2 + 25 + (this.i - this.o) * 16, 0, 0, 112, 23);
        int n3 = Math.min(this.o + 4, this.c);
        int n4 = n2 + 16;
        for (int i = this.o; i < n3; ++i) {
            this.a(this.s, n + 16, n4 += 16, 0, i * 10, 98, 10);
            if ((this.n.d.N & 1 << i) != 0) {
                this.e.setColor(0xFF8000);
            } else {
                if (i >= this.c - 1) continue;
                this.e.setColor(0);
            }
            this.e.drawLine(n + 16, n4 + 3, n + 114, n4 + 3);
            this.e.drawLine(n + 16, n4 + 6, n + 114, n4 + 6);
        }
        this.p();
    }

    void z() {
        if ((this.n.d.N & 1 << this.i) != 0) {
            this.k(748);
            return;
        }
        if (this.i < this.c - 1) {
            this.k(749);
            return;
        }
        p p2 = new p(this);
        p2.o = -(40 + this.i * 5);
        this.p = 2;
        this.b(new ac(this, p2, 2, this.s, 0, this.i * 10, 98));
    }

    void x() {
        int n = this.p;
        this.p = 0;
        switch (n) {
            case 1: {
                this.y();
                return;
            }
            case 2: {
                if (this.A != 100) break;
                if (this.n.d.C < 40 + this.i * 5) {
                    n = 348;
                } else {
                    n = 347;
                    this.p = 3;
                }
                this.k(n);
                return;
            }
            case 3: {
                p p2 = new p(this);
                p2.d = this.i < 6 ? 2 : 3;
                p2.a(this.i / 3 + 1);
                this.n.d.N = (short)(this.n.d.N | 1 << this.i);
                this.n.n(-(40 + this.i * 5));
                this.n.i(p2.d);
                for (n = 0; n < 4; ++n) {
                    this.n.d.r[n] = (byte)Math.min(this.n.d.r[n] + (this.i / 3 + 1), 100);
                }
                this.n();
                this.p = 4;
                this.b(new ac(this, p2, 0, this.s, 0, this.i * 10, 98));
                return;
            }
            case 4: {
                this.a.A = 1;
                this.v();
                return;
            }
        }
        this.c();
    }

    void y() {
        this.f();
        this.c(this.v - 56, 112, 3, 32, 0);
        this.a(this.q, this.d - 51, this.v - 51, 112, 0, 19, 10);
        this.g = this.a(this.d - 56, this.v - 31, 112, 71);
        this.c = (this.n.f() >> 4) + 1;
        this.i = this.c - 1;
        this.c();
    }
}
