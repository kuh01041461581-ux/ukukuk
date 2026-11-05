package com.seoultycoon;
import java.nio.charset.Charset;
/** 원본이 기본 문자셋(EUC-KR)을 가정하던 문자열 변환을 EUC-KR 로 고정. */
public final class Cp {
    static final Charset CS = Charset.forName("EUC-KR");
    public static String str(byte[] b) { return new String(b, CS); }
    public static String str(byte[] b, int o, int l) { return new String(b, o, l, CS); }
    public static byte[] bytes(String s) { return s.getBytes(CS); }
    // ---- 게임 속도(원본 타이밍 배율): 원본의 System.currentTimeMillis / Thread.sleep 호출을 이쪽으로 바꿔 시간을 k배로 흘린다
    static volatile double scale = 1.0; static long realBase = System.currentTimeMillis(), virtBase = realBase;
    public static synchronized void setScale(double k) { long v = now(); realBase = System.currentTimeMillis(); virtBase = v; scale = k; }
    public static long now() { return virtBase + (long) ((System.currentTimeMillis() - realBase) * scale); }
    public static void sleep(long ms) throws InterruptedException { Thread.sleep(ms > 0 ? Math.max(1L, (long) (ms / scale)) : 0L); }
}
