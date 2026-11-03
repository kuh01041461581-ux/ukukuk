/*
 * Decompiled with CFR 0.152.
 */
class p
extends y {
    aq a;
    int m;
    int l;
    int e;
    boolean[] h;
    int o;
    int n;
    int c;
    int g;
    int[] f;
    int b;
    int k;
    int i;
    int d;
    int p;
    String j;

    p(aq aq2) {
        this.a = aq2;
        this.f = new int[4];
        this.h = new boolean[7];
        this.e = this.a.j - 50;
        this.a();
    }

    void a() {
        this.o = 0;
        this.n = 0;
        this.c = 0;
        this.g = 0;
        for (int i = 0; i < 4; ++i) {
            this.f[i] = 0;
        }
        this.b = 0;
        this.k = 0;
        this.i = 0;
        this.d = 0;
    }

    int b() {
        int n;
        this.h[0] = this.j != null;
        this.h[1] = this.p != 0;
        this.h[2] = this.o != 0 || this.n != 0 || this.c != 0;
        this.h[3] = this.g != 0;
        this.h[4] = false;
        for (n = 0; n < 4; ++n) {
            if (this.f[n] == 0) continue;
            this.h[4] = true;
            break;
        }
        this.h[5] = this.b != 0 || this.k != 0;
        this.h[6] = this.i != 0 || this.d != 0;
        this.m = 0;
        this.l = 0;
        n = 0;
        for (int i = 0; i < this.h.length; ++i) {
            if (!this.h[i]) continue;
            this.m = i == 0 || i == 1 || i == 3 ? (this.m += 15) : (this.m += 29);
            if (n != 0) {
                this.m += 3;
            }
            if (this.m <= this.e || this.l == 0) {
                this.l = this.m;
            }
            n = 1;
        }
        return this.m;
    }

    void a(int n) {
        for (int i = 0; i < 4; ++i) {
            this.f[i] = n;
        }
    }
}
