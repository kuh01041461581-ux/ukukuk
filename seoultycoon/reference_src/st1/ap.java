/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class ap
extends ar {
    ap(aq aq2) {
        super(aq2);
    }

    void b() {
        this.d(50, 10, 30);
        this.u();
    }

    void J() {
        switch (this.D) {
            case 0: {
                if (this.e(1, 1, 189)) {
                    return;
                }
                if (this.al[this.D] != 1 || this.n.h(3) || (this.e() & 1) != 0) break;
                int n2 = this.n.d.E >> 13 & 3;
                if (n2 < 2) {
                    this.n.d.E += 8192;
                    this.aq = 201 + (n2 << 1);
                    this.k(643 + n2 * 3);
                    return;
                }
                if (this.n.d.C < 20) break;
                this.aq = 205;
                this.b(new n((aq)this, (int)this.n.d.e, 649, 662, -1));
                return;
            }
            case 1: {
                if (this.n.b(4)) {
                    if (this.e(1, 5, 199)) {
                        return;
                    }
                    if (this.al[this.D] == 0) {
                        this.b(-1, 3);
                        return;
                    }
                }
                if (this.al[this.D] != 1 || !this.n.h(3) && !this.n.h(4) || this.n.k(3) || (this.e() & 3) != 0) break;
                this.G = this.n.h(3) ? 4 : 5;
                this.t = 1;
                if (this.n.l(this.t) && this.n.e(1004)) {
                    this.aq = 401;
                    this.a(this.G, 517);
                    return;
                }
                this.aq = 411;
                this.a(this.G, 353);
                return;
            }
            case 2: {
                if (this.e(1, 2, 206)) {
                    return;
                }
                if (this.n.k(0) && this.al[this.D] == 1) {
                    this.b(-1, 3);
                    return;
                }
                if (!this.n.k(17) || this.al[this.D] != 2) break;
                this.b(-1, 3);
                return;
            }
            case 3: {
                if (this.e(1, 3, 222)) {
                    return;
                }
                if (this.e(2, 4, 229)) {
                    return;
                }
                if (this.al[this.D] == 2 && (this.n.h(3) || this.n.h(4)) && !this.n.k(2) && (this.e() & 3) == 0) {
                    this.G = this.n.h(3) ? 4 : 5;
                    this.t = 0;
                    if (this.n.l(this.t) && this.n.e(997)) {
                        this.aq = 301;
                        this.a(this.G, 516);
                        return;
                    }
                    this.aq = 311;
                    this.a(this.G, 349);
                    return;
                }
                if (!this.n.b(3) || this.al[this.D] != 1) break;
                this.b(-1, 3);
                return;
            }
            case 4: {
                if (this.e(1, 0, 233)) {
                    return;
                }
                switch (this.al[this.D]) {
                    case 1: {
                        this.aq = 101;
                        this.a(6, 237 + this.e() % 3);
                        return;
                    }
                    case 2: {
                        this.aq = 102;
                        this.a(6, 240 + this.e() % 3);
                        return;
                    }
                }
                break;
            }
            case 5: {
                int n3;
                if (this.n.k(4)) {
                    if (this.e(1, 6, 249)) {
                        return;
                    }
                    if (this.al[this.D] == 0) {
                        this.b(-1, 3);
                        return;
                    }
                }
                if (this.n.k(5) && this.e(2, 7, 253)) {
                    return;
                }
                if (this.al[this.D] != 2) break;
                if ((this.n.h(0) || this.n.h(4)) && !this.n.l(2) && (this.e() & 3) == 0) {
                    this.G = this.n.h(0) ? 1 : 5;
                    this.t = 2;
                    this.aq = 511;
                    this.a(this.G, 357);
                    return;
                }
                if (this.n.k(6)) {
                    this.b(-1, 3);
                    return;
                }
                if (!this.n.h(0) && !this.n.h(4) || (this.e() & 3) != 0) break;
                int n4 = this.G = this.n.h(0) ? 1 : 5;
                if (this.n.e(1025)) {
                    this.aq = 601;
                    n3 = 520;
                } else {
                    this.aq = 5;
                    n3 = 363;
                }
                this.b(new n((aq)this, this.G, 362, n3, -1));
                return;
            }
        }
        this.c();
    }

    void m(int n2) {
        switch (n2) {
            case 101: {
                this.o(30);
                return;
            }
            case 102: {
                this.o(40);
                return;
            }
            case 201: {
                this.aq = 202;
                this.a(4, 644);
                return;
            }
            case 202: {
                this.aq = 5;
                this.a(6, 645);
                return;
            }
            case 203: {
                this.aq = 204;
                this.a(4, 647);
                return;
            }
            case 204: {
                this.aq = 5;
                this.a(6, 648);
                return;
            }
            case 205: {
                this.aq = 206;
                this.b(new v(this, 667, 1, 0, 0));
                return;
            }
            case 206: {
                if (this.A == 100) {
                    this.n.n(-20);
                    this.n.j(3);
                    this.aq = 207;
                    this.a(4, 650);
                    return;
                }
                this.aq = 5;
                this.n.d.E &= 0xFFFFCFFF;
                this.k(666);
                this.n();
                return;
            }
            case 207: {
                this.a("\uccad\ub9d1\uc74c(\ud658\uacbd\ubd84\uc57c)", 20);
                return;
            }
            case 301: {
                this.aq = 302;
                this.l(2);
                return;
            }
            case 302: {
                int n3;
                if (this.A == 100) {
                    this.aq = 303;
                    n3 = 536;
                } else {
                    this.aq = 81;
                    n3 = 537;
                }
                this.a(this.G, n3);
                return;
            }
            case 303: {
                this.aq = 5;
                this.j(2);
                this.n();
                this.O();
                return;
            }
            case 311: {
                this.aq = 312;
                this.b(new l(this, 350, 351));
                return;
            }
            case 312: {
                int n4;
                if (this.A == 100) {
                    this.aq = 80;
                    n4 = 513;
                } else {
                    this.aq = 81;
                    n4 = 352;
                }
                this.a(this.G, n4);
                return;
            }
            case 401: {
                this.aq = 402;
                this.l(3);
                return;
            }
            case 402: {
                int n5;
                if (this.A == 100) {
                    this.aq = 403;
                    n5 = 536;
                } else {
                    this.aq = 81;
                    n5 = 537;
                }
                this.a(this.G, n5);
                return;
            }
            case 403: {
                this.aq = 5;
                this.j(3);
                this.n();
                this.O();
                return;
            }
            case 411: {
                this.aq = 412;
                this.b(new l(this, 354, 355));
                return;
            }
            case 412: {
                int n6;
                if (this.A == 100) {
                    this.aq = 80;
                    n6 = 513;
                } else {
                    this.aq = 81;
                    n6 = 356;
                }
                this.a(this.G, n6);
                return;
            }
            case 511: {
                this.aq = 512;
                this.b(new l(this, 358, 359));
                return;
            }
            case 512: {
                int n7;
                if (this.A == 100) {
                    this.aq = 80;
                    n7 = 360;
                } else {
                    this.aq = 81;
                    n7 = 361;
                }
                this.a(this.G, n7);
                return;
            }
            case 601: {
                this.aq = 602;
                this.l(6);
                return;
            }
            case 602: {
                int n8;
                if (this.A == 100) {
                    this.aq = 603;
                    n8 = 536;
                } else {
                    this.aq = 5;
                    n8 = 537;
                }
                this.a(this.G, n8);
                return;
            }
            case 603: {
                this.aq = 5;
                this.j(6);
                this.n();
                this.O();
                return;
            }
        }
    }

    void o(int n2) {
        try {
            Image[] imageArray = new Image[]{this.n.c("u2"), this.n.c("u3")};
            int n3 = 1;
            boolean bl = true;
            while (bl) {
                this.a(imageArray[n3]);
                n3 ^= 1;
                this.p();
                long l2 = System.currentTimeMillis() + 1000L;
                while (bl && l2 > System.currentTimeMillis()) {
                    if (!this.h()) {
                        switch (this.f.y()) {
                            case 53: 
                            case 131: 
                            case 148: {
                                bl = false;
                            }
                        }
                    }
                    aq.h(0);
                }
            }
        }
        catch (Exception exception) {
            // empty catch block
        }
        this.a(n2, "\ud734\uc2dd");
    }
}
