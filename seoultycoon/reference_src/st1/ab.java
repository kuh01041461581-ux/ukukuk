/*
 * Decompiled with CFR 0.152.
 */
class ab
extends ar {
    ab(aq aq2) {
        super(aq2);
    }

    void b() {
        this.d(30, 9, 10);
        this.u();
    }

    void J() {
        switch (this.D) {
            case 0: {
                if (this.e(2, 21, 79)) {
                    return;
                }
                if (this.al[this.D] != 2 || this.n.l(8) || (this.e() & 3) != 0) break;
                this.t = 8;
                this.aq = 80;
                this.k(378);
                return;
            }
            case 1: {
                if (this.e(1, 17, 86)) {
                    return;
                }
                if (this.al[this.D] != 1 || this.n.h(2) || (this.e() & 1) != 0) break;
                int n2 = this.n.d.E >> 7 & 3;
                if (n2 < 2) {
                    this.n.d.E += 128;
                    this.n();
                    this.aq = 201 + (n2 << 1);
                    this.k(635 + n2 * 3);
                    return;
                }
                if (this.n.d.C < 20) break;
                this.aq = 205;
                this.b(new n((aq)this, (int)this.n.d.e, 641, 662, -1));
                return;
            }
            case 2: {
                if (this.e(1, 20, 93)) {
                    return;
                }
                if (this.al[this.D] != 1 || !this.n.h(0) && !this.n.h(4) || this.n.l(10) || (this.e() & 3) != 0) break;
                this.G = this.n.h(0) ? 1 : 5;
                this.t = 10;
                this.aq = 511;
                this.a(this.G, 383);
                return;
            }
            case 3: {
                if (this.e(1, 18, 100)) {
                    return;
                }
                if (this.n.b(14) && this.e(2, 19, 104)) {
                    return;
                }
                if (this.al[this.D] != 2 || !this.n.h(1) && !this.n.h(4) || this.n.l(9) || (this.e() & 3) != 0) break;
                this.G = this.n.h(1) ? 2 : 5;
                this.t = 9;
                this.aq = 411;
                this.a(this.G, 379);
                return;
            }
            case 4: {
                if ((this.n.d.a & 0x1000000) == 0) {
                    this.n.d.a |= 0x1000000;
                    this.n();
                    this.c(10);
                    this.k(this.j < 160 ? -108 : 108);
                    return;
                }
                if (this.e(1, 16, 112)) {
                    return;
                }
                if (this.al[this.D] != 1) break;
                if ((this.n.d.a & 0x800000) == 0 && !this.n.h(1) && !this.n.h(4)) {
                    this.n.d.a |= 0x800000;
                    this.n();
                    this.c(10);
                    this.k(this.j < 160 ? -113 : 113);
                    return;
                }
                if (!this.n.k(14) && this.n.e(1081) && (this.n.h(1) || this.n.h(4)) && (this.e() & 1) == 0) {
                    this.aq = 110;
                    this.a(this.n.h(1) ? 2 : 5, 528);
                    return;
                }
                if (this.n.d.h >= 30) {
                    this.aq = 101;
                    this.k(115);
                } else {
                    this.b((this.e() & 1) + 1, 0);
                }
                return;
            }
            case 5: {
                if (this.n.k(15) && this.e(2, 15, 127)) {
                    return;
                }
                if (this.al[this.D] != 2 || this.n.h(4) || (this.e() & 1) != 0) break;
                int n3 = this.n.d.E >> 19 & 3;
                if (n3 < 3) {
                    this.n.d.E += 524288;
                    this.aq = 301 + (n3 << 1);
                    this.k(651 + n3 * 3);
                    return;
                }
                if (this.n.d.C < 50) break;
                this.aq = 307;
                this.b(new n((aq)this, (int)this.n.d.e, 660, 663, -1));
                return;
            }
        }
        this.c();
    }

    void m(int n2) {
        switch (n2) {
            case 101: {
                this.aq = 102;
                this.c(9);
                this.a(7, this.j < 160 ? -114 : 114);
                return;
            }
            case 102: {
                this.a(30, "\ube75");
                return;
            }
            case 110: {
                this.aq = 111;
                this.l(14);
                return;
            }
            case 111: {
                int n3;
                if (this.A == 100) {
                    this.aq = 112;
                    n3 = 536;
                } else {
                    this.aq = 5;
                    n3 = 537;
                }
                this.a(this.n.h(1) ? 2 : 5, n3);
                return;
            }
            case 112: {
                this.aq = 5;
                this.j(14);
                this.n();
                this.O();
                return;
            }
            case 201: {
                this.aq = 202;
                this.a(3, 636);
                return;
            }
            case 202: {
                this.aq = 5;
                this.a(6, 637);
                return;
            }
            case 203: {
                this.aq = 204;
                this.a(3, 639);
                return;
            }
            case 204: {
                this.aq = 5;
                this.a(6, 640);
                return;
            }
            case 205: {
                this.aq = 206;
                this.b(new v(this, 667, 1, 0, 0));
                return;
            }
            case 206: {
                this.n.d.E &= 0xFFFFFE7F;
                if (this.A == 100) {
                    this.n.n(-20);
                    this.n.j(2);
                    this.aq = 207;
                    this.a(3, 642);
                    return;
                }
                this.aq = 5;
                this.k(666);
                this.n();
                return;
            }
            case 207: {
                this.a("\uc608\uc220\uc778(\ubb38\ud654\ubd84\uc57c)", 20);
                return;
            }
            case 301: {
                this.aq = 302;
                this.a(5, 652);
                return;
            }
            case 302: {
                this.aq = 5;
                this.a(6, 653);
                return;
            }
            case 303: {
                this.aq = 304;
                this.a(5, 655);
                return;
            }
            case 304: {
                this.aq = 5;
                this.a(6, 656);
                return;
            }
            case 305: {
                this.aq = 306;
                this.a(5, 658);
                return;
            }
            case 306: {
                this.aq = 5;
                this.a(6, 659);
                return;
            }
            case 307: {
                this.aq = 308;
                this.b(new v(this, 667, 1, 0, 0));
                return;
            }
            case 308: {
                this.n.d.E &= 0xFFE7FFFF;
                if (this.A == 100) {
                    this.n.n(-50);
                    this.n.j(4);
                    this.aq = 309;
                    this.a(5, 661);
                    return;
                }
                this.aq = 5;
                this.k(666);
                this.n();
                return;
            }
            case 309: {
                this.a("\ub9cc\ub2a5\uc778(\uc804\ubd84\uc57c)", 50);
                return;
            }
            case 411: {
                this.aq = 412;
                this.b(new l(this, 380, 381));
                return;
            }
            case 412: {
                int n4;
                if (this.A == 100) {
                    this.aq = 80;
                    n4 = 513;
                } else {
                    this.aq = 81;
                    n4 = 382;
                }
                this.a(this.G, n4);
                return;
            }
            case 511: {
                this.aq = 512;
                this.b(new l(this, 384, 385));
                return;
            }
            case 512: {
                int n5;
                if (this.A == 100) {
                    this.aq = 80;
                    n5 = 513;
                } else {
                    this.aq = 81;
                    n5 = 386;
                }
                this.a(this.G, n5);
                return;
            }
        }
    }
}
