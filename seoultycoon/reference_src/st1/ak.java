/*
 * Decompiled with CFR 0.152.
 * 
 * Could not load the following classes:
 *  com.skt.m.ProgressBar
 */
import com.skt.m.ProgressBar;

class ak
extends y {
    long c = System.currentTimeMillis();
    long b;
    ProgressBar a;

    ak(String string, int n) {
        this.a = new ProgressBar(string);
        this.a.setMaxValue(n);
        this.a.setValue(1);
    }

    void a() {
        this.a.setValue(this.a.getValue() + 1);
        this.b = System.currentTimeMillis();
    }
}
