/*
 * Decompiled with CFR 0.152.
 */
import java.io.InputStream;

class d
extends y {
    byte[] c;
    int e;
    int f;
    int a;
    int b;
    int i;
    int g;
    boolean d;
    boolean h;

    d(s s2, int n) {
        try {
            InputStream inputStream = s2.b("m" + (n >> 5));
            this.c = new byte[32];
            inputStream.read(this.c, 0, 32);
            int n2 = n & 0x1F;
            int n3 = 0;
            for (int i = 0; i < n2; ++i) {
                n3 += this.c[i] & 0xFF;
            }
            this.e = this.c[n2] & 0xFF;
            inputStream.skip(n3);
            this.c = new byte[this.e];
            inputStream.read(this.c, 0, this.e);
            inputStream.close();
        }
        catch (Exception exception) {
            aq.r();
        }
    }

    d(byte[] byArray, int n) {
        this.c = byArray;
        this.e = n;
    }

    String f() {
        int n;
        this.g();
        if (!this.b()) {
            return null;
        }
        int n2 = this.f - 1;
        for (n = this.a; n > 0 && (this.c[n2 + n] == 32 || this.c[n2 + n] == 124); --n) {
        }
        ++this.g;
        return d.a(new String(this.c, this.f, n), "_", " ");
    }

    boolean b() {
        byte by;
        this.f += this.a;
        while (this.f < this.e && this.c[this.f] == 32) {
            ++this.f;
        }
        this.a = 0;
        int n = this.f;
        int n2 = Math.min(this.e - this.f, this.b);
        while (this.a < n2) {
            by = this.c[n + this.a];
            if ((by & 0x80) != 0) {
                this.a += 2;
                continue;
            }
            ++this.a;
            if (by != 124) continue;
            return true;
        }
        if (this.a > this.b) {
            this.a -= 2;
        }
        if (this.f + this.a < this.e) {
            by = this.c[n + this.a];
            block0 : switch (by) {
                case 33: 
                case 44: 
                case 46: 
                case 63: {
                    if (this.f + this.a + 1 < this.e) {
                        switch (this.c[n + this.a + 1]) {
                            case 33: 
                            case 44: 
                            case 46: 
                            case 63: {
                                break block0;
                            }
                            case 32: 
                            case 124: {
                                ++this.a;
                            }
                        }
                    }
                    ++this.a;
                    break;
                }
                case 124: {
                    ++this.a;
                    break;
                }
                default: {
                    if (!this.h) break;
                    int n3 = this.a;
                    while (this.a > 1 && this.c[n + this.a] != 32) {
                        --this.a;
                    }
                    if (this.a >= 4) break;
                    this.a = n3;
                }
            }
        }
        return this.a > 0;
    }

    void a() {
        this.h();
        this.i = 0;
        while (this.b()) {
            ++this.i;
        }
        this.h();
    }

    int d() {
        this.g();
        return this.i;
    }

    int c() {
        this.g();
        return this.i - this.g;
    }

    void g() {
        if (!this.d) {
            this.a(18, true);
        }
    }

    void h() {
        this.f = 0;
        this.a = 0;
        this.g = 0;
    }

    void a(int n, boolean bl) {
        this.b = n;
        this.d = true;
        this.h = bl;
        this.a();
    }

    static String a(String string, String string2, String string3) {
        int n;
        while ((n = string.indexOf(string2)) != -1) {
            string = string.substring(0, n) + string3 + string.substring(n + string2.length());
        }
        return string;
    }

    static String a(String string, String string2) {
        if (!string.startsWith(string2)) {
            string = "  " + string;
        }
        return string;
    }
}
