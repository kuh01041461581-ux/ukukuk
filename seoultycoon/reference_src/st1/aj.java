/*
 * Decompiled with CFR 0.152.
 */
import java.io.DataInputStream;
import java.io.DataOutputStream;

class aj
extends y {
    byte I;
    byte u;
    byte d;
    short[] P = new short[4];
    byte[] z = new byte[4];
    int C;
    int B;
    int R;
    byte h;
    byte q;
    byte L;
    byte[] v = new byte[48];
    byte[] r = new byte[4];
    short N;
    byte[][] o = new byte[4][];
    short[] H;
    byte e;
    short b;
    int a;
    byte G;
    byte[] Q;
    int y;
    int c;
    int O;
    byte A;
    int D;
    int i;
    byte[] g;
    int t;
    int E;
    int K;
    int x;
    byte j;
    byte T;
    short f;
    byte[] Y;
    String V;
    int m;
    byte M;
    int s;
    short F;
    int p;
    int l;
    short U;
    int n;
    int S;
    byte w;
    int W;
    byte X;
    int J = 0;
    int k;

    aj() {
        while (this.J < 4) {
            this.o[this.J] = new byte[6];
            ++this.J;
        }
        this.H = new short[4];
        this.Q = new byte[4];
        this.g = new byte[4];
        this.Y = new byte[5];
        this.V = "???";
    }

    void a(aj aj2) {
        this.I = aj2.I;
        this.u = aj2.u;
        this.d = aj2.d;
        this.J = 0;
        while (this.J < this.P.length) {
            this.P[this.J] = aj2.P[this.J];
            ++this.J;
        }
        this.J = 0;
        while (this.J < this.z.length) {
            this.z[this.J] = aj2.z[this.J];
            ++this.J;
        }
        this.C = aj2.C;
        this.B = aj2.B;
        this.R = aj2.R;
        this.h = aj2.h;
        this.q = aj2.q;
        this.L = aj2.L;
        this.J = 0;
        while (this.J < this.v.length) {
            this.v[this.J] = aj2.v[this.J];
            ++this.J;
        }
        this.J = 0;
        while (this.J < this.r.length) {
            this.r[this.J] = aj2.r[this.J];
            ++this.J;
        }
        this.N = aj2.N;
        this.J = 0;
        while (this.J < this.o.length) {
            this.k = 0;
            while (this.k < this.o[this.J].length) {
                this.o[this.J][this.k] = aj2.o[this.J][this.k];
                ++this.k;
            }
            ++this.J;
        }
        this.J = 0;
        while (this.J < this.H.length) {
            this.H[this.J] = aj2.H[this.J];
            ++this.J;
        }
        this.e = aj2.e;
        this.b = aj2.b;
        this.a = aj2.a;
        this.G = aj2.G;
        this.J = 0;
        while (this.J < this.Q.length) {
            this.Q[this.J] = aj2.Q[this.J];
            ++this.J;
        }
        this.y = aj2.y;
        this.c = aj2.c;
        this.O = aj2.O;
        this.A = aj2.A;
        this.D = aj2.D;
        this.i = aj2.i;
        this.J = 0;
        while (this.J < this.g.length) {
            this.g[this.J] = aj2.g[this.J];
            ++this.J;
        }
        this.t = aj2.t;
        this.E = aj2.E;
        this.K = aj2.K;
        this.x = aj2.x;
        this.j = aj2.j;
        this.T = aj2.T;
        this.f = aj2.f;
        this.J = 0;
        while (this.J < this.Y.length) {
            this.Y[this.J] = aj2.Y[this.J];
            ++this.J;
        }
        this.V = aj2.V;
        this.m = aj2.m;
        this.M = aj2.M;
        this.s = aj2.s;
        this.n = aj2.n;
        this.F = aj2.F;
        this.p = aj2.p;
        this.l = aj2.l;
        this.U = aj2.U;
        this.w = aj2.w;
        this.W = aj2.W;
        this.S = aj2.S;
    }

    void a(DataInputStream dataInputStream) {
        try {
            this.I = dataInputStream.readByte();
            this.u = dataInputStream.readByte();
            this.d = dataInputStream.readByte();
            this.J = 0;
            while (this.J < this.P.length) {
                this.P[this.J] = dataInputStream.readShort();
                ++this.J;
            }
            this.J = 0;
            while (this.J < this.z.length) {
                this.z[this.J] = dataInputStream.readByte();
                ++this.J;
            }
            this.C = dataInputStream.readInt();
            this.B = dataInputStream.readInt();
            this.R = dataInputStream.readInt();
            this.h = dataInputStream.readByte();
            this.q = dataInputStream.readByte();
            this.L = dataInputStream.readByte();
            this.J = 0;
            while (this.J < this.v.length) {
                this.v[this.J] = dataInputStream.readByte();
                ++this.J;
            }
            this.J = 0;
            while (this.J < this.r.length) {
                this.r[this.J] = dataInputStream.readByte();
                ++this.J;
            }
            this.N = dataInputStream.readShort();
            this.J = 0;
            while (this.J < this.o.length) {
                this.k = 0;
                while (this.k < this.o[this.J].length) {
                    this.o[this.J][this.k] = dataInputStream.readByte();
                    ++this.k;
                }
                ++this.J;
            }
            this.J = 0;
            while (this.J < this.H.length) {
                this.H[this.J] = dataInputStream.readShort();
                ++this.J;
            }
            this.e = dataInputStream.readByte();
            this.b = dataInputStream.readShort();
            this.a = dataInputStream.readInt();
            this.G = dataInputStream.readByte();
            this.J = 0;
            while (this.J < this.Q.length) {
                this.Q[this.J] = dataInputStream.readByte();
                ++this.J;
            }
            this.y = dataInputStream.readInt();
            this.c = dataInputStream.readInt();
            this.O = dataInputStream.readInt();
            this.A = dataInputStream.readByte();
            this.D = dataInputStream.readInt();
            this.i = dataInputStream.readInt();
            this.J = 0;
            while (this.J < this.g.length) {
                this.g[this.J] = dataInputStream.readByte();
                ++this.J;
            }
            this.t = dataInputStream.readInt();
            this.E = dataInputStream.readInt();
            this.K = dataInputStream.readInt();
            this.x = dataInputStream.readInt();
            this.j = dataInputStream.readByte();
            this.T = dataInputStream.readByte();
            this.f = dataInputStream.readShort();
            this.J = 0;
            while (this.J < this.Y.length) {
                this.Y[this.J] = dataInputStream.readByte();
                ++this.J;
            }
            byte[] byArray = new byte[dataInputStream.readByte()];
            dataInputStream.read(byArray, 0, byArray.length);
            this.V = new String(byArray, 0, byArray.length);
            this.m = dataInputStream.readInt();
            this.M = dataInputStream.readByte();
            this.s = dataInputStream.readInt();
            this.n = dataInputStream.readInt();
            this.F = dataInputStream.readShort();
            this.p = dataInputStream.readInt();
            this.l = dataInputStream.readInt();
            this.U = dataInputStream.readShort();
            this.w = dataInputStream.readByte();
            this.W = dataInputStream.readInt();
            this.S = dataInputStream.readInt();
        }
        catch (Exception exception) {
            // empty catch block
        }
    }

    void a(DataOutputStream dataOutputStream) {
        try {
            dataOutputStream.writeByte(this.I);
            dataOutputStream.writeByte(this.u);
            dataOutputStream.writeByte(this.d);
            this.J = 0;
            while (this.J < this.P.length) {
                dataOutputStream.writeShort(this.P[this.J]);
                ++this.J;
            }
            this.J = 0;
            while (this.J < this.z.length) {
                dataOutputStream.writeByte(this.z[this.J]);
                ++this.J;
            }
            dataOutputStream.writeInt(this.C);
            dataOutputStream.writeInt(this.B);
            dataOutputStream.writeInt(this.R);
            dataOutputStream.writeByte(this.h);
            dataOutputStream.writeByte(this.q);
            dataOutputStream.writeByte(this.L);
            this.J = 0;
            while (this.J < this.v.length) {
                dataOutputStream.writeByte(this.v[this.J]);
                ++this.J;
            }
            this.J = 0;
            while (this.J < this.r.length) {
                dataOutputStream.writeByte(this.r[this.J]);
                ++this.J;
            }
            dataOutputStream.writeShort(this.N);
            this.J = 0;
            while (this.J < this.o.length) {
                this.k = 0;
                while (this.k < this.o[this.J].length) {
                    dataOutputStream.writeByte(this.o[this.J][this.k]);
                    ++this.k;
                }
                ++this.J;
            }
            this.J = 0;
            while (this.J < this.H.length) {
                dataOutputStream.writeShort(this.H[this.J]);
                ++this.J;
            }
            dataOutputStream.writeByte(this.e);
            dataOutputStream.writeShort(this.b);
            dataOutputStream.writeInt(this.a);
            dataOutputStream.writeByte(this.G);
            this.J = 0;
            while (this.J < this.Q.length) {
                dataOutputStream.writeByte(this.Q[this.J]);
                ++this.J;
            }
            dataOutputStream.writeInt(this.y);
            dataOutputStream.writeInt(this.c);
            dataOutputStream.writeInt(this.O);
            dataOutputStream.writeByte(this.A);
            dataOutputStream.writeInt(this.D);
            dataOutputStream.writeInt(this.i);
            this.J = 0;
            while (this.J < this.g.length) {
                dataOutputStream.writeByte(this.g[this.J]);
                ++this.J;
            }
            dataOutputStream.writeInt(this.t);
            dataOutputStream.writeInt(this.E);
            dataOutputStream.writeInt(this.K);
            dataOutputStream.writeInt(this.x);
            dataOutputStream.writeByte(this.j);
            dataOutputStream.writeByte(this.T);
            dataOutputStream.writeShort(this.f);
            this.J = 0;
            while (this.J < this.Y.length) {
                dataOutputStream.writeByte(this.Y[this.J]);
                ++this.J;
            }
            byte[] byArray = this.V.getBytes();
            dataOutputStream.writeByte(byArray.length);
            dataOutputStream.write(byArray, 0, byArray.length);
            dataOutputStream.writeInt(this.m);
            dataOutputStream.writeByte(this.M);
            dataOutputStream.writeInt(this.s);
            dataOutputStream.writeInt(this.n);
            dataOutputStream.writeShort(this.F);
            dataOutputStream.writeInt(this.p);
            dataOutputStream.writeInt(this.l);
            dataOutputStream.writeShort(this.U);
            dataOutputStream.writeByte(this.w);
            dataOutputStream.writeInt(this.W);
            dataOutputStream.writeInt(this.S);
        }
        catch (Exception exception) {
            // empty catch block
        }
    }
}
