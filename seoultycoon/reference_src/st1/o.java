/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class o
extends aq {
    Image g;
    int c;
    int i;

    o(aq aq2) {
        super(aq2);
    }

    void b() {
        int n;
        String string = this.n.d.V;
        int n2 = 699 + (this.e() & 7);
        int n3 = this.n.d.I * 12 + this.n.d.u;
        if (n3 == 0) {
            n2 = 316;
        } else if (n3 == 12) {
            n2 = this.n.d.q < 51 ? 668 : 669;
        } else if (n3 == 24) {
            n2 = this.n.d.q < 61 ? 670 : 671;
        } else if (n3 == 36) {
            n2 = this.n.d.q < 70 ? 672 : 673;
        } else if (n3 == 46) {
            n2 = 674;
        } else if (n3 == 47) {
            n2 = 675;
        } else if (this.n.d.u == 5) {
            switch (ag.a(this.n.d, -1)) {
                case 0: 
                case 1: {
                    n2 = 676;
                    break;
                }
                case 2: {
                    n2 = 677;
                    break;
                }
                case 3: {
                    n2 = 678;
                    break;
                }
                case 4: {
                    n2 = 680;
                    break;
                }
                default: {
                    n2 = 679;
                    break;
                }
            }
        } else if ((this.n.d.t & 0x30000) == 65536) {
            n2 = 693;
            this.n.d.t |= 0x30000;
        } else {
            boolean bl = false;
            if ((this.n.d.a & 0x400000) != 0) {
                this.n.d.a ^= 0x400000;
                n2 = 689 + (this.e() & 3);
                bl = true;
            }
            if (!bl) {
                n = 3;
                int n4 = 0;
                for (int i = 0; i < 4; ++i) {
                    if ((this.n.d.j & n) != 0) {
                        ++n4;
                    }
                    n <<= 2;
                }
                if (n4 > 0) {
                    n2 = 681 + (this.e() & 3);
                    if (n4 > 1) {
                        n2 += 4;
                    }
                    bl = true;
                }
            }
            if (!bl && (this.n.d.x & 1) == 0) {
                for (n = 0; n < 4; ++n) {
                    if (this.n.d.r[n] < 90) continue;
                    this.n.d.x |= 1;
                    n2 = 694;
                    string = s.b[65 + n];
                    bl = true;
                    break;
                }
            }
            if (!bl && (this.n.d.x & 2) == 0) {
                for (n = 0; n < 4; ++n) {
                    if (this.n.d.z[n] < 90) continue;
                    this.n.d.x |= 2;
                    n2 = 695;
                    string = s.b[61 + n];
                    bl = true;
                    break;
                }
            }
            if (!bl && this.n.k(14) && (this.e() & 3) == 0) {
                n2 = 697 + (this.e() & 1);
                bl = true;
            }
        }
        this.n();
        d d2 = this.n.g(n2);
        d2.a(120 / this.n.e, true);
        n = this.m(d2.d() * 13 + 2) + 27;
        this.e.setColor(0);
        while (d2.c() > 0) {
            this.a(d.a(d2.f(), "@@@@", string), this.d, n, 1);
            n += 13;
        }
        this.p();
        this.o();
        this.u();
    }

    void d(int n) {
        switch (n) {
            case 8: 
            case 53: 
            case 131: 
            case 148: {
                this.a(this.g, this.d - 64, this.c);
                this.p();
                this.v();
                return;
            }
        }
    }

    int m(int n) {
        this.i = n + 66;
        this.c = this.v - (this.i >> 1);
        this.g = this.a(this.d - 64, this.c, 128, this.i);
        try {
            this.e(0);
            for (int i = 0; i < 2; ++i) {
                this.e.drawImage(this.n.c("ta" + i), this.d, this.v, 3);
                this.p();
                this.e(500);
                this.a(this.g, this.d - 64, this.c);
            }
            this.e.drawImage(this.n.c("ta2"), this.d, this.c, 17);
            this.e.drawImage(this.n.c("ta3"), this.d, this.c + this.i, 33);
            this.e.setColor(0x6D6D55);
            this.e.drawRect(this.d - 64, this.c + 25, 127, n - 1);
            this.e.setColor(0xDBDBAA);
            this.e.drawLine(this.d - 63, this.c + 25, this.d - 63, this.c + 24 + n);
            this.e.setColor(0x494955);
            this.e.drawLine(this.d + 62, this.c + 25, this.d + 62, this.c + 24 + n);
            this.e.setColor(0xB6B6AA);
            this.e.fillRect(this.d - 62, this.c + 25, 124, n);
        }
        catch (Exception exception) {
            // empty catch block
        }
        return this.c;
    }
}
