/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  com.skt.m.AudioSystem
 *  com.xce.io.XFile
 *  javax.microedition.lcdui.Image
 */
import com.skt.m.AudioSystem;
import com.xce.io.XFile;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.InputStream;
import javax.microedition.lcdui.Image;

class s
extends y {
    static final String[] b = new String[]{"\uc11d\ucd0c\ud638\uc218", "\uc11d\ucd0c\ud638\uc218\uacf5\uc6d0", "\uc11d\ucd0c\uc2dc\ub9bd\uacf5\uc6d0", "\ud14c\ub9c8\ud30c\ud06c\ubd80\uc9c0", "\ub86f\ub514\uc6d4\ub4dc", "\ub3c4\uc11c\uad00\ubd80\uc9c0", "\uad6d\ub9bd\ub3c4\uc11c\uad00", "\uc804\uc790\ub3c4\uc11c\uad00", "\uc18c\ubc29\uc11c\ubd80\uc9c0", "\uc18c\ubc29\uc11c", "\ubcd1\uc6d0\ubd80\uc9c0", "\uac15\ub3d9\ubcd1\uc6d0", "\uac15\ub3d9\uc885\ud569\ubcd1\uc6d0", "\uc120\uc0ac\uc2dc\ub300\uc720\uc801\ud130", "\uc720\uc801\ubc15\ubb3c\uad00", "\uc232", "\uc0b0\ucc45\ub85c", "\ud734\uc591\ub9bc", "\ubbf8\uc220\uad00\ubd80\uc9c0", "\ubbf8\uc220\uad00", "\uacf5\ud56d\ubd80\uc9c0", "\uacf5\ud56d", "\uc740\ud589\ubd80\uc9c0", "\uc740\ud589", "\ud55c\uad6d\uc740\ud589", "\uc288\ud37c\ub9c8\ucf13\ubd80\uc9c0", "\uc288\ud37c\ub9c8\ucf13", "\ub300\ud559\uad50\ubd80\uc9c0", "\uc11c\uc6b8\ubb38\ud654\ub300\ud559", "\uc11c\uc6b8\uc885\ud569\ub300\ud559\uad50", "\uacbd\ucc30\uc11c\ubd80\uc9c0", "\uacbd\ucc30\uc11c", "\ube4c\ub529\ubd80\uc9c0", "\uc138\uc30d\ub465\uc774\ube4c\ub529", "65\ube4c\ub529", "\uc885\ud569\uc1fc\ud551\ubab0\ubd80\uc9c0", "\ucfe0\uc5d1\uc2a4\ubab0", "\ubc29\uc1a1\uad6d\ubd80\uc9c0", "\ubc29\uc1a1\uad6d", "\uc544\ud30c\ud2b8\ubd80\uc9c0", "\uac15\ub0a8\uc544\ud30c\ud2b8", "\ud558\uc774\ud3a0\ub9ac\uc2a4", "\ubc84\uc2a4\ud130\ubbf8\ub110\ubd80\uc9c0", "\uc2dc\uc678\ubc84\uc2a4\ud130\ubbf8\ub110", "\uace0\uc18d\ubc84\uc2a4\ud130\ubbf8\ub110", "\ud558\uc218\ucc98\ub9ac\uc7a5\ubd80\uc9c0", "\ud558\uc218\ucc98\ub9ac\uc7a5", "\ubd81\ud55c\uc0b0", "\ub4f1\uc0b0\ub85c", "\uadf9\uc7a5\ubd80\uc9c0", "\ub300\ud559\ub85c\uc18c\uadf9\uc7a5", "\uad6d\ub9bd\uadf9\uc7a5", "\ub3d9\ubb3c\uc6d0\ubd80\uc9c0", "\uc5b4\ub9b0\uc774\ub3d9\ubb3c\uc6d0", "\uc5b4\ub9b0\uc774\ub300\ub3d9\ubb3c\uc6d0", "\uc2dc\uc7a5\uc800\ud0dd\ubd80\uc9c0", "\uc2dc\uc7a5\uc800\ud0dd", "\uace0\uae09\uc2dc\uc7a5\uc800\ud0dd", "\uc11c\uc6b8\uc5ed", "\uc7ac\uac74\ucd95\uc11c\uc6b8\uc5ed", "\uace0\uc18d\ucca0\ub3c4\uc11c\uc6b8\uc5ed", "\uac15\ub3d9", "\uac15\uc11c", "\uac15\ub0a8", "\uac15\ubd81", "\uad50\ud1b5", "\uacbd\uc81c", "\ubb38\ud654", "\ud658\uacbd", "\ud559\uc0dd\ubb34\ub8cc\uad00\ub78c", "\ud558\uc218\ub3c4\uc815\ube44", "\ub3d9\ubb3c\ud559\ub300\ubc29\uc9c0", "\uc0b0\ubd88\uc608\ubc29", "\ub178\uc219\uc790\uc9c0\uc6d0", "\uc9c0\ud558\ucca0\uc548\uc804", "\ucca0\ub3c4\uc694\uae08\uc778\ud558", "\uc18c\ubc29\uc7a5\ube44\uac15\ud654", "\uc18c\ubc29\ud559\uad50\uc6b4\uc601", "\uc720\uc801\ubc1c\uad74", "\uace0\ubb38\uc11c\uc815\ub9ac", "\uc218\uc790\uc6d0\uad00\ub9ac", "\uc2dc\ubbfc\ubb34\ub8cc\uac80\uc9c4", "\uc548\uc804\uae30\uc900\uac15\ud654", "\ub85c\ub610", "\uc7a5\ud559\uc0dd\uc9c0\uc6d0", "\ubc14\ub78c\uae38\uc870\uc131", "\ubc29\ubc94\uac15\ud654", "\ubc94\uc8c4\uc18c\ud0d5", "\ub3c4\ub85c\uc815\ube44", "\uce35\uac04\uc18c\uc74c\uc81c\ud55c", "\uc77c\uc870\uad8c\ubcf4\uc7a5", "-", "\ub300\uc120\ucd9c\ub9c8", "\uc7ac\uc120\uc131\uacf5", "\ub300\ud559\uad50\uc218", "\uc288\ud37c\ub9c8\ucf13\uc0ac\uc7a5", "\uac00\ud310\ub300\uc6b4\uc601", "\uac74\ucd95\uac00", "\ub300\uae30\uc5c5 \uc784\uc6d0", "\uc0ac\ub9dd", "\uc8fc\ubbfc\uc18c\ud658", "\uad6c\uc18d"};
    int i;
    int h;
    byte j;
    aj k;
    aj d;
    int m;
    int l;
    int e;
    int c;
    byte[] f;
    Image[] n;
    byte[][] o;
    aq a;
    h g;

    s(aq aq2, ak ak2) {
        this.a = aq2;
        this.k = new aj();
        this.d = new aj();
        ak2.a();
        this.n = new Image[12];
        try {
            for (int i = 0; i < this.n.length; ++i) {
                this.n[i] = this.c("f" + i);
                ak2.a();
            }
        }
        catch (Exception exception) {
            aq.r();
        }
        ak2.a();
        this.a(ak2);
        this.c();
        ak2.a();
    }

    void h() {
        try {
            int n = Math.max(1, AudioSystem.getMaxVolume((String)"mmf") / 10);
            this.h = AudioSystem.getVolume((String)"mmf") / n * n;
        }
        catch (Exception exception) {
            this.h = 5;
        }
        this.j = (byte)7;
        this.d();
        this.a((aj)null);
    }

    void c() {
        try {
            if (XFile.exists((String)"cc")) {
                byte[] byArray = new byte[XFile.filesize((String)"cc")];
                XFile xFile = new XFile("cc", 1);
                xFile.read(byArray, 0, byArray.length);
                xFile.close();
                byArray = s.a(byArray);
                if (byArray == null) {
                    this.h();
                } else {
                    DataInputStream dataInputStream = new DataInputStream(new ByteArrayInputStream(byArray));
                    if (dataInputStream.readByte() != 24) {
                        this.h();
                    } else {
                        this.i = dataInputStream.readShort();
                        this.h = dataInputStream.readUnsignedByte();
                        this.j = dataInputStream.readByte();
                        this.k.a(dataInputStream);
                        this.d.a(this.k);
                    }
                    dataInputStream.close();
                }
            } else {
                this.h();
            }
            AudioSystem.setVolume((String)"mmf", (int)this.h);
        }
        catch (Exception exception) {
            aq.r();
        }
        this.a();
    }

    void j() {
        try {
            ByteArrayOutputStream byteArrayOutputStream = new ByteArrayOutputStream();
            DataOutputStream dataOutputStream = new DataOutputStream(byteArrayOutputStream);
            dataOutputStream.writeByte(24);
            dataOutputStream.writeShort(this.i);
            dataOutputStream.writeByte(this.h);
            dataOutputStream.writeByte(this.j);
            this.k.a(dataOutputStream);
            byte[] byArray = byteArrayOutputStream.toByteArray();
            byArray = s.c(byArray);
            dataOutputStream.close();
            XFile xFile = new XFile("cc", 2);
            xFile.write(byArray, 0, byArray.length);
            xFile.close();
        }
        catch (Exception exception) {
            // empty catch block
        }
    }

    void b() {
        this.k.a(this.d);
        this.j();
    }

    void a(ak ak2) {
        ak2.a();
        try {
            DataInputStream dataInputStream = new DataInputStream(this.b("mx"));
            ak2.a();
            this.f = new byte[dataInputStream.readUnsignedShort()];
            dataInputStream.read(this.f, 0, this.f.length);
            dataInputStream.close();
            ak2.a();
            this.o = new byte[0][];
            for (int i = 0; i < this.o.length; ++i) {
                this.o[i] = this.a("sf" + i);
            }
            ak2.a();
        }
        catch (Exception exception) {
            aq.r();
        }
    }

    void a() {
        this.b();
    }

    void a(aj aj2) {
        this.d.n = 15936085;
        if (aj2 != null) {
            this.d.M = aj2.M;
            this.d.s = aj2.s;
            this.d.F = aj2.F;
            this.d.p = aj2.p;
            this.d.l = aj2.l;
            this.d.U = aj2.U;
            this.d.S = aj2.S;
            this.d.w = aj2.w;
            this.d.W = aj2.W;
        }
        this.d.q = (byte)50;
        for (int i = 0; i < 4; ++i) {
            this.d.P[i] = 50;
        }
        this.d.R = 10;
        this.d.B = 10;
        if (this.d.w == 0) {
            this.d.C = 180;
        } else if (this.d.w < 6) {
            this.d.C = (7 - this.d.w) * 50 - 20;
        } else if (this.d.w < 8) {
            this.d.C = 180;
        }
    }

    Image c(String string) throws Exception {
        return Image.createImage((String)(string + ".d"));
    }

    InputStream b(String string) {
        return this.getClass().getResourceAsStream(string + ".d");
    }

    byte[] a(String string) {
        try {
            InputStream inputStream = this.b(string);
            byte[] byArray = new byte[inputStream.available()];
            inputStream.read(byArray);
            inputStream.close();
            return byArray;
        }
        catch (Exception exception) {
            aq.r();
            return null;
        }
    }

    d g(int n) {
        return new d(this, n);
    }

    d a(int n, boolean bl) {
        d d2 = new d(this, n);
        if (bl && d2 != null) {
            d2.a(this.c, true);
        }
        return d2;
    }

    void a(int n) {
        try {
            int n2 = AudioSystem.getMaxVolume((String)"mmf");
            this.h = Math.max(Math.min(n2, this.h + n * n2 / Math.min(10, n2)), 0);
            AudioSystem.setVolume((String)"mmf", (int)this.h);
        }
        catch (Exception exception) {
            // empty catch block
        }
    }

    void d() {
        this.i = 150;
        if (this.a.a.h[4] > 0L) {
            this.a.a.h[4] = this.i;
        }
    }

    static byte[] c(byte[] byArray) {
        int n;
        byte[] byArray2 = new byte[byArray.length + 1];
        int n2 = 1;
        byArray2[0] = 85;
        for (n = 0; n < byArray.length; ++n) {
            byArray2[0] = (byte)(byArray2[0] + byArray[n]);
        }
        for (n = 0; n < byArray.length; ++n) {
            byArray2[n2] = (byte)(byArray[n] ^ byArray2[n2 - 1]);
            int n3 = n2;
            byArray2[n3] = (byte)(byArray2[n3] + 85);
            byArray2[n2] = (byte)(byArray2[n2] << 1 | (byArray2[n2] & 0xFF) >> 7);
            ++n2;
        }
        return byArray2;
    }

    static byte[] a(byte[] byArray) {
        int n;
        byte[] byArray2 = new byte[byArray.length - 1];
        int n2 = 0;
        for (n = 1; n < byArray.length; ++n) {
            byArray2[n2] = (byte)((byArray[n] & 0xFF) >> 1 | byArray[n] << 7);
            int n3 = n2;
            byArray2[n3] = (byte)(byArray2[n3] - 85);
            int n4 = n2++;
            byArray2[n4] = (byte)(byArray2[n4] ^ byArray[n - 1]);
        }
        n = byArray[0];
        for (int i = 0; i < byArray2.length; ++i) {
            n = (byte)(n - byArray2[i]);
        }
        if (n == 85) {
            return byArray2;
        }
        return null;
    }

    static String b(byte[] byArray) {
        String string = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        String string2 = "";
        int n = 0;
        int n2 = 6;
        while (n <= byArray.length) {
            int n3;
            if (n2 < 0) {
                n3 = (byArray[n - 1] & (1 << -n2) - 1) << 6 + n2;
                n2 += 6;
            } else {
                n3 = 0;
            }
            if (n < byArray.length) {
                n3 |= byArray[n] >> 8 - n2 & (1 << n2) - 1;
            }
            if (n2 == 0) {
                n2 = 6;
            } else {
                n2 -= 8;
                ++n;
            }
            string2 = string2 + string.charAt(n3);
        }
        return string2;
    }

    int f() {
        return this.d.I * 48 + this.d.u * 4 + this.d.d;
    }

    void i() {
        this.d.d = (byte)(this.d.d + 1);
        if (this.d.d > 3) {
            this.d.v[this.d.I * 12 + this.d.u] = this.d.q;
            this.d.d = 0;
            this.d.u = (byte)(this.d.u + 1);
            if (this.d.u > 11) {
                this.d.u = 0;
                this.d.I = (byte)(this.d.I + 1);
            }
        }
        this.d.j = (byte)(this.d.j << 2);
    }

    int a(int n, int n2) {
        if (this.d.o[n][n2] == 0) {
            return 357 + n * 60 + n2 * 10;
        }
        return 597 + n * 30 + this.f[727 + n * 6 + n2] * 10;
    }

    String a(int n, int n2, int n3) {
        return b[n * 15 + this.f[232 + n * 6 + n2] + n3];
    }

    boolean b(int n) {
        return (this.d.O & 1 << n) != 0;
    }

    boolean k(int n) {
        return (this.d.y & 1 << n) != 0;
    }

    boolean h(int n) {
        return (this.d.b & 1 << n) != 0;
    }

    boolean l(int n) {
        return (this.d.x & 1 << n + 8) != 0;
    }

    boolean e(int n) {
        return this.f[n] <= this.d.B && this.f[n + 1] <= this.d.R && this.f[n + 2] <= this.d.C;
    }

    boolean d(int n) {
        if (!this.h(n)) {
            return true;
        }
        return (this.d.f >> (n << 1) & 3) >= this.d.I;
    }

    void m(int n) {
        this.d.h = (byte)Math.min(Math.max(this.d.h + n, 0), 100);
    }

    void i(int n) {
        this.d.q = (byte)Math.min(Math.max(this.d.q + n, 0), 100);
        this.d.L = (byte)(n <= 0 ? 1 : 0);
    }

    void b(int n, int n2) {
        this.d.r[n] = (byte)Math.min(Math.max(this.d.r[n] + n2, 0), 100);
    }

    void n(int n) {
        this.d.C = Math.max(this.d.C + n, 0);
    }

    void f(int n) {
        this.d.B = Math.max(this.d.B + n, 0);
    }

    void c(int n) {
        this.d.R = Math.max(this.d.R + n, 0);
    }

    void j(int n) {
        this.d.b = (short)(this.d.b | 33 << n);
        this.d.e = (byte)(n + 1);
        this.d.F = (short)(this.d.F | 1 << n + 1);
        this.d.f = (short)(this.d.f ^ this.d.f & 3 << (n <<= 1));
        this.d.f = (short)(this.d.f | this.d.I << n);
    }

    boolean g() {
        return (this.d.a & 0x2000000) == 0;
    }
}
