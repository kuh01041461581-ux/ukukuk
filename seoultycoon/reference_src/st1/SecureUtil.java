/*
 * Decompiled with CFR 0.152.
 */
public class SecureUtil {
    int[] b;
    byte[] d;
    long a;
    int[] c = new int[4];

    public SecureUtil() {
        this.c[0] = 1732584193;
        this.c[1] = -271733879;
        this.c[2] = -1732584194;
        this.c[3] = 271733878;
        this.a = 0L;
        this.d = new byte[64];
        this.b = new int[16];
    }
}
