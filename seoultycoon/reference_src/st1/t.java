/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class t
extends aq {
    int w;
    int q;
    int c;
    int z;
    int t;
    int[] s;
    int g;
    int o;
    Image i;
    w p;

    t(aq aq2) {
        super(aq2);
        this.a = aq2;
    }

    void b() {
        int n;
        this.p = new w(this, this.n.n[1], 0, 0, 5, 8);
        this.s = new int[6];
        this.g = 0;
        this.t = 0;
        this.w = this.d - 60;
        this.q = this.v - 56;
        this.c(this.q, 112, 3, 56, 2);
        this.e.setColor(0xFFFFFF);
        this.a("\ub85c\ub610\uac8c\uc784", this.w + 30, this.q + 4, 1);
        int n2 = 0;
        int n3 = this.q + 13;
        for (n = 1; n < 46; ++n) {
            if ((n & 7) == 1) {
                n2 = this.w + 15;
                n3 += 11;
            }
            this.p.a(n, n2, n3, 0);
            n2 += 14;
        }
        this.a(this.w + 5, this.q + 92, 110, 16, 18688);
        n2 = this.w + 13;
        n3 = this.q + 94;
        for (n = 0; n < 6; ++n) {
            this.a(n2, n3, 15, 12, 0);
            n2 += 16;
        }
        this.i = this.a(this.w, this.q, 120, 112);
        this.u();
    }

    void d(int n) {
        switch (this.t) {
            case 0: {
                switch (n) {
                    case 50: 
                    case 141: {
                        this.c = this.c > 7 ? (this.c -= 8) : (this.c += 40);
                        if (this.c <= 44) break;
                        this.c -= 8;
                        break;
                    }
                    case 52: 
                    case 142: {
                        if (this.c > 0) {
                            --this.c;
                            break;
                        }
                        this.c = 44;
                        break;
                    }
                    case 56: 
                    case 146: {
                        this.c = this.c < 37 ? (this.c += 8) : (this.c -= 40);
                        if (this.c >= 0) break;
                        this.c += 8;
                        break;
                    }
                    case 54: 
                    case 145: {
                        if (this.c < 44) {
                            ++this.c;
                            break;
                        }
                        this.c = 0;
                        break;
                    }
                    case 53: 
                    case 131: 
                    case 148: {
                        this.z();
                        break;
                    }
                    case 8: {
                        if (this.g <= 0) break;
                        --this.g;
                        break;
                    }
                    case 190: {
                        this.y();
                        break;
                    }
                    default: {
                        return;
                    }
                }
                this.z = 0;
                this.k();
                return;
            }
            case 1: {
                switch (n) {
                    case 8: 
                    case 53: 
                    case 131: 
                    case 148: {
                        this.t = 2;
                        if (this.o == 1) {
                            this.a(7, 118);
                        } else if (this.o < 6) {
                            this.a(7, 119);
                        } else {
                            this.k(120);
                        }
                        return;
                    }
                }
                return;
            }
        }
    }

    void b(int n) {
        switch (this.t) {
            case 0: {
                this.a(this.i, this.w, this.q);
                for (int i = 0; i < this.g; ++i) {
                    int n2 = this.s[i] - 1;
                    int n3 = this.w + 4 + (n2 & 7) * 14;
                    int n4 = this.q + 23 + (n2 >> 3) * 11;
                    this.e.setColor(18688);
                    this.e.fillRect(n3, n4, 13, 10);
                    this.e.setColor(0xFFFFFF);
                    this.p.a(this.s[i], n3 + 11, n4 + 1, 0);
                    this.p.a(this.s[i], this.w + 25 + i * 16, this.q + 96, 0);
                }
                if ((this.z & 2) == 0) {
                    this.e.setColor(16767744);
                    this.e.drawRect(this.w + 3 + (this.c & 7) * 14, this.q + 22 + (this.c >> 3) * 11, 14, 11);
                }
                this.p();
                ++this.z;
                if (this.g < 6) break;
                this.b(new v(this, 624, 1, 0, 0));
            }
        }
    }

    void x() {
        switch (this.t) {
            case 0: {
                if (this.A == 100) {
                    this.c();
                } else {
                    this.g = 0;
                }
                return;
            }
            case 2: {
                int n = 0;
                for (int i = 0; i < 4; ++i) {
                    n += this.n.d.P[i];
                }
                p p2 = new p(this);
                p2.j = "\uc21c\uc704 : ";
                if (this.o == 6) {
                    p2.j = p2.j + "\uaf5d";
                    this.n.d.m |= 0x100;
                } else {
                    p2.j = p2.j + this.o + "\ub4f1";
                    this.n.d.m &= 0xFFFFFEFF;
                }
                p2.o = n / 10 * this.n.f[1943 + this.o] / 10;
                p2.i = 5;
                this.n.n(p2.o);
                this.n.m(5);
                this.n();
                this.t = 3;
                ((ar)this.a).O();
                ((ar)this.a).g(false);
                this.b(new ac(this, p2, 0, "\ub85c\ub610\uacb0\uacfc"));
                return;
            }
            case 3: {
                this.v();
                return;
            }
        }
    }

    void z() {
        int n;
        if (this.g >= 6) {
            return;
        }
        int n2 = this.c + 1;
        for (n = 0; n < this.g; ++n) {
            if (this.s[n] != n2) continue;
            for (n2 = n + 1; n2 < this.g; ++n2) {
                this.s[n2 - 1] = this.s[n2];
            }
            --this.g;
            return;
        }
        for (n = 0; n < this.g; ++n) {
            if (this.s[n] <= n2) continue;
            int n3 = this.s[n];
            this.s[n] = n2;
            n2 = n3;
        }
        this.s[this.g++] = n2;
    }

    void y() {
        int n = 0;
        while (this.g < 6) {
            int n2;
            do {
                n = this.e() % 45 + 1;
                for (n2 = 0; n2 < this.g && this.s[n2] != n; ++n2) {
                }
            } while (n2 < this.g);
            for (n2 = 0; n2 < this.g; ++n2) {
                if (this.s[n2] <= n) continue;
                int n3 = this.s[n2];
                this.s[n2] = n;
                n = n3;
            }
            this.s[this.g++] = n;
        }
    }

    void c() {
        int n;
        int n2;
        int n3;
        this.t = 1;
        int[] nArray = new int[7];
        int n4 = this.e() & 0x3FF;
        if (n4 < 100) {
            this.o = 1;
            n3 = 6;
        } else if (n4 < 250) {
            this.o = 2;
            n3 = 6;
        } else if (n4 < 400) {
            this.o = 3;
            n3 = 5;
        } else if (n4 < 550) {
            this.o = 4;
            n3 = 4;
        } else if (n4 < 750) {
            this.o = 5;
            n3 = 3;
        } else {
            this.o = 6;
            n3 = this.e() % 3;
        }
        for (n4 = 0; n4 < 7; ++n4) {
            if (n3 > 0) {
                do {
                    n = this.s[this.e() % 6];
                    for (n2 = 0; n2 < n4 && nArray[n2] != n; ++n2) {
                    }
                } while (n2 < n4);
                --n3;
            } else {
                while (true) {
                    n = this.e() % 45 + 1;
                    for (n2 = 0; n2 < n4 && nArray[n2] != n; ++n2) {
                    }
                    if (n2 < n4) continue;
                    for (n2 = 0; n2 < 6 && this.s[n2] != n; ++n2) {
                    }
                    if (n2 >= 6) break;
                }
            }
            nArray[n4] = n;
        }
        if (this.o == 2) {
            n2 = this.e() % 6;
            n = nArray[n2];
            nArray[n2] = nArray[6];
            nArray[6] = n;
        }
        for (n4 = 0; n4 < 6; ++n4) {
            for (n = n4 + 1; n < 6; ++n) {
                if (nArray[n4] <= nArray[n]) continue;
                n2 = nArray[n4];
                nArray[n4] = nArray[n];
                nArray[n] = n2;
            }
        }
        this.e(0);
        this.c(5);
        this.p();
        this.e(1000);
        n = this.w + 22;
        n2 = this.q + 58;
        this.p = new w(this, this.n.n[1], 0, 15, 3, 5);
        for (n4 = 0; n4 < 7; ++n4) {
            this.p.a(nArray[n4], n, n2, 1);
            n = n4 < 5 ? (n += 12) : (n += 14);
            if (n4 < 6) {
                this.e.setColor(0xFF6060);
            } else {
                this.e.setColor(0x60FF60);
            }
            for (n3 = 0; n3 < 6; ++n3) {
                if (nArray[n4] != this.s[n3]) continue;
                this.e.drawRect(this.w + 13 + (n3 << 4), this.q + 94, 14, 11);
            }
            this.p();
            this.e(1000);
        }
        this.o();
    }
}
