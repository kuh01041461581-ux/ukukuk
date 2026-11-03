/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  com.xce.lcdui.TextComponent
 *  com.xce.lcdui.TextComponentHandler
 */
import com.xce.lcdui.TextComponent;
import com.xce.lcdui.TextComponentHandler;

class j
extends aq {
    int z;
    int c;
    String t;
    String[] q;
    int D;
    int o;
    int F;
    int E;
    boolean g;
    char[] p;
    int B;
    int s;
    int C;
    TextComponentHandler i;
    am w;

    j(aq aq2, String string, int n, String[] stringArray) {
        super(aq2);
        this.t = string;
        this.B = n;
        this.q = stringArray;
        this.F = 100;
        this.E = 39 + this.n.l;
        this.D = this.d - (this.F >> 1);
        this.o = this.v - (this.E >> 1);
        this.p = new char[this.B];
        this.i = TextComponentHandler.getTextComponentHandler();
        this.w = new am(this);
        this.i.setTextComponent((TextComponent)this.w);
        this.i.clear();
    }

    void b() {
        this.a(this.D, this.o, this.F, this.E, 1, false);
        this.a(this.t, this.d, this.o + 12, 1, 0xFFFFFF);
        this.y();
        this.u();
    }

    void d(int n) {
        switch (n) {
            case 131: 
            case 148: {
                if (this.c > 0) {
                    this.v();
                }
                return;
            }
            case 142: {
                n = 8;
            }
        }
        this.i.keyPressed(n);
        this.i.keyReleased(n);
        this.y();
    }

    void b(int n) {
        this.c();
    }

    void c() {
        this.a(this.D + 5, this.o + 30, this.F - 10, this.E - 35, 85);
        this.e.setColor(0xFFFFFF);
        this.a(this.q[0], this.D + 10, this.o + 32, 4);
        this.g ^= true;
        if (this.g) {
            this.e.drawLine(this.z, this.o + 32, this.z, this.o + 31 + this.n.l);
        }
        this.p();
    }

    void y() {
        this.q[0] = new String(this.p, 0, this.s);
        this.c = this.q[0].getBytes().length;
        this.z = this.D + 10 + this.c * this.n.e;
        this.g = false;
        this.c(0, 0, 300);
    }
}
