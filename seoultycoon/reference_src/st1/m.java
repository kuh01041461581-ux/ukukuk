/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  com.skt.m.Device
 *  javax.microedition.lcdui.Image
 */
import com.skt.m.Device;
import javax.microedition.lcdui.Image;

class m
extends aq {
    Image p;
    int i;
    int g;
    int c;
    int o;
    String[] q = new String[]{"\uce90\ub9ad\ud130", "\uac74  \ubb3c", "\uc5d4  \ub529", "\uac15 \ub3d9", "\uac15 \uc11c", "\uac15 \ub0a8", "\uac15 \ubd81", "\uc870 \uc791 \ubc95", "\uac8c\uc784\uc18c\uac1c", "\uac8c\uc784\ubc29\ubc95", "\uace0\uac1d\uc9c0\uc6d0 \uc548\ub0b4", "\uac8c\uc784 \ud3c9\uac00\ud558\uae30", "\uce5c\uad6c\uc5d0\uac8c \uc120\ubb3c", "\uce58\uc988\ucf00\uc775 \ud648\ud53c", "Cheese Cake", "Production", "0505-538-8584", "www.ccake.co.kr", "service@ccake.co.kr", "\uaebc\uc9d0", "\ucf1c\uc9d0", "\ub290\ub9bc", "\ubcf4\ud1b5", "\ube60\ub984"};

    m(aq aq2, Image image, int n) {
        super(aq2);
        this.a = aq2;
        this.p = image;
        this.i = n;
    }

    void b() {
        this.y();
        this.u();
    }

    void d(int n) {
        switch (n) {
            case 52: 
            case 142: {
                if (this.i == 4) {
                    switch (this.g) {
                        case 0: {
                            this.n.a(-1);
                            this.n();
                            break;
                        }
                        case 1: {
                            this.n.j = (byte)(this.n.j ^ 1);
                            this.n();
                            this.g(500);
                            break;
                        }
                        case 2: {
                            this.n.j = this.n.j >= 16 ? (byte)(this.n.j - 16) : (byte)(this.n.j & 0xF | 0x20);
                            this.n();
                        }
                    }
                    break;
                }
            }
            case 50: 
            case 141: {
                if (this.c < 1) {
                    return;
                }
                this.g = (this.g + this.c - 1) % this.c;
                break;
            }
            case 54: 
            case 145: {
                if (this.i == 4) {
                    switch (this.g) {
                        case 0: {
                            this.n.a(1);
                            this.n();
                            break;
                        }
                        case 1: {
                            this.n.j = (byte)(this.n.j ^ 1);
                            this.n();
                            this.g(500);
                            break;
                        }
                        case 2: {
                            this.n.j = this.n.j < 32 ? (byte)(this.n.j + 16) : (byte)(this.n.j & 0xF);
                            this.n();
                        }
                    }
                    break;
                }
            }
            case 56: 
            case 146: {
                if (this.c < 1) {
                    return;
                }
                this.g = (this.g + 1) % this.c;
                break;
            }
            case 53: 
            case 131: 
            case 148: {
                switch (this.i) {
                    case 2: {
                        switch (this.g) {
                            case 0: {
                                this.b(new af(this, this.q[0], 0));
                                return;
                            }
                            case 1: {
                                this.b(new m(this, this.p, 7));
                                return;
                            }
                            case 2: {
                                this.b(new af(this, this.q[2], 20));
                                return;
                            }
                        }
                        return;
                    }
                    case 3: {
                        this.o = 30;
                        this.b(new a(this, this.q[7 + this.g], this.n.g(733 + this.g)));
                        return;
                    }
                    case 6: {
                        switch (this.g) {
                            case 0: {
                                this.B();
                                return;
                            }
                            case 1: 
                            case 2: 
                            case 3: {
                                this.o = 10 + this.g;
                                this.b(new v(this, 813, 1, 0, 1));
                                return;
                            }
                        }
                    }
                    case 7: {
                        this.b(new af(this, "", this.g + 10));
                        return;
                    }
                }
                this.v();
                return;
            }
            case 190: {
                if (this.i != 5) {
                    return;
                }
                this.o = 20;
                this.b(new v(this, 732, 1, 0, 1));
                return;
            }
            case 8: {
                this.v();
                return;
            }
            case 194: 
            case 195: {
                if (this.i == 4) {
                    this.n();
                    break;
                }
                return;
            }
            default: {
                return;
            }
        }
        this.c();
    }

    void c() {
        this.a(this.d - 54, this.v - 26, 108, this.i < 5 ? 55 : 71, 170);
        switch (this.i) {
            case 2: {
                this.c = 3;
                this.z();
                this.m(0);
                break;
            }
            case 3: {
                this.c = 3;
                this.z();
                this.m(7);
                break;
            }
            case 4: {
                this.c = 3;
                this.z();
                this.A();
                this.a("(\uad8c\uc7a5\uc18d\ub3c4:\ub290\ub9bc)", this.v + 36, 0xFFFFFF);
                break;
            }
            case 5: {
                this.a("\ucd5c\uace0\uc810\uc218", this.v - 19, 0xFFFFFF);
                this.a(this.d - 40, this.v, 80, this.n.l + 2, 0);
                this.a(Integer.toString(this.n.d.s), this.v + 1, 0xFFFFFF);
                this.a("[\ud1b5\ud654]\ub7ad\ud0b9\ud655\uc778", this.v + 26, 0xFFFFFF);
                break;
            }
            case 6: {
                this.c = 4;
                this.z();
                this.m(10);
                break;
            }
            case 7: {
                this.c = 4;
                this.z();
                this.m(3);
            }
        }
        this.p();
    }

    void z() {
        int n = this.v - 21 + (this.g << 4);
        int n2 = n + this.n.l + 4;
        this.e.setColor(0);
        this.e.fillRect(this.d - 50, n, 100, n2 - n);
        this.e.setColor(2396927);
        this.e.drawLine(this.d - 49, n - 1, this.d + 48, n - 1);
        this.e.drawLine(this.d + 48, n - 1, this.d + 50, n + 1);
        this.e.drawLine(this.d + 50, n + 1, this.d + 50, n2 - 2);
        this.e.drawLine(this.d + 50, n2 - 2, this.d + 48, n2);
        this.e.drawLine(this.d + 48, n2, this.d - 49, n2);
        this.e.drawLine(this.d - 49, n2, this.d - 51, n2 - 2);
        this.e.drawLine(this.d - 51, n2 - 2, this.d - 51, n + 1);
        this.e.drawLine(this.d - 51, n + 1, this.d - 49, n - 1);
    }

    void m(int n) {
        int n2 = this.v - 19;
        for (int i = 0; i < this.c; ++i) {
            this.a(this.q[n + i], n2, 0xFFFFFF);
            n2 += 16;
        }
    }

    void A() {
        int n = this.v - 19;
        this.a("\uc74c\ub7c9", this.d - 47, n, 4, 0xFFFFFF);
        this.a("\uc9c4\ub3d9", this.d - 47, n + 16, 4, 0xFFFFFF);
        this.a("\uc18d\ub3c4", this.d - 47, n + 32, 4, 0xFFFFFF);
        this.a(Integer.toString(this.n.h), this.d + 22, n, 1, 0xFFFFFF);
        this.a(this.q[19 + (this.n.j & 1)], this.d + 22, n + 16, 1, 0xFFFFFF);
        this.a(this.q[21 + (this.n.j >> 4)], this.d + 22, n + 32, 1, 0xFFFFFF);
        this.a("\u25c1", this.d + 2, n + (this.g << 4), 1, 0xFFFFFF);
        this.a("\u25b7", this.d + 42, n + (this.g << 4), 1, 0xFFFFFF);
    }

    void a(String string, int n, int n2) {
        this.a(string, this.d, n, 1, n2);
    }

    void y() {
        this.a(this.d - 60, this.v - 56, 120, 112, 0, false);
        this.a(this.p, this.d - 20, this.v - 44, 42, 13 * (this.i < 7 ? this.i : 2), 42, 13);
        this.c();
    }

    void B() {
        this.a(this.d - 60, this.v - 56, 120, 112, 1, false);
        this.a(this.q[10], this.v - 44, 0xFFFFFF);
        this.a(this.d - 57, this.v - 26, 114, 75, 85);
        int n = this.v - 23;
        for (int i = 14; i < 19; ++i) {
            this.a(this.q[i], n, 0xFFFFFF);
            n += 14;
            if (i != 15) continue;
            n += 2;
        }
        this.p();
        this.j();
        this.y();
    }

    void x() {
        int n = this.o;
        this.o = 0;
        switch (n) {
            case 11: {
                if (this.A != 100) break;
                Device.invokeWapBrowser((String)"http://zad.co.kr:7000/st/point.php");
                return;
            }
            case 12: {
                if (this.A != 100) break;
                Device.invokeWapBrowser((String)"http://zad.co.kr:7000/st/gift.php");
                return;
            }
            case 13: {
                if (this.A != 100) break;
                Device.invokeWapBrowser((String)"http://zad.co.kr:7000/main.php?gid=30301");
                return;
            }
            case 20: {
                if (this.A != 100) break;
                this.o = 30;
                this.b(new k(this));
                return;
            }
            case 30: {
                this.a.g();
            }
        }
        this.y();
    }

    void n() {
        this.n.b();
    }
}
