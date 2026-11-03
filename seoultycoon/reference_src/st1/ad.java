/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class ad
extends aq {
    Image g;
    int c;

    ad(aq aq2) {
        super(aq2);
        this.a = aq2;
    }

    void b() {
        try {
            this.g = this.n.c("te");
        }
        catch (Exception exception) {
            aq.r();
        }
        if (!this.a(6, (int)this.n.d.e, 6)) {
            this.c();
        }
        this.u();
    }

    void d(int n) {
        switch (n) {
            case 50: 
            case 52: 
            case 141: 
            case 142: {
                this.c = (this.c + 2) % 3;
                break;
            }
            case 54: 
            case 56: 
            case 145: 
            case 146: {
                this.c = (this.c + 1) % 3;
                break;
            }
            case 53: 
            case 131: 
            case 148: {
                switch (this.c) {
                    case 0: {
                        this.b(new ai(this, true));
                        return;
                    }
                    case 1: {
                        this.b(new ah(this));
                        return;
                    }
                    case 2: {
                        this.b(new x(this));
                        return;
                    }
                }
                return;
            }
            case 8: {
                this.v();
                return;
            }
            default: {
                return;
            }
        }
        this.c();
    }

    void x() {
        ((c)this.a).e(true);
        this.c();
    }

    void c() {
        int n = this.d + 4;
        int n2 = this.v - 41;
        for (int i = 0; i < 3; ++i) {
            this.a(this.g, n, n2, i == this.c ? 46 : 0, 0, 46, 17);
            this.a(this.g, n + 20, n2 + 3, i * 19, 17, 19, 11);
            n += 4;
            n2 += 18;
        }
        this.a(this.g, this.d + 7, this.v - 38, 57, 17, 10, 12);
        this.a(this.g, this.d + 10, this.v - 20, 67, 17, 13, 12);
        this.a(this.g, this.d + 14, this.v - 3, 80, 17, 13, 13);
        this.p();
    }
}
