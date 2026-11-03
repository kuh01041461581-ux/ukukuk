/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class b
extends aq {
    Image w;
    int s;
    int p;
    int t;
    int c;
    int i;
    int q;
    int o;
    w g;

    b(aq aq2) {
        super(aq2);
    }

    void b() {
        try {
            this.w = this.n.c("tj");
        }
        catch (Exception exception) {
            // empty catch block
        }
        this.g = new w(this, this.n.n[1], 0, 0, 5, 8);
        this.s = this.d - 60;
        this.p = this.v - 56;
        this.f();
        this.c(this.v - 56, 112, 3, 68, 2);
        this.a(this.w, this.s + 16, this.p + 5, 252, 9, 39, 10);
        this.a(this.s + 15, this.p + 29, 90, 50, 27904);
        this.a(this.s + 15, this.p + 81, 44, 15, 18688);
        this.a(this.s + 61, this.p + 81, 44, 15, 18688);
        this.a(this.s + 37, this.p + 84, 11, 10, 0);
        this.a(this.s + 83, this.p + 84, 11, 10, 0);
        this.a(this.n.n[2], this.s + 18, this.p + 82, 13, 10, 13, 13);
        this.a(this.n.n[2], this.s + 64, this.p + 82, 26, 10, 13, 13);
        this.u();
    }

    void d(int n) {
        switch (n) {
            case 53: 
            case 131: 
            case 148: {
                switch (this.t) {
                    case 1: {
                        this.t = 2;
                        this.c = 0;
                        this.m(0);
                        return;
                    }
                    case 3: {
                        this.v();
                        return;
                    }
                }
                return;
            }
            case 8: {
                if (this.t == 3) {
                    this.v();
                }
                return;
            }
        }
    }

    void b(int n) {
        if (this.t == 0 && this.c > 2) {
            this.t = 1;
        }
        this.e.setColor(27904);
        this.e.fillRect(this.s + 26, this.p + 40, 68, 36);
        if (this.t < 2) {
            this.i = this.e() % 6;
            this.o = 168 + this.f.p % 3 * 28;
            if ((this.f.p & 2) == 0) {
                this.a(this.w, this.s + 53, this.p + 67, 252, 0, 14, 9);
            }
        } else {
            this.o = this.i * 28;
        }
        this.a(this.w, this.s + 26, this.p + 40, this.o, 0, 28, 29);
        if (this.t == 2 && this.c > 2) {
            this.t = 3;
            this.n.f(this.i + 1);
            this.n.c(this.q + 1);
            this.n.d.G = (byte)(this.n.d.G + 1);
            this.n();
            this.m(1);
        }
        if (this.t < 3) {
            this.q = this.e() % 6;
            this.o = 168 + (this.f.p + 1) % 3 * 28;
        } else {
            this.o = this.q * 28;
        }
        this.a(this.w, this.s + 66, this.p + 40, this.o, 0, 28, 29);
        this.p();
        ++this.c;
    }

    void m(int n) {
        int n2;
        if (n == 0) {
            n = this.i;
            n2 = 44;
        } else {
            n = this.q;
            n2 = 90;
        }
        this.e.setColor(0xFFFFFF);
        this.g.a(n + 1, this.s + n2, this.p + 85, 0);
        this.a(this.n.n[2], this.s + n2 + 5, this.p + 84, 46, 10, 7, 10);
    }
}
