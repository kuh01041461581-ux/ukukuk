/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  com.skt.m.Graphics2D
 *  javax.microedition.lcdui.Graphics
 *  javax.microedition.lcdui.Image
 */
import com.skt.m.Graphics2D;
import javax.microedition.lcdui.Graphics;
import javax.microedition.lcdui.Image;

class e
extends y {
    static final short[] d = new short[]{0, 42, 85, 170, 218, 255};
    Image a;
    Graphics c;
    Graphics2D b;

    e(aq aq2) {
        try {
            this.a = Image.createImage((int)64, (int)64);
            this.c = this.a.getGraphics();
            this.b = aq2.l;
        }
        catch (Exception exception) {
            // empty catch block
        }
    }

    void a(int n, int n2, int n3, int n4, int n5, int n6) {
        if (this.a == null) {
            return;
        }
        this.c.setColor((int)d[n5], (int)d[n5], (int)d[n5]);
        this.c.fillRect(0, 0, 64, 64);
        for (int i = 0; i < n4; i += 64) {
            for (int j = 0; j < n3; j += 64) {
                this.b.drawImage(n + j, n2 + i, this.a, 0, 0, Math.min(n3 - j, 64), Math.min(n4 - i, 64), n6);
            }
        }
    }
}
