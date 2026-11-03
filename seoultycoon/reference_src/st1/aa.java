/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class aa
extends aq {
    int c;
    int o;
    int i = 0;
    int q;
    Image p;
    String[] g = new String[]{"\uacc4\uc18d\ud558\uae30", "\uac8c\uc784\ubc29\ubc95", "\ud658\uacbd\uc124\uc815", "\uba54\uc778\uba54\ub274"};

    aa(aq aq2, int n) {
        super(aq2);
        this.a = aq2;
        this.i = n;
        this.q = this.n.e << 3;
    }

    void b() {
        this.c();
        try {
            this.p = this.n.c("z6");
        }
        catch (Exception exception) {
            aq.r();
        }
        this.p();
        this.u();
    }

    void d(boolean bl) {
        this.a(3, this.j - 92, this.q + 9, 71, 85);
        this.a(5, this.j - 88 + (this.c << 4), this.q + 5, this.n.l + 4, 2396927);
        this.m(0);
        if (bl) {
            this.p();
        }
    }

    void c() {
        this.a(0, this.j - 99, this.q + 15, 84, 0, false);
        this.d(false);
    }

    void m(int n) {
        int n2 = 0;
        for (int i = 0; i < 4; ++i) {
            this.a(this.g[n + i], 7, this.j - 86 + n2, 4, 0xFFFFFF);
            n2 += 16;
        }
    }

    void d(int n) {
        switch (n) {
            case 141: {
                this.c = this.c + 3 & 3;
                break;
            }
            case 146: {
                this.c = this.c + 1 & 3;
                break;
            }
            case 53: 
            case 131: 
            case 148: {
                if (!this.n(this.c)) break;
                return;
            }
            case 8: {
                this.a.g();
                this.v();
                return;
            }
        }
        this.d(true);
    }

    void x() {
        int n = this.o;
        this.o = 0;
        switch (n) {
            case 1: {
                this.g();
                this.p();
                return;
            }
        }
    }

    boolean n(int n) {
        switch (n) {
            case 0: {
                this.a.g();
                this.v();
                return true;
            }
            case 1: {
                this.o = 1;
                this.b(new m(this, this.p, 3));
                break;
            }
            case 2: {
                this.o = 1;
                this.b(new m(this, this.p, 4));
                break;
            }
            case 3: {
                this.m();
                this.a(new ae(this, null));
            }
        }
        return false;
    }

    void g() {
        this.a.g();
        this.c();
    }
}
