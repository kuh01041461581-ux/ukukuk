/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class ai
extends aq {
    int g;
    Image c;
    Image o;
    w i;
    boolean p;

    ai(aq aq2, boolean bl) {
        super(aq2);
        this.p = bl;
    }

    void b() {
        try {
            this.o = this.n.c("tf");
        }
        catch (Exception exception) {
            aq.r();
        }
        this.i = new w(this, this.n.n[1], 0, 15, 3, 5);
        if (!this.p || !this.a(7, (int)this.n.d.e, 7)) {
            this.y();
        }
        this.u();
    }

    void d(int n) {
        switch (n) {
            case 52: 
            case 142: {
                this.g = this.g + 3 & 3;
                break;
            }
            case 54: 
            case 145: {
                this.g = this.g + 1 & 3;
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
        int n = this.d - 60;
        int n2 = this.v - 56;
        this.a(this.c, n, n2);
        this.c(n2, 112, this.g * 28, 32, 0);
        byte by = this.n.f[224 + this.g];
        this.a(this.o, n + 15 + this.g * 28 - (by >> 1), n2 + 5, this.n.f[220 + this.g], 0, by, 10);
        switch (this.g) {
            case 0: {
                int n3;
                this.a(this.n.n[2], n + 17, n2 + 34, 13, 10, 13, 13);
                this.a(this.n.n[2], n + 64, n2 + 34, 26, 10, 13, 13);
                this.e.setColor(0xB6B6AA);
                int n4 = n + 17;
                int n5 = n2 + 52;
                for (n3 = 0; n3 < 22; ++n3) {
                    this.e.drawLine(n4, n5, n4 + 1, n5);
                    n4 += 4;
                }
                this.a(this.o, n + 17, n2 + 59, 0, 20, 29, 10);
                this.a(this.o, n + 17, n2 + 76, 29, 20, 29, 10);
                this.a(this.n.n[2], n + 85, n2 + 59, 57, 10, 18, 10);
                this.a(this.n.n[2], n + 85, n2 + 76, 0, 23, 18, 10);
                this.a(n + 34, n2 + 35, 21, 12, 4858112);
                this.a(n + 81, n2 + 35, 21, 12, 4858112);
                this.a(n + 54, n2 + 58, 27, 12, 4858112);
                this.a(n + 54, n2 + 75, 27, 12, 4858112);
                this.i.a(this, this.n.n[1], 0, 0, 5, 8);
                this.i.a(this.n.d.B, n + 52, n2 + 37, 0);
                this.i.a(this.n.d.R, n + 99, n2 + 37, 0);
                n4 = 0;
                for (n3 = 0; n3 < 4; ++n3) {
                    n4 += this.n.d.P[n3];
                }
                this.i.a(n4, n + 78, n2 + 60, 0);
                this.i.a(this.n.d.C, n + 78, n2 + 77, 0);
                break;
            }
            case 1: {
                int n6;
                int n7;
                this.a(this.o, n + 4, n2 + 20, 34, 10, 32, 9);
                this.e.setColor(9577472);
                this.e.drawLine(n + 63, n2 + 41, n + 63, n2 + 91);
                this.e.setColor(0x6D0000);
                this.e.drawRect(n + 15, n2 + 41, 96, 50);
                this.e.drawLine(n + 15, n2 + 66, n + 111, n2 + 66);
                int n8 = n + 19;
                for (n7 = 0; n7 < 23; ++n7) {
                    this.e.drawLine(n8, n2 + 90, n8, n2 + 91);
                    n8 += 4;
                }
                n8 = n + 15 + 24;
                this.e.drawLine(n8, n2 + 89, n8, n2 + 90);
                this.e.drawLine(n8 += 24, n2 + 88, n8, n2 + 90);
                this.e.drawLine(n8 += 24, n2 + 89, n8, n2 + 90);
                this.i.a(this, this.n.n[1], 0, 15, 3, 5);
                this.i.a(100, n + 13, n2 + 41, 0);
                this.i.a(50, n + 13, n2 + 66, 0);
                this.e.setColor(16767744);
                int n9 = this.n.d.I * 12 + this.n.d.u;
                n8 = n2 + (n9 == 0 ? 66 : 91 - (this.n.d.v[0] >> 1));
                int n10 = n6 = n + 15;
                int n11 = 0;
                for (n7 = 1; n7 < n9; ++n7) {
                    while (n11 < 96) {
                        n11 += n9;
                        ++n6;
                    }
                    while (n11 >= 96) {
                        n11 -= 96;
                    }
                    int n12 = n2 + 91 - (this.n.d.v[n7] >> 1);
                    this.e.drawLine(n10, n8, n6, n12);
                    n10 = n6;
                    n8 = n12;
                }
                n6 = n2 + 91 - (this.n.d.q >> 1);
                this.e.drawLine(n10, n8, n + 111, n6);
                this.a(this.o, n + 76, n6 - 19, 0, 30, 42, 18);
                this.a(this.o, n + 78, n6 - 17, 0, 10, 18, 10);
                this.i.a(this, this.n.n[1], 0, 0, 5, 8);
                this.i.a(this.n.d.q, n + 115, n6 - 16, 0);
                this.i.a(1, n + 19, n2 += 93, 0);
                this.i.a(1, n + 39, n2, 0);
                this.i.a(this.n.d.I + 1, n + 82, n2, 0);
                this.i.a(this.n.d.u + 1, n + 102, n2, 0);
                this.a(this.o, n + 21, n2, 18, 10, 8, 10);
                this.a(this.o, n + 41, n2, 26, 10, 8, 10);
                this.a(this.o, n + 84, n2, 18, 10, 8, 10);
                this.a(this.o, n + 104, n2, 26, 10, 8, 10);
                break;
            }
            case 2: {
                this.a(this.o, n + 84, n2 + 20, 34, 10, 32, 9);
                this.e.setColor(0x6D0000);
                this.e.drawRect(n + 41, n2 + 30, 52, 68);
                this.e.drawLine(n + 67, n2 + 30, n + 67, n2 + 98);
                this.i.a(this, this.n.n[1], 0, 15, 3, 5);
                int n13 = n2 + 34;
                for (int i = 0; i < 4; ++i) {
                    byte by2 = this.n.d.r[i];
                    this.a(this.n.n[2], n + 18, n13, i * 19, 33, 19, 10);
                    this.e.setColor(0x6D0000);
                    this.e.drawRect(n + 42, n13, by2 >> 1, 10);
                    this.e.setColor(16758272);
                    this.e.fillRect(n + 42, n13, by2 >> 1, 10);
                    this.i.a(by2, n + 45 + (by2 >> 1), n13 + 3, 2);
                    n13 += 17;
                }
                break;
            }
            case 3: {
                int n14;
                this.a(this.o, n + 84, n2 + 20, 34, 10, 32, 9);
                this.a(this.n.n[2], n + 51, n2 + 21, 0, 0, 19, 10);
                this.a(this.n.n[2], n + 14, n2 + 66, 19, 0, 19, 10);
                this.a(this.n.n[2], n + 88, n2 + 66, 38, 0, 19, 10);
                this.a(this.n.n[2], n + 51, n2 + 98, 57, 0, 19, 10);
                this.e.setColor(16739584);
                this.e.drawLine(n + 35, n2 + 63, n + 59, n2 + 39);
                this.e.drawLine(n + 61, n2 + 39, n + 85, n2 + 63);
                this.e.drawLine(n + 85, n2 + 65, n + 61, n2 + 89);
                this.e.drawLine(n + 59, n2 + 89, n + 35, n2 + 65);
                this.e.setColor(0);
                this.e.drawLine(n + 60, n2 + 38, n + 60, n2 + 90);
                this.e.drawLine(n + 34, n2 + 64, n + 86, n2 + 64);
                byte by3 = 1;
                for (n14 = 0; n14 < 4; ++n14) {
                    if (this.n.d.z[n14] <= by3) continue;
                    by3 = this.n.d.z[n14];
                }
                n14 = this.n.d.z[0] * 25 / by3;
                int n15 = this.n.d.z[1] * 25 / by3;
                int n16 = this.n.d.z[2] * 25 / by3;
                int n17 = this.n.d.z[3] * 25 / by3;
                int n18 = n + 60;
                int n19 = n2 + 64;
                this.e.setColor(16767744);
                this.e.drawLine(n18, n19 - n17, n18 + n14, n19);
                this.e.drawLine(n18 + n14, n19, n18, n19 + n16);
                this.e.drawLine(n18, n19 + n16, n18 - n15, n19);
                this.e.drawLine(n18 - n15, n19, n18, n19 - n17);
                this.i.a(this, this.n.n[1], 0, 15, 3, 5);
                this.i.a(this.n.d.z[3], n + 60, n2 + 32, 1);
                this.i.a(this.n.d.z[1], n + 23, n2 + 58, 1);
                this.i.a(this.n.d.z[0], n + 97, n2 + 58, 1);
                this.i.a(this.n.d.z[2], n + 60, n2 + 92, 1);
            }
        }
        this.p();
    }

    void x() {
        this.y();
    }

    void y() {
        this.f();
        int n = 3;
        for (int i = 84; i >= 0; i -= 28) {
            this.b(this.v - 54, i, 32);
            byte by = this.n.f[224 + n];
            this.a(this.o, this.d - 45 + i - (by >> 1), this.v - 51, this.n.f[220 + n], 0, by, 10);
            --n;
        }
        this.c = this.a(this.d - 60, this.v - 56, 120, 16);
        this.c();
    }
}
