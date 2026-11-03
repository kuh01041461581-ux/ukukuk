/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class w
extends y {
    Image c;
    aq b;
    int a;
    int e;
    int f;
    int d;

    w(aq aq2, Image image, int n, int n2, int n3, int n4) {
        this.a(aq2, image, n, n2, n3, n4);
    }

    void a(aq aq2, Image image, int n, int n2, int n3, int n4) {
        this.b = aq2;
        this.c = image;
        this.a = n;
        this.e = n2;
        this.f = n3;
        this.d = n4;
    }

    void a(int n, int n2, int n3, int n4) {
        n = Math.abs(n);
        if (n4 != 0) {
            int n5 = 1;
            for (int i = n; i > 9; i /= 10) {
                ++n5;
            }
            n2 = n4 == 1 ? (n2 += (n5 * (this.f + 1) >> 1) + 1) : (n2 += n5 * (this.f + 1));
        } else {
            n2 += 2;
        }
        do {
            this.b.a(this.c, n2 -= this.f + 1, n3, this.a + n % 10 * this.f, this.e, this.f, this.d);
        } while ((n /= 10) > 0);
    }
}
