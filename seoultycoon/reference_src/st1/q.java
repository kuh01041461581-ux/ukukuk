/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  com.skt.m.BackLight
 *  com.skt.m.Device
 *  com.skt.m.Graphics2D
 *  javax.microedition.lcdui.Graphics
 *  javax.microedition.lcdui.Image
 */
import com.skt.m.BackLight;
import com.skt.m.Device;
import com.skt.m.Graphics2D;
import java.util.Random;
import java.util.Stack;
import javax.microedition.lcdui.Graphics;
import javax.microedition.lcdui.Image;

class q
extends aq
implements Runnable {
    Random c;
    Stack t;
    int p;
    int[] q;
    int g;
    int w;
    int o;
    boolean B;
    boolean z;
    boolean i;
    int s;

    q(ak ak2) {
        ak2.a();
        this.f = this;
        this.a = this;
        long l = Runtime.getRuntime().totalMemory();
        boolean bl = this.z = l < 500000L;
        if (l < 1000000L) {
            Device.setColorMode((int)1);
        }
        Device.setKeyToneEnabled((boolean)false);
        Device.setBacklightEnabled((boolean)false);
        BackLight.on((int)0);
        this.k = this.getWidth();
        this.j = this.getHeight() + 16;
        this.d = this.k >> 1;
        this.v = this.j >> 1;
        this.t = new Stack();
        this.c = new Random();
        this.a(0L);
        this.q = new int[8];
        ak2.a();
        this.n = new s(this, ak2);
        boolean bl2 = this.i = ak2.b - ak2.c < 3000L;
        if (!this.z) {
            Device.enableRestoreLCD((boolean)true);
        }
    }

    void b() {
        this.k();
        new Thread(this).start();
    }

    public void run() {
        this.z();
        long l = 0L;
        long l2 = 0L;
        while (true) {
            if (this.s != 0) {
                this.c();
            }
            switch (this.a.u) {
                case 1: {
                    this.a.u = 2;
                    this.i();
                    this.a.b();
                    break;
                }
                case 3: {
                    this.a.u = 2;
                    this.i();
                    this.a.x();
                    break;
                }
                case 2: {
                    for (int i = 0; this.a.m && i < 4; ++i) {
                        if (this.a.h[i] <= 0L || this.a.h[i] >= System.currentTimeMillis()) continue;
                        this.a.h[i] = this.a.h[i + 4] > 0L ? System.currentTimeMillis() + this.a.h[i + 4] : 0L;
                        this.a.b(i);
                    }
                    if (!this.a.m || this.h()) break;
                    this.a.d(this.y());
                }
            }
            aq.h(0);
            if (this.B) {
                l2 = System.currentTimeMillis() + 1000L;
                this.B = false;
                continue;
            }
            if (l2 == 0L || l2 >= System.currentTimeMillis()) continue;
            this.n.b();
            l2 = 0L;
        }
    }

    public void keyPressed(int n) {
        switch (n) {
            case 194: {
                this.n.a(1);
                this.n();
                break;
            }
            case 195: {
                this.n.a(-1);
                this.n();
                break;
            }
            case 146: {
                this.o = 128;
                break;
            }
            case 141: {
                this.o = 129;
                break;
            }
            case 142: {
                this.o = 130;
                break;
            }
            case 145: {
                this.o = 131;
                break;
            }
            case 56: {
                this.o = 144;
                break;
            }
            case 50: {
                this.o = 145;
                break;
            }
            case 52: {
                this.o = 146;
                break;
            }
            case 54: {
                this.o = 147;
            }
        }
        this.m(n);
    }

    public void keyReleased(int n) {
        switch (n) {
            case 146: {
                n = 128;
                break;
            }
            case 141: {
                n = 129;
                break;
            }
            case 142: {
                n = 130;
                break;
            }
            case 145: {
                n = 131;
                break;
            }
            case 56: {
                n = 144;
                break;
            }
            case 50: {
                n = 145;
                break;
            }
            case 52: {
                n = 146;
                break;
            }
            case 54: {
                n = 147;
                break;
            }
            default: {
                return;
            }
        }
        if (n == this.o) {
            this.o = 0;
        }
    }

    synchronized int y() {
        if (this.w > 0) {
            int n = this.q[this.g++];
            this.g %= this.q.length;
            --this.w;
            return n;
        }
        return 0;
    }

    synchronized void m(int n) {
        if (this.w < this.q.length) {
            this.q[(this.g + this.w++) % this.q.length] = n;
        }
    }

    void z() {
        try {
            boolean bl = false;
            int n = 0;
            Image image = Image.createImage((int)100, (int)50);
            Graphics graphics = image.getGraphics();
            Graphics2D graphics2D = Graphics2D.getGraphics2D((Graphics)graphics);
            graphics.setColor(0xFFFFFF);
            graphics.fillRect(0, 0, 100, 50);
            graphics.setColor(0);
            graphics.drawString("\ucf00\uc775\uc0ac\ub791", 0, 0, 20);
            for (int i = 0; i < 50; ++i) {
                for (int j = 99; j >= 0; --j) {
                    if (graphics2D.getPixel(j, i) == 0) {
                        if (!bl) {
                            this.n.m = -i;
                            bl = true;
                        }
                        this.n.l = i + this.n.m + 1;
                        if (j > n) {
                            n = j;
                            continue;
                        }
                        j = 0;
                        continue;
                    }
                    if (j != 0 || !bl) continue;
                    i = 50;
                }
            }
            this.n.e = (n >> 3) + 1;
            this.n.c = (this.k - 10) / this.n.e;
        }
        catch (Exception exception) {
            this.n.m = 0;
            this.n.e = 6;
            this.n.l = 12;
            this.n.c = (this.k - 12) / this.n.e;
        }
        while (this.e == null) {
            aq.h(0);
        }
        this.l = Graphics2D.getGraphics2D((Graphics)this.e);
        this.a(new u(this));
        this.u();
    }

    void c() {
        this.a = null;
        this.i();
        switch (this.s) {
            case 1: {
                this.a(new c(this));
                break;
            }
            case 2: {
                this.a(new ap(this));
                break;
            }
            case 3: {
                this.a(new ab(this));
                break;
            }
            case 4: {
                this.a(new ar(this));
                break;
            }
            case 5: {
                this.a(new an(this));
            }
        }
        this.s = 0;
    }
}
