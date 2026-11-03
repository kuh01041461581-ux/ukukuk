/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class v
extends aq {
    int s;
    int o;
    int c;
    int q;
    int p;
    int t;
    d g;
    Image i;

    v(aq aq2, int n, int n2, int n3, int n4) {
        super(aq2);
        this.a = aq2;
        this.a(n, n2, n3, n4, false);
    }

    v(aq aq2, int n, int n2, int n3, int n4, boolean bl) {
        super(aq2);
        this.a = aq2;
        this.a(n, n2, n3, n4, bl);
    }

    void a(int n, int n2, int n3, int n4, boolean bl) {
        if (n >= 0) {
            this.g = this.n.g(n);
            this.g.a(15, true);
        }
        this.s = n2;
        this.o = n3;
        this.c = n4;
        if (this.s != this.o && bl) {
            this.p = 1;
        }
    }

    void b() {
        if (this.g == null) {
            this.t = this.v - 15;
            this.i = this.a(this.d - 51, this.t, 104, 35);
            this.b(this.d - 51, this.t, 104, 35, this.c);
            this.q = this.v - 7;
        } else {
            String string;
            int n = 17 * this.g.d() + 37;
            int n2 = n - 5 >> 1;
            this.t = this.v - n2;
            this.i = this.a(this.d - 51, this.t, 104, n);
            this.b(this.d - 51, this.t, 104, n, this.c);
            this.q = this.v - n2 + 9;
            this.e.setColor(0xFFFFFF);
            while ((string = this.g.f()) != null) {
                this.a(string, this.d, this.q, 1);
                this.q += 17;
            }
            this.q += 2;
        }
        this.c();
        this.u();
    }

    void d(int n) {
        switch (n) {
            case 52: 
            case 142: {
                if (this.s == this.o) {
                    return;
                }
                this.p = 0;
                break;
            }
            case 54: 
            case 145: {
                if (this.s == this.o) {
                    return;
                }
                this.p = 1;
                break;
            }
            case 53: 
            case 131: 
            case 148: {
                this.y();
                return;
            }
            case 8: {
                this.z();
                return;
            }
            default: {
                return;
            }
        }
        this.c();
    }

    void c() {
        this.b(this.q, this.s, this.o, this.p);
        this.p();
    }

    void y() {
        this.a.A = 100 + this.p;
        this.z();
    }

    void z() {
        this.a(this.i, this.d - 51, this.t);
        this.v();
    }
}
