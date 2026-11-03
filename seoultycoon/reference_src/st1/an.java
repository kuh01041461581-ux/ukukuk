/*
 * Decompiled with CFR 0.152.
 */
class an
extends ar {
    an(aq aq2) {
        super(aq2);
    }

    void b() {
        this.d(40, 9, 20);
        this.u();
    }

    void J() {
        switch (this.D) {
            case 0: {
                if (this.al[this.D] == 1 && !this.n.h(0) && (this.e() & 1) == 0) {
                    if ((this.n.d.E & 2) == 0) {
                        this.n.d.E |= 2;
                        this.aq = 101;
                        this.k(625);
                        return;
                    }
                    if (this.n.d.C >= 20) {
                        this.aq = 103;
                        this.b(new n((aq)this, (int)this.n.d.e, 628, 662, -1));
                        return;
                    }
                }
                if ((this.n.d.a & 0x8000000) == 0 || (this.n.d.t & 0x100) == 0 || (this.n.d.m & 3) != 0 || (this.e() & 1) != 0) break;
                this.aq = 601;
                this.a(15, 770);
                return;
            }
            case 1: {
                if (this.e(1, 26, 140)) {
                    return;
                }
                if (!this.e(2, 27, 144)) break;
                return;
            }
            case 2: {
                if (this.al[this.D] != 1 || this.n.h(1) || (this.e() & 1) != 0) break;
                if ((this.n.d.E & 0x10) == 0) {
                    this.n.d.E |= 0x10;
                    this.aq = 201;
                    this.k(630);
                    return;
                }
                if (this.n.d.C < 20) break;
                this.aq = 203;
                this.b(new n((aq)this, (int)this.n.d.e, 633, 662, -1));
                return;
            }
            case 3: {
                if (this.e(1, 28, 157)) {
                    return;
                }
                if (this.al[this.D] != 1 || !this.n.h(2) && !this.n.h(4) || this.n.l(13) || (this.e() & 3) != 0) break;
                this.G = this.n.h(2) ? 3 : 5;
                this.t = 13;
                this.aq = 511;
                this.a(this.G, 393);
                return;
            }
            case 4: {
                if (this.e(1, 22, 164)) {
                    return;
                }
                if (this.n.k(20)) {
                    if (this.e(2, 23, 171)) {
                        return;
                    }
                    if (this.al[this.D] == 1) {
                        this.b(-1, 3);
                        return;
                    }
                }
                if (this.al[this.D] != 2 || this.n.l(11) || (this.n.d.t & 0x100) == 0 || (this.e() & 3) != 0) break;
                this.t = 11;
                this.aq = 311;
                this.k(387);
                return;
            }
            case 5: {
                if (this.e(1, 24, 178)) {
                    return;
                }
                if (this.n.b(20) && this.e(2, 25, 182)) {
                    return;
                }
                if (this.al[this.D] != 2 || !this.n.h(0) && !this.n.h(4) || this.n.l(12) || (this.e() & 3) != 0) break;
                this.G = this.n.h(0) ? 1 : 5;
                this.t = 12;
                this.aq = 411;
                this.a(this.G, 389);
                return;
            }
        }
        this.c();
    }

    void m(int n2) {
        switch (n2) {
            case 101: {
                this.aq = 102;
                this.a(1, 626);
                return;
            }
            case 102: {
                this.aq = 5;
                this.a(6, 627);
                return;
            }
            case 103: {
                this.aq = 104;
                this.b(new v(this, 667, 1, 0, 0));
                return;
            }
            case 104: {
                this.n.d.E &= 0xFFFFFFFD;
                if (this.A == 100) {
                    this.n.n(-20);
                    this.n.j(0);
                    this.aq = 105;
                    this.a(1, 629);
                    return;
                }
                this.aq = 5;
                this.k(666);
                this.n();
                return;
            }
            case 105: {
                this.a("\uae38\uc218\uc6d4(\uad50\ud1b5\ubd84\uc57c)", 20);
                return;
            }
            case 201: {
                this.aq = 202;
                this.a(2, 631);
                return;
            }
            case 202: {
                this.aq = 5;
                this.a(6, 632);
                return;
            }
            case 203: {
                this.aq = 204;
                this.b(new v(this, 667, 1, 0, 0));
                return;
            }
            case 204: {
                this.n.d.E &= 0xFFFFFFEF;
                if (this.A == 100) {
                    this.n.n(-20);
                    this.n.j(1);
                    this.aq = 205;
                    this.a(2, 634);
                    return;
                }
                this.aq = 5;
                this.k(666);
                this.n();
                return;
            }
            case 205: {
                this.a("\ub3c8\ubaa8\uc544(\uacbd\uc81c\ubd84\uc57c)", 20);
                return;
            }
            case 311: {
                this.aq = 80;
                this.a(17, 387);
                return;
            }
            case 411: {
                this.aq = 412;
                this.b(new l(this, 390, 391));
                return;
            }
            case 412: {
                int n3;
                if (this.A == 100) {
                    this.aq = 80;
                    n3 = 513;
                } else {
                    this.aq = 81;
                    n3 = 392;
                }
                this.a(this.G, n3);
                return;
            }
            case 511: {
                this.aq = 512;
                this.b(new l(this, 394, 395));
                return;
            }
            case 512: {
                int n4;
                if (this.A == 100) {
                    this.aq = 80;
                    n4 = 513;
                } else {
                    this.aq = 81;
                    n4 = 396;
                }
                this.a(this.G, n4);
                return;
            }
            case 601: {
                this.aq = 602;
                this.a(8, 771);
                return;
            }
            case 602: {
                this.aq = 603;
                this.a(15, 772);
                return;
            }
            case 603: {
                this.aq = 610;
                this.a(6, 773);
                return;
            }
            case 610: {
                this.aq = 611;
                this.b(new v(this, 667, 1, 0, 0));
                return;
            }
            case 611: {
                if (this.A == 100) {
                    this.n.d.m |= 3;
                    this.n.d.e = 0;
                    this.n.i(-10);
                    this.n();
                    this.aq = 612;
                    this.a(15, 774);
                    return;
                }
                this.n.d.m |= 1;
                this.n();
                this.aq = 620;
                this.a(15, 777);
                return;
            }
            case 612: {
                this.aq = 613;
                this.k(775);
                return;
            }
            case 613: {
                this.aq = 614;
                this.k(776);
                return;
            }
            case 614: {
                p p2 = new p(this);
                p2.j = "\uac15\ub3c4(\uae30\ubcf8\ube44\uc11c)";
                p2.d = -10;
                this.aq = 5;
                this.b(new ac(this, p2, 0, "\ube44\uc11c\ub4f1\uc6a9"));
                return;
            }
            case 620: {
                this.aq = 5;
                this.k(778);
                return;
            }
        }
    }
}
