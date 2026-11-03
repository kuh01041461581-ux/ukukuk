/*
 * Decompiled with CFR 0.152.
 */
class i
extends aq {
    int o;
    int c;
    int p;
    int i;
    int g;

    i(aq aq2) {
        super(aq2);
        this.a = aq2;
    }

    void b() {
        this.u();
        this.p = this.n.d.e;
        this.c = (this.n.d.A & 0x7F) - 23;
        switch (this.c) {
            case 0: {
                if (this.n.d.o[3][0] < 1) {
                    this.c(0, 543);
                } else if (!this.n.h(3) && !this.n.h(4)) {
                    this.c(1, 544);
                } else if (this.m(1)) {
                    this.b(1, 3);
                } else {
                    this.c(2, 545);
                }
                return;
            }
            case 1: {
                if (this.n.d.o[0][3] < 1) {
                    this.c(4, 546);
                } else if (!this.n.k(7)) {
                    if (!this.n.h(0) && !this.n.h(4)) {
                        this.c(5, 547);
                    } else if (!this.n.e(1032)) {
                        this.c(6, 548);
                    } else {
                        this.b(7, 7);
                    }
                } else if (!this.n.h(2) && !this.n.h(4)) {
                    this.c(8, 549);
                } else if (this.m(8)) {
                    this.b(8, 9);
                } else {
                    this.c(8, 549);
                }
                return;
            }
            case 2: {
                if (this.n.d.o[0][0] < 1) {
                    this.c(10, 550);
                } else if (!this.n.h(3) && !this.n.h(4)) {
                    this.c(11, 551);
                } else if (this.m(11)) {
                    this.b(11, 13);
                } else {
                    this.c(12, 552);
                }
                return;
            }
            case 3: {
                if (this.n.d.o[1][0] < 1) {
                    this.c(14, 553);
                } else if (!this.n.h(3) && !this.n.h(4)) {
                    this.c(15, 554);
                } else if (this.m(16)) {
                    this.b(16, 17);
                } else {
                    this.c(16, 555);
                }
                return;
            }
        }
    }

    void x() {
        int n2 = this.o;
        this.o = 0;
        switch (n2) {
            case 1: {
                n2 = 1811 + this.i * 5;
                int n3 = this.n.f[n2];
                if (n3 > this.n.d.C) {
                    this.o = 2;
                    this.b(new n((aq)this, this.p, 559, 560, -1));
                    return;
                }
                p p2 = new p(this);
                p2.o = -n3;
                p2.f[this.n.f[n2 + 1]] = this.n.f[n2 + 2];
                p2.i = this.n.f[n2 + 3];
                p2.b = -this.n.f[n2 + 4];
                this.n.n(-n3);
                this.n.b(this.n.f[n2 + 1], this.n.f[n2 + 2]);
                this.n.m(this.n.f[n2 + 3]);
                byte by = this.n.f[1901 + this.c];
                this.n.d.P[by] = (short)Math.max(this.n.d.P[by] - this.n.f[n2 + 4], 0);
                this.n.d.A = 0;
                this.n();
                ((ar)this.a).O();
                this.o = n3 != 0 && this.n.h(4) ? 99 : 100;
                this.b(new ac(this, p2, 0, "\ucc98\ub9ac\uacb0\uacfc"));
                return;
            }
            case 2: {
                p p3 = new p(this);
                p3.f[3] = -2;
                p3.d = -5;
                p3.i = 20;
                this.n.b(3, -2);
                this.n.i(-5);
                this.n.m(20);
                this.n.d.A = 0;
                this.n();
                ((ar)this.a).O();
                this.o = 100;
                this.b(new ac(this, p3, 0, "\ucc98\ub9ac\uacb0\uacfc"));
                return;
            }
            case 5: {
                this.o = 6;
                this.l(this.g);
                return;
            }
            case 6: {
                if (this.A == 100) {
                    this.o = 7;
                    n2 = 536;
                } else {
                    this.o = 1;
                    n2 = 537;
                }
                this.a(this.p, n2);
                return;
            }
            case 7: {
                this.j(this.g);
                this.n.d.A = 0;
                this.n();
                ((ar)this.a).O();
                this.o = 100;
                return;
            }
            case 98: {
                p p4 = new p(this);
                p4.o = Math.max(this.n.d.r[3] >> 2, 1);
                this.n.n(p4.o);
                this.n();
                ((ar)this.a).O();
                ((ar)this.a).g(false);
                this.o = 100;
                this.b(new ac(this, p4, 0, "\ubaa8\uae08\uacb0\uacfc"));
                return;
            }
            case 99: {
                this.o = 98;
                ((ar)this.a).c(8);
                this.a(5, this.j < 160 ? -556 : 556);
                return;
            }
            case 100: {
                this.v();
                return;
            }
        }
    }

    boolean m(int n2) {
        if (this.n.h(this.n.f[(n2 = 983 + n2 * 7) + 3])) {
            this.p = this.n.f[n2 + 3] + 1;
        } else if (this.n.h(4)) {
            this.p = 5;
        }
        return this.n.e(n2);
    }

    void c(int n2, int n3) {
        this.i = n2;
        this.o = 1;
        this.a(this.p, n3);
    }

    void b(int n2, int n3) {
        this.g = n2;
        this.i = n3;
        this.o = 5;
        this.a(this.p, 514 + n2);
    }
}
