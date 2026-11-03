/*
 * Decompiled with CFR 0.152.
 */
class a
extends aq {
    String o;
    String[] q;
    int p;
    int g;
    int z;
    int s;
    int w;
    int i;
    boolean c;
    boolean t;

    a(aq aq2, String string, d d2) {
        super(aq2);
        this.o = string;
        this.z = Math.min(this.k, 176);
        this.s = Math.min(this.j, 178);
        this.p = this.d - (this.z >> 1);
        this.g = this.v - (this.s >> 1);
        d2.a((this.z - 20) / this.n.e, false);
        this.q = new String[d2.d()];
        for (int i = 0; i < this.q.length; ++i) {
            this.q[i] = d2.f();
        }
    }

    void b() {
        this.a(this.p, this.g, this.z, this.s, 1, false);
        this.a(this.o, this.d, this.g + 12, 1, 0xFFFFFF);
        this.u();
        this.c(0, 0, 300);
    }

    void d(int n) {
        switch (n) {
            case 50: 
            case 52: 
            case 141: 
            case 142: {
                if (this.w > 0) {
                    --this.w;
                    break;
                }
                return;
            }
            case 54: 
            case 56: 
            case 145: 
            case 146: {
                if (this.t) {
                    ++this.w;
                    break;
                }
                return;
            }
            case 8: 
            case 53: 
            case 131: 
            case 148: {
                this.v();
                return;
            }
            default: {
                return;
            }
        }
        this.c = false;
        this.c(0, 0, 300);
    }

    void b(int n) {
        this.c();
    }

    void c() {
        this.a(this.p + 5, this.g + 30, this.z - 10, this.s - 35, 85);
        int n = this.g + 35;
        int n2 = n + this.s - 44;
        this.e.setClip(this.p + 7, this.g + 32, this.z - 14, this.s - 39);
        this.i = this.w;
        while (this.i < this.q.length && n < n2) {
            if (this.q[this.i].equals("---")) {
                this.e.setColor(0x8080FF);
                this.e.drawLine(this.p + 10, n + 2, this.p + this.z - 20, n + 2);
                n += 8;
            } else {
                this.e.setColor(0xFFFFFF);
                this.a(this.q[this.i], this.p + 10, n, 4);
                n += this.n.l + 4;
            }
            ++this.i;
        }
        this.t = this.i < this.q.length || (n -= 4) >= n2;
        this.e.setClip(0, 0, this.k, this.j);
        this.c ^= true;
        if (this.c) {
            if (this.w > 0) {
                this.a(this.n.n[0], this.p + this.z - 15, this.g + 31, 115, 24, 9, 6);
            }
            if (this.t) {
                this.a(this.n.n[0], this.p + this.z - 15, this.g + this.s - 12, 115, 30, 9, 6);
            }
        }
        this.p();
    }
}
