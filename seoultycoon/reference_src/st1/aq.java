/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  com.skt.m.Graphics2D
 *  com.skt.m.Vibration
 *  com.xce.lcdui.XDisplay
 *  javax.microedition.lcdui.Canvas
 *  javax.microedition.lcdui.Graphics
 *  javax.microedition.lcdui.Image
 */
import com.skt.m.Graphics2D;
import com.skt.m.Vibration;
import com.xce.lcdui.XDisplay;
import javax.microedition.lcdui.Canvas;
import javax.microedition.lcdui.Graphics;
import javax.microedition.lcdui.Image;

class aq
extends Canvas {
    q f;
    aq a;
    Graphics e;
    Graphics2D l;
    int k;
    int j;
    int d;
    int v;
    s n;
    boolean m;
    long x;
    int u;
    long[] h;
    int A;
    static final int[] y = new int[]{9577472, 0, 2387200, 18688};
    Image b;
    int r;

    static boolean t() {
        return false;
    }

    aq() {
        this.a();
    }

    aq(aq aq2) {
        this.f = aq2.f;
        this.e = this.f.e;
        this.l = this.f.l;
        this.k = this.f.k;
        this.j = this.f.j;
        this.d = this.f.d;
        this.v = this.f.v;
        this.n = this.f.n;
        this.a();
    }

    void a() {
        this.h = new long[8];
    }

    public void paint(Graphics graphics) {
        this.e = graphics;
    }

    void b() {
    }

    void d(int n) {
    }

    void b(int n) {
    }

    void x() {
    }

    void u() {
        this.m = true;
    }

    void a(int n, boolean bl) {
        this.a(this.n.a("s" + n), bl);
    }

    void b(int n, boolean bl) {
        if ((this.n.j & 2) != 0) {
            this.a(n, bl);
        }
    }

    void a(byte[] byArray, boolean bl) {
        this.w();
        this.n.g = new h(byArray, bl);
        this.n.g.start();
    }

    void w() {
        if (this.n.g == null) {
            return;
        }
        this.n.g.a();
        this.n.g = null;
    }

    void g(int n) {
        if ((this.n.j & 1) != 0) {
            if (n == 0) {
                Vibration.stop();
            } else {
                Vibration.start((int)Vibration.getLevelNum(), (int)n);
            }
        }
    }

    static void h(int n) {
        try {
            Thread.sleep(n);
        }
        catch (Exception exception) {
            // empty catch block
        }
    }

    void e(int n) {
        do {
            aq.h(0);
        } while (this.x + (long)n > System.currentTimeMillis());
        this.x = System.currentTimeMillis();
    }

    void s() {
        this.e(this.h() ? this.n.i : 0);
    }

    int j() {
        while (this.h()) {
            aq.h(0);
        }
        return this.f.y();
    }

    void n() {
        this.f.B = true;
    }

    int e() {
        return Math.abs(this.f.c.nextInt());
    }

    void a(long l) {
        this.f.c.setSeed(System.currentTimeMillis() / (l == 0L ? 1L : l * 60000L));
    }

    void o() {
        this.f.g = 0;
        this.f.w = 0;
    }

    boolean h() {
        return this.f.w == 0;
    }

    void p() {
        XDisplay.refresh((int)0, (int)0, (int)this.k, (int)this.j);
        ++this.f.p;
        this.e.setClip(0, 0, this.k, this.j);
    }

    void a(aq aq2) {
        aq2.u = 1;
        aq2.k();
        this.f.a = aq2;
    }

    void b(aq aq2) {
        this.f.t.push(this.f.a);
        this.a(aq2);
        this.A = 0;
    }

    void v() {
        if (!this.f.t.empty()) {
            this.f.a = (aq)((Object)this.f.t.pop());
            this.f.a.u = 3;
        }
    }

    void m() {
        while (!this.f.t.empty()) {
            this.f.t.pop();
        }
    }

    void f(int n) {
        this.f.s = n;
    }

    void c(int n, int n2, int n3) {
        this.h[n] = System.currentTimeMillis() + (long)n2;
        this.h[n + 4] = n3;
    }

    void k() {
        this.c(0, 0, this.n.i);
    }

    static void r() {
        Vibration.start((int)Vibration.getLevelNum(), (int)1000);
        System.exit(0);
    }

    void i(int n) {
        this.e.setColor(n);
        this.e.fillRect(0, 0, this.k, this.j);
    }

    void a(String string, int n, int n2, int n3) {
        this.e.drawString(string, n, n2 + this.n.m, 0x10 | n3);
    }

    Image a(int n, int n2, int n3, int n4) {
        this.i();
        n = Math.max(n, 0);
        n2 = Math.max(n2, 0);
        Image image = null;
        try {
            image = Graphics2D.captureLCD((int)n, (int)n2, (int)Math.min(n3, this.k - n), (int)Math.min(n4, this.j - n2));
        }
        catch (Exception exception) {
            // empty catch block
        }
        return image;
    }

    void a(Image image, int n, int n2) {
        if (image != null) {
            this.e.drawImage(image, Math.max(n, 0), Math.max(n2, 0), 20);
        }
    }

    void a(Image image, int n, int n2, int n3, int n4, int n5, int n6) {
        this.l.drawImage(n, n2, image, n3, n4, n5, n6, 0);
    }

    void i() {
        if (this.f.z || this.f.i) {
            System.gc();
        }
    }

    void c(int n, int n2, int n3, int n4, int n5) {
        int n6;
        int n7;
        if (n5 < 3) {
            n7 = this.d - 60;
            n6 = 120;
        } else {
            n7 = 0;
            n6 = this.k;
        }
        int n8 = n7 + n6 - 1;
        int n9 = n5 * 18;
        this.e.setClip(n7 + 3, n + 19, n6 - 6, n2 - 22);
        n2 += n;
        for (int i = n + 19; i < n + n2 - 3; i += 18) {
            for (int j = n7 + 3; j < n8 - 2; j += 18) {
                this.a(this.n.n[0], j, i, n9, 0, 18, 18);
            }
        }
        if (n5 < 3) {
            this.a(this.n.n[0], this.d - 55, n2 - 17, 0, 18 + n5 * 14, 105, 14);
        }
        n3 += n7;
        this.e.setClip(0, 0, this.k, this.j);
        this.e.setColor(0);
        this.e.drawLine(n7, n + 16, n8, n + 16);
        this.e.drawLine(n7, n + 16, n7, n2 - 1);
        this.e.drawLine(n8, n + 16, n8, n2 - 6);
        this.e.drawLine(n7, n2 - 1, n8 - 5, n2 - 1);
        this.e.drawLine(n8 - 4, n2 - 2, n8 - 1, n2 - 5);
        this.e.setColor(y[n5]);
        this.e.drawLine(n7 + 2, n + 18, n8 - 2, n + 18);
        this.e.drawLine(n7 + 2, n + 18, n7 + 2, n2 - 3);
        this.e.drawLine(n8 - 2, n + 18, n8 - 2, n2 - 6);
        this.e.drawLine(n7 + 2, n2 - 3, n8 - 5, n2 - 3);
        this.e.drawLine(n8 - 4, n2 - 4, n8 - 3, n2 - 5);
        this.e.setColor(n5 < 2 ? 16749056 : 7208789);
        this.e.drawLine(n7 + 1, n + 17, n8 - 1, n + 17);
        this.e.drawLine(n7 + 1, n + 17, n7 + 1, n2 - 2);
        this.e.drawLine(n3 + 1, n + 1, n3 + n4 - 6, n + 1);
        this.e.drawLine(n3 + 1, n + 1, n3 + 1, n + 16);
        this.e.setColor(n5 < 2 ? 14380288 : 46592);
        this.e.drawLine(n8 - 1, n + 18, n8 - 1, n2 - 6);
        this.e.drawLine(n7 + 2, n2 - 2, n8 - 5, n2 - 2);
        this.e.drawLine(n8 - 4, n2 - 3, n8 - 2, n2 - 5);
        this.e.fillRect(n3 + 2, n + 2, n4 - 7, 16);
        if (n5 == 3) {
            this.a(this.n.n[0], n8 - 5, n2 - 10, 123, 0, 6, 10);
        }
        this.e.setColor(0);
        this.e.drawLine(n3, n, n3, n + 15);
        this.e.drawLine(n3, n, n3 + n4 - 6, n);
        this.a(this.n.n[0], n3 + n4 - 5, n + 1, 108 + (n5 > 1 ? 5 : 0), 0, 5, 17);
    }

    void b(int n, int n2, int n3) {
        this.e.setColor(0x494955);
        this.e.drawLine(n2 += this.d - 60, n, n2 + n3 - 6, n);
        this.e.drawLine(n2, n, n2, n + 13);
        this.a(this.n.n[0], n2 + n3 - 5, n + 1, 118, 0, 5, 13);
        this.e.setColor(0xB6B6AA);
        this.e.drawLine(n2 + 1, n + 1, n2 + n3 - 6, n + 1);
        this.e.drawLine(n2 + 1, n + 1, n2 + 1, n + 12);
        this.e.setColor(0x9292AA);
        this.e.fillRect(n2 + 2, n + 2, n3 - 7, 12);
    }

    void a(int n, int n2, int n3, int n4, int n5) {
        this.e.setColor(n5);
        this.e.fillRect(n + 1, n2, n3 - 2, n4);
        this.e.drawLine(n, n2 + 1, n, n2 + n4 - 2);
        this.e.drawLine(n + n3 - 1, n2 + 1, n + n3 - 1, n2 + n4 - 2);
    }

    void b(int n, int n2, int n3, int n4, int n5) {
        int[] nArray = new int[]{18688, 85, 85, 18688, 27904, 85, 9386, 27904};
        this.e.setColor(nArray[n5]);
        this.e.drawRect(n, n2 + 2, n3 - 3, n4 - 10);
        this.e.drawRect(n + 1, n2 + 1, n3 - 5, n4 - 8);
        this.e.drawRect(n + 2, n2, n3 - 7, n4 - 6);
        this.e.setColor(0xFFFFFF);
        this.e.drawLine(n + 2, n2 + 1, n + n3 - 5, n2 + 1);
        this.e.drawLine(n + 1, n2 + 2, n + 1, n2 + n4 - 8);
        this.e.setColor(0xDBDBAA);
        this.e.drawLine(n + n3 - 4, n2 + 2, n + n3 - 4, n2 + n4 - 8);
        this.e.drawLine(n + 2, n2 + n4 - 7, n + n3 - 5, n2 + n4 - 7);
        this.e.setColor(0);
        this.e.drawLine(n + n3 - 4, n2, n + n3 - 1, n2 + 3);
        this.e.drawLine(n + n3 - 1, n2 + 3, n + n3 - 1, n2 + n4 - 7);
        this.e.drawLine(n + n3 - 1, n2 + n4 - 7, n + n3 - 4, n2 + n4 - 4);
        this.e.drawLine(n + n3 - 4, n2 + n4 - 4, n + 4, n2 + n4 - 4);
        this.e.drawLine(n + 3, n2 + n4 - 5, n + n3 - 4, n2 + n4 - 5);
        this.e.drawLine(n + n3 - 4, n2 + n4 - 5, n + n3 - 2, n2 + n4 - 7);
        this.e.drawLine(n + n3 - 2, n2 + n4 - 7, n + n3 - 2, n2 + 2);
        this.e.drawLine(n + n3 - 4, n2 + n4 - 6, n + n3 - 3, n2 + n4 - 7);
        this.e.setColor(nArray[n5 + 4]);
        this.e.fillRect(n + 3, n2 + 3, n3 - 8, n4 - 11);
        this.a(this.n.n[0], n + (n3 >> 1) - 4, n2 + n4 - 8, 105, 24 + (n5 << 3), 10, 8);
    }

    void b(int n, int n2, int n3, int n4) {
        if (n2 == n3) {
            this.a(this.n.n[3], this.d - 19, n, 0, 0, 37, 14);
            this.a(this.n.n[4], this.d - 10, n + 2, n2 * 19, 0, 19, 10);
        } else {
            this.a(this.n.n[3], this.d - 38, n, 0, n4 * 13, 37, 14);
            this.a(this.n.n[3], this.d + 1, n, 0, (1 - n4) * 13, 37, 14);
            this.a(this.n.n[4], this.d - 29, n + 2, n2 * 19, 0, 19, 10);
            this.a(this.n.n[4], this.d + 10, n + 2, n3 * 19, 0, 19, 10);
        }
    }

    void f() {
    }

    boolean a(int n2, int n3, int n4) {
        if ((this.n.d.a & (n2 = 1 << n2)) == 0) {
            this.n.d.a |= n2;
            this.n();
            this.b(new n(this, n3, n4));
            return true;
        }
        return false;
    }

    void a(boolean bl) {
        int n2;
        this.w();
        int n3 = this.d - 64;
        int n4 = Math.min(this.j - 144, this.v - 72);
        this.e.setColor(2378239);
        this.e.fillRect(0, 0, this.k, n4 + 14);
        this.e.setColor(37631);
        this.e.fillRect(0, n4 + 47, this.k, 20);
        this.e.setColor(0x6D6D55);
        this.e.fillRect(n3, n4 + 51, 128, 16);
        this.e.setColor(85);
        this.e.fillRect(0, n4 + 66, this.k, this.j - n4 - 66);
        for (n2 = 0; n2 < this.k; n2 += 4) {
            this.a(this.n.n[9], n2, n4 + 14, 37, 0, 4, 33);
        }
        this.e.setColor(9596245);
        for (n2 = 0; n2 < 128; n2 += 4) {
            this.e.drawLine(n3 + n2, n4 + 57, n3 + n2 + 1, n4 + 57);
        }
        this.a(this.n.n[6], n3 + 9, n4 + 12, 4, 8, 12, 5);
        this.a(this.n.n[6], n3 + 23, n4 + 18, 4, 0, 27, 8);
        this.a(this.n.n[6], n3 + 61, n4 + 8, 0, 15, 35, 11);
        this.a(this.n.n[6], n3 + 95, n4 + 18, 16, 8, 14, 5);
        this.a(this.n.n[6], n3 + 107, n4 + 11, 4, 8, 12, 5);
        this.a(this.n.n[9], n3 + (bl ? 91 : 0), n4 + 36, 0, 0, 37, 28);
        this.a(this.n.n[9], n3 + 5, n4 + 71, 27, 33, 15, 12);
        d d2 = this.n.g(266 + this.e() % 37);
        d2.a(120 / this.n.e, false);
        n2 = n4 + 87;
        this.e.setColor(0xFFFFFF);
        while (d2.c() > 0) {
            this.a(d2.f(), n3 + 4, n2, 4);
            n2 += 14;
        }
        this.e.setColor(0xFFFFFF);
        this.e.drawRect(n3 - 1, n4 - 1, 129, 145);
        this.e(0);
        int n5 = this.n.g() ? 7 : 8;
        int n6 = 337;
        int n7 = n3 + (bl ? 5 : 38);
        this.b = this.a(n7 - 3, n4 + 27, 34, 40);
        for (n2 = 0; n2 < 4; ++n2) {
            this.a(this.b, n7 - 3, n4 + 27);
            this.a(this.n.n[n5], n7 + this.n.f[n6], n4 + this.n.f[n6 + 4], this.n.f[n6 + 8], 0, this.n.f[n6 + 12], this.n.f[n6 + 16]);
            this.a(this.n.n[9], n7, n4 + 47, 0, 28, 27, 16);
            this.p();
            ++n6;
            this.e(200);
        }
        this.a(this.b, n7 - 3, n4 + 27);
        this.b = this.a(n7, n4 + 47, 87, 16);
        this.r = 0;
    }

    boolean c(boolean bl) {
        if (this.r >= 11) {
            return false;
        }
        int n2 = this.d - 64;
        int n3 = Math.min(this.j - 144, this.v - 72);
        int n4 = n2 + (bl ? 5 : 38);
        this.a(this.b, n4, n3 + 47);
        this.a(this.n.n[9], n4 + this.r * 6, n3 + 47, 0, 28, 27, 16);
        this.p();
        ++this.r;
        this.e(200);
        return true;
    }

    void b(boolean bl) {
        while (this.c(bl)) {
        }
        this.b = null;
    }

    void k(int n2) {
        if (this.n.d.h >= 70 && (this.e() & 3) == 0) {
            this.b(new n(this, (int)this.n.d.e, n2, 538 + this.e() % 3, -1));
        } else {
            this.a((int)this.n.d.e, n2);
        }
    }

    void a(int n2, int n3) {
        this.b(new n(this, n2, n3));
    }

    void a(Image image) {
        this.e.setColor(0);
        this.e.drawRect(this.d - 47, this.v - 32, 93, 43);
        this.e.setColor(0xFFFFFF);
        this.e.drawRect(this.d - 46, this.v - 31, 91, 41);
        this.e.drawImage(image, this.d - 45, this.v - 30, 20);
    }

    void c(int n2) {
        try {
            this.a(this.n.c("u" + n2));
        }
        catch (Exception exception) {
            // empty catch block
        }
    }

    void l(int n2) {
        int n3 = 983 + n2 * 7;
        p p2 = new p(this);
        p2.n = -this.n.f[n3];
        p2.c = -this.n.f[n3 + 1];
        p2.o = -this.n.f[n3 + 2];
        p2.i = this.n.f[n3 + 5];
        p2.d = this.n.f[n3 + 6];
        this.b(new ac(this, p2, 3, s.b[69 + n2] + "\uc815\ucc45"));
    }

    void j(int n2) {
        int n3 = 983 + n2 * 7;
        p p2 = new p(this);
        p2.f[this.n.f[n3 + 3]] = this.n.f[n3 + 4];
        p2.i = this.n.f[n3 + 5];
        p2.d = this.n.f[n3 + 6];
        this.n.f(-this.n.f[n3]);
        this.n.c(-this.n.f[n3 + 1]);
        this.n.n(-this.n.f[n3 + 2]);
        this.n.b(this.n.f[n3 + 3], this.n.f[n3 + 4]);
        this.n.m(this.n.f[n3 + 5]);
        this.n.i(this.n.f[n3 + 6]);
        this.n.d.y |= 1 << n2;
        this.n.d.a |= 0x400000;
        this.b(new ac(this, p2, 0, "\uc815\ucc45\uc2dc\ud589\uacb0\uacfc"));
    }

    void a(int n2) {
        int[] nArray = new int[this.k];
        boolean bl = true;
        this.e(0);
        while (bl) {
            bl = false;
            this.e.setColor(n2);
            for (int i = 0; i < nArray.length; ++i) {
                if (nArray[i] >= this.j) continue;
                int n3 = this.e() & 0x3F;
                this.e.drawLine(i, nArray[i], i, nArray[i] + n3);
                int n4 = i;
                nArray[n4] = nArray[n4] + n3;
                bl = true;
            }
            this.p();
            this.s();
        }
    }

    void l() {
        Image image = this.a(0, 0, this.k, this.j);
        e e2 = new e(this);
        this.e(0);
        for (int i = 0; i < 6; ++i) {
            this.a(image, 0, 0);
            e2.a(0, 0, this.k, this.j, i, 2);
            this.p();
            this.s();
        }
    }

    void q() {
        int n2 = this.n.d.A & 0x7F;
        int n3 = n2 > 22 ? 3 : this.n.f[1281 + n2 * 7 - 6];
        this.n.n(-30);
        this.n.b(n3, -3);
        this.n.i(-10);
        this.n.m(30);
        if (n2 > 22) {
            this.n.d.g[this.n.f[1901 + n2 - 23]] = (byte)(this.n.f() + 2);
            n3 = 557;
            n2 = 558;
        } else {
            n3 = 747;
            n2 = -1;
        }
        this.n();
        this.b(new n(this, (int)this.n.d.e, 557, n2, -1));
    }

    void d() {
        p p2 = new p(this);
        int n2 = this.n.d.A & 0x7F;
        n2 = n2 > 22 ? 3 : this.n.f[1281 + n2 * 7 - 6];
        p2.o = -30;
        p2.f[n2] = -3;
        p2.d = -10;
        p2.i = 30;
        this.n.n(-30);
        this.n.b(n2, -3);
        this.n.i(-10);
        this.n.m(30);
        this.n.d.A = 0;
        this.n();
        this.b(new ac(this, p2, 0, "\ub300\ud615\uc0ac\uace0"));
    }

    Image a(int n2, int n3, int n4, int n5, int n6, boolean bl) {
        Image image = null;
        Image image2 = this.n.n[0];
        if (bl) {
            image = this.a(n2, n3, n4, n5);
        }
        n6 = 72 + n6 * 18;
        this.e.setClip(n2 + 2, n3 + 2, n4 - 4, n5 - 4);
        for (int i = 2; i < n5; i += 18) {
            for (int j = 2; j < n4; j += 18) {
                this.a(image2, n2 + j, n3 + i, n6, 0, 18, 18);
            }
        }
        this.a(image2, n2 + 3, n3 + 3, 123, 10, 2, 2);
        this.a(image2, n2 + 3, n3 + n5 - 5, 123, 10, 2, 2);
        this.a(image2, n2 + n4 - 5, n3 + 3, 123, 10, 2, 2);
        this.a(image2, n2 + n4 - 5, n3 + n5 - 5, 123, 10, 2, 2);
        this.e.setClip(0, 0, this.k, this.j);
        this.e.setColor(0x242455);
        this.e.drawRect(n2, n3, n4 - 1, n5 - 1);
        this.e.setColor(37631);
        this.e.drawRect(n2 + 1, n3 + 1, n4 - 3, n5 - 3);
        this.e.setColor(2359551);
        this.e.drawLine(n2 + n4 - 2, n3 + 2, n2 + n4 - 2, n3 + n5 - 2);
        this.e.drawLine(n2 + 2, n3 + n5 - 2, n2 + n4 - 2, n3 + n5 - 2);
        return image;
    }

    void a(String string, int n2, int n3, int n4, int n5) {
        this.e.setColor(0);
        this.a(string, n2, n3 + 1, n4);
        this.e.setColor(n5);
        this.a(string, n2, n3, n4);
    }

    void g() {
    }
}
