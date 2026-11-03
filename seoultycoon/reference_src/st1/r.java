/*
 * Decompiled with CFR 0.152.
 */
class r
extends aq {
    int i;
    int g;
    int o;
    int c;

    r(aq aq2) {
        super(aq2);
        this.a = aq2;
    }

    void b() {
        this.g = (this.n.d.A & 0x7F) - 27;
        if (this.g < 3) {
            if (this.n.d.C < (this.g + 1) * 10) {
                this.v();
                return;
            }
            this.o = 0;
        } else if (this.g < 6) {
            this.o = 0;
        } else if (this.g < 8) {
            if (this.n.d.C < 50) {
                this.v();
                return;
            }
            this.o = this.n.h(3) ? 4 : 5;
        } else {
            this.o = this.n.h(1) ? 2 : 5;
        }
        this.i = 1;
        byte by = this.n.f[1905 + this.g];
        this.a((int)by, this.n.f[1914 + this.g] + 564);
        this.n.d.F = (short)(this.n.d.F | 1 << by - 6);
        this.n();
        this.u();
    }

    void x() {
        int n = this.i;
        this.i = 0;
        switch (n) {
            case 1: {
                this.i = 2;
                this.a(this.o, this.n.f[1923 + this.g] + 564);
                return;
            }
            case 2: {
                this.i = 3;
                n = this.n.f[1932 + this.g] + 564;
                this.b(new l(this, n, n + 1));
                return;
            }
            case 3: {
                this.c = this.A == 100 ? 1 : 2;
                this.c();
                return;
            }
            case 10: {
                this.i = !this.n.g() ? 16 : 100;
                this.a(7, 566);
                return;
            }
            case 11: {
                this.n.d.F = (short)(this.n.d.F | 0x80);
                this.n();
                this.i = 12;
                this.a(13, 576);
                return;
            }
            case 12: {
                this.n.n(100);
                this.n();
                ((ar)this.a).O();
                this.i = 13;
                this.a(this.o, 577);
                return;
            }
            case 13: {
                this.i = !this.n.g() ? 16 : 100;
                this.a(7, 578);
                return;
            }
            case 14: {
                this.n.d.F = (short)(this.n.d.F | 0x80);
                this.n();
                this.i = 15;
                this.a(13, 568);
                return;
            }
            case 15: {
                this.i = 100;
                this.a(8, 569);
                return;
            }
            case 16: {
                this.n.d.a ^= 0x2000000;
                this.n();
                this.i = 17;
                this.a(this.o, 598);
                return;
            }
            case 17: {
                this.i = 100;
                this.a(7, 599);
                return;
            }
            case 20: {
                this.i = 21;
                this.a(8, 583);
                return;
            }
            case 21: {
                this.i = (this.g == 5 || (this.e() & 1) == 0) && this.n.g() ? 24 : 100;
                this.a(this.o, 584 + this.g);
                return;
            }
            case 22: {
                this.i = 26;
                this.a(this.o, 594);
                return;
            }
            case 26: {
                this.v();
                return;
            }
            case 23: {
                this.i = 100;
                this.a(this.o, 590);
                return;
            }
            case 24: {
                this.n.d.a |= 0x2000000;
                this.n.d.F = (short)(this.n.d.F | 0x4000);
                this.n();
                this.i = 25;
                this.a(this.o, 591);
                return;
            }
            case 25: {
                this.i = 100;
                this.a(8, 592);
                return;
            }
            case 30: {
                this.i = 100;
                this.a(this.o, 609);
                return;
            }
            case 31: {
                this.i = 32;
                this.a(16, 612);
                return;
            }
            case 32: {
                this.i = 33;
                this.a(8, 613);
                return;
            }
            case 33: {
                this.i = 34;
                this.a(this.o, 614);
                return;
            }
            case 34: {
                this.v();
                return;
            }
            case 40: {
                this.i = 100;
                this.a(this.o, 620);
                return;
            }
            case 41: {
                this.i = 42;
                this.a(17, 622);
                return;
            }
            case 42: {
                this.i = 100;
                this.a(this.o, 623);
                return;
            }
            case 100: {
                this.v();
                return;
            }
        }
    }

    void c() {
        this.n.d.t |= this.c << (this.g << 1);
        this.n.d.A = 0;
        if (this.g < 3) {
            if (this.c == 1) {
                this.n.n(-(this.g + 1) * 10);
                if (this.g < 2) {
                    this.i = 10;
                    this.a(12, 565);
                } else {
                    this.i = 11;
                    this.a(12, 575);
                }
            } else {
                this.i = 14;
                this.a(12, 567);
            }
        } else if (this.g < 6) {
            if (this.c == 1) {
                this.n.n(this.n.f[1938 + this.g] * 10);
                this.n.i(-this.n.f[1938 + this.g]);
                this.n();
                if (this.g == 5 && (this.n.d.t & 0x140) == 320) {
                    this.i = 22;
                    this.a(this.o, 593);
                } else {
                    this.i = 20;
                    this.a(14, 582);
                }
            } else if (this.g < 5) {
                this.i = 100;
                this.a(14, 584);
            } else {
                this.i = 23;
                this.a(14, 585);
            }
        } else if (this.g < 8) {
            if (this.c == 1) {
                this.n.n(-50);
                this.n.i(-(3 + (this.g - 6 << 1)));
                this.i = 100;
                this.a(this.o, 600 + this.g);
            } else if (this.g == 6) {
                this.i = 30;
                this.a(8, 608);
            } else {
                this.n.i(5);
                this.n.d.x |= 8;
                this.n();
                this.i = 31;
                this.a(8, 611);
            }
        } else if (this.c == 1) {
            this.n.i(10);
            this.i = 40;
            this.a(6, 619);
        } else {
            this.n.n(20);
            this.n.i(-5);
            this.i = 41;
            this.a(6, 621);
        }
        this.n();
        ((ar)this.a).O();
    }
}
