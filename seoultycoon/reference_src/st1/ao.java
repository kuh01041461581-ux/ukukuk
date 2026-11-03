/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  javax.microedition.io.Connector
 *  javax.microedition.io.HttpConnection
 */
import java.io.InputStream;
import javax.microedition.io.Connector;
import javax.microedition.io.HttpConnection;

class ao
extends Thread {
    HttpConnection b;
    InputStream f;
    String d;
    String g;
    byte[] c;
    boolean e;
    int a;

    ao(String string, int n) {
        this.d = string;
        this.c = new byte[n];
    }

    void a() {
        try {
            if (this.f != null) {
                this.f.close();
                this.f = null;
            }
            if (this.b != null) {
                this.b.close();
                this.b = null;
            }
        }
        catch (Exception exception) {
            // empty catch block
        }
        this.e = true;
    }

    public void run() {
        try {
            int n;
            this.b = (HttpConnection)Connector.open((String)this.d);
            this.f = this.b.openInputStream();
            this.a = 0;
            while (this.a < this.c.length && (n = this.f.read()) != -1) {
                this.c[this.a] = (byte)n;
                ++this.a;
            }
        }
        catch (Exception exception) {
            this.g = exception.toString();
        }
        this.a();
    }
}
