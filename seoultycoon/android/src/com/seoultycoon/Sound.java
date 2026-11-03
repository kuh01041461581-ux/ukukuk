package com.seoultycoon;
import com.skt.m.AudioClip;
/** MMF(야마하 SMAF) 는 안드로이드가 재생하지 못해 현재는 무음 처리(볼륨값만 보존). */
public class Sound implements AudioClip {
    public static volatile int volume = 5;
    public void open(byte[] b, int o, int l) {}
    public void play() {}
    public void loop() {}
    public void stop() {}
    public void close() {}
}
