/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class u
extends aq {
    boolean c;
    Image g;
    e i;

    u(aq aq2) {
        super(aq2);
    }

    void b() {
        this.c = false;
        this.i = new e(this);
        try {
            this.g = this.n.c("z0");
        }
        catch (Exception exception) {
            aq.r();
        }
        this.b(0, false);
        this.e(0);
        for (int i = 5; i >= 0; --i) {
            this.i(2378239);
            this.e.drawImage(this.g, this.d - 40, this.v - 27, 20);
            this.i.a(0, 0, this.k, this.j, i, 2);
            this.p();
            this.s();
        }
        this.c(0, 1500, 0);
        this.u();
    }

    void b(int n) {
        this.c();
    }

    void d(int n) {
        if (!this.c) {
            this.c();
        }
    }

    void c() {
        this.c = true;
        this.l();
        this.a(new al(this));
    }
}
