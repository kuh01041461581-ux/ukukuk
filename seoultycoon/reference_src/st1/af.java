/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class af
extends aq {
    String i;
    Image p;
    int o;
    int g;
    int c;

    af(aq aq2, String string, int n) {
        super(aq2);
        this.i = string;
        this.o = n;
        this.c = this.o == 0 ? 15 : (this.o == 20 ? 10 : (this.o == 13 ? 10 : 9));
    }

    void b() {
        try {
            this.p = this.n.c("to");
        }
        catch (Exception exception) {
            // empty catch block
        }
        this.c();
        this.u();
    }

    void d(int n) {
        switch (n) {
            case 50: 
            case 52: 
            case 141: 
            case 142: {
                if (this.g <= 0) break;
                --this.g;
                break;
            }
            case 54: 
            case 56: 
            case 145: 
            case 146: {
                if (this.g >= this.c - 1) break;
                ++this.g;
                break;
            }
            case 8: 
            case 53: 
            case 131: 
            case 148: {
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
        block27: {
            this.a(this.d - 60, this.v - 56, 120, 112, 1, false);
            this.a(this.i, this.d, this.v - 50, 1, 0xFFFFFF);
            if (this.g > 0) {
                this.a(this.n.n[0], this.d - 53, this.v + 42, 115, 36, 6, 9);
            }
            if (this.g < this.c - 1) {
                this.a(this.n.n[0], this.d + 47, this.v + 42, 115, 45, 6, 9);
            }
            if (this.o == 0) {
                int n;
                this.a(this.p, this.d - 37, this.v - 35, 0, 0, 74, 6);
                this.a(this.p, this.d - 37, this.v + 31, 0, 0, 74, 6);
                this.e.setColor(0);
                this.e.fillRect(this.d - 37, this.v - 35, 3, 72);
                this.e.fillRect(this.d + 34, this.v - 35, 3, 72);
                int n2 = this.g;
                for (n = 0; n < this.c && ((this.n.d.F & 1 << n) == 0 || --n2 >= 0); ++n) {
                }
                if ((this.n.d.F & 1 << n) == 0) {
                    this.e.setColor(85);
                    this.e.fillRect(this.d - 34, this.v - 29, 68, 60);
                    this.m(this.v);
                } else {
                    this.e.setColor(11983871);
                    this.e.fillRect(this.d - 34, this.v - 29, 68, 60);
                    try {
                        if (n < 13) {
                            this.e.drawImage(this.n.c("b" + this.n.f[785 + n]), this.d + 34, this.v + 31, 40);
                            if (n < 6) {
                                this.a(this.n.c("c0"), this.d - 15, this.v + 41, 0, n * 10, 29, 10);
                            } else {
                                this.a(this.n.c("c1"), this.d - (this.n.f[260 + n] >> 1), this.v + 41, this.n.f[250 + n] & 0xFF, 0, this.n.f[260 + n], 10);
                            }
                            break block27;
                        }
                        this.e.drawImage(this.n.c("ba"), this.d + 34, this.v + 31, 40);
                        this.e.drawImage(this.n.c("b" + (6 + (n - 13) * 3)), this.d - 4, this.v - 26, 20);
                        this.e.setColor(0xFFFFFF);
                        this.a(n == 13 ? "\uc2dc\uc7a5" : "\ub300\uba38\ub9ac\uc2dc\uc7a5", this.d, this.v + 40, 1);
                    }
                    catch (Exception exception) {}
                }
            } else if (this.o == 20) {
                int n;
                this.a(this.p, this.d - 53, this.v - 35, 0, 0, 106, 6);
                this.a(this.p, this.d - 53, this.v + 31, 0, 0, 106, 6);
                this.e.setColor(0);
                this.e.fillRect(this.d - 53, this.v - 35, 3, 72);
                this.e.fillRect(this.d + 50, this.v - 35, 3, 72);
                int n3 = this.g;
                for (n = 0; n < this.c && ((this.n.d.U & 1 << n) == 0 || --n3 >= 0); ++n) {
                }
                if ((this.n.d.U & 1 << n) == 0) {
                    this.e.setColor(85);
                    this.e.fillRect(this.d - 50, this.v - 29, 100, 60);
                    this.m(this.v);
                } else {
                    try {
                        this.e.drawImage(this.n.c("v" + n), this.d - 50, this.v - 29, 20);
                        this.e.setColor(0xFFFFFF);
                        this.a(s.b[92 + n], this.d, this.v + 40, 1);
                    }
                    catch (Exception exception) {}
                }
            } else {
                int n;
                this.a(this.d - 54, this.v - 50, 108, 88, 85);
                int n4 = (this.o & 1) == 1 ? 9 : 0;
                int n5 = this.o < 12 ? this.n.d.p : this.n.d.l;
                int n6 = this.g;
                for (n = 0; n < this.c && ((n5 & 1 << n + n4) == 0 || --n6 >= 0); ++n) {
                }
                if (n >= this.c || (n5 & 1 << n + n4) == 0) {
                    this.m(this.v - 6);
                } else {
                    try {
                        boolean bl;
                        int n7 = n;
                        n5 = 232 + (this.o - 10) * 6;
                        for (n4 = 0; n4 < 6 && n7 >= this.n.f[n5]; ++n7, ++n4) {
                            ++n5;
                        }
                        this.e.setColor(0xFFFFFF);
                        this.a(s.b[(this.o - 10) * 15 + n7], this.d, this.v + 40, 1);
                        n4 = (this.o - 8) * 10 + n;
                        boolean bl2 = bl = this.o == 10 && n == 1;
                        if (bl) {
                            --n4;
                        }
                        this.e.setClip(this.d - 54, this.v - 49, 108, 86);
                        this.e.drawImage(this.n.c("t" + n4), this.d, this.v - 6, 3);
                        if (bl) {
                            this.e.drawImage(this.n.c("t" + (n4 + 1)), this.d + 4, this.v + 14, 3);
                        }
                    }
                    catch (Exception exception) {
                        // empty catch block
                    }
                }
            }
        }
        this.p();
    }

    void m(int n) {
        try {
            this.e.drawImage(this.n.c("tp"), this.d, n, 3);
        }
        catch (Exception exception) {
            // empty catch block
        }
    }
}
