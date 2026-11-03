package com.seoultycoon;
import java.nio.charset.Charset;
/** 원본이 기본 문자셋(EUC-KR)을 가정하던 문자열 변환을 EUC-KR 로 고정. */
public final class Cp {
    static final Charset CS = Charset.forName("EUC-KR");
    public static String str(byte[] b) { return new String(b, CS); }
    public static String str(byte[] b, int o, int l) { return new String(b, o, l, CS); }
    public static byte[] bytes(String s) { return s.getBytes(CS); }
}
