/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class al
extends aq {
    int s;
    int i;
    Image c;
    Image t;
    Image o;
    Image p;
    Image q;
    w g;

    al(aq aq2) {
        super(aq2);
    }

    void b() {
        int n;
        this.s = this.d - 120;
        this.i = Math.max(this.v - 120, -40);
        this.g = new w(this, this.n.n[1], 0, 15, 3, 5);
        try {
            this.t = this.n.c("z3");
            for (n = 0; n < this.k; n += 2) {
                this.e.drawImage(this.t, n, this.i + 85, 20);
            }
            this.e.setColor(2378239);
            this.e.fillRect(0, 0, this.k, this.i + 85);
            this.e.setColor(4830720);
            this.e.fillRect(0, this.i + 156, this.k, this.j - this.i - 156);
            this.t = this.n.c("z1");
            for (n = this.s + 55; n < this.k - 120; n += 120) {
            }
            while (n > -120) {
                this.e.drawImage(this.t, n, this.i + 119, 20);
                n -= 120;
            }
            this.p();
        }
        catch (Exception exception) {
            // empty catch block
        }
        try {
            this.e(0);
            for (n = 1950; n < 1980; n += 3) {
                int n2 = this.s + (this.n.f[n + 1] & 0xFF) - 42;
                int n3 = this.i + (this.n.f[n + 2] & 0xFF);
                if (n2 < -40 || n3 < -40 || n2 >= this.k || n3 >= this.j) continue;
                this.e.drawImage(this.n.c("t" + this.n.f[n]), n2, n3, 20);
                this.p();
                this.s();
            }
        }
        catch (Exception exception) {
            // empty catch block
        }
        if (!this.f.z) {
            this.c = this.a(0, 0, this.k, this.j);
        }
        try {
            this.p = this.n.c("z5");
            this.e(0);
            for (n = 1980; n < 2016; n += 6) {
                if (n == 1992) {
                    this.e.drawImage(this.p, this.s + (this.n.f[n] & 0xFF), this.i + this.n.f[n + 1], 20);
                } else {
                    this.a(this.n.n[6], this.s + (this.n.f[n] & 0xFF), this.i + this.n.f[n + 1], this.n.f[n + 2], this.n.f[n + 3], this.n.f[n + 4], this.n.f[n + 5]);
                }
                this.p();
                this.s();
            }
        }
        catch (Exception exception) {
            // empty catch block
        }
        try {
            this.t = this.n.c("z4");
            this.o = this.n.c("z2");
            this.q = this.n.c("z8");
        }
        catch (Exception exception) {
            // empty catch block
        }
        if (!this.f.z) {
            this.e(0);
            for (n = -71; n <= 49; n += 40) {
                this.m(n);
                this.p();
                this.s();
            }
            this.m(45);
            this.p();
            this.s();
        }
        this.u();
        this.o();
        this.b(1, true);
    }

    void d(int n) {
        this.a(new ae(this, this.c));
    }

    void b(int n) {
        this.m(49);
        this.e.drawImage(this.t, Math.min(this.s + 173, this.k - 21), this.i + 52 + (this.f.p & 2) * (this.f.z ? 0 : 1), 20);
        this.e.drawImage(this.q, this.k - 23, 1, 20);
        this.e.setColor(4830720);
        this.e.fillRect(0, this.j - 7, 35, 7);
        this.a(this.n.n[5], 1, this.j - 6, 21, 0, 16, 5);
        this.g.a(1046, 19, this.j - 6, 2);
        if (this.f.i) {
            this.l.setPixel(0, 0, 0xA0A0FF);
        }
        if (this.f.z | (this.f.p & 2) == 0) {
            this.a(this.n.n[5], this.d - 24, this.j - 30, 0, 7, 47, 7);
        }
        this.p();
    }

    void m(int n) {
        this.a(this.c, 0, 0);
        this.c();
        this.e.drawImage(this.o, this.s + 62, this.i + n, 20);
    }

    void c() {
        for (int i = 1980; i < 2016; i += 6) {
            if (i == 1992) {
                this.e.drawImage(this.p, this.s + (this.n.f[i] & 0xFF), this.i + this.n.f[i + 1], 20);
                continue;
            }
            this.a(this.n.n[6], this.s + (this.n.f[i] & 0xFF), this.i + this.n.f[i + 1], this.n.f[i + 2], this.n.f[i + 3], this.n.f[i + 4], this.n.f[i + 5]);
        }
    }
}
