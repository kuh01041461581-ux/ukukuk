/*
 * Decompiled with CFR 0.152.
 */
class k
extends aq {
    ao c;

    k(aq aq2) {
        super(aq2);
    }

    void b() {
        String string = new String(s.a(this.n.a("t" + this.n.f[892])));
        String string2 = 1046 + "&" + k.c() + "&" + this.n.d.V + "&" + this.n.d.s;
        this.c = new ao(string + s.b(s.c(string2.getBytes())), 1024);
        this.c.start();
        this.a(this.d - 50, this.v - 30, 100, 60, 0, false);
        this.a("\uc811\uc18d\uc911\uc785\ub2c8\ub2e4", this.d, this.v - 25, 1, 0xFFFFFF);
        this.a("[\ucde8\uc18c]\ub3cc\uc544\uac00\uae30", this.d, this.v + 25 - this.n.l, 1, 0xFFFFFF);
        this.e(0);
        for (int i = 0; i < 16 || !this.c.e; ++i) {
            if (!this.h()) {
                if (this.f.y() == 8) {
                    this.o();
                    this.c.a();
                    this.v();
                    return;
                }
                this.o();
            }
            int n = 255 - ((i & 7) << 5);
            for (int j = 0; j < 8; ++j) {
                this.e.setColor(n << 16 | n << 8 | 255 - n);
                this.e.fillRect(this.d - 40 + j * 10, this.v - 5, 9, 10);
                n = n + 32 & 0xFF;
            }
            this.p();
            this.s();
        }
        this.u();
        if (this.c.g == null && this.c.a > 0) {
            this.b(new a(this, "\ub7ad\ud0b9\ud655\uc778", new d(this.c.c, this.c.a)));
            return;
        }
        this.a(this.d - 50, this.v - 30, 100, 60, 0, false);
        this.a("\uc7a0\uc2dc \ud6c4 \ub2e4\uc2dc", this.d, this.v - 2 - this.n.l, 1, 0xFFFFFF);
        this.a("\uc2dc\ub3c4\ud574 \uc8fc\uc138\uc694", this.d, this.v + 2, 1, 0xFFFFFF);
        this.p();
    }

    void d(int n) {
        switch (n) {
            case 8: 
            case 53: 
            case 131: 
            case 148: {
                this.v();
                return;
            }
        }
    }

    void x() {
        this.v();
    }

    static String c() {
        String string;
        String string2 = System.getProperty("m.CARRIER").toUpperCase();
        string = System.getProperty("m.VENDER").toUpperCase().equals("LG") && System.getProperty("m.MODEL").equals("56") ? ((string = System.getProperty("MIN")).charAt(0) == '0' ? string.substring(3) : string.substring(2)) : System.getProperty("m.MIN");
        string = string2.equals("SKT") ? "011" + string : (string2.equals("STI") ? "017" + string : (string2.equals("KTF") ? "016" + string : (string2.equals("HSP") ? "018" + string : (string2.equals("LGT") ? "019" + string : string2 + string))));
        return string;
    }
}
