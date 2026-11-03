/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class l
extends aq {
    int c;
    int i;
    int o;
    Image g;

    l(aq aq2, int n, int n2) {
        super(aq2);
        this.a = aq2;
        this.c = n;
        this.i = n2;
    }

    void b() {
        int n = 100;
        int n2 = 0;
        d d2 = this.n.g(this.i);
        d2.a(n, false);
        n2 += d2.d();
        d2 = this.n.g(this.c);
        d2.a(n, false);
        int n3 = (n2 += d2.d()) * (this.n.l + 2) + 30;
        this.o = this.v - (n3 >> 1) - 10;
        this.g = this.a(this.d - 60, this.o, 120, n3);
        this.c(this.o, n3, 3, 34, 1);
        this.e.setColor(0xFFFFFF);
        this.a("\uc120\ud0dd", this.d - 41, this.o + 4, 1);
        n3 = this.o + 23;
        while (d2.c() > 0) {
            this.a(d.a(d2.f(), "1."), this.d - 55, n3, 4);
            n3 += this.n.l + 2;
        }
        d2 = this.n.g(this.i);
        d2.a(n, false);
        n3 += 3;
        while (d2.c() > 0) {
            this.a(d.a(d2.f(), "2."), this.d - 55, n3, 4);
            n3 += this.n.l + 2;
        }
        this.p();
        this.u();
    }

    void d(int n) {
        switch (n) {
            case 49: {
                this.a.A = 100;
                break;
            }
            case 50: {
                this.a.A = 101;
                break;
            }
            default: {
                return;
            }
        }
        this.a(this.g, this.d - 60, this.o);
        this.v();
    }
}
