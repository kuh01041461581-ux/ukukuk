/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  com.skt.m.AudioClip
 *  com.skt.m.AudioSystem
 */
import com.skt.m.AudioClip;
import com.skt.m.AudioSystem;

class h
extends Thread {
    byte[] d;
    boolean b;
    boolean a;
    AudioClip c;

    static boolean b() {
        return false;
    }

    h(byte[] byArray, boolean bl) {
        try {
            this.c = AudioSystem.getAudioClip((String)"mmf");
        }
        catch (Exception exception) {
            // empty catch block
        }
        this.d = byArray;
        this.b = bl;
    }

    void a() {
        if (this.c == null) {
            return;
        }
        try {
            if (this.a) {
                this.c.stop();
            }
        }
        catch (Exception exception) {
            // empty catch block
        }
    }

    public void run() {
        if (this.c == null) {
            return;
        }
        this.a = true;
        try {
            this.c.open(this.d, 0, this.d.length);
            if (this.b) {
                this.c.loop();
            } else {
                this.c.play();
            }
        }
        catch (Exception exception) {
            // empty catch block
        }
        try {
            this.d = null;
            this.c.close();
        }
        catch (Exception exception) {
            // empty catch block
        }
        this.a = false;
    }
}
