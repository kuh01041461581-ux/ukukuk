/*
 * Decompiled with CFR 0.152.
 */
class z
extends aq {
    final int s = 3084513;
    int g;
    int o;
    int q;
    int c;
    int i;
    int p;

    z(aq aq2) {
        super(aq2);
        this.a = aq2;
    }

    void b() {
        this.p = this.n.d.e;
        this.g = (this.n.d.A & 0x7F) - 1;
        if ((0x2F10E1 & 1 << this.g) != 0) {
            int n2 = 1 << this.g;
            this.q = 0;
            for (int i = 1; i < n2; i <<= 1) {
                if ((0x2F10E1 & i) == 0) continue;
                ++this.q;
            }
            this.q = 1753 + (this.q << 2);
            if (this.b((int)this.n.f[this.q], this.n.f[this.q + 3])) {
                if (this.n.e(983 + this.n.f[this.q + 1] * 7)) {
                    this.o = 1;
                    this.b(new n(this, this.p, 514 + this.n.f[this.q + 1]));
                } else {
                    this.c(422 + this.n.f[this.q + 2], this.n.f[this.q + 3] + 1);
                }
            }
        } else {
            switch (this.g) {
                case 1: {
                    if (this.n.k(17)) {
                        this.o = 3;
                        this.a(this.p, 426);
                        return;
                    }
                    this.c(427, 3);
                    return;
                }
                case 2: {
                    this.c(431, 5);
                    return;
                }
                case 3: {
                    this.c(0, 7);
                    this.o = 19;
                    return;
                }
                case 4: {
                    if (this.b(3, 9)) {
                        this.c(442, 10);
                    }
                    return;
                }
                case 8: {
                    this.c(458, 21);
                    return;
                }
                case 9: {
                    if (this.b(3, 23)) {
                        this.c(462, 24);
                    }
                    return;
                }
                case 10: {
                    if (this.b(2, 26)) {
                        this.c(466, 27);
                    }
                    return;
                }
                case 11: {
                    if (this.b(2, 29)) {
                        this.i = 30;
                        this.o = 14;
                        this.a(this.p, 470);
                    }
                    return;
                }
                case 13: {
                    this.c(475, 34);
                    return;
                }
                case 14: {
                    if (this.b(1, 36)) {
                        this.c(479, 37);
                    }
                    return;
                }
                case 15: {
                    if (this.b(0, 39)) {
                        this.c(483, 40);
                    }
                    return;
                }
                case 20: {
                    if (this.b(0, 54)) {
                        this.c(503, 55);
                    }
                    return;
                }
            }
        }
        this.u();
    }

    void x() {
        int n2 = this.o;
        this.o = 0;
        switch (n2) {
            case 1: {
                this.o = 2;
                this.l(this.n.f[this.q + 1]);
                return;
            }
            case 2: {
                if (this.A == 100) {
                    this.o = 5;
                    n2 = 536;
                } else {
                    this.o = 6;
                    n2 = 537;
                }
                this.a(this.p, n2);
                return;
            }
            case 3: {
                p p2 = new p(this);
                p2.d = 1;
                this.n.i(1);
                this.n.d.A = 0;
                this.n();
                this.o = 100;
                this.b(new ac(this, p2, 0, "\ucc98\ub9ac\uacb0\uacfc"));
                return;
            }
            case 5: {
                this.j(this.n.f[this.q + 1]);
                this.n.d.D |= 1 << this.g;
                this.n.d.A = 0;
                this.n();
                ((ar)this.a).O();
                this.o = 100;
                return;
            }
            case 6: {
                p p3 = new p(this);
                p3.i = 10;
                p3.d = -2;
                this.n.m(10);
                this.n.i(-2);
                this.n.d.A = 0;
                this.n();
                this.o = 100;
                this.b(new ac(this, p3, 0, "\uc815\ucc45\uac70\uc808"));
                return;
            }
            case 11: {
                this.o = 12;
                this.b(new l(this, this.c, this.c + 2));
                return;
            }
            case 12: {
                if (this.A == 100) {
                    this.o = 14;
                    n2 = 1;
                } else {
                    this.o = 15;
                    n2 = 3;
                }
                if (this.c()) {
                    this.a(this.p, this.c + n2);
                } else {
                    this.x();
                }
                return;
            }
            case 14: 
            case 15: {
                int n3 = 1281 + (this.i + n2 - 14) * 7;
                int n4 = this.n.f[n3];
                if (this.n.h(4)) {
                    n4 >>= 1;
                }
                if (n4 > this.n.d.C) {
                    this.o = 16;
                    this.b(new n((aq)this, this.p, 559, 562, -1));
                    return;
                }
                p p4 = new p(this);
                p4.o = -n4;
                p4.f[this.n.f[n3 + 1]] = this.n.f[n3 + 2];
                p4.d = this.n.f[n3 + 3];
                p4.i = this.n.f[n3 + 4];
                p4.b = this.n.f[n3 + 5];
                if (this.g == 21 && n2 == 14) {
                    p4.k = -40;
                    this.n.d.z[2] = (byte)Math.max(this.n.d.z[2] - 40, 0);
                    this.n.d.o[2][1] = 0;
                }
                this.n.n(-n4);
                this.n.b(this.n.f[n3 + 1], this.n.f[n3 + 2]);
                this.n.i(this.n.f[n3 + 3]);
                this.n.m(this.n.f[n3 + 4]);
                int n5 = (this.n.f[1701 + this.g] & 0xFF) >> 6;
                this.n.d.P[n5] = (short)Math.max(Math.min(this.n.d.P[n5] + this.n.f[n3 + 5], 250), 0);
                this.n.d.A = 0;
                if (this.n.f[n3 + 6] == 0) {
                    this.n.d.D |= 1 << this.g;
                }
                this.n();
                ((ar)this.a).O();
                this.o = n4 != 0 && this.n.h(4) ? 99 : 100;
                this.b(new ac(this, p4, 0, "\ucc98\ub9ac\uacb0\uacfc"));
                return;
            }
            case 16: {
                p p5 = new p(this);
                p5.d = -5;
                p5.i = 20;
                this.n.i(-5);
                this.n.m(20);
                this.n.d.A = 0;
                this.n();
                ((ar)this.a).O();
                this.o = 100;
                this.b(new ac(this, p5, 0, "\ucc98\ub9ac\uacb0\uacfc"));
                return;
            }
            case 19: {
                this.o = 20;
                this.b(new l(this, 435, 440));
                return;
            }
            case 20: {
                if (this.A == 100) {
                    if (this.n.h(3) || this.n.h(4)) {
                        this.o = 14;
                        n2 = 439;
                    } else {
                        this.n.d.x |= 8;
                        this.n();
                        this.o = 21;
                        n2 = 436;
                    }
                } else {
                    this.o = 15;
                    n2 = 441;
                }
                this.a(this.p, n2);
                return;
            }
            case 21: {
                this.o = 22;
                this.a(6, 437);
                return;
            }
            case 22: {
                this.o = 14;
                this.a(this.p, 438);
                return;
            }
            case 99: {
                this.o = 100;
                this.a(5, 561);
                return;
            }
            case 100: {
                this.v();
                return;
            }
        }
    }

    void c(int n2, int n3) {
        this.c = n2;
        this.i = n3;
        this.o = 11;
        this.b(new n((aq)this, this.p, 400 + this.g, 511, -1));
    }

    boolean b(int n2, int n3) {
        if (this.n.h(n2)) {
            this.p = n2 + 1;
            return true;
        }
        if (this.n.h(4)) {
            this.p = 5;
            return true;
        }
        this.i = n3;
        this.o = 14;
        this.b(new n((aq)this, (int)this.n.d.e, 512, "@@@@", s.b[65 + n2]));
        return false;
    }

    boolean c() {
        int n2 = 1281 + (this.i + this.o - 14) * 7;
        int n3 = this.n.f[n2];
        if (this.n.h(4)) {
            n3 >>= 1;
        }
        return n3 <= this.n.d.C;
    }
}
