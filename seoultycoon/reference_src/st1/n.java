/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class n
extends aq {
    int z;
    int q;
    int i;
    int C;
    int B;
    int o;
    int t;
    d g;
    Image p;
    Image s;
    Image E;
    String c;
    String D;
    boolean w;

    n(aq aq2, int n2, int n3) {
        super(aq2);
        this.a(n2, n3, null, null);
    }

    n(aq aq2, int n2, int n3, String string, String string2) {
        super(aq2);
        this.a(n2, n3, string, string2);
    }

    n(aq aq2, int n2, int n3, int n4, int n5) {
        super(aq2);
        this.a(n2, n3, null, null);
        this.o = n4;
        this.t = n5;
    }

    void a(int n2, int n3, String string, String string2) {
        if (n2 == 0) {
            switch (this.n.d.m & 3) {
                case 2: {
                    n2 = 15;
                    break;
                }
                case 3: {
                    this.n.d.m ^= 1;
                    this.n();
                }
            }
        }
        this.i = n2;
        if (this.i >= 6 && this.i <= 8 && !this.n.g()) {
            this.i += 3;
        }
        this.g = this.n.a(Math.abs(n3), true);
        this.w = n3 >= 0 && !this.f.z;
        this.c = string;
        this.D = string2;
        this.o = -1;
        this.t = -1;
    }

    void b() {
        this.p = this.a(0, this.j - 52, this.k, 52);
        try {
            if (this.i < 6 || this.i > 11) {
                this.E = this.n.c("c" + this.i / 10);
                if (this.w) {
                    Image image = this.n.c("b" + this.i);
                    this.z = image.getWidth();
                    this.q = image.getHeight();
                    this.s = this.a(this.k - this.z, this.j - this.q - 36, this.z, this.q);
                    this.e.drawImage(image, this.k - this.z, this.j - 36 - this.q, 20);
                }
                if (this.i > 11) {
                    this.B = this.n.f[244 + this.i] & 0xFF;
                    this.C = this.n.f[254 + this.i] & 0xFF;
                } else {
                    this.C = 29;
                }
            } else {
                if (this.w) {
                    this.z = 67;
                    this.q = 57;
                    this.s = this.a(this.k - this.z, this.j - this.q - 36, this.z, this.q);
                    this.e.drawImage(this.n.c("b" + this.i), this.k - 38, this.j - 93, 20);
                    this.e.drawImage(this.n.c("ba"), this.k - 67, this.j - 60, 20);
                }
                this.C = this.n.d.V.getBytes().length * this.n.e - 1;
            }
        }
        catch (Exception exception) {
            aq.r();
        }
        this.y();
        this.o();
        this.u();
    }

    void d(int n2) {
        switch (n2) {
            case 53: 
            case 54: 
            case 56: 
            case 131: 
            case 145: 
            case 146: 
            case 148: {
                this.c();
                return;
            }
            case 8: {
                this.A();
                return;
            }
        }
    }

    void y() {
        this.c(this.j - 52, 52, 3, this.C + 19, 3);
        this.e.setColor(0xFFFFFF);
        if (this.E == null) {
            this.a(this.n.d.V, 11, this.j - 48, 4);
        } else if (this.i < 6) {
            this.a(this.E, 12, this.j - 47, 0, this.i * 10, 29, 10);
        } else {
            this.a(this.E, 12, this.j - 47, this.B, 0, this.C, 10);
        }
        this.m(this.j - 30);
        this.m(this.j - 16);
        this.p();
    }

    void m(int n2) {
        if (!this.z()) {
            return;
        }
        String string = this.g.f();
        if (this.c != null) {
            string = d.a(string, this.c, this.D);
        }
        this.a(string, 5, n2, 4);
    }

    void c() {
        if (!this.z()) {
            this.A();
            return;
        }
        this.y();
    }

    void A() {
        this.a(this.p, 0, this.j - 52);
        this.a(this.s, this.k - this.z, this.j - this.q - 36);
        this.v();
    }

    boolean z() {
        if (this.g.c() < 1) {
            if (this.o >= 0) {
                this.g = this.n.a(this.o, true);
                this.o = -1;
                return true;
            }
            if (this.t >= 0) {
                this.g = this.n.a(this.t, true);
                this.t = -1;
                return true;
            }
            return false;
        }
        return true;
    }
}
