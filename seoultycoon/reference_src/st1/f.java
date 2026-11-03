/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.lcdui.Image
 */
import javax.microedition.lcdui.Image;

class f
extends y {
    Image d;
    int a;
    int c;
    int b;
    int f;
    int g;
    int e;

    f() {
    }

    void a(Image image, int n, int n2, int n3, int n4, int n5, int n6) {
        this.d = image;
        this.a = n;
        this.c = n2;
        this.b = n3;
        this.f = n4;
        this.g = n5;
        this.e = n6;
    }

    void a(aq aq2) {
        aq2.a(this.d, this.a, this.c, this.b, this.f, this.g, this.e);
    }

    static void a(f[] fArray, int n, aq aq2) {
        int n2;
        for (n2 = 0; n2 < n; ++n2) {
            for (int i = n2 + 1; i < n; ++i) {
                if (fArray[n2].c <= fArray[i].c) continue;
                f f2 = fArray[n2];
                fArray[n2] = fArray[i];
                fArray[i] = f2;
            }
        }
        for (n2 = 0; n2 < n; ++n2) {
            fArray[n2].a(aq2);
        }
    }
}
