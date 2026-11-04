package com.seoultycoon.st2;

/** 플랫폼(안드로이드/JVM) 의존 기능: 리소스, PNG 디코드, 글자 래스터, 저장소 */
public interface Platform {
    /** 게임 리소스(jar 내용: "lx.d", "25.m" ...) 바이트. 없으면 null */
    byte[] resource(String name);
    /** PNG 디코드 → ARGB 픽셀. out[0]=w, out[1]=h */
    int[] decodePng(byte[] data, int[] out);
    /** 문자열 글자 마스크: 반환 alpha[w*h], dims[0]=w, dims[1]=h (위쪽 여백 없이 글자 윗선이 0행) */
    byte[] renderText(String s, int[] dims);
    /** 앱 저장소 파일 읽기/쓰기 (세이브·data1.pak 등) */
    byte[] readFile(String name);
    void writeFile(String name, byte[] data);
    boolean fileExists(String name);
    void log(String s);
    /** 효과음/배경음(표준 MIDI 바이트) 재생·정지·음량(0~100) — 기본은 무음 */
    default void playClip(byte[] midi, boolean loop) {}
    default void stopClip() {}
    default void setClipVolume(int v) {}
}
